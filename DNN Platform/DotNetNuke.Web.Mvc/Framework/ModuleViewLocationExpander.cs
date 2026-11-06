// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Web.Mvc.Framework.Modules;
    using Microsoft.AspNetCore.Mvc.Razor;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Contributes the DNN MVC module view locations (<c>~/DesktopModules/MVC/{module}/Views/...</c>)
    /// to the Razor view engine.
    /// </summary>
    /// <remarks>
    /// Replaces the legacy per-module <c>RazorViewEngine</c> instance whose
    /// <c>ViewLocationFormats</c>/<c>MasterLocationFormats</c>/<c>PartialViewLocationFormats</c> the
    /// <see cref="ModuleApplication"/> used to set. ASP.NET Core has a single Razor view engine, so the
    /// module specific locations are expanded per request from the module folder path that the executing
    /// <see cref="ModuleApplication"/> publishes on the current request.
    /// </remarks>
    public class ModuleViewLocationExpander : IViewLocationExpander
    {
        /// <summary>The key under which the current module folder path is stored in the expander values.</summary>
        public const string ModuleFolderKey = "dnn_module_folder";

        /// <inheritdoc />
        public void PopulateValues(ViewLocationExpanderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var httpContext = context.ActionContext?.HttpContext;
            var folderPath = string.Empty;
            try
            {
                folderPath = ModuleApplication.GetCurrentModuleFolderPath(httpContext) ?? string.Empty;
            }
            catch (Exception ex)
            {
                // Never fail view resolution because the module folder could not be determined: fall back
                // to the default (non module) view locations instead.
                var logger = httpContext?.RequestServices?.GetService<ILogger<ModuleViewLocationExpander>>();
                logger?.ModuleViewLocationExpanderUnableToDetermineModuleFolder(ex);
            }

            context.Values[ModuleFolderKey] = folderPath;
        }

        /// <inheritdoc />
        public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(viewLocations);

            if (context.Values.TryGetValue(ModuleFolderKey, out var folderPath) && !string.IsNullOrEmpty(folderPath))
            {
                foreach (var format in ModuleApplication.GetModuleViewLocationFormats(folderPath, context.IsMainPage))
                {
                    yield return format;
                }
            }

            // The original locations must always remain as a fallback, otherwise every non module view breaks.
            foreach (var location in viewLocations)
            {
                yield return location;
            }
        }
    }
}
