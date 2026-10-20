// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.Api.Auth;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.ActionConstraints;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Handles the requests of a services framework route: sets up the DNN context, runs the authentication message handlers
    /// and selects and invokes the controller action (the controller is looked up in the namespaces of the route, the action by
    /// its name and the HTTP method, following the ASP.NET Web API conventions).
    /// </summary>
    internal sealed class ServicesFrameworkEndpoint
    {
        private const string ControllerKey = "controller";
        private const string ActionKey = "action";
        private const string ControllerSuffix = "Controller";
        private static readonly string[] HttpMethodPrefixes = ["GET", "POST", "PUT", "DELETE", "HEAD", "OPTIONS", "PATCH"];

        private readonly DnnServiceRoute route;
        private readonly ConcurrentDictionary<(int Version, string Controller, string Action, string Method), ControllerActionDescriptor[]> selectionCache =
            new ConcurrentDictionary<(int Version, string Controller, string Action, string Method), ControllerActionDescriptor[]>();

        /// <summary>Initializes a new instance of the <see cref="ServicesFrameworkEndpoint"/> class.</summary>
        /// <param name="route">The route.</param>
        public ServicesFrameworkEndpoint(DnnServiceRoute route)
        {
            this.route = route ?? throw new ArgumentNullException(nameof(route));
        }

        /// <summary>Handles a request.</summary>
        /// <param name="httpContext">The HTTP context.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext httpContext)
        {
            var cancellationToken = httpContext.RequestAborted;
            Thread.CurrentPrincipal = httpContext.User;
            var requestMessage = ServicesFrameworkRequestMessage.Create(httpContext);
            var authHandlers = ServicesRoutingManager.GetAuthHandlers();
            try
            {
                ServicesRoutingManager.ContextHandler.Process(requestMessage);
                foreach (var handler in authHandlers)
                {
                    var handlerResponse = handler.OnInboundRequest(requestMessage, cancellationToken);
                    if (handlerResponse != null)
                    {
                        await HttpResponseMessageOutputFormatter.WriteResponseMessageAsync(httpContext, handlerResponse);
                        return;
                    }
                }

                ServicesRoutingManager.WebFormsAuthHandler.Process(requestMessage);
            }
            catch (HttpResponseException ex)
            {
                await HttpResponseMessageOutputFormatter.WriteResponseMessageAsync(httpContext, ex.Response);
                return;
            }

            Thread.CurrentPrincipal = httpContext.User;
            if (authHandlers.Count > 0)
            {
                httpContext.Response.OnStarting(() => ApplyOutboundHandlers(httpContext, requestMessage, authHandlers, cancellationToken));
            }

            var descriptor = this.SelectAction(httpContext, out var errorStatusCode, out var errorMessage);
            if (descriptor == null)
            {
                await HttpResponseMessageOutputFormatter.WriteResponseMessageAsync(httpContext, httpContext.Request.CreateErrorResponse(errorStatusCode, errorMessage));
                return;
            }

            var routeData = new RouteData(httpContext.GetRouteData());
            routeData.Values[ControllerKey] = descriptor.ControllerName;
            routeData.Values[ActionKey] = descriptor.ActionName;
            routeData.DataTokens["namespaces"] = this.route.Namespaces;
            routeData.DataTokens["name"] = this.route.Name;

            var actionContext = new ActionContext(httpContext, routeData, descriptor);
            var invoker = httpContext.RequestServices.GetRequiredService<IActionInvokerFactory>().CreateInvoker(actionContext);
            if (invoker == null)
            {
                throw new InvalidOperationException($"Unable to create an action invoker for {descriptor.DisplayName}.");
            }

            await invoker.InvokeAsync();
        }

        private static Task ApplyOutboundHandlers(HttpContext httpContext, HttpRequestMessage requestMessage, IReadOnlyList<AuthMessageHandlerBase> authHandlers, CancellationToken cancellationToken)
        {
            using var responseMessage = new HttpResponseMessage((HttpStatusCode)httpContext.Response.StatusCode) { RequestMessage = requestMessage, };
            var processed = responseMessage;

            // the response flows back through the message handlers in the reverse order
            for (var i = authHandlers.Count - 1; i >= 0; i--)
            {
                processed = authHandlers[i].OnOutboundResponse(processed, cancellationToken) ?? processed;
            }

            foreach (var header in processed.Headers)
            {
                httpContext.Response.Headers[header.Key] = header.Value.ToArray();
            }

            return Task.CompletedTask;
        }

        private static string GetHttpMethodOfConvention(ControllerActionDescriptor descriptor)
        {
            var methodName = descriptor.MethodInfo.Name;
            return HttpMethodPrefixes.FirstOrDefault(prefix => methodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ?? "POST";
        }

        private static bool SupportsHttpMethod(ControllerActionDescriptor descriptor, string httpMethod)
        {
            var constraints = descriptor.ActionConstraints?.OfType<HttpMethodActionConstraint>().ToArray() ?? [];
            if (constraints.Length > 0)
            {
                return constraints.Any(c => c.HttpMethods.Contains(httpMethod, StringComparer.OrdinalIgnoreCase));
            }

            return string.Equals(GetHttpMethodOfConvention(descriptor), httpMethod, StringComparison.OrdinalIgnoreCase);
        }

        private static int CountBoundParameters(ControllerActionDescriptor descriptor, HttpContext httpContext)
        {
            var routeValues = httpContext.Request.RouteValues;
            var query = httpContext.Request.Query;
            return descriptor.Parameters.Count(p => routeValues.ContainsKey(p.Name) || query.ContainsKey(p.Name));
        }

        private ControllerActionDescriptor SelectAction(HttpContext httpContext, out HttpStatusCode errorStatusCode, out string errorMessage)
        {
            errorStatusCode = HttpStatusCode.NotFound;
            errorMessage = null;
            var requestUri = httpContext.Request.RequestUri;
            var controllerName = httpContext.Request.RouteValues[ControllerKey] as string;
            var actionName = httpContext.Request.RouteValues[ActionKey] as string;
            if (this.route.Namespaces.Count == 0 || string.IsNullOrEmpty(controllerName))
            {
                errorMessage = "Unable to locate a controller for " + requestUri;
                return null;
            }

            var provider = httpContext.RequestServices.GetRequiredService<IActionDescriptorCollectionProvider>();
            var actionDescriptors = provider.ActionDescriptors;
            var key = (actionDescriptors.Version, controllerName.ToUpperInvariant(), (actionName ?? string.Empty).ToUpperInvariant(), httpContext.Request.Method.ToUpperInvariant());
            var candidates = this.selectionCache.GetOrAdd(key, cacheKey => this.FindCandidates(actionDescriptors.Items, controllerName, actionName, out var unused));
            if (candidates.Length == 0)
            {
                this.FindCandidates(actionDescriptors.Items, controllerName, actionName, out var controllerMatches);
                if (controllerMatches == 0)
                {
                    errorMessage = string.Format(CultureInfo.CurrentCulture, Localization.GetString("ControllerNotFound", Localization.ExceptionsResourceFile), requestUri, string.Join(", ", this.route.Namespaces));
                    return null;
                }

                if (controllerMatches > 1)
                {
                    errorStatusCode = HttpStatusCode.Conflict;
                    errorMessage = string.Format(CultureInfo.CurrentCulture, Localization.GetString("AmbiguousController", Localization.ExceptionsResourceFile), controllerName, string.Join(", ", this.route.Namespaces));
                    return null;
                }

                errorMessage = $"No action was found on the controller '{controllerName}' that matches the request.";
                return null;
            }

            var methodMatches = candidates.Where(c => SupportsHttpMethod(c, httpContext.Request.Method)).ToArray();
            if (methodMatches.Length == 0)
            {
                errorStatusCode = HttpStatusCode.MethodNotAllowed;
                errorMessage = $"The requested resource does not support http method '{httpContext.Request.Method}'.";
                return null;
            }

            return methodMatches.Length == 1
                ? methodMatches[0]
                : methodMatches.OrderByDescending(c => CountBoundParameters(c, httpContext)).ThenBy(c => c.Parameters.Count).First();
        }

        private ControllerActionDescriptor[] FindCandidates(IReadOnlyList<ActionDescriptor> actionDescriptors, string controllerName, string actionName, out int controllerTypeCount)
        {
            var fullNames = this.route.Namespaces
                .Select(ns => $"{ns}.{controllerName}{ControllerSuffix}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var controllerActions = actionDescriptors
                .OfType<ControllerActionDescriptor>()
                .Where(d => typeof(ControllerBase).IsAssignableFrom(d.ControllerTypeInfo) && fullNames.Contains(d.ControllerTypeInfo.FullName))
                .ToArray();

            controllerTypeCount = controllerActions.Select(d => d.ControllerTypeInfo).Distinct().Count();
            if (controllerTypeCount != 1)
            {
                return [];
            }

            return controllerActions
                .Where(d => string.IsNullOrEmpty(actionName) || string.Equals(d.ActionName, actionName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }
}
