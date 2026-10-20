// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Modules
{
    using System.Web.UI;

    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;

    /// <summary>Carries the state needed to execute an MVC module action.</summary>
    public class ModuleRequestContext
    {
        /// <summary>Gets or sets the hosting Web Forms page.</summary>
        public Page DnnPage { get; set; }

        /// <summary>Gets or sets the current ASP.NET Core HTTP context.</summary>
        public HttpContext HttpContext { get; set; }

        /// <summary>Gets or sets the module instance context.</summary>
        public ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets or sets the module application responsible for running the module.</summary>
        public ModuleApplication ModuleApplication { get; set; }

        /// <summary>Gets or sets the route data describing the controller/action to invoke.</summary>
        public RouteData RouteData { get; set; }
    }
}
