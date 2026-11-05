// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Utilities
{
    using System;

    using DotNetNuke.Framework;

    using Microsoft.AspNetCore.Http;

    public class JavaScriptUtils : ServiceLocator<IJavaScriptUtils, JavaScriptUtils>, IJavaScriptUtils
    {
        /// <inheritdoc />
        /// <remarks>
        /// The WebForms version chose between <c>ScriptManager.RegisterStartupScript</c> and
        /// <c>Page.ClientScript.RegisterStartupScript</c> so that the script survived an MS AJAX partial
        /// postback. There are no partial postbacks on ASP.NET Core, so there is a single path: the
        /// startup script is registered on the request's <see cref="DotNetNuke.UI.Utilities.IClientApiPage"/>,
        /// which is the seam that emits per-page client script.
        /// </remarks>
        public void RegisterJavascriptVariable(string variableName, object value, HttpContext context, Type type)
        {
            var valueAsJson = Json.Serialize(value);

            var script = $"var {variableName} = {valueAsJson};";

            var page = ClientApiPageAccessor.GetPage(context, nameof(this.RegisterJavascriptVariable));
            page?.RegisterStartupScript(type, variableName, script, true);
        }

        /// <inheritdoc />
        protected override Func<IJavaScriptUtils> GetFactory()
        {
            return () => new JavaScriptUtils();
        }
    }
}
