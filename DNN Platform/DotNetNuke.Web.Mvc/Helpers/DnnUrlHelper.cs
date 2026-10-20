// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Helpers
{
    using DotNetNuke.Common;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Common;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Routing;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.Routing;
    using Microsoft.AspNetCore.Routing;

    /// <summary>A DNN URL helper for MVC module views and controllers.</summary>
    public class DnnUrlHelper
    {
        private readonly IDnnController controller;

        /// <summary>Initializes a new instance of the <see cref="DnnUrlHelper"/> class from a controller.</summary>
        /// <param name="controller">The DNN controller.</param>
        public DnnUrlHelper(IDnnController controller)
        {
            Requires.NotNull("controller", controller);

            this.controller = controller;
            this.ModuleContext = controller.ModuleContext;
        }

        /// <summary>Initializes a new instance of the <see cref="DnnUrlHelper"/> class from a view context.</summary>
        /// <param name="viewContext">The view context.</param>
        public DnnUrlHelper(ViewContext viewContext)
        {
            Requires.NotNull("viewContext", viewContext);

            this.controller = viewContext.HttpContext.Items.TryGetValue(typeof(IDnnController), out var c) ? c as IDnnController : null;
            this.ModuleContext = this.controller?.ModuleContext;
            this.ViewContext = viewContext;
        }

        /// <summary>Gets or sets the module instance context.</summary>
        public ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets the view context, if available.</summary>
        internal ViewContext ViewContext { get; }

        /// <summary>URL-encodes the given URL.</summary>
        /// <param name="url">The URL.</param>
        /// <returns>The encoded URL.</returns>
        public virtual string Encode(string url)
        {
            return System.Web.HttpUtility.UrlEncode(url);
        }

        /// <summary>Converts a virtual (relative) path to an application absolute path.</summary>
        /// <param name="contentPath">The virtual path of the content.</param>
        /// <returns>The application absolute path.</returns>
        public virtual string Content(string contentPath)
        {
            var request = this.GetHttpContext()?.Request;
            if (request == null)
            {
                return contentPath;
            }

            var pathBase = request.PathBase.HasValue ? request.PathBase.Value : "/";
            if (!string.IsNullOrEmpty(contentPath) && contentPath.StartsWith("~/", System.StringComparison.Ordinal))
            {
                return pathBase.TrimEnd('/') + "/" + contentPath.Substring(2);
            }

            return contentPath;
        }

        /// <summary>Returns a value that indicates whether the URL is local.</summary>
        /// <param name="url">The URL.</param>
        /// <returns><see langword="true"/> if the URL is local; otherwise, <see langword="false"/>.</returns>
        public virtual bool IsLocalUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            return (url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\')))
                || (url.Length > 1 && url[0] == '~' && url[1] == '/');
        }

        /// <summary>Returns the raw URL of the current request.</summary>
        /// <returns>The raw URL.</returns>
        public virtual string Action()
        {
            var request = this.GetHttpContext()?.Request;
            return request == null ? null : request.Path + request.QueryString;
        }

        /// <summary>Generates a DNN URL for the given action.</summary>
        /// <param name="actionName">The action name.</param>
        /// <returns>The URL.</returns>
        public virtual string Action(string actionName)
        {
            return this.GenerateUrl(actionName, null, new RouteValueDictionary());
        }

        /// <summary>Generates a DNN URL for the given action and route values.</summary>
        /// <param name="actionName">The action name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <returns>The URL.</returns>
        public virtual string Action(string actionName, RouteValueDictionary routeValues)
        {
            return this.GenerateUrl(actionName, null, routeValues);
        }

        /// <summary>Generates a DNN URL for the given action and route values.</summary>
        /// <param name="actionName">The action name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <returns>The URL.</returns>
        public virtual string Action(string actionName, object routeValues)
        {
            return this.GenerateUrl(actionName, null, TypeHelper.ObjectToDictionary(routeValues));
        }

        /// <summary>Generates a DNN URL for the given action and controller.</summary>
        /// <param name="actionName">The action name.</param>
        /// <param name="controllerName">The controller name.</param>
        /// <returns>The URL.</returns>
        public virtual string Action(string actionName, string controllerName)
        {
            return this.GenerateUrl(actionName, controllerName, new RouteValueDictionary());
        }

        /// <summary>Generates a DNN URL for the given action, controller, and route values.</summary>
        /// <param name="actionName">The action name.</param>
        /// <param name="controllerName">The controller name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <returns>The URL.</returns>
        public virtual string Action(string actionName, string controllerName, RouteValueDictionary routeValues)
        {
            return this.GenerateUrl(actionName, controllerName, routeValues);
        }

        /// <summary>Generates a DNN URL for the given action, controller, and route values.</summary>
        /// <param name="actionName">The action name.</param>
        /// <param name="controllerName">The controller name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <returns>The URL.</returns>
        public virtual string Action(string actionName, string controllerName, object routeValues)
        {
            return this.GenerateUrl(actionName, controllerName, TypeHelper.ObjectToDictionary(routeValues));
        }

        private HttpContext GetHttpContext()
        {
            return this.ViewContext?.HttpContext ?? this.controller?.ControllerContext?.HttpContext;
        }

        private string GenerateUrl(string actionName, string controllerName, RouteValueDictionary routeValues)
        {
            var currentController = this.controller?.ControllerContext?.RouteData.Values.TryGetValue("controller", out var c) == true ? c : null;
            routeValues["controller"] = controllerName ?? currentController;
            routeValues["action"] = actionName;
            return ModuleRoutingProvider.Instance().GenerateUrl(routeValues, this.ModuleContext);
        }
    }
}
