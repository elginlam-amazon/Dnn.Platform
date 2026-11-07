// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Utilities
{
    using System;

    using Microsoft.AspNetCore.Http;

    public interface IJavaScriptUtils
    {
        /// <summary>Registers a javascript variable in a page with its value.</summary>
        /// <param name="variableName">Variable name and also the name of the registered code.</param>
        /// <param name="value">Object to be assigned to the variable.</param>
        /// <param name="context">The HTTP context of the request whose page the variable is registered on.</param>
        /// <param name="type">Type.</param>
        /// <remarks>The <c>page</c> parameter was a <c>System.Web.UI.Page</c>; the current page is now identified by the request.</remarks>
        void RegisterJavascriptVariable(string variableName, object value, HttpContext context, Type type);
    }
}
