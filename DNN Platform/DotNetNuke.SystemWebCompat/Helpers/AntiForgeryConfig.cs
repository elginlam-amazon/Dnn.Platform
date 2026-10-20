// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Helpers
{
    /// <summary>Replacement for the System.Web.WebPages anti-forgery configuration on .NET 10.</summary>
    /// <remarks>The values have to match the <c>AntiforgeryOptions</c> configured for ASP.NET Core during the application startup.</remarks>
    public static class AntiForgeryConfig
    {
        /// <summary>Gets or sets the name of the anti-forgery cookie.</summary>
        public static string CookieName { get; set; } = "__RequestVerificationToken";

        /// <summary>Gets or sets the name of the form field which contains the request token.</summary>
        public static string FormFieldName { get; set; } = "__RequestVerificationToken";

        /// <summary>Gets or sets a value indicating whether the anti-forgery cookie requires SSL.</summary>
        public static bool RequireSsl { get; set; }

        /// <summary>Gets or sets a value indicating whether the X-Frame-Options header is not sent.</summary>
        public static bool SuppressXFrameOptionsHeader { get; set; }
    }
}
