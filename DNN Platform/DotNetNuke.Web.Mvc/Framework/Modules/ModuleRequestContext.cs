// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Modules
{
    using DotNetNuke.UI.Modules;
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Http;

    /// <summary>Describes a single MVC module execution request.</summary>
    public class ModuleRequestContext
    {
        /// <summary>Gets or sets the DNN page hosting the module.</summary>
        /// <remarks>
        /// Was a <c>System.Web.UI.Page</c>. The module host controls are Blazor components now
        /// (see <see cref="IModuleControl.Control"/>), so the hosting page is an <see cref="IComponent"/>.
        /// </remarks>
        public IComponent DnnPage { get; set; }

        /// <summary>Gets or sets the current HTTP context.</summary>
        public HttpContext HttpContext { get; set; }

        /// <summary>Gets or sets the module instance context.</summary>
        public ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets or sets the module application which executes the request.</summary>
        public ModuleApplication ModuleApplication { get; set; }

        /// <summary>Gets or sets the route data for the module request.</summary>
        public Microsoft.AspNetCore.Routing.RouteData RouteData { get; set; }
    }
}
