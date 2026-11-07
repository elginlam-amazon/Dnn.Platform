// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Routing;

    /// <summary>
    /// Produces the <see cref="RequestDelegate"/> which executes a DNN MVC module request. This is the
    /// ASP.NET Core endpoint-routing replacement of the legacy <c>System.Web.Routing.IRouteHandler</c>
    /// implementation: instead of returning an <c>IHttpHandler</c> it runs <see cref="DnnMvcHandler"/>.
    /// </summary>
    public class DnnMvcRouteHandler
    {
        private readonly IControllerFactory controllerFactory;

        /// <summary>Initializes a new instance of the <see cref="DnnMvcRouteHandler"/> class.</summary>
        public DnnMvcRouteHandler()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnMvcRouteHandler"/> class.</summary>
        /// <param name="controllerFactory">The controller factory.</param>
        public DnnMvcRouteHandler(IControllerFactory controllerFactory)
        {
            this.controllerFactory = controllerFactory;
        }

        /// <summary>Gets the controller factory used to create the module controllers, if one was supplied.</summary>
        protected IControllerFactory ControllerFactory => this.controllerFactory;

        /// <summary>Gets the request delegate which executes the DNN MVC module request.</summary>
        /// <returns>A <see cref="RequestDelegate"/> which can be mapped onto an endpoint.</returns>
        public RequestDelegate GetRequestDelegate()
        {
            return this.HandleRequestAsync;
        }

        /// <summary>Maps the DNN MVC module request handling onto the given endpoint route builder.</summary>
        /// <param name="endpoints">The endpoint route builder of the host.</param>
        /// <param name="pattern">The route pattern to map.</param>
        /// <returns>The convention builder of the mapped endpoint.</returns>
        public IEndpointConventionBuilder MapRoute(IEndpointRouteBuilder endpoints, string pattern)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            return endpoints.Map(pattern, this.GetRequestDelegate());
        }

        /// <summary>Executes the DNN MVC module for the current request.</summary>
        /// <param name="httpContext">The current request context.</param>
        /// <returns>A task.</returns>
        public virtual Task HandleRequestAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            var routeData = httpContext.GetRouteData() ?? new RouteData();

            // The legacy handler required session state (IRequiresSessionState) and asked the controller
            // factory for its SessionStateBehavior. ASP.NET Core has no equivalent: session is enabled by
            // the host (UseSession) and exposed through ISession, so all we can do is assert the controller
            // is routable and record whether a session is actually available for this request.
            this.EnsureControllerIsRouted(routeData);
            this.IsSessionAvailable(httpContext);

            return new DnnMvcHandler(httpContext, routeData).ProcessRequestAsync();
        }

        /// <summary>Determines whether an ASP.NET Core session is available for the current request.</summary>
        /// <param name="httpContext">The current request context.</param>
        /// <returns><see langword="true"/> if session state can be used in this request, otherwise <see langword="false"/>.</returns>
        protected virtual bool IsSessionAvailable(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            return httpContext.Features.Get<ISessionFeature>() is not null && httpContext.Session.IsAvailable;
        }

        /// <summary>Preserves the legacy "No Controller" guard of the route handler.</summary>
        /// <param name="routeData">The route data matched for the request.</param>
        protected virtual void EnsureControllerIsRouted(RouteData routeData)
        {
            ArgumentNullException.ThrowIfNull(routeData);

            var controllerName = routeData.Values["controller"] as string;
            if (string.IsNullOrWhiteSpace(controllerName))
            {
                throw new InvalidOperationException("No Controller");
            }
        }
    }
}
