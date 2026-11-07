// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using DotNetNuke.Tests.Utilities;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Routing;

    using Moq;

    /// <summary>Builds the ASP.NET Core contexts the MVC module tests execute against.</summary>
    /// <remarks>
    /// The net48 version built mocks of the MVC 5 abstract context, request and response base classes as
    /// doubles. On ASP.NET Core the pre-populated context comes from
    /// <see cref="HttpContextHelper"/> (real writable <see cref="HttpContext.Items"/>, a request with an
    /// empty <see cref="HttpRequest.Query"/>/<see cref="HttpRequest.QueryString"/>, and a response with
    /// writable headers and body).
    /// </remarks>
    public class MockHelper
    {
        /// <summary>Creates a pre-populated ASP.NET Core <see cref="HttpContext"/> test double.</summary>
        /// <returns>The HTTP context.</returns>
        public static HttpContext CreateMockHttpContext()
        {
            return CreateMockHttpContext(new Dictionary<object, object>());
        }

        /// <summary>Creates a pre-populated <see cref="HttpContext"/> whose <see cref="HttpContext.Items"/> is the given dictionary.</summary>
        /// <param name="items">The dictionary which backs <see cref="HttpContext.Items"/>.</param>
        /// <returns>The HTTP context.</returns>
        public static HttpContext CreateMockHttpContext(IDictionary<object, object> items)
        {
            var mockContext = HttpContextHelper.RegisterMockHttpContext();
            if (items is not null)
            {
                mockContext.SetupGet(c => c.Items).Returns(items);
            }

            return mockContext.Object;
        }

        /// <summary>
        /// Creates a pre-populated <see cref="HttpContext"/> seeded with the given items.
        /// </summary>
        /// <param name="items">The items to copy into <see cref="HttpContext.Items"/>.</param>
        /// <returns>The HTTP context.</returns>
        /// <remarks>
        /// Kept for callers which used the net48 <c>Dictionary&lt;string, object&gt;</c> signature;
        /// the Core <see cref="HttpContext.Items"/> dictionary is keyed by <see cref="object"/>.
        /// </remarks>
        public static HttpContext CreateMockHttpContext(IDictionary<string, object> items)
        {
            var coreItems = new Dictionary<object, object>();
            if (items is not null)
            {
                foreach (var pair in items)
                {
                    coreItems[pair.Key] = pair.Value;
                }
            }

            return CreateMockHttpContext(coreItems);
        }

        /// <summary>Creates a pre-populated <see cref="HttpContext"/> for the given request URL.</summary>
        /// <param name="requestUrl">The absolute URL of the request.</param>
        /// <returns>The HTTP context.</returns>
        /// <remarks>
        /// Replaces the net48 <c>Request.Url</c>/<c>ApplicationPath</c>/<c>RawUrl</c> setups: the URL is
        /// split onto <see cref="HttpRequest.Scheme"/>, <see cref="HttpRequest.Host"/>,
        /// <see cref="HttpRequest.Path"/> and <see cref="HttpRequest.QueryString"/>.
        /// <c>Response.ApplyAppPathModifier</c> has no ASP.NET Core equivalent and is dropped.
        /// </remarks>
        public static HttpContext CreateMockHttpContext(string requestUrl)
        {
            var httpContext = CreateMockHttpContext();
            var uri = new Uri(requestUrl);
            var request = httpContext.Request;

            request.Scheme = uri.Scheme;
            request.Host = uri.IsDefaultPort ? new HostString(uri.Host) : new HostString(uri.Host, uri.Port);
            request.IsHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            request.PathBase = PathString.Empty;
            request.Path = new PathString(string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath);
            request.QueryString = string.IsNullOrEmpty(uri.Query) ? QueryString.Empty : new QueryString(uri.Query);
            request.Query = new QueryCollection(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query));

            return httpContext;
        }

        /// <summary>Creates a <see cref="ControllerContext"/> around the given controller.</summary>
        /// <param name="controller">The controller of the request, published onto the HTTP context.</param>
        /// <returns>The controller context.</returns>
        public static ControllerContext CreateMockControllerContext(object controller)
        {
            return CreateMockControllerContext(CreateMockHttpContext(), new RouteData(), controller);
        }

        /// <summary>Creates a <see cref="ControllerContext"/> around the given HTTP context.</summary>
        /// <param name="httpContext">The HTTP context of the request.</param>
        /// <returns>The controller context.</returns>
        public static ControllerContext CreateMockControllerContext(HttpContext httpContext)
        {
            return CreateMockControllerContext(httpContext, new RouteData(), null);
        }

        /// <summary>Creates a <see cref="ControllerContext"/> with a fresh HTTP context and route data.</summary>
        /// <returns>The controller context.</returns>
        public static ControllerContext CreateMockControllerContext()
        {
            return CreateMockControllerContext(CreateMockHttpContext(), new RouteData(), null);
        }

        /// <summary>Creates a <see cref="ControllerContext"/> with the given route data.</summary>
        /// <param name="routeData">The route data of the request.</param>
        /// <returns>The controller context.</returns>
        public static ControllerContext CreateMockControllerContext(RouteData routeData)
        {
            return CreateMockControllerContext(CreateMockHttpContext(), routeData, null);
        }

        /// <summary>Creates a <see cref="ControllerContext"/> with the given HTTP context and route data.</summary>
        /// <param name="httpContext">The HTTP context of the request, a new one is created when <see langword="null"/>.</param>
        /// <param name="routeData">The route data of the request.</param>
        /// <returns>The controller context.</returns>
        public static ControllerContext CreateMockControllerContext(HttpContext httpContext, RouteData routeData)
        {
            return CreateMockControllerContext(httpContext, routeData, null);
        }

        /// <summary>Creates a <see cref="ControllerContext"/> with the given HTTP context, route data and controller.</summary>
        /// <param name="httpContext">The HTTP context of the request, a new one is created when <see langword="null"/>.</param>
        /// <param name="routeData">The route data of the request.</param>
        /// <param name="controller">The controller of the request, published onto the HTTP context when not <see langword="null"/>.</param>
        /// <returns>The controller context.</returns>
        /// <remarks>
        /// The ASP.NET Core <see cref="ControllerContext"/> does not carry the controller instance, the DNN
        /// helpers and filters resolve it through
        /// <see cref="ModuleApplication.GetCurrentDnnController(HttpContext)"/>, which reads
        /// <c>HttpContext.Items</c>.
        /// </remarks>
        public static ControllerContext CreateMockControllerContext(HttpContext httpContext, RouteData routeData, object controller)
        {
            httpContext ??= CreateMockHttpContext();
            if (controller is not null)
            {
                httpContext.Items[ModuleApplication.CurrentControllerItemKey] = controller;
            }

            return new ControllerContext(new ActionContext(httpContext, routeData ?? new RouteData(), new ActionDescriptor()));
        }

        /// <summary>Creates a real <see cref="ViewContext"/> for the given request URL.</summary>
        /// <param name="url">The absolute URL of the request.</param>
        /// <returns>The view context.</returns>
        /// <remarks>
        /// ASP.NET Core <see cref="ViewContext"/> members are not virtual, so a real instance is built
        /// instead of a mock.
        /// </remarks>
        public static ViewContext CreateViewContext(string url)
        {
            var routeData = new RouteData();
            routeData.Values["controller"] = "Controller";
            routeData.Values["action"] = "Action";
            routeData.Values["id"] = "Id";

            var controllerContext = CreateMockControllerContext(CreateMockHttpContext(url), routeData, null);
            return CreateViewContext(controllerContext);
        }

        /// <summary>Creates a real <see cref="ViewContext"/> for the given action context.</summary>
        /// <param name="actionContext">The context of the executing action.</param>
        /// <returns>The view context.</returns>
        public static ViewContext CreateViewContext(ActionContext actionContext)
        {
            var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
            var tempData = new TempDataDictionary(actionContext.HttpContext, Mock.Of<ITempDataProvider>());
            return new ViewContext(
                actionContext,
                Mock.Of<IView>(),
                viewData,
                tempData,
                new StringWriter(),
                new HtmlHelperOptions());
        }
    }
}
