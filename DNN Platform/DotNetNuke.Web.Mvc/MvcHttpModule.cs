// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.ComponentModel;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Initializes DNN for MVC module requests. This is the ASP.NET Core middleware replacement of the
    /// legacy <c>IHttpModule</c>: the former <c>Init</c> wiring runs once on the first request and the
    /// former <c>BeginRequest</c> handler runs on every request.
    /// </summary>
    public class MvcHttpModule : IDisposable
    {
        /// <summary>Matches the requests which are handled by the DNN MVC module host.</summary>
        public static readonly Regex MvcServicePath = new Regex(@"DesktopModules/MVC/", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly RequestDelegate next;
        private readonly SemaphoreSlim initializationLock = new SemaphoreSlim(1, 1);

        private bool initialized;
        private bool suppressXFrameOptionsHeader;
        private bool disposed;

        /// <summary>Initializes a new instance of the <see cref="MvcHttpModule"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public MvcHttpModule(RequestDelegate next)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>Invokes the middleware.</summary>
        /// <param name="context">The current request context.</param>
        /// <param name="applicationStatusInfo">The application status info of the current request scope.</param>
        /// <param name="moduleExecutionEngine">The module execution engine registered in the container (see <see cref="Extensions.ModuleExecutionExtensions.AddDnnModuleExecution"/>).</param>
        /// <returns>A task.</returns>
        public async Task InvokeAsync(HttpContext context, IApplicationStatusInfo applicationStatusInfo, IModuleExecutionEngine moduleExecutionEngine)
        {
            ArgumentNullException.ThrowIfNull(context);

            await this.EnsureInitializedAsync(applicationStatusInfo, moduleExecutionEngine);

            if (this.suppressXFrameOptionsHeader)
            {
                // ASP.NET Core does not emit X-Frame-Options itself (the legacy
                // AntiForgeryConfig.SuppressXFrameOptionsHeader switch is gone), but another component or the
                // host may have added it; web.config asked for it to be supplied by the configuration only.
                context.Response.OnStarting(
                    state =>
                    {
                        ((HttpContext)state).Response.Headers.Remove("X-Frame-Options");
                        return Task.CompletedTask;
                    },
                    context);
            }

            // Former BeginRequest handler (InitDnn).
            var rawUrl = $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
            if (MvcServicePath.IsMatch(rawUrl))
            {
                var redirect = DotNetNuke.Common.Initialize.Init(context);
                if (!string.IsNullOrEmpty(redirect))
                {
                    // Initialize.Init already issued the install/upgrade redirect.
                    return;
                }
            }

            await this.next(context);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Releases the resources used by this middleware.</summary>
        /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>, <see langword="false"/> when called from a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.initializationLock.Dispose();
            }

            this.disposed = true;
        }

        /// <summary>Determines whether web.config declares a custom X-Frame-Options header.</summary>
        /// <param name="appStatus">The application status info.</param>
        /// <returns><see langword="true"/> when the header is configured in web.config, otherwise <see langword="false"/>.</returns>
        private static bool IsXFrameOptionsHeaderPresentInConfig(IApplicationStatusInfo appStatus)
        {
            if (appStatus is null)
            {
                return false;
            }

            var xmlConfig = Config.Load(appStatus);
            var xmlCustomHeaders =
                xmlConfig.SelectSingleNode("configuration/system.webServer/httpProtocol/customHeaders") ??
                xmlConfig.SelectSingleNode("configuration/location/system.webServer/httpProtocol/customHeaders");

            if (xmlCustomHeaders?.ChildNodes != null)
            {
                foreach (XmlNode header in xmlCustomHeaders.ChildNodes)
                {
                    if (header.Attributes?["name"]?.Value == "X-Frame-Options")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private async Task EnsureInitializedAsync(IApplicationStatusInfo applicationStatusInfo, IModuleExecutionEngine moduleExecutionEngine)
        {
            if (this.initialized)
            {
                return;
            }

            await this.initializationLock.WaitAsync();
            try
            {
                if (this.initialized)
                {
                    return;
                }

                this.suppressXFrameOptionsHeader = IsXFrameOptionsHeaderPresentInConfig(applicationStatusInfo);

                // The legacy Init registered a new ModuleExecutionEngine into DNN's ComponentFactory.
                // The engine now comes from the host container; it is published into the ComponentFactory
                // so the legacy ComponentFactory.GetComponent<IModuleExecutionEngine>() callers (inside
                // and outside this assembly) keep resolving the very same instance.
                if (ComponentFactory.GetComponent<IModuleExecutionEngine>() is null)
                {
                    ComponentFactory.RegisterComponentInstance<IModuleExecutionEngine>(
                        moduleExecutionEngine ?? new ModuleExecutionEngine());
                }

                this.initialized = true;
            }
            finally
            {
                this.initializationLock.Release();
            }
        }
    }
}
