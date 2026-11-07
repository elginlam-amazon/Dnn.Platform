// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Website.Endpoints
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    using Localization = DotNetNuke.Services.Localization.Localization;

    /// <summary>
    /// The endpoint which replaces <c>Portals/_default/subhost.aspx</c>, the page IIS sub-host
    /// portals point their physical directory at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original page had no code-behind: its whole implementation was an inline
    /// <c>&lt;script runat="server"&gt;</c> block declaring <c>OnInit</c> and <c>OnLoad</c>. It
    /// rendered NO markup whatsoever - every path through <c>OnLoad</c> ends in a
    /// <c>Response.Redirect</c> - so it converts to a minimal API endpoint rather than a
    /// component.
    /// </para>
    /// <para>
    /// The path is the literal legacy <c>/Portals/_default/subhost.aspx</c>. Sub-host portals are
    /// configured in IIS with a physical directory whose default document is this page, and
    /// external links point at it, so the URL cannot change. Both redirects below are the ordinary
    /// 302 that <c>Response.Redirect(url, true)</c> produced, and their target URLs are byte for
    /// byte the ones the page built.
    /// </para>
    /// </remarks>
    public static class SubHostEndpoint
    {
        /// <summary>The preserved sub-host URL, so IIS sub-host portal configurations keep working.</summary>
        public const string SubHostPath = "/Portals/_default/subhost.aspx";

        /// <summary>Maps the sub-host endpoint onto its original URL.</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        public static IEndpointRouteBuilder MapDnnSubHost(this IEndpointRouteBuilder endpoints)
        {
            endpoints.Map(SubHostPath, HandleSubHostAsync);
            return endpoints;
        }

        /// <summary>Replaces the page's <c>OnInit</c>: resolves the portal and applies its page locale to the request thread.</summary>
        private static void ApplyPageLocale()
        {
            // GetCurrentPortalSettings() is obsolete in favour of GetCurrentSettings(), but it is
            // what the page called and it returns the concrete PortalSettings that
            // Localization.SetThreadCultures and Globals.NavigateURL below are given, so the call
            // is preserved rather than changed under a behavioural migration.
#pragma warning disable CS0618 // Type or member is obsolete
            var settings = PortalController.Instance.GetCurrentPortalSettings();
#pragma warning restore CS0618 // Type or member is obsolete
            // GetPageLocale/SetThreadCultures now take IPortalSettings; PortalSettings implements
            // it and both overloads of SetThreadCultures delegate to the IPortalSettings one, so
            // the resolved culture and the thread cultures set are identical.
            var pageLocale = Localization.GetPageLocale((IPortalSettings)settings);
            if ((settings != null) && (pageLocale != null))
            {
                Localization.SetThreadCultures(pageLocale, (IPortalSettings)settings);
            }
        }

        /// <summary>Replaces the page's <c>OnLoad</c>: parses the request URL into an alias token and redirects.</summary>
        private static Task HandleSubHostAsync(HttpContext context)
        {
            ApplyPageLocale();

            var request = context.Request;

            var domainName = string.Empty;
            int urlIndex;

            // parse the Request URL into a Domain Name token
            // Was Request.Url.ToString(): the absolute request URL. Splitting it on '/' puts the
            // scheme in [0], the empty string in [1] and the HOST in [2], which is why the walk
            // below starts at index 2 - a DNN portal alias is "host[/child[/child]]", so the host
            // is the first token of the alias.
            string[] url = request.GetDisplayUrl().Split('/');
            for (urlIndex = 2; urlIndex <= url.GetUpperBound(0); urlIndex++)
            {
                bool willExit = false;
                switch (url[urlIndex].ToUpperInvariant())
                {
                    case "ADMIN":
                    case "DESKTOPMODULES":
                    case "MOBILEMODULES":
                    case "PREMIUMMODULES":
                        willExit = true;
                        break;
                    default:
                        // check if filename
                        if (url[urlIndex].IndexOf(".aspx", StringComparison.Ordinal) == -1)
                        {
                            domainName = domainName + (!string.IsNullOrEmpty(domainName) ? "/" : string.Empty) + url[urlIndex];
                        }
                        else
                        {
                            willExit = true;
                        }

                        break;
                }

                if (willExit)
                {
                    break;
                }
            }

            // format the Request.ApplicationPath
            // Was Request.ApplicationPath, the virtual path of the application root. On ASP.NET
            // Core that is PathBase, which is the empty string (rather than "/") for an
            // application at the site root; the trailing-slash normalisation below therefore
            // produces the same "/" the original did.
            string serverPath = request.PathBase.Value ?? string.Empty;
            if (serverPath.Length < 1 || serverPath.Substring(serverPath.Length - 1, 1) != "/")
            {
                serverPath = serverPath + "/";
            }

#pragma warning disable CS0618 // Type or member is obsolete
            PortalSettings portal = PortalController.Instance.GetCurrentPortalSettings();
#pragma warning restore CS0618 // Type or member is obsolete

            // Was Request.Url.Query, which includes the leading '?' when there is a query string
            // and is empty when there is not. QueryString.Value has exactly those semantics.
            var rawQuery = request.QueryString.Value ?? string.Empty;
            var queryString = rawQuery.TrimStart('?');

            if (rawQuery.Length == 0 && portal.HomeTabId > Null.NullInteger)
            {
                // Redirect target preserved verbatim:
                //   Globals.NavigateURL(portal.HomeTabId, portal, string.Empty, queryString)
                // The obsolete Globals overload did nothing but delegate to
                // INavigationManager.NavigateURL(int, IPortalSettings, string, params string[]),
                // which is called directly here, so the 302 target is byte for byte the same.
                var navigationManager = context.RequestServices.GetRequiredService<INavigationManager>();
                context.Response.Redirect(navigationManager.NavigateURL(portal.HomeTabId, portal, string.Empty, queryString));
            }
            else
            {
                // Redirect target preserved verbatim:
                //   "{serverPath}Default.aspx?alias={domainName}&{queryString}"
                domainName = string.Format(CultureInfo.InvariantCulture, "{0}Default.aspx?alias={1}&{2}", serverPath, domainName, queryString);

                context.Response.Redirect(domainName);
            }

            return Task.CompletedTask;
        }
    }
}
