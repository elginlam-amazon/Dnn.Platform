// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc.Helpers
{
    using System;
    using System.Net;

    using DotNetNuke.Common;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Common;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Routing;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.Routing;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    public class DnnUrlHelper
    {
        private readonly ActionContext actionContext;

        private readonly IDnnController controller;

        /// <summary>Initializes a new instance of the <see cref="DnnUrlHelper"/> class.</summary>
        /// <param name="viewContext">The view context.</param>
        public DnnUrlHelper(ViewContext viewContext)
            : this(viewContext, (RouteCollection)null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnUrlHelper"/> class.</summary>
        /// <param name="actionContext">The context of the executing action (a <see cref="ControllerContext"/> is an <see cref="ActionContext"/>).</param>
        /// <param name="controller">The DNN controller.</param>
        public DnnUrlHelper(ActionContext actionContext, IDnnController controller)
        {
            Requires.NotNull("actionContext", actionContext);
            Requires.NotNull("controller", controller);

            this.actionContext = actionContext;
            this.UrlHelper = CreateUrlHelper(actionContext);
            this.controller = controller;
            this.ModuleContext = this.controller.ModuleContext;
        }

        /// <summary>Initializes a new instance of the <see cref="DnnUrlHelper"/> class.</summary>
        /// <param name="viewContext">The view context.</param>
        /// <param name="routeCollection">The route collection.</param>
        /// <exception cref="InvalidOperationException">The DnnUrlHelper class can only be used in Views that inherit from DnnWebViewPage.</exception>
        public DnnUrlHelper(ViewContext viewContext, RouteCollection routeCollection)
        {
            Requires.NotNull("viewContext", viewContext);

            this.actionContext = viewContext;
            this.RouteCollection = routeCollection;
            this.UrlHelper = CreateUrlHelper(viewContext);

            this.controller = Framework.Modules.ModuleApplication.GetCurrentDnnController(viewContext.HttpContext);

            if (this.controller == null)
            {
                throw new InvalidOperationException("The DnnUrlHelper class can only be used in Views that inherit from DnnWebViewPage");
            }

            this.ModuleContext = this.controller.ModuleContext;
        }

        public ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets the route collection this helper was created with, if any.</summary>
        /// <remarks>ASP.NET Core uses endpoint routing, there is no ambient route table, so this is only
        /// populated when a route collection was passed to the constructor.</remarks>
        public RouteCollection RouteCollection { get; }

        internal IUrlHelper UrlHelper { get; set; }

        private HttpRequest Request => this.actionContext?.HttpContext?.Request;

        /// <summary>URL encodes the given URL.</summary>
        /// <param name="url">The URL to encode.</param>
        /// <returns>The encoded URL.</returns>
        public virtual string Encode(string url)
        {
            return WebUtility.UrlEncode(url);
        }

        /// <summary>Converts a virtual (relative) path to an application absolute path.</summary>
        ///
        /// <returns>
        /// The application absolute path.
        /// </returns>
        /// <param name="contentPath">The virtual path of the content.</param>
        public virtual string Content(string contentPath)
        {
            return this.UrlHelper.Content(contentPath);
        }

        /// <summary>Returns a value that indicates whether the URL is local.</summary>
        ///
        /// <returns>
        /// true if the URL is local; otherwise, false.
        /// </returns>
        /// <param name="url">The URL.</param>
        public virtual bool IsLocalUrl(string url)
        {
            return this.UrlHelper.IsLocalUrl(url);
        }

        /// <summary>Gets the URL of the current request.</summary>
        /// <returns>The raw URL of the current request.</returns>
        public virtual string Action()
        {
            var request = this.Request;
            return request is null ? null : $"{request.Path}{request.QueryString}";
        }

        public virtual string Action(string actionName)
        {
            return this.GenerateUrl(actionName, null, new RouteValueDictionary());
        }

        public virtual string Action(string actionName, RouteValueDictionary routeValues)
        {
            return this.GenerateUrl(actionName, null, routeValues);
        }

        public virtual string Action(string actionName, object routeValues)
        {
            return this.GenerateUrl(actionName, null, TypeHelper.ObjectToDictionary(routeValues));
        }

        public virtual string Action(string actionName, string controllerName)
        {
            return this.GenerateUrl(actionName, controllerName, new RouteValueDictionary());
        }

        public virtual string Action(string actionName, string controllerName, RouteValueDictionary routeValues)
        {
            return this.GenerateUrl(actionName, controllerName, routeValues);
        }

        public virtual string Action(string actionName, string controllerName, object routeValues)
        {
            return this.GenerateUrl(actionName, controllerName, TypeHelper.ObjectToDictionary(routeValues));
        }

        /// <summary>Generates a URL for the given named route.</summary>
        /// <param name="routeName">The route name.</param>
        /// <returns>The generated URL.</returns>
        public virtual string RouteUrl(string routeName)
        {
            return this.UrlHelper.RouteUrl(routeName);
        }

        /// <summary>Generates a URL for the given route values.</summary>
        /// <param name="routeValues">The route values.</param>
        /// <returns>The generated URL.</returns>
        public virtual string RouteUrl(object routeValues)
        {
            return this.UrlHelper.RouteUrl(routeValues);
        }

        /// <summary>Generates a URL for the given named route and route values.</summary>
        /// <param name="routeName">The route name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <returns>The generated URL.</returns>
        public virtual string RouteUrl(string routeName, object routeValues)
        {
            return this.UrlHelper.RouteUrl(routeName, routeValues);
        }

        private static IUrlHelper CreateUrlHelper(ActionContext actionContext)
        {
            var serviceProvider = actionContext.HttpContext?.RequestServices ?? Globals.GetCurrentServiceProvider();
            return serviceProvider.GetRequiredService<IUrlHelperFactory>().GetUrlHelper(actionContext);
        }

        private string GenerateUrl(string actionName, string controllerName, RouteValueDictionary routeValues)
        {
            routeValues["controller"] = controllerName ?? this.controller.ControllerContext?.RouteData.Values["controller"];
            routeValues["action"] = actionName;
            return ModuleRoutingProvider.Instance().GenerateUrl(routeValues, this.ModuleContext);
        }
    }
}
