// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DNNConnect.CKEditorProvider
{
    using System;
    using System.Globalization;
    using System.Text;
    using System.Text.Encodings.Web;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using DNNConnect.CKEditorProvider.Utilities;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    using Globals = DotNetNuke.Common.Globals;

    /// <summary>
    /// Renders the Tab JavaScript (the <c>dnnpagesSelectBox</c> array the page-link picker of the
    /// editor uses), which was served by the <c>Tabs.ashx</c> <c>IHttpHandler</c>.
    /// </summary>
    /// <remarks>
    /// The URL is preserved exactly: <c>Options.aspx</c>/<c>CKEditorOptions</c> and the CKEditor
    /// <c>dnnpages</c> plugin build this URL themselves, and the generated script is included with a
    /// <c>&lt;script src&gt;</c> tag, so the path may not change.
    /// </remarks>
    public static class TabsEndpoint
    {
        /// <summary>The preserved URL of the tab-tree script.</summary>
        public const string TabsPath = "/Providers/HtmlEditorProviders/DNNConnect.CKE/Tabs.ashx";

        /// <summary>Maps the tab-tree script endpoint onto its original URL.</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The same <paramref name="endpoints"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
        public static IEndpointRouteBuilder MapCKEditorTabsEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            // The handler was reachable with any verb, but it is only ever requested as a script.
            endpoints.MapGet(TabsPath, HandleRequest);

            return endpoints;
        }

        /// <summary>
        /// Escapes a string the way the <c>Microsoft.JScript</c> <c>GlobalObject.escape</c> function
        /// did (that is, the way the JavaScript <c>escape()</c> function does), so that the
        /// <c>unescape()</c> call the client-side picker performs on the tab name keeps working.
        /// </summary>
        /// <param name="value">The value to escape.</param>
        /// <returns>The escaped value.</returns>
        internal static string JScriptEscape(string value)
            => Utility.JavaScriptEscape(value);

        private static Task HandleRequest(HttpContext context)
        {
            var services = context.RequestServices;
            var portalController = services.GetRequiredService<IPortalController>();
            var hostSettings = services.GetRequiredService<IHostSettings>();
            var appStatus = services.GetRequiredService<IApplicationStatusInfo>();

            var portalId = portalController.GetCurrentSettings().PortalId;

            // Generate Pages Array
            var pagesArray = new StringBuilder();

            pagesArray.Append("var dnnpagesSelectBox = new Array(");

            var domainName = $"http://{Globals.GetDomainName(context.Request, true)}";

            var contentLocalizationEnabled = PortalController.GetPortalSettingAsBoolean(
                portalController,
                "ContentLocalizationEnabled",
                portalId,
                false);

            foreach (TabInfo tab in TabController.GetPortalTabs(hostSettings, appStatus, portalId, -1, false, null, true, false, true, true, true))
            {
                var tabUrl = contentLocalizationEnabled && !string.IsNullOrEmpty(tab.CultureCode)
                    ? Globals.FriendlyUrl(tab, $"{Globals.ApplicationURL(tab.TabID)}&language={tab.CultureCode}")
                    : Globals.FriendlyUrl(tab, Globals.ApplicationURL(tab.TabID));

                tabUrl = Globals.ResolveUrl(Regex.Replace(tabUrl, domainName, "~", RegexOptions.IgnoreCase));

                var tabName = JScriptEscape(tab.TabName);

                if (tab.Level.Equals(0))
                {
                    pagesArray.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "new Array('| {0}','{1}'),",
                        JavaScriptEncoder.Default.Encode(tabName),
                        JavaScriptEncoder.Default.Encode(tabUrl));
                }
                else
                {
                    var separator = new StringBuilder();

                    for (int index = 0; index < tab.Level; index++)
                    {
                        separator.Append("--");
                    }

                    pagesArray.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "new Array('|{0} {1}','{2}'),",
                        JavaScriptEncoder.Default.Encode(separator.ToString()),
                        JavaScriptEncoder.Default.Encode(tabName),
                        JavaScriptEncoder.Default.Encode(tabUrl));
                }
            }

            if (pagesArray.ToString().EndsWith(",", StringComparison.Ordinal))
            {
                pagesArray.Remove(pagesArray.Length - 1, 1);
            }

            pagesArray.Append(");");

            context.Response.ContentType = "text/javascript";
            return context.Response.WriteAsync(pagesArray.ToString());
        }
    }
}
