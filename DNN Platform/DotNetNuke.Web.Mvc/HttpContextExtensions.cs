// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using Microsoft.AspNetCore.Http;

    public static class HttpContextExtensions
    {
        public const string ModuleRequestResultKey = "Dnn_ModuleRequestResult";

        public static ModuleRequestResult GetModuleRequestResult(this HttpContext context)
        {
            return context.Items.TryGetValue(ModuleRequestResultKey, out var value)
                ? value as ModuleRequestResult
                : null;
        }

        public static bool HasModuleRequestResult(this HttpContext context)
        {
            return context.GetModuleRequestResult() != null;
        }

        public static void SetModuleRequestResult(this HttpContext context, ModuleRequestResult moduleRequestResult)
        {
            context.Items[ModuleRequestResultKey] = moduleRequestResult;
        }
    }
}
