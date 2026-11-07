// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Framework
{
    using System;
    using System.Globalization;
    using System.Text.Encodings.Web;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Abstractions.Pages;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.UI.Utilities;
    using Globals = DotNetNuke.Common.Globals;
    using Microsoft.AspNetCore.Antiforgery;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The default <see cref="IServicesFramework"/> implementation.</summary>
    internal class ServicesFrameworkImpl : IServicesFramework, IServiceFrameworkInternals
    {
        /// <summary>The name of the field the Services Framework client script reads the anti-forgery token from.</summary>
        internal const string AntiForgeryFieldName = "__RequestVerificationToken";

        private const string AntiForgeryKey = "dnnAntiForgeryRequested";
        private const string ScriptKey = "dnnSFAjaxScriptRequested";
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;
        private readonly IHostSettings hostSettings;

        /// <summary>Initializes a new instance of the <see cref="ServicesFrameworkImpl"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IApplicationStatusInfo and IEventLogger. Scheduled removal in v12.0.0.")]
        public ServicesFrameworkImpl()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ServicesFrameworkImpl"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        public ServicesFrameworkImpl(IApplicationStatusInfo appStatus, IEventLogger eventLogger)
        {
            var servicesProvider = Globals.GetCurrentServiceProvider();
            this.appStatus = appStatus ?? servicesProvider.GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? servicesProvider.GetRequiredService<IEventLogger>();
            this.hostSettings = servicesProvider.GetRequiredService<IHostSettings>();
        }

        /// <inheritdoc />
        public bool IsAjaxAntiForgerySupportRequired
        {
            get { return CheckKey(AntiForgeryKey); }
        }

        /// <inheritdoc />
        public bool IsAjaxScriptSupportRequired
        {
            get { return CheckKey(ScriptKey); }
        }

        /// <inheritdoc />
        public void RequestAjaxAntiForgerySupport()
        {
            this.RequestAjaxScriptSupport();
            SetKey(AntiForgeryKey);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Was <c>page.FindControl("ClientResourcesFormBottom").Controls.Add(new LiteralControl(AntiForgery.GetHtml()))</c>.
        /// The ASP.NET Core equivalent of <c>System.Web.Helpers.AntiForgery.GetHtml()</c> is
        /// <see cref="IAntiforgery.GetAndStoreTokens"/>; the resulting hidden field is handed to
        /// <see cref="IPageService"/>, which is what renders the page's own markup, so the field the
        /// Services Framework client script looks for (<c>[name="__RequestVerificationToken"]</c>) is
        /// actually emitted.
        /// </remarks>
        public void RegisterAjaxAntiForgery(HttpContext context)
        {
            if (context == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(this.RegisterAjaxAntiForgery)} requires a request in flight: the anti-forgery token is stored in the response's cookie.");
            }

            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
            var tokens = antiforgery.GetAndStoreTokens(context);
            var html = string.Format(
                CultureInfo.InvariantCulture,
                "<input name=\"{0}\" type=\"hidden\" value=\"{1}\" />",
                AntiForgeryFieldName,
                HtmlEncoder.Default.Encode(tokens.RequestToken));

            context.RequestServices.GetRequiredService<IPageService>()
                   .AddToHead(new PageTag(html, PagePriority.Default));
        }

        /// <inheritdoc />
        public void RequestAjaxScriptSupport()
        {
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, PortalSettings.Current, CommonJs.jQuery);
            SetKey(ScriptKey);
        }

        /// <inheritdoc />
        public void RegisterAjaxScript(HttpContext context)
        {
            var path = ServicesFramework.GetServiceFrameworkRoot();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            JavaScript.RegisterClientReference(context, ClientAPI.ClientNamespaceReferences.dnn);

            var clientApiPage = ClientApiPageAccessor.GetPage(context, nameof(this.RegisterAjaxScript));
            if (clientApiPage != null)
            {
                ClientAPI.RegisterClientVariable(clientApiPage, "sf_siteRoot", path, /*overwrite*/ true);
                ClientAPI.RegisterClientVariable(clientApiPage, "sf_tabId", PortalSettings.Current.ActiveTab.TabID.ToString(CultureInfo.InvariantCulture), /*overwrite*/ true);
            }

            var scriptPath = this.hostSettings.DebugMode
                ? "~/js/Debug/dnn.servicesframework.js"
                : "~/js/dnn.servicesframework.js";

            GetClientResourcesController(context).CreateScript(scriptPath).Register();
        }

        private static void SetKey(string key)
        {
            HttpContextSource.CurrentOrThrow.Items[key] = true;
        }

        private static bool CheckKey(string antiForgeryKey)
        {
            return HttpContextSource.Current?.Items.ContainsKey(antiForgeryKey) == true;
        }

        private static IClientResourceController GetClientResourcesController(HttpContext context)
        {
            var serviceProvider = context?.RequestServices ?? Globals.GetCurrentServiceProvider();
            return serviceProvider.GetRequiredService<IClientResourceController>();
        }
    }
}
