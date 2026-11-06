// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionResults
{
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Common;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Framework.Modules;
    using DotNetNuke.Web.Mvc.Helpers;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Redirects to a DNN MVC module route.</summary>
    /// <remarks>
    /// The legacy implementation overrode <c>ExecuteResult(ControllerContext)</c>. In ASP.NET Core an
    /// action result is executed through <see cref="IActionResult.ExecuteResultAsync(ActionContext)"/>, and
    /// named routes are resolved with <see cref="LinkGenerator"/> instead of the legacy route collection.
    /// The legacy "Cannot Redirect In Child Action" guard is gone because ASP.NET Core has no child actions
    /// (<c>Html.Action</c> became view components, which never execute an action result).
    /// </remarks>
    internal class DnnRedirecttoRouteResult : RedirectToRouteResult
    {
        /// <summary>Initializes a new instance of the <see cref="DnnRedirecttoRouteResult"/> class.</summary>
        /// <param name="actionName">The action to redirect to.</param>
        /// <param name="controllerName">The controller to redirect to.</param>
        /// <param name="routeName">The name of the route to redirect to.</param>
        /// <param name="routeValues">The route values.</param>
        /// <param name="permanent">Whether the redirect is permanent.</param>
        public DnnRedirecttoRouteResult(string actionName, string controllerName, string routeName, RouteValueDictionary routeValues, bool permanent)
            : base(routeName, routeValues, permanent)
        {
            this.NavigationManager = Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
            this.ActionName = actionName;
            this.ControllerName = controllerName;
        }

        /// <summary>Initializes a new instance of the <see cref="DnnRedirecttoRouteResult"/> class.</summary>
        /// <param name="actionName">The action to redirect to.</param>
        /// <param name="controllerName">The controller to redirect to.</param>
        /// <param name="routeName">The name of the route to redirect to.</param>
        /// <param name="routeValues">The route values.</param>
        /// <param name="permanent">Whether the redirect is permanent.</param>
        /// <param name="url">The DNN URL helper of the redirecting controller.</param>
        public DnnRedirecttoRouteResult(string actionName, string controllerName, string routeName, RouteValueDictionary routeValues, bool permanent, DnnUrlHelper url)
            : this(actionName, controllerName, routeName, routeValues, permanent)
        {
            this.Url = url;
        }

        /// <summary>Gets the DNN URL helper used to generate the redirect URL.</summary>
        public DnnUrlHelper Url { get; private set; }

        /// <summary>Gets the name of the action to redirect to.</summary>
        public string ActionName { get; private set; }

        /// <summary>Gets the name of the controller to redirect to.</summary>
        public string ControllerName { get; private set; }

        /// <summary>Gets the navigation manager.</summary>
        protected INavigationManager NavigationManager { get; }

        /// <inheritdoc />
        public override Task ExecuteResultAsync(ActionContext context)
        {
            Requires.NotNull("context", context);

            var url = this.GenerateUrl(context);

            context.HttpContext.Response.Redirect(url, this.Permanent);

            return Task.CompletedTask;
        }

        private string GenerateUrl(ActionContext context)
        {
            var controller = ModuleApplication.GetCurrentDnnController(context.HttpContext);

            if (this.Url is not null && controller is IDnnController)
            {
                return this.Url.Action(this.ActionName, this.ControllerName);
            }

            if (!string.IsNullOrEmpty(this.RouteName))
            {
                // Named route behavior: the route names registered by the MvcRoutingManager are the
                // endpoint names, so the link generator can resolve them.
                var linkGenerator = context.HttpContext.RequestServices?.GetService<LinkGenerator>();
                var generated = linkGenerator?.GetPathByRouteValues(
                    context.HttpContext,
                    this.RouteName,
                    this.RouteValues ?? new RouteValueDictionary());
                if (!string.IsNullOrEmpty(generated))
                {
                    return generated;
                }
            }

            return this.NavigationManager.NavigateURL();
        }
    }
}
