// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.DDRMenu
{
    using System.Collections.Generic;
    using System.Net;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.UI;
    using DotNetNuke.UI.WebControls;
    using DotNetNuke.Web.DDRMenu.DNNCommon;
    using DotNetNuke.Web.DDRMenu.TemplateEngine;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Dnn abstractions.</summary>
    internal static partial class DNNAbstract
    {
        /// <summary>Gets the Dnn login url.</summary>
        /// <returns>The url to login.</returns>
        /// <remarks>
        /// <c>Request.IsAuthenticated</c> is replaced by the portal user lookup, which needs no
        /// <c>HttpContext</c>: this runs while the menu renders, and on a Blazor circuit
        /// <c>HttpContextSource.Current</c> is null, which would have made every authenticated user
        /// look anonymous. The request is still used, when it is available, for the return url and
        /// the <c>override</c> flag.
        /// </remarks>
        public static string GetLoginUrl()
        {
            var navigationManager = Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
            if (IsAuthenticated())
            {
                return navigationManager.NavigateURL(PortalSettings.Current.ActiveTab.TabID, "Logoff");
            }

            var request = HttpContextSource.Current?.Request;

            // Core's HttpRequest has no RawUrl; PathBase + Path + QueryString is the same value.
            // Without a request in flight the current tab's url is the equivalent return target.
            var returnUrl = request == null
                ? navigationManager.NavigateURL()
                : request.PathBase + request.Path + request.QueryString;
            if (returnUrl.IndexOf("?returnurl=") != -1)
            {
                returnUrl = returnUrl.Substring(0, returnUrl.IndexOf("?returnurl="));
            }

            returnUrl = WebUtility.UrlEncode(returnUrl);

            return Globals.LoginURL(returnUrl, !string.IsNullOrEmpty(request?.Query["override"]));
        }

        /// <summary>Gets a url to the user profile or or the registration page.</summary>
        /// <returns>If the user is logged in, returns the url to the user profile page, if not returns the url to the registration page.</returns>
        public static string GetUserUrl()
        {
            var portalSettings = PortalSettings.Current;
            if (!IsAuthenticated())
            {
                if (portalSettings.UserRegistration != (int)Globals.PortalRegistrationType.NoRegistration)
                {
                    var navigationManager = Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
                    return Globals.RegisterURL(WebUtility.UrlEncode(navigationManager.NavigateURL()), Null.NullString);
                }
            }
            else
            {
                var objUserInfo = UserController.Instance.GetCurrentUserInfo();
                if (objUserInfo.UserID != -1)
                {
                    return Globals.UserProfileURL(objUserInfo.UserID);
                }
            }

            return string.Empty;
        }

        /// <summary>Gets the current culture.</summary>
        /// <returns>The current culture code.</returns>
        public static string GetCurrentCulture()
        {
            return DNNContext.Current.PortalSettings.CultureCode;
        }

        /// <summary>Gets all the supported templating engines processors.</summary>
        /// <returns>An enumeration of all the available processors.</returns>
        [DnnDeprecated(10, 2, 4, "Use overload taking IHostSettings")]
        public static partial IEnumerable<ITemplateProcessor> SupportedTemplateProcessors()
            => SupportedTemplateProcessors(Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>());

        /// <summary>Gets all the supported templating engines processors.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <returns>An enumeration of all the available processors.</returns>
        public static IEnumerable<ITemplateProcessor> SupportedTemplateProcessors(IHostSettings hostSettings)
        {
            return [new TokenTemplateProcessor(hostSettings), new RazorTemplateProcessor(hostSettings), new XsltTemplateProcessor(hostSettings),];
        }

        /// <summary>Gets the navigation nodes options.</summary>
        /// <param name="includeHidden">A value indicating whether to include the hidden nodes.</param>
        /// <returns>An integer totaling the options values, <see cref="Navigation.NavNodeOptions"/> for the values.</returns>
        public static int GetNavNodeOptions(bool includeHidden)
        {
            return (int)Navigation.NavNodeOptions.IncludeSiblings + (int)Navigation.NavNodeOptions.IncludeSelf +
                   (includeHidden ? (int)Navigation.NavNodeOptions.IncludeHiddenNodes : 0);
        }

        /// <summary>Gets a value indicating whether it is supported to include hidden nodes.</summary>
        /// <returns>Always true.</returns>
        public static bool IncludeHiddenSupported()
        {
            return true;
        }

        /// <summary>Converts a <see cref="DNNNode"/> into a <see cref="MenuNode"/>.</summary>
        /// <param name="dnnNode">The DnnNode to convert.</param>
        /// <param name="menuNode">The MenuNode to return.</param>
        public static void DNNNodeToMenuNode(DNNNode dnnNode, MenuNode menuNode)
        {
            menuNode.LargeImage = dnnNode.LargeImage;
        }

        /// <summary>Gets a value indicating whether a portal user is signed in.</summary>
        /// <returns><see langword="true"/> when a portal user is signed in.</returns>
        /// <remarks>
        /// Replaces <c>Request.IsAuthenticated</c>. The menu renders both during the initial
        /// request and over a Blazor circuit, where there is no <c>HttpContext</c> at all; the
        /// portal user lookup is request independent and gives the same answer in both cases.
        /// </remarks>
        internal static bool IsAuthenticated()
        {
            return UserController.Instance.GetCurrentUserInfo().UserID > Null.NullInteger;
        }
    }
}
