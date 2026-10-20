// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework
{
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Mvc.Razor;

    /// <summary>Expands view locations to the DNN MVC module convention (<c>~/DesktopModules/MVC/{FolderPath}/Views/...</c>).</summary>
    public class DnnModuleViewLocationExpander : IViewLocationExpander
    {
        private const string ModuleFolderKey = "DnnModuleFolder";

        /// <inheritdoc />
        public void PopulateValues(ViewLocationExpanderContext context)
        {
            var folder = GetModuleFolder(context);
            if (!string.IsNullOrEmpty(folder))
            {
                context.Values[ModuleFolderKey] = folder;
            }
        }

        /// <inheritdoc />
        public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
        {
            if (!context.Values.TryGetValue(ModuleFolderKey, out var folder) || string.IsNullOrEmpty(folder))
            {
                return viewLocations;
            }

            var dnnLocations = new[]
            {
                $"~/DesktopModules/MVC/{folder}/Views/{{1}}/{{0}}.cshtml",
                $"~/DesktopModules/MVC/{folder}/Views/Shared/{{0}}.cshtml",
            };

            return dnnLocations.Concat(viewLocations);
        }

        private static string GetModuleFolder(ViewLocationExpanderContext context)
        {
            var httpContext = context.ActionContext.HttpContext;
            if (httpContext.Items.TryGetValue(typeof(DnnModuleState), out var stateObj) && stateObj is DnnModuleState state)
            {
                return state.ModuleApplication?.FolderPath?.Trim('/');
            }

            return null;
        }
    }
}
