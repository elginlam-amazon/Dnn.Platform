// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Common.Internal
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Options;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Search.Internals;

    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// The application-initialization task of <c>DotNetNuke.Web</c>: the port of the former
    /// <c>DotNetNukeHttpApplication</c>'s static constructor, <c>Application_Start</c> and
    /// <c>Application_End</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>HttpApplication</c> and its <c>Application_Start</c>/<c>_End</c>/<c>_BeginRequest</c>/
    /// <c>_Error</c> events do not exist on modern .NET. The host (<c>Website/Program.cs</c>) owns
    /// the pipeline; the once-per-application work is an <see cref="IHostedService"/>, the
    /// per-request work is <see cref="DnnWebBeginRequestMiddleware"/>, and unhandled exceptions are
    /// handled by the platform's <c>UseDnnExceptionHandler</c> branch
    /// (<c>Application_Error</c> parity).
    /// </para>
    /// <para>
    /// The provider installs run here — synchronously, in <see cref="StartAsync"/>, before the host
    /// starts accepting requests — which preserves the guarantee the static constructor gave:
    /// every provider is installed before the first request touches
    /// <see cref="DotNetNuke.ComponentModel.ComponentFactory"/>.
    /// </para>
    /// </remarks>
    public sealed class DnnWebApplicationInitializationService : IHostedService
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<DnnWebApplicationInitializationService>();

        private readonly IServiceProvider serviceProvider;
        private readonly IHostApplicationLifetime lifetime;
        private readonly IApplicationStatusInfo applicationStatusInfo;
        private readonly IOptions<HttpRuntimeOptions> httpRuntimeOptions;

        /// <summary>Initializes a new instance of the <see cref="DnnWebApplicationInitializationService"/> class.</summary>
        /// <param name="serviceProvider">The host's (root) service provider — the single container.</param>
        /// <param name="lifetime">The host application lifetime, which replaces <c>HostingEnvironment</c>.</param>
        /// <param name="applicationStatusInfo">The application status.</param>
        /// <param name="httpRuntimeOptions">The <c>&lt;httpRuntime&gt;</c> replacement options, which carry the FCN mode.</param>
        public DnnWebApplicationInitializationService(
            IServiceProvider serviceProvider,
            IHostApplicationLifetime lifetime,
            IApplicationStatusInfo applicationStatusInfo,
            IOptions<HttpRuntimeOptions> httpRuntimeOptions)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            this.applicationStatusInfo = applicationStatusInfo ?? throw new ArgumentNullException(nameof(applicationStatusInfo));
            this.httpRuntimeOptions = httpRuntimeOptions ?? throw new ArgumentNullException(nameof(httpRuntimeOptions));
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // ---- the static constructor ------------------------------------------------
            // 20 ProviderInstaller installs + the 3 named FolderProvider registrations, against
            // the HOST container. No second service provider is built here any more: the
            // dependencyProvider.SetProvider(DependencyInjectionInitialize.BuildServiceProvider())
            // call and the LazyServiceProvider seam are gone, and Globals.DependencyProvider is set
            // by the platform's UseDnnPlatform to this very same provider.
            DnnWebProviderInstaller.EnsureInstalled(this.serviceProvider);

            // ---- Application_Start -----------------------------------------------------
            Logger.ApplicationStarting(Globals.ElapsedSinceAppStart); // just to start the timer

            var name = Config.GetSetting("ServerName");
            Globals.ServerName = string.IsNullOrEmpty(name) ? Dns.GetHostName() : name;

            Logger.ApplicationStarted(Globals.ElapsedSinceAppStart); // just to start the timer

            // Replaces the HttpRuntime FileChangesMonitor reflection: shutdown reporting is now
            // driven by IHostApplicationLifetime.
            DotNetNukeShutdownOverload.Initialize(this.applicationStatusInfo, this.lifetime, this.httpRuntimeOptions.Value.FcnMode);

            // register the assembly-lookup to correct the breaking rename in DNN 9.2
            DotNetNuke.Services.Zip.SharpZipLibRedirect.RegisterSharpZipLibRedirect();

            this.lifetime.ApplicationStopped.Register(OnApplicationStopped);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            // ---- Application_End -------------------------------------------------------
            // Initialize.LogEnd() and Initialize.StopScheduler() are run by the platform's own
            // DnnApplicationInitializationService on ApplicationStopping; what remains here is the
            // part that only this assembly's Global.asax base type did: disposing Lucene (unless
            // we are installing).
            Logger.ApplicationEnding();

            if (this.applicationStatusInfo.Status != UpgradeStatus.Install)
            {
                Logger.ApplicationDisposingLucene();
                try
                {
                    if (LuceneController.Instance is IDisposable lucene)
                    {
                        lucene.Dispose();
                    }
                }
                catch (Exception e)
                {
                    // Parity with Application_End, which logged rather than threw: throwing from
                    // shutdown would abort the remaining shutdown work.
                    Logger.ApplicationLogEndException(e);
                }
            }

            return Task.CompletedTask;
        }

        private static void OnApplicationStopped()
        {
            Logger.ApplicationEnded();
        }
    }
}
