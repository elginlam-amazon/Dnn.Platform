// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;

    using DataCache = DotNetNuke.UI.Utilities.DataCache;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.Cache;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Utilities;
    using Globals = DotNetNuke.Common.Globals;
    using Microsoft.AspNetCore.Http;

#pragma warning disable 618
    /// <summary>Manages requests to add jQuery to a page.</summary>
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]

    // ReSharper disable once InconsistentNaming
    public partial class jQuery
    {
        /// <summary>Returns the default URL for a hosted version of the jQuery script.</summary>
        /// <remarks>
        /// Google hosts versions of many popular javascript libraries on their CDN.
        /// Using the hosted version increases the likelihood that the file is already
        /// cached in the user's browser.
        /// </remarks>
        public const string DefaultHostedUrl = "https://ajax.googleapis.com/ajax/libs/jquery/3.7.1/jquery.min.js";

        public const string DefaultUIHostedUrl = "https://ajax.googleapis.com/ajax/libs/jqueryui/1.13.3/jquery-ui.min.js";
        private const string JQueryDebugFile = "~/resources/Shared/Scripts/jquery/jquery.js";
        private const string JQueryMinFile = "~/resources/Shared/Scripts/jquery/jquery.min.js";
        private const string JQueryMigrateDebugFile = "~/resources/Shared/Scripts/jquery/jquery-migrate.js";
        private const string JQueryMigrateMinFile = "~/resources/Shared/Scripts/jquery/jquery-migrate.min.js";
        private const string JQueryVersionKey = "jQueryVersionKey";
        private const string JQueryVersionMatch = "(?<=(jquery|core_version)\\s*[:=]\\s*\")(.*)(?=\")";

        private const string JQueryUIDebugFile = "~/resources/Shared/Scripts/jquery/jquery-ui.js";
        private const string JQueryUIMinFile = "~/resources/Shared/Scripts/jquery/jquery-ui.min.js";
        private const string JQueryUIVersionKey = "jQueryUIVersionKey";
        private const string JQueryUIVersionMatch = "(?<=version:\\s\")(.*)(?=\")";

        public static bool IsRequested
        {
            get
            {
                return GetSettingAsBoolean("jQueryRequested", false);
            }
        }

        public static bool IsUIRequested
        {
            get
            {
                return GetSettingAsBoolean("jQueryUIRequested", false);
            }
        }

        public static bool AreDnnPluginsRequested
        {
            get
            {
                return GetSettingAsBoolean("jQueryDnnPluginsRequested", false);
            }
        }

        public static bool IsHoverIntentRequested
        {
            get
            {
                return GetSettingAsBoolean("jQueryHoverIntentRequested", false);
            }
        }

        /// <summary>Active the page with keep alive, so that authentication will not expire.</summary>
        /// <param name="context">The HTTP context of the current request.</param>
        /// <remarks>
        /// The <c>page</c> parameter was a <c>System.Web.UI.Page</c>; the current page is now identified
        /// by the request, from which the <see cref="IClientApiPage"/> that emits the script block is
        /// resolved.
        /// </remarks>
        public static void KeepAlive(HttpContext context)
        {
            var clientApiPage = ClientApiPageAccessor.GetPage(context, nameof(KeepAlive));
            var cookieTimeout = Config.GetAuthCookieTimeout();
            if (cookieTimeout <= 0 || clientApiPage == null || clientApiPage.IsClientScriptBlockRegistered("PageKeepAlive"))
            {
                return;
            }

            if (cookieTimeout > 5)
            {
                cookieTimeout = 5; // ping server in 5 minutes to make sure the server is not IDLE.
            }

            JavaScript.RequestRegistration(CommonJs.jQuery);

            var seconds = ((cookieTimeout * 60) - 30) * 1000; // ping server 30 seconds before cookie is time out.
            var scriptBlock = string.Format(CultureInfo.InvariantCulture, "(function($){{setInterval(function(){{$.get(location.href)}}, {1});}}(jQuery));", Globals.ApplicationPath, seconds);
            clientApiPage.RegisterClientScriptBlock(clientApiPage.GetType(), "PageKeepAlive", scriptBlock);
        }

        private static bool GetSettingAsBoolean(string key, bool defaultValue)
        {
            bool retValue = defaultValue;
            try
            {
                object setting = DotNetNuke.Common.HttpContextSource.Current?.Items[key];
                if (setting != null)
                {
                    retValue = Convert.ToBoolean(setting, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception ex)
            {
                Exceptions.LogException(ex);
            }

            return retValue;
        }
    }
#pragma warning restore 618
}
