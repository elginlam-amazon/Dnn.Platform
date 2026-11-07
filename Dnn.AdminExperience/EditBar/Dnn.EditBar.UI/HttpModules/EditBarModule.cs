// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.EditBar.UI.HttpModules
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Dnn.EditBar.UI.Controllers;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Entities.Controllers;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Services.Log.EventLog;
    using DotNetNuke.UI.Skins.EventListeners;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    /// <summary>An <see cref="IHostedService"/> which injects the <see cref="ContentEditorManager"/> component on requests that may need to contain the Edit Bar.</summary>
    /// <remarks>
    /// Was an <c>IHttpModule</c> registered through the extension's <c>.dnn</c> manifest
    /// (<c>system.webServer/modules</c>) whose <c>Init(HttpApplication)</c> performed the
    /// application-start work once behind a double-checked lock (ASP.NET created one module
    /// instance per <c>HttpApplication</c>) and whose <c>Dispose</c> was a no-op. ASP.NET Core has
    /// no <c>IHttpModule</c> pipeline, so the application-lifetime equivalent is a hosted service:
    /// <see cref="StartAsync"/> is the former <c>Init</c> and <see cref="StopAsync"/> the former
    /// <c>Dispose</c> (still a no-op — there was no application-end behavior to preserve). The
    /// once-only guard is kept because <see cref="ApplicationStart"/> adds a global skin-event
    /// listener that must never be added twice. Mirrors <c>Dnn.PersonaBar.UI.HttpModules.PersonaBarModule</c>.
    /// </remarks>
    public class EditBarModule : IHostedService
    {
        private static readonly object LockAppStarted = new object();
        private static bool hasAppStarted;

        private readonly IHostSettings hostSettings;

        /// <summary>Initializes a new instance of the <see cref="EditBarModule"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public EditBarModule()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="EditBarModule"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        public EditBarModule(IHostSettings hostSettings)
        {
            if (hostSettings is not null)
            {
                this.hostSettings = hostSettings;
            }
            else
            {
                var scope = HttpContextSource.Current?.GetScope();
                if (scope is not null)
                {
                    this.hostSettings = scope.ServiceProvider.GetRequiredService<IHostSettings>();
                }
                else
                {
                    this.hostSettings = new HostSettings(
                        new HostController(
#pragma warning disable CS0618 // Type or member is obsolete
                            new EventLogController(),
#pragma warning restore CS0618 // Type or member is obsolete
                            new Lazy<IPortalController>(() => PortalController.Instance)));
                }
            }
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (hasAppStarted)
            {
                return Task.CompletedTask;
            }

            lock (LockAppStarted)
            {
                if (hasAppStarted)
                {
                    return Task.CompletedTask;
                }

                this.ApplicationStart();
                hasAppStarted = true;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            // The former IHttpModule.Dispose was a no-op, so there is no application-end behavior to run.
            return Task.CompletedTask;
        }

        private void ApplicationStart()
        {
            DotNetNukeContext.Current.SkinEventListeners.Add(new SkinEventListener(SkinEventType.OnSkinInit, this.OnSkinInit));
        }

        private void OnSkinInit(object sender, SkinEventArgs e)
        {
            if (this.hostSettings.DisableEditBar)
            {
                return;
            }

            // The Blazor skin exposes the ASP.NET Core request directly (there is no
            // System.Web.UI.Page behind e.Skin.Page any more, so e.Skin.Page.Request is gone).
            var request = e.Skin.Request;
            var isSpecialPageMode = request.Query["dnnprintmode"].ToString() == "true" || request.Query["popUp"].ToString() == "true";
            if (isSpecialPageMode || Globals.IsAdminControl())
            {
                return;
            }

            if (ContentEditorManager.GetCurrent(e.Skin.Page) == null && !Globals.IsAdminControl())
            {
                if (PortalSettings.Current.UserId > 0)
                {
                    var scope = HttpContextSource.Current.GetScope();
                    var clientResourceController = scope.ServiceProvider.GetRequiredService<IClientResourceController>();
                    var appStatus = scope.ServiceProvider.GetRequiredService<IApplicationStatusInfo>();
                    var eventLogger = scope.ServiceProvider.GetRequiredService<IEventLogger>();
                    var portalController = scope.ServiceProvider.GetRequiredService<IPortalController>();
                    var userController = scope.ServiceProvider.GetRequiredService<IUserController>();
                    var hostSettingsService = scope.ServiceProvider.GetRequiredService<IHostSettingsService>();
                    var servicesFramework = scope.ServiceProvider.GetRequiredService<IServicesFramework>();

                    // The Blazor skin has no <form runat="server">, so the component is added to
                    // the skin's own control tree instead of e.Skin.Page.Form.Controls.
                    e.Skin.Controls.Add(new ContentEditorManager(clientResourceController, appStatus, eventLogger, portalController, this.hostSettings, userController, hostSettingsService, servicesFramework) { Skin = e.Skin, });
                }
            }
        }
    }
}
