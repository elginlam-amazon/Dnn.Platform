// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Net;
    using System.Text.RegularExpressions;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Pages;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Icons;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Client.ClientResourceManagement;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>PageBase provides a custom DotNetNuke base class for pages.</summary>
    /// <remarks>
    /// <para>
    /// This was a <c>System.Web.UI.Page</c>. On ASP.NET Core the platform renders with Blazor, so the
    /// base type is <see cref="LayoutComponentBase"/>: a DNN page both <em>is</em> a page and
    /// <em>hosts</em> the page's content (see <see cref="CDefault"/>), which is exactly what a layout
    /// component does. It must not emit <c>&lt;html&gt;</c>/<c>&lt;head&gt;</c>/<c>&lt;body&gt;</c> —
    /// the host's <c>App.razor</c> owns those.
    /// </para>
    /// <para>
    /// Lifecycle: the WebForms <c>Init</c>/<c>Load</c>/<c>PreRender</c> hooks are raised from
    /// <see cref="OnInitialized"/>/<see cref="OnParametersSet"/>/<see cref="OnAfterRender(bool)"/>, the
    /// same protocol <see cref="UserControlBase"/> uses. <c>HttpContext</c> is <see langword="null"/>
    /// once the circuit is interactive, so the request is captured during initialization and exposed
    /// through <see cref="Request"/>.
    /// </para>
    /// <para>
    /// Localization: WebForms walked the control tree reading a <c>resourcekey</c> attribute off every
    /// control's <c>AttributeCollection</c>. A Blazor component has no such tree, but it does have the
    /// direct equivalent of an <c>AttributeCollection</c> — the attribute-splatting dictionary
    /// (<c>IDictionary&lt;string, object&gt;</c>) a component renders with. The localization helpers
    /// therefore operate on those dictionaries; see <see cref="ProcessControl"/>.
    /// </para>
    /// </remarks>
    public abstract partial class PageBase : LayoutComponentBase
    {
        private const string LinkItemPattern = "<(a|link|img|script|input|form|object).[^>]*(href|src|action)=(\\\"|'|)(.[^\\\"']*)(\\\"|'|)[^>]*>";

        private static readonly ILogger Logger = DnnLoggingController.GetLogger<PageBase>();
        private static readonly Regex LinkItemMatchRegex = new Regex(LinkItemPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly ILogger traceLogger = DnnLoggingController.GetLogger("DNN.Trace");
        private readonly ArrayList localizedControls = [];

        private readonly IPortalController injectedPortalController;
        private readonly IApplicationStatusInfo injectedAppStatus;
        private readonly IHostSettings injectedHostSettings;
        private readonly IUserController injectedUserController;

        private IPortalController resolvedPortalController;
        private IApplicationStatusInfo resolvedAppStatus;
        private IHostSettings resolvedHostSettings;
        private IUserController resolvedUserController;

        private PageStatePersister persister;
        private CultureInfo pageCulture;
        private string localResourceFile;
        private HttpRequest capturedRequest;
        private string capturedRequestPath;
        private IPageService pageService;

        /// <summary>Initializes a new instance of the <see cref="PageBase"/> class.</summary>
        /// <remarks>This is the constructor the Blazor renderer uses; services are resolved on first use.</remarks>
        [Obsolete("Deprecated in DotNetNuke 10.0.2. Please use overload with IPortalController. Scheduled removal in v12.0.0.")]
        protected PageBase()
            : this(null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="PageBase"/> class.</summary>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="hostSettings">The host settings.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.1. Please use overload with IUserController. Scheduled removal in v12.0.0.")]
        protected PageBase(IPortalController portalController, IApplicationStatusInfo appStatus, IHostSettings hostSettings)
            : this(portalController, appStatus, hostSettings, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="PageBase"/> class.</summary>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="userController">The user controller.</param>
        /// <remarks>
        /// Blazor activates components through their parameterless constructor, so the services passed
        /// here are only stored; anything not supplied is resolved lazily from
        /// <see cref="Globals.GetCurrentServiceProvider"/> on first use. That keeps these constructors
        /// (which are part of the public contract) usable while never requiring a live service provider
        /// at component-activation time.
        /// </remarks>
        protected PageBase(IPortalController portalController, IApplicationStatusInfo appStatus, IHostSettings hostSettings, IUserController userController)
        {
            this.injectedPortalController = portalController;
            this.injectedAppStatus = appStatus;
            this.injectedHostSettings = hostSettings;
            this.injectedUserController = userController;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        public PortalSettings PortalSettings => this.PortalController.GetCurrentPortalSettings();
#pragma warning restore CS0618 // Type or member is obsolete

        public NameValueCollection HtmlAttributes { get; } = [];

        public CultureInfo PageCulture => this.pageCulture ??= Localization.GetPageLocale(this.PortalController.GetCurrentSettings());

        /// <summary>
        /// Gets the request that produced this page, or <see langword="null"/> when there is no request
        /// in flight.
        /// </summary>
        /// <remarks>
        /// Replaces <c>Page.Request</c>. The request is captured during <see cref="OnInitialized"/>,
        /// i.e. during the initial (static) render, because <c>HttpContext</c> is
        /// <see langword="null"/> for the rest of a Blazor Server circuit.
        /// </remarks>
        public HttpRequest Request => this.capturedRequest ?? HttpContextSource.Current?.Request;

        /// <summary>Gets the identity of this page instance.</summary>
        /// <remarks>
        /// Replaces the <c>Session.SessionID</c> the page-state persisters scoped their keys by. A
        /// Blazor Server page component instance lives for exactly as long as the user's view of that
        /// page, which is the scope the persisted state needs.
        /// </remarks>
        public string PageStateId { get; } = Guid.NewGuid().ToString("N");

        /// <summary>Gets or sets the cache key under which this page's state is persisted.</summary>
        /// <remarks>
        /// Replaces the <c>__VIEWSTATE_CACHEKEY</c> hidden field <see cref="CachePageStatePersister"/>
        /// used to round-trip through the browser.
        /// </remarks>
        public string PageStateCacheKey { get; set; }

        public string LocalResourceFile
        {
            get
            {
                if (!string.IsNullOrEmpty(this.localResourceFile))
                {
                    return this.localResourceFile;
                }

                // Was built from Request.ServerVariables["SCRIPT_NAME"] and TemplateSourceDirectory
                // (the virtual directory of the page's source file). On ASP.NET Core the request path
                // plays both roles.
                var path = this.capturedRequestPath ?? this.Request?.Path.Value ?? string.Empty;
                var page = path.Split('/');
                var pageName = page[page.GetUpperBound(0)];
                var directory = path.Length > pageName.Length
                    ? path.Substring(0, path.Length - pageName.Length - 1)
                    : string.Empty;

                return string.Concat(directory, "/", Localization.LocalResourceDirectory, "/", pageName, ".resx");
            }

            set
            {
                this.localResourceFile = value;
            }
        }

        /// <summary>Gets or sets the canonical link URL of this page.</summary>
        /// <remarks>
        /// Backed by <see cref="IPageService"/>, which is what renders the page's head on ASP.NET Core;
        /// setting this property therefore actually emits a <c>&lt;link rel="canonical"&gt;</c> rather
        /// than only being remembered on the page object.
        /// </remarks>
        public string CanonicalLinkUrl
        {
            get => this.PageService.GetCanonicalLinkUrl();
            set => this.PageService.SetCanonicalLinkUrl(value);
        }

        /// <summary>Gets a value indicating whether HTTP headers has been sent to client.</summary>
        public bool HeaderIsWritten { get; internal set; }

        /// <summary>Gets the portal controller.</summary>
        protected IPortalController PortalController
            => this.resolvedPortalController ??= this.injectedPortalController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>();

        /// <summary>Gets the application status.</summary>
        protected IApplicationStatusInfo AppStatus
            => this.resolvedAppStatus ??= this.injectedAppStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();

        /// <summary>Gets the host settings.</summary>
        protected IHostSettings HostSettings
            => this.resolvedHostSettings ??= this.injectedHostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();

        /// <summary>Gets the user controller.</summary>
        protected IUserController UserController
            => this.resolvedUserController ??= this.injectedUserController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IUserController>();

        /// <summary>Gets the page service that collects this page's head content and messages.</summary>
        protected IPageService PageService
            => this.pageService ??= Globals.GetCurrentServiceProvider().GetRequiredService<IPageService>();

        /// <summary>Gets pageStatePersister returns an instance of the class that will be used to persist the Page State.</summary>
        /// <returns>A <see cref="PageStatePersister"/>.</returns>
        protected virtual PageStatePersister PageStatePersister
        {
            get
            {
                if (this.persister == null)
                {
                    if (this.AppStatus.Status == UpgradeStatus.None)
                    {
                        switch (this.HostSettings.PageStatePersister)
                        {
                            case "M":
                                this.persister = new CachePageStatePersister(this);
                                break;
                            case "D":
                                this.persister = new DiskPageStatePersister(this);
                                break;
                        }
                    }

                    // Default (as it was in the base class) is the in-circuit state of the component
                    // itself, which needs no persister at all.
                    this.persister ??= new CachePageStatePersister(this);
                }

                return this.persister;
            }
        }

        /// <inheritdoc cref="HtmlUtils.JavaScriptStringEncode(string)"/>
        public static IHtmlContent JavaScriptStringEncode(string value)
            => HtmlUtils.JavaScriptStringEncode(value);

        /// <inheritdoc cref="HtmlUtils.JavaScriptStringEncode(string,bool)"/>
        public static IHtmlContent JavaScriptStringEncode(string value, bool addDoubleQuotes)
            => HtmlUtils.JavaScriptStringEncode(value, addDoubleQuotes);

        /// <summary>
        /// <para>RemoveKeyAttribute remove the key attribute from the control. If this isn't done, then the HTML output will have
        /// a bad attribute on it which could cause some older browsers problems.</para>
        /// </summary>
        /// <param name="affectedControls">
        /// ArrayList that holds the attribute dictionaries that have been localized. This is later used
        /// for the removal of the key attribute. Each entry is an
        /// <c>IDictionary&lt;string, object&gt;</c>, the Blazor equivalent of the WebForms
        /// <c>AttributeCollection</c>.
        /// </param>
        public static void RemoveKeyAttribute(ArrayList affectedControls)
        {
            if (affectedControls == null)
            {
                return;
            }

            int i;
            for (i = 0; i <= affectedControls.Count - 1; i++)
            {
                if (affectedControls[i] is not IDictionary<string, object> ac)
                {
                    continue;
                }

                ac.Remove(Localization.KeyName);
                ac.Remove(IconController.IconKeyName);
                ac.Remove(IconController.IconSizeName);
                ac.Remove(IconController.IconStyleName);
            }
        }

        /// <summary><para>GetControlAttribute looks in an attribute dictionary for the given attribute.</para></summary>
        /// <param name="attributes">The attribute dictionary the control renders with.</param>
        /// <param name="affectedControls">ArrayList that holds the attribute dictionaries that have been localized. This is later used for the removal of the key attribute.</param>
        /// <param name="attributeName">Name of key to search for.</param>
        /// <returns>A string containing the key for the specified control or null if a key attribute wasn't found.</returns>
        internal static string GetControlAttribute(IDictionary<string, object> attributes, ArrayList affectedControls, string attributeName)
        {
            if (attributes == null || !attributes.TryGetValue(attributeName, out var value) || value == null)
            {
                return null;
            }

            var key = value.ToString();
            if (key != null && affectedControls != null)
            {
                affectedControls.Add(attributes);
            }

            return key;
        }

        /// <summary><para>ProcessControl performs the high level localization for a single control's attribute dictionary.</para></summary>
        /// <param name="attributes">The attribute dictionary the control renders with.</param>
        /// <param name="affectedControls">ArrayList that holds the attribute dictionaries that have been localized. This is later used for the removal of the key attribute.</param>
        /// <param name="resourceFileRoot">Root Resource File.</param>
        /// <returns>
        /// The localized text for the control, or <see langword="null"/> when the control carries no
        /// resource key. Callers render it as the control's text/label.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The WebForms version did three things: look the resource key up, assign the resulting text to
        /// whichever text-bearing property the concrete control type had (<c>Label.Text</c>,
        /// <c>Button.Text</c>, <c>Image.AlternateText</c>, …), and resolve <c>~/</c> URLs on image and
        /// link controls because <c>ResolveClientUrl</c> misbehaved under URL rewriting. In Blazor a
        /// component owns its own markup, so the first part is done here and the per-type assignment is
        /// the caller's (that is what the return value is for); URL resolution is done for the
        /// <c>src</c>/<c>href</c>/<c>imageurl</c> attributes and for the icon-key attributes, which are
        /// the same attributes the WebForms code rewrote.
        /// </para>
        /// </remarks>
        internal string ProcessControl(IDictionary<string, object> attributes, ArrayList affectedControls, string resourceFileRoot)
        {
            if (attributes == null)
            {
                return null;
            }

            string localizedText = null;

            // Perform the substitution if a key was found
            var key = GetControlAttribute(attributes, affectedControls, Localization.KeyName);
            if (!string.IsNullOrEmpty(key))
            {
                // Translation starts here ....
                localizedText = this.LocalizeValue(Localization.GetString(key, resourceFileRoot));
            }

            // UrlRewriting Issue - ResolveClientUrl gets called instead of ResolveUrl
            // Manual Override to ResolveUrl
            foreach (var urlAttribute in new[] { "src", "href", "imageurl", "navigateurl", })
            {
                if (attributes.TryGetValue(urlAttribute, out var urlValue)
                    && urlValue?.ToString() is { } url
                    && url.IndexOf("~", StringComparison.Ordinal) != -1)
                {
                    attributes[urlAttribute] = Globals.ResolveUrl(url);
                }
            }

            // Check for IconKey
            var hasImageUrl = (attributes.TryGetValue("src", out var src) && !string.IsNullOrEmpty(src?.ToString()))
                              || (attributes.TryGetValue("imageurl", out var imageUrl) && !string.IsNullOrEmpty(imageUrl?.ToString()));
            if (!hasImageUrl)
            {
                var iconKey = GetControlAttribute(attributes, affectedControls, IconController.IconKeyName);
                var iconSize = GetControlAttribute(attributes, affectedControls, IconController.IconSizeName);
                var iconStyle = GetControlAttribute(attributes, affectedControls, IconController.IconStyleName);
                if (!string.IsNullOrEmpty(iconKey))
                {
                    attributes["src"] = IconController.IconURL(iconKey, iconSize, iconStyle);
                }
            }

            this.LogDnnTrace("PageBase.ProcessControl", "Info", $"ResourceKey: {key}");
            return localizedText;
        }

        /// <summary>Localizes every attribute dictionary registered on this page.</summary>
        /// <remarks>
        /// Replaces the <c>Render</c>-time walk of the control tree. A component registers its
        /// attribute bag with <see cref="RegisterLocalizedAttributes"/> during initialization; this is
        /// invoked after the first render, which is the Blazor equivalent of the WebForms render pass.
        /// </remarks>
        protected void LocalizeRegisteredControls()
        {
            this.LogDnnTrace("PageBase.LocalizeRegisteredControls", "Start", this.capturedRequestPath);

            lock (this.localizedControls)
            {
                foreach (var attributes in this.localizedControls.ToArray())
                {
                    this.ProcessControl(attributes as IDictionary<string, object>, null, this.LocalResourceFile);
                }

                RemoveKeyAttribute(this.localizedControls);
            }

            this.LogDnnTrace("PageBase.LocalizeRegisteredControls", "End", this.capturedRequestPath);
        }

        /// <summary>Registers an attribute dictionary to be localized by this page.</summary>
        /// <param name="attributes">The attribute dictionary the control renders with.</param>
        /// <remarks>
        /// Replaces the implicit enrolment the WebForms control tree gave every child control. A DNN
        /// component that carries a <c>resourcekey</c> attribute calls this from its
        /// <c>OnInitialized</c>.
        /// </remarks>
        protected internal void RegisterLocalizedAttributes(IDictionary<string, object> attributes)
        {
            if (attributes == null)
            {
                return;
            }

            lock (this.localizedControls)
            {
                this.localizedControls.Add(attributes);
            }
        }

        /// <summary>Requests that the Services Framework AJAX script be registered on this page.</summary>
        protected virtual void RegisterAjaxScript()
        {
            if (ServicesFrameworkInternal.Instance.IsAjaxScriptSupportRequired)
            {
                ServicesFrameworkInternal.Instance.RegisterAjaxScript(HttpContextSource.Current);
            }
        }

        /// <summary>Handles an unhandled exception raised while this page was processed.</summary>
        /// <param name="exc">The exception.</param>
        /// <remarks>
        /// Replaces <c>Page.OnError(EventArgs)</c>, which read the exception from
        /// <c>Server.GetLastError()</c>. On ASP.NET Core there is no ambient "last error", so the
        /// exception is passed in; the host's exception handler calls it for a failure during the
        /// initial request, and a derived page calls it from its own <c>try/catch</c>.
        /// </remarks>
        protected virtual void HandleError(Exception exc)
            => this.HandleError(exc, exc is BadHttpRequestException badRequest ? badRequest.StatusCode : 500);

        /// <summary>Handles an unhandled exception raised while this page was processed.</summary>
        /// <param name="exc">The exception.</param>
        /// <param name="statusCode">
        /// The HTTP status code the exception maps to. Replaces
        /// <c>HttpException.GetHttpCode()</c>, which does not exist on ASP.NET Core.
        /// </param>
        protected virtual void HandleError(Exception exc, int statusCode)
        {
            Logger.PageBaseAnErrorHasOccurredWhileLoadingPage(exc);

            var strUrl = Globals.ApplicationURL();
            var response = HttpContextSource.Current?.Response;
            if (statusCode >= 400 && !this.IsViewStateFailure(exc))
            {
                try
                {
                    // if the exception's status code set to 404, we need display 404 page if defined or show no found info.
                    if (statusCode == 404 && response != null)
                    {
                        UrlUtils.Handle404Exception(response, this.PortalSettings);
                        return;
                    }

                    if (this.PortalSettings?.ErrorPage500 != -1)
                    {
                        var url = this.GetErrorUrl($"~/Default.aspx?tabid={this.PortalSettings.ErrorPage500}", exc, false);
                        this.Redirect(url);
                        return;
                    }

                    this.Redirect(DotNetNuke.Common.ErrorPage.Path);
                    return;
                }
                catch (Exception)
                {
                    var errorMessage = Localization.GetString("NoSitesForThisInstallation.Error", Localization.GlobalResourceFile);
                    this.Redirect(DotNetNuke.Common.ErrorPage.BuildUrl(503, errorMessage));
                    return;
                }
            }

            strUrl = this.GetErrorUrl(strUrl, exc);
            Exceptions.ProcessPageLoadException(exc, strUrl);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Replaces the WebForms <c>Init</c> phase. Captures the request (see <see cref="Request"/>),
        /// registers the core client script and raises <see cref="OnInit(EventArgs)"/>.
        /// </remarks>
        protected override void OnInitialized()
        {
            base.OnInitialized();

            var context = HttpContextSource.Current;
            this.capturedRequest ??= context?.Request;
            this.capturedRequestPath ??= context?.Request?.Path.Value;

            // Was: if (ScriptManager.GetCurrent(this) == null) AJAX.AddScriptManager(this, !isInstallPage);
            // Blazor keeps the client and server DOM in sync over the circuit, so there is no script
            // manager to add; the equivalent request-scoped flag is still set so that callers of
            // AJAX.IsEnabled() keep working.
            if (context != null)
            {
                AJAX.RegisterScriptManager();
            }

            var dnncoreFilePath = this.HostSettings.DebugMode
                   ? "~/js/Debug/dnncore.js"
                   : "~/js/dnncore.js";

            if (context != null)
            {
                ClientResourceManager.RegisterScript(context, dnncoreFilePath);
            }

            this.OnInit(EventArgs.Empty);
        }

        /// <inheritdoc />
        /// <remarks>Replaces the WebForms <c>Load</c> phase and raises <see cref="OnLoad(EventArgs)"/>.</remarks>
        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            this.OnLoad(EventArgs.Empty);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Replaces the WebForms <c>PreRender</c> and <c>Render</c> phases: the anti-forgery and AJAX
        /// scripts are registered and the registered attribute dictionaries are localized, which is
        /// what <c>Render</c> did before handing off to the base implementation.
        /// </remarks>
        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);
            if (!firstRender)
            {
                return;
            }

            // Because we have delayed registration of the jQuery script,
            // Modules can override the standard behavior by including their own script on the page.
            // The module must register the script with the "jQuery" key and should notify user
            // of potential version conflicts with core jQuery support.
            if (ServicesFrameworkInternal.Instance.IsAjaxAntiForgerySupportRequired)
            {
                ServicesFrameworkInternal.Instance.RegisterAjaxAntiForgery(HttpContextSource.Current);
            }

            this.RegisterAjaxScript();
            this.LocalizeRegisteredControls();
            this.OnPreRender(EventArgs.Empty);
        }

        /// <summary>Called during the page's initialization phase.</summary>
        /// <param name="e">The event arguments (always <see cref="EventArgs.Empty"/>).</param>
        /// <remarks>Replaces the WebForms <c>Page.OnInit</c> hook; raised from <see cref="OnInitialized"/>.</remarks>
        protected virtual void OnInit(EventArgs e)
        {
        }

        /// <summary>Called when the page's parameters have been set.</summary>
        /// <param name="e">The event arguments (always <see cref="EventArgs.Empty"/>).</param>
        /// <remarks>Replaces the WebForms <c>Page_Load</c> hook; raised from <see cref="OnParametersSet"/>.</remarks>
        protected virtual void OnLoad(EventArgs e)
        {
        }

        /// <summary>Called once the page has rendered for the first time.</summary>
        /// <param name="e">The event arguments (always <see cref="EventArgs.Empty"/>).</param>
        /// <remarks>Replaces the WebForms <c>Page.OnPreRender</c> hook; raised from <see cref="OnAfterRender(bool)"/>.</remarks>
        protected virtual void OnPreRender(EventArgs e)
        {
        }

        /// <summary>Localizes a resource value, resolving any application-relative URLs it contains.</summary>
        /// <param name="value">The localized text.</param>
        /// <returns>The text with its <c>~/</c> URLs resolved.</returns>
        /// <remarks>Replaces the <c>LinkButton</c> branch of the WebForms <c>LocalizeControl</c>, which resolved URLs embedded in the localized markup.</remarks>
        private string LocalizeValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var imgMatches = LinkItemMatchRegex.Matches(value);
            foreach (Match match in imgMatches)
            {
                var matched = match.Groups[match.Groups.Count - 2].Value;
                if (matched.IndexOf("~", StringComparison.Ordinal) == -1)
                {
                    continue;
                }

                value = value.Replace(matched, Globals.ResolveUrl(matched));
            }

            return value;
        }

        private void Redirect(string url)
        {
            var response = HttpContextSource.Current?.Response;
            if (response == null)
            {
                // Never swallow this: without a response there is nowhere to send the user, and
                // silently returning would render the error page's content as if nothing happened.
                throw new InvalidOperationException($"Cannot redirect to '{url}': there is no request in flight. Handle the error from the initial request or navigate with NavigationManager.");
            }

            response.Redirect(Globals.ResolveUrl(url));
        }

        private string GetErrorUrl(string url, Exception exc, bool hideContent = true)
        {
            var separator = url.IndexOf('?') == -1 ? '?' : '&';
            if (this.Request?.GetValue("error") != null)
            {
                return $"{url}{separator}error=terminate";
            }

            var user = this.UserController.GetCurrentUserInfo();
            var errorMessage = exc == null || user is not { IsSuperUser: true }
                    ? "An unexpected error has occurred"
                    : exc.Message;
            url = $"{url}{separator}error={WebUtility.UrlEncode(errorMessage)}";
            if (!Globals.IsAdminControl() && hideContent)
            {
                return $"{url}&content=0";
            }

            return url;
        }

        private bool IsViewStateFailure(Exception e)
        {
            var user = HttpContextSource.Current?.User;
            return user?.Identity?.IsAuthenticated != true && e?.InnerException is InvalidViewStateCacheKeyException;
        }

        private void LogDnnTrace(string origin, string action, string message)
        {
            var tabId = -1;
            if (this.PortalSettings?.ActiveTab != null)
            {
                tabId = this.PortalSettings.ActiveTab.TabID;
            }

            this.traceLogger.PageBaseTrace(origin, action, tabId, message);
        }
    }
}
