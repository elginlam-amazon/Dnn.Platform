// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    using DotNetNuke.Framework.Reflections;
    using DotNetNuke.Internal.SourceGenerators;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Maps the Model-View-Presenter handlers and web services as endpoints.</summary>
    /// <remarks>
    /// This is the replacement for the <c>.ashx</c> handler registrations and the <c>.asmx</c>
    /// endpoints the WebForms host served. It is called from
    /// <c>DnnWebApiHostWiringExtensions.MapDnnServiceRoutes</c>, so a DNN host picks these endpoints
    /// up with the rest of the DNN service routes.
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public static partial class MvpEndpointRouteBuilderExtensions
    {
        private static readonly JsonSerializerOptions ResponseOptions = new JsonSerializerOptions();

        /// <summary>
        /// Maps every <see cref="HttpHandlerView"/> and <see cref="WebServiceViewBase"/> which
        /// declares an <see cref="MvpHandlerRouteAttribute"/>.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The <paramref name="endpoints"/>, for chaining.</returns>
        public static IEndpointRouteBuilder MapDnnMvpEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            foreach (var handlerType in LocateTypes(typeof(IHttpHandlerView)))
            {
                foreach (var route in GetRoutes(handlerType))
                {
                    MapHandler(endpoints, handlerType, route);
                }
            }

            foreach (var serviceType in LocateTypes(typeof(IWebServiceViewBase)))
            {
                foreach (var route in GetRoutes(serviceType))
                {
                    MapService(endpoints, serviceType, route);
                }
            }

            return endpoints;
        }

        private static IEnumerable<Type> LocateTypes(Type contract)
        {
            return new TypeLocator().GetAllMatchingTypes(candidate =>
                candidate != null
                && candidate.IsClass
                && !candidate.IsAbstract
                && !candidate.IsGenericTypeDefinition
                && contract.IsAssignableFrom(candidate)
                && candidate.IsDefined(typeof(MvpHandlerRouteAttribute), false));
        }

        private static IEnumerable<MvpHandlerRouteAttribute> GetRoutes(Type type)
        {
            return type
                .GetCustomAttributes(typeof(MvpHandlerRouteAttribute), false)
                .OfType<MvpHandlerRouteAttribute>();
        }

        private static void MapHandler(IEndpointRouteBuilder endpoints, Type handlerType, MvpHandlerRouteAttribute route)
        {
            var builder = endpoints.Map(
                route.GetRoutePattern(),
                async context =>
                {
                    // one instance per request, as ASP.NET created one handler per request.
                    var view = (IHttpHandlerView)ActivatorUtilities.CreateInstance(context.RequestServices, handlerType);
                    try
                    {
                        await view.HandleRequestAsync(context);
                    }
                    finally
                    {
                        (view as IDisposable)?.Dispose();
                    }
                });

            Constrain(builder, route);
        }

        private static void MapService(IEndpointRouteBuilder endpoints, Type serviceType, MvpHandlerRouteAttribute route)
        {
            var builder = endpoints.Map(
                route.GetRoutePattern().TrimEnd('/') + "/{operation}",
                async context =>
                {
                    var operation = context.Request.RouteValues.TryGetValue("operation", out var value)
                        ? value?.ToString()
                        : null;

                    var view = (IWebServiceViewBase)ActivatorUtilities.CreateInstance(context.RequestServices, serviceType);
                    try
                    {
                        var result = await view.InvokeAsync(operation, context);

                        if (context.Response.HasStarted)
                        {
                            // the operation wrote the response itself.
                            return;
                        }

                        context.Response.ContentType = "application/json; charset=utf-8";
                        await JsonSerializer.SerializeAsync(context.Response.Body, result, ResponseOptions, context.RequestAborted);
                    }
                    catch (MvpServiceOperationNotFoundException)
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                    }
                    finally
                    {
                        (view as IDisposable)?.Dispose();
                    }
                });

            Constrain(builder, route);
        }

        private static void Constrain(IEndpointConventionBuilder builder, MvpHandlerRouteAttribute route)
        {
            var methods = route.GetMethods();
            if (methods.Length > 0)
            {
                builder.WithMetadata(new HttpMethodMetadata(methods));
            }
        }
    }
}
