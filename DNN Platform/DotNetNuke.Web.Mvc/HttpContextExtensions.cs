// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Http;

    /// <summary>Extensions for storing and retrieving the current module request result on the HTTP context.</summary>
    public static class HttpContextExtensions
    {
        /// <summary>The key under which the module request result is stored.</summary>
        public const string ModuleRequestResultKey = ModuleRequestItemKeys.ModuleRequestResult;

        /// <summary>Gets the module request result stored on the context.</summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>The module request result, or <see langword="null"/>.</returns>
        public static ModuleRequestResult GetModuleRequestResult(this HttpContext context)
        {
            return context.Items.TryGetValue(ModuleRequestResultKey, out var value) ? value as ModuleRequestResult : null;
        }

        /// <summary>Gets a value indicating whether a module request result is stored on the context.</summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns><see langword="true"/> if a result is stored.</returns>
        public static bool HasModuleRequestResult(this HttpContext context)
        {
            return context.GetModuleRequestResult() != null;
        }

        /// <summary>Stores the module request result on the context.</summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="moduleRequestResult">The module request result.</param>
        public static void SetModuleRequestResult(this HttpContext context, ModuleRequestResult moduleRequestResult)
        {
            context.Items[ModuleRequestResultKey] = moduleRequestResult;
        }
    }
}
