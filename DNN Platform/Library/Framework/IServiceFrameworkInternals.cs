// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Framework
{
    using Microsoft.AspNetCore.Http;

    internal interface IServiceFrameworkInternals
    {
        bool IsAjaxAntiForgerySupportRequired { get; }

        bool IsAjaxScriptSupportRequired { get; }

        /// <summary>Emits the anti-forgery token the Services Framework client script reads.</summary>
        /// <param name="context">The HTTP context of the current request.</param>
        /// <remarks>Was <c>RegisterAjaxAntiForgery(System.Web.UI.Page)</c>.</remarks>
        void RegisterAjaxAntiForgery(HttpContext context);

        /// <summary>Registers the Services Framework client script and its client variables.</summary>
        /// <param name="context">The HTTP context of the current request.</param>
        /// <remarks>Was <c>RegisterAjaxScript(System.Web.UI.Page)</c>.</remarks>
        void RegisterAjaxScript(HttpContext context);
    }
}
