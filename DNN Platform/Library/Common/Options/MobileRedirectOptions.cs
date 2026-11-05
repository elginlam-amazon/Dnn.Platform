// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Options
{
    /// <summary>
    /// The names of the cookies that drive the mobile-redirect behaviour. These used to be read
    /// from <c>&lt;appSettings&gt;</c> keys <c>MobileViewSiteCookieName</c> and
    /// <c>DisableMobileViewSiteCookieName</c> into <see langword="static readonly"/> fields.
    /// </summary>
    public class MobileRedirectOptions
    {
        /// <summary>The configuration section name.</summary>
        public const string SectionName = "MobileRedirect";

        /// <summary>Gets or sets the name of the cookie that records that the mobile site is being viewed.</summary>
        public string MobileViewSiteCookieName { get; set; } = "dnn_IsMobile";

        /// <summary>Gets or sets the name of the cookie that suppresses the redirect to the mobile site.</summary>
        public string DisableMobileViewSiteCookieName { get; set; } = "dnn_NoMobile";
    }
}
