// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Modules
{
    /// <summary>Well-known keys used to flow DNN module state through <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/>.</summary>
    public static class ModuleRequestItemKeys
    {
        /// <summary>The key under which the <see cref="ModuleRequestContext"/> for the currently executing module is stored.</summary>
        public const string ModuleRequestContext = "Dnn_ModuleRequestContext";

        /// <summary>The key under which the <see cref="ModuleRequestResult"/> for the currently executing module is stored.</summary>
        public const string ModuleRequestResult = "Dnn_ModuleRequestResult";
    }
}
