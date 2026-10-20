// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionResults
{
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Common;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Helpers;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A redirect result that resolves a DNN navigation URL and redirects the browser to it.</summary>
    internal class DnnRedirecttoRouteResult : ActionResult
    {
        /// <summary>Initializes a new instance of the <see cref="DnnRedirecttoRouteResult"/> class.</summary>
        /// <param name="actionName">The action name.</param>
        /// <param name="controllerName">The controller name.</param>
        /// <param name="routeName">The route name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <param name="permanent">Whether the redirect is permanent.</param>
        public DnnRedirecttoRouteResult(string actionName, string controllerName, string routeName, RouteValueDictionary routeValues, bool permanent)
        {
            this.NavigationManager = Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
            this.ActionName = actionName;
            this.ControllerName = controllerName;
            this.RouteName = routeName;
            this.RouteValues = routeValues;
            this.Permanent = permanent;
        }

        /// <summary>Initializes a new instance of the <see cref="DnnRedirecttoRouteResult"/> class.</summary>
        /// <param name="actionName">The action name.</param>
        /// <param name="controllerName">The controller name.</param>
        /// <param name="routeName">The route name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <param name="permanent">Whether the redirect is permanent.</param>
        /// <param name="url">The DNN URL helper.</param>
        public DnnRedirecttoRouteResult(string actionName, string controllerName, string routeName, RouteValueDictionary routeValues, bool permanent, DnnUrlHelper url)
            : this(actionName, controllerName, routeName, routeValues, permanent)
        {
            this.Url = url;
        }

        /// <summary>Gets the DNN URL helper.</summary>
        public DnnUrlHelper Url { get; private set; }

        /// <summary>Gets the action name.</summary>
        public string ActionName { get; private set; }

        /// <summary>Gets the controller name.</summary>
        public string ControllerName { get; private set; }

        /// <summary>Gets the route name.</summary>
        public string RouteName { get; private set; }

        /// <summary>Gets the route values.</summary>
        public RouteValueDictionary RouteValues { get; private set; }

        /// <summary>Gets a value indicating whether the redirect is permanent.</summary>
        public bool Permanent { get; private set; }

        /// <summary>Gets the navigation manager.</summary>
        protected INavigationManager NavigationManager { get; }

        /// <inheritdoc />
        public override Task ExecuteResultAsync(ActionContext context)
        {
            Requires.NotNull("context", context);

            string url;
            if (this.Url != null && context.HttpContext.Items.TryGetValue(typeof(IDnnController), out var controllerObj) && controllerObj is IDnnController)
            {
                url = this.Url.Action(this.ActionName, this.ControllerName);
            }
            else
            {
                url = this.NavigationManager.NavigateURL();
            }

            context.HttpContext.Response.Redirect(url, this.Permanent);
            return Task.CompletedTask;
        }
    }
}
