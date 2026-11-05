// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Tokens
{
    using Microsoft.AspNetCore.Http;

    public class HtmlTokenReplace : TokenReplace
    {
        /// <summary>Initializes a new instance of the <see cref="HtmlTokenReplace"/> class.</summary>
        /// <param name="context">The request the module is rendering on.</param>
        /// <remarks>Was <c>HtmlTokenReplace(System.Web.UI.Page)</c>; the token sources register client resources per request now.</remarks>
        public HtmlTokenReplace(HttpContext context)
            : base(Scope.DefaultSettings)
        {
            this.AddPropertySource("css", new CssPropertyAccess(context));
            this.AddPropertySource("js", new JavaScriptPropertyAccess(context));
            this.AddPropertySource("javascript", new JavaScriptPropertyAccess(context));
            this.AddPropertySource("antiforgerytoken", new AntiForgeryTokenPropertyAccess());
        }
    }
}
