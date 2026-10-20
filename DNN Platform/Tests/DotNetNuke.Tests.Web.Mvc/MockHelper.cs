// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc
{
    using System.Collections.Generic;
    using System.Reflection;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Routing;

    public class MockHelper
    {
        public static HttpContext CreateMockHttpContext()
        {
            return CreateMockHttpContext(new Dictionary<object, object>());
        }

        public static HttpContext CreateMockHttpContext(IDictionary<object, object> items)
        {
            var context = new DefaultHttpContext();
            foreach (var pair in items)
            {
                context.Items[pair.Key] = pair.Value;
            }

            return context;
        }

        public static HttpContext CreateMockHttpContext(string requestUrl)
        {
            var context = new DefaultHttpContext();

            if (!string.IsNullOrEmpty(requestUrl))
            {
                var uri = new System.Uri(requestUrl);
                context.Request.Scheme = uri.Scheme;
                context.Request.Host = uri.IsDefaultPort ? new HostString(uri.Host) : new HostString(uri.Host, uri.Port);
                context.Request.PathBase = new PathString(uri.AbsolutePath.TrimEnd('/'));
                context.Request.Path = new PathString(uri.AbsolutePath);
                context.Request.QueryString = string.IsNullOrEmpty(uri.Query) ? QueryString.Empty : new QueryString(uri.Query);
            }

            return context;
        }

        public static ControllerContext CreateMockControllerContext()
        {
            return CreateMockControllerContext((HttpContext)null, new RouteData());
        }

        public static ControllerContext CreateMockControllerContext(HttpContext httpContext)
        {
            return CreateMockControllerContext(httpContext, new RouteData());
        }

        public static ControllerContext CreateMockControllerContext(RouteData routeData)
        {
            return CreateMockControllerContext(null, routeData);
        }

        public static ControllerContext CreateMockControllerContext(HttpContext httpContext, RouteData routeData)
        {
            var actionContext = new ActionContext(
                httpContext ?? CreateMockHttpContext(),
                routeData ?? new RouteData(),
                new ControllerActionDescriptor());
            return new ControllerContext(actionContext);
        }

        public static Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext CreateAuthorizationFilterContext(
            System.Type controllerType,
            string actionMethodName,
            params Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata[] filters)
        {
            var methodInfo = controllerType.GetMethod(actionMethodName);
            var descriptor = new ControllerActionDescriptor
            {
                ControllerTypeInfo = controllerType.GetTypeInfo(),
                MethodInfo = methodInfo,
                ControllerName = controllerType.Name,
                ActionName = actionMethodName,
            };

            var actionContext = new ActionContext(CreateMockHttpContext(), new RouteData(), descriptor);
            return new Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext(
                actionContext,
                new System.Collections.Generic.List<Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata>(filters));
        }

        public static ViewContext CreateViewContext(string url)
        {
            var routeData = new RouteData();
            routeData.Values["controller"] = "Controller";
            routeData.Values["action"] = "Action";
            routeData.Values["id"] = "Id";

            var actionContext = new ActionContext(
                CreateMockHttpContext(url),
                routeData,
                new ControllerActionDescriptor());

            var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
            var tempData = new TempDataDictionary(actionContext.HttpContext, new Moq.Mock<ITempDataProvider>().Object);

            return new ViewContext(
                actionContext,
                new Moq.Mock<Microsoft.AspNetCore.Mvc.ViewEngines.IView>().Object,
                viewData,
                tempData,
                new System.IO.StringWriter(),
                new Microsoft.AspNetCore.Mvc.ViewFeatures.HtmlHelperOptions());
        }
    }
}
