// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.CoreMessaging
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Services.Authentication;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Social.Messaging;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    /// <summary>Implements the logic of the Subscription view.</summary>
    /// <remarks>
    /// Was <c>Subscriptions.ascx</c> + <c>Subscriptions.ascx.cs</c> +
    /// <c>Subscriptions.ascx.designer.cs</c>; the markup now lives in <c>Subscriptions.razor</c>.
    /// The control was a <c>System.Web.UI.UserControl</c>, whose ASP.NET Core equivalent in the
    /// migrated platform is <see cref="UserControlBase"/> — it is NOT a
    /// <see cref="PortalModuleBase"/>, which is why <c>ModuleContext</c>, <c>ModuleConfiguration</c>
    /// and <c>LocalResourceFile</c> stay settable members (now component parameters) rather than
    /// being inherited. The designer file's <c>ScopeWrapper</c> panel became
    /// <see cref="ScopeId"/>, which is what the <c>moduleScope</c> setting points at.
    /// </remarks>
    public partial class Subscriptions : UserControlBase
    {
        private const string SharedResources = "~/DesktopModules/CoreMessaging/App_LocalResources/SharedResources.resx";

        private readonly IClientResourceController clientResourceController;
        private readonly IServicesFramework servicesFramework;
        private readonly IHostSettings hostSettings;

        /// <summary>
        /// The request derived values, captured ONCE during the initial (static) render because
        /// <c>HttpContext</c> is <see langword="null"/> for the rest of a Blazor Server circuit.
        /// </summary>
        private Uri capturedReturnUrl;
        private string capturedRawUrl;

        /// <summary>Initializes a new instance of the <see cref="Subscriptions"/> class.</summary>
        /// <param name="clientResourceController">The client resources controller.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <remarks>
        /// This is the former primary constructor, written out so that the component's base type can
        /// also be declared by the generated <c>Subscriptions.razor</c> partial.
        /// </remarks>
        public Subscriptions(IClientResourceController clientResourceController, IServicesFramework servicesFramework, IHostSettings hostSettings)
        {
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
            this.servicesFramework = servicesFramework ?? Globals.GetCurrentServiceProvider().GetRequiredService<IServicesFramework>();
            this.hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
        }

        /// <summary>Initializes a new instance of the <see cref="Subscriptions"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.0. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public Subscriptions()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Subscriptions"/> class.</summary>
        /// <param name="clientResourceController">The client resources controller.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public Subscriptions(IClientResourceController clientResourceController)
            : this(clientResourceController, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Subscriptions"/> class.</summary>
        /// <param name="clientResourceController">The client resources controller.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public Subscriptions(IClientResourceController clientResourceController, IServicesFramework servicesFramework)
            : this(clientResourceController, servicesFramework, null)
        {
        }

        /// <summary>Gets or sets the module context.</summary>
        [Parameter]
        public ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets or sets the module information.</summary>
        /// <remarks>
        /// The WebForms control read and wrote straight through to
        /// <c>ModuleContext.Configuration</c>. A component parameter must be an auto property
        /// (BL0007), and the assignment order relative to <see cref="ModuleContext"/> is not
        /// guaranteed, so the two are synchronised in <see cref="OnLoad"/> instead: a value set
        /// here is pushed into the context, and when only the context was supplied its
        /// configuration is adopted here. Both read paths therefore still yield the context's
        /// configuration.
        /// </remarks>
        [Parameter]
        public ModuleInfo ModuleConfiguration { get; set; }

        /// <summary>Gets or sets the localization resource file.</summary>
        [Parameter]
        public string LocalResourceFile { get; set; }

        /// <summary>
        /// Gets the client side id of the scope element, which replaces
        /// <c>ScopeWrapper.ClientID</c>.
        /// </summary>
        /// <remarks>
        /// This is the element the <c>moduleScope</c> setting points at and the one
        /// <c>SubscriptionsViewModel.js</c> binds its view model against, so it must be stable and
        /// unique per module instance. Follows the <c>dnn_ctr{ModuleId}_{TypeName}</c> pattern the
        /// migrated DDRMenu module established for module-scoped client ids.
        /// </remarks>
        public string ScopeId => $"dnn_ctr{this.ModuleContext?.ModuleId ?? Null.NullInteger}_Subscriptions";

        /// <summary>Gets the settings formatted as a JSON string.</summary>
        /// <returns>The settings formatted as a JSON string.</returns>
        public string GetSettingsAsJson()
        {
            var settings = this.GetModuleSettings(PortalSettings.Current, this.ModuleConfiguration ?? this.ModuleContext?.Configuration, Null.NullInteger);
            foreach (DictionaryEntry entry in this.GetViewSettings())
            {
                if (settings.ContainsKey(entry.Key))
                {
                    settings[entry.Key] = entry.Value;
                }
                else
                {
                    settings.Add(entry.Key, entry.Value);
                }
            }

            return settings.ToJson();
        }

        /// <summary>Localizes a string.</summary>
        /// <param name="key">The localization key to get the localized value.</param>
        /// <returns>The localized value for the provided key.</returns>
        protected string LocalizeString(string key)
        {
            return Localization.GetString(key, this.LocalResourceFile);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Captures the request derived values the settings are built from while the request is
        /// still in flight.
        /// </remarks>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            var request = this.Request;
            this.capturedRawUrl ??= request?.GetRawUrl();
            this.capturedReturnUrl ??= GetReferrer(request);
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (this.ModuleContext != null)
            {
                // Keeps ModuleConfiguration and ModuleContext.Configuration in step, which the
                // WebForms property did implicitly by delegating to the context.
                if (this.ModuleConfiguration != null)
                {
                    this.ModuleContext.Configuration = this.ModuleConfiguration;
                }
                else
                {
                    this.ModuleConfiguration = this.ModuleContext.Configuration;
                }
            }

            this.servicesFramework.RequestAjaxAntiForgerySupport();

            if (this.Request.IsAuthenticated())
            {
                this.clientResourceController.RegisterScript("~/DesktopModules/CoreMessaging/Scripts/LocalizationController.js");
                this.clientResourceController.RegisterScript("~/DesktopModules/CoreMessaging/Scripts/SubscriptionsViewModel.js");
                this.clientResourceController.RegisterScript("~/DesktopModules/CoreMessaging/Scripts/Subscription.js");
                this.clientResourceController.RegisterStylesheet("~/DesktopModules/CoreMessaging/subscriptions.css");
            }
            else
            {
                // Replaces Response.Redirect(Globals.AccessDeniedURL(), false). Globals.Redirect
                // writes the redirect onto the response captured for this request, which is the
                // initial (static) render — the same point at which the WebForms OnLoad ran.
                Globals.Redirect(Globals.AccessDeniedURL(), false);
            }
        }

        private static string GetHistoryNavigationKey(string moduleName)
        {
            // Replaces HttpContext.Current.Server.HtmlEncode; WebUtility.HtmlEncode is the
            // framework equivalent and is what the migrated platform uses elsewhere. Note the
            // small encoding delta: WebUtility does not encode the apostrophe that
            // Server.HtmlEncode did. Harmless here, because the input is a module name that has
            // already been lower-cased and had its spaces stripped, and the result is only used
            // as a history navigation key.
            return WebUtility.HtmlEncode(moduleName.ToLowerInvariant().Replace(" ", string.Empty));
        }

        /// <summary>Gets the session timeout.</summary>
        /// <returns>The session timeout, defaulting to 25 minutes.</returns>
        /// <remarks>
        /// Replaces <c>WebConfigurationManager.GetSection("system.web/sessionState")</c>, which does
        /// not exist on ASP.NET Core. The real session idle timeout now lives in
        /// <see cref="SessionOptions"/>, configured by <c>AddSession</c>, so that is the primary
        /// source; the <c>SessionState:Timeout</c> / <c>Session:IdleTimeout</c> configuration keys
        /// are honoured as an explicit override (either a whole number of minutes or a
        /// <see cref="TimeSpan"/> literal). The original swallowed every failure and defaulted to
        /// 25 minutes, which is preserved — including the deliberate silence (logging this filled
        /// the Event Log).
        /// </remarks>
        private static TimeSpan GetSessionTimeout()
        {
            try
            {
                var serviceProvider = Globals.GetCurrentServiceProvider();
                var configuration = serviceProvider?.GetService<IConfiguration>();
                var configured = configuration?["SessionState:Timeout"] ?? configuration?["Session:IdleTimeout"];
                if (!string.IsNullOrEmpty(configured))
                {
                    if (int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
                    {
                        return TimeSpan.FromMinutes(minutes);
                    }

                    if (TimeSpan.TryParse(configured, CultureInfo.InvariantCulture, out var timeout))
                    {
                        return timeout;
                    }
                }

                // No explicit override: report the timeout the session middleware actually uses,
                // which is the direct counterpart of the old system.web/sessionState timeout.
                var sessionOptions = serviceProvider?.GetService<IOptions<SessionOptions>>()?.Value;
                if (sessionOptions is not null && sessionOptions.IdleTimeout > TimeSpan.Zero)
                {
                    return sessionOptions.IdleTimeout;
                }
            }
            catch
            {
                // FIXME(cbond): The default configuration doesn't actually let us see this data
                // FIXME(cbond): It's too annoying seeing this fill the Event Log, we need to add the permission to web.config
                ////Exceptions.LogException(ex);
            }

            return TimeSpan.FromMinutes(25);
        }

        private static IDictionary<string, string> GetSharedResources()
        {
            return new Dictionary<string, string>
                {
                    { "ExceptionTitle", Localization.GetString("ExceptionTitle", SharedResources) },
                    { "ExceptionMessage", Localization.GetString("ExceptionMessage", SharedResources) },
                };
        }

        /// <summary>The ASP.NET Core replacement for the legacy <c>Request.UrlReferrer</c>.</summary>
        /// <param name="request">The request to read the referrer from.</param>
        /// <returns>The referring URI, or <see langword="null"/> when the client sent none.</returns>
        private static Uri GetReferrer(Microsoft.AspNetCore.Http.HttpRequest request)
        {
            var referer = request?.Headers.Referer.ToString();
            return !string.IsNullOrEmpty(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var referrerUri)
                ? referrerUri
                : null;
        }

        private Hashtable GetModuleSettings(PortalSettings portalSettings, ModuleInfo moduleInfo, int uniqueId)
        {
            var usePopup =
                portalSettings.EnablePopUps &&
                portalSettings.LoginTabId == Null.NullInteger &&
                !AuthenticationController.HasSocialAuthenticationEnabled();

            var navigationKey =
                moduleInfo is { DesktopModule: not null, }
                    ? GetHistoryNavigationKey(moduleInfo.DesktopModule.FriendlyName)
                    : null;

            var moduleRoot =
                moduleInfo is { DesktopModule: not null, }
                    ? moduleInfo.DesktopModule.FolderName
                    : null;

            var moduleTitle = moduleInfo?.ModuleTitle;

            var moduleId = moduleInfo?.ModuleID ?? Null.NullInteger;

            var moduleSettings = moduleInfo != null ? moduleInfo.ModuleSettings : new Hashtable();

            var debug = false;

#if DEBUG
            debug = true;
#else
            // Replaces HttpContext.Current.IsDebuggingEnabled (the <compilation debug="true">
            // switch, which does not exist on ASP.NET Core) with IHostSettings.DebugMode, which is
            // the same decision the migrated platform's client resource pipeline now makes.
            debug = this.hostSettings is not null && this.hostSettings.DebugMode;
#endif

            return new Hashtable
                   {
                       { "anonymous", PortalSettings.Current.UserId < 0 },
                       { "currentUserId", PortalSettings.Current.UserId },
                       { "debug", debug },
                       { "culture", CultureInfo.CurrentUICulture.Name },
                       { "showMissingKeys", Localization.ShowMissingKeys },
                       { "portalId", portalSettings.PortalId },
                       { "moduleId", moduleId },
                       { "moduleSettings", moduleSettings },
                       { "moduleTitle", moduleTitle },
                       { "moduleRoot", moduleRoot },
                       { "navigationKey", navigationKey },
                       { "sessionTimeout", Convert.ToInt32(GetSessionTimeout().TotalMinutes) },
                       { "sharedResources", GetSharedResources() },
                       { "authorizationUrl", this.GetLoginUrl(portalSettings) },
                       { "usePopup", usePopup },
                       { "returnUrl", this.capturedReturnUrl ?? GetReferrer(this.Request) },
                       { "uniqueId", uniqueId },
                   };
        }

        private string GetLoginUrl(PortalSettings portalSettings)
        {
            var returnUrl = this.capturedRawUrl ?? this.Request?.GetRawUrl() ?? string.Empty;

            if (portalSettings.UserId < 1)
            {
                var indexOf = returnUrl.IndexOf("?returnurl=", StringComparison.InvariantCultureIgnoreCase);
                if (indexOf >= 0)
                {
                    returnUrl = returnUrl.Substring(0, indexOf);
                }

                // Replaces HttpUtility.UrlEncode. WebUtility.UrlEncode is the framework
                // equivalent used elsewhere in the migrated platform; it emits upper-case hex
                // escapes (%2F rather than %2f) and additionally escapes ' ! ( ). Both forms are
                // decoded identically by Uri/WebUtility.UrlDecode on the receiving side, so the
                // returnurl round-trip is unaffected.
                returnUrl = Common.Globals.LoginURL(WebUtility.UrlEncode(returnUrl), true);
            }

            return returnUrl;
        }

        /// <summary>
        /// These values are passed in as the 'settings' parameter of the JavaScript initialization function, together with
        /// values that are automatically retrieved by Social Library such as portalId and moduleId.
        /// </summary>
        private Hashtable GetViewSettings()
        {
            var portalSettings = PortalSettings.Current;
            var userPreferenceController = UserPreferencesController.Instance;
            var user = UserController.GetUserById(this.hostSettings, portalSettings.PortalId, portalSettings.UserId);
            UserPreference userPreference = null;
            if (user != null)
            {
                userPreference = userPreferenceController.GetUserPreference(user);
            }

            const int notifyFrequency = 2;
            const int messageFrequency = 0;

            return new Hashtable
                   {
                       { "moduleScope", $"#{this.ScopeId}" },
                       { "pageSize", 25 },
                       { "notifyFrequency", userPreference != null ? (int)userPreference.NotificationsEmailFrequency : notifyFrequency },
                       { "msgFrequency", userPreference != null ? (int)userPreference.MessagesEmailFrequency : messageFrequency },
                   };
        }
    }
}
