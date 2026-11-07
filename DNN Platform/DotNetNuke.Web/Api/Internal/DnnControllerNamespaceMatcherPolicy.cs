// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.AspNetCore.Routing.Matching;

    /// <summary>
    /// Scopes the controller selection of a DNN service route to the controller namespaces that were
    /// supplied when the route was mapped, which is what the legacy
    /// <c>DnnHttpControllerSelector</c> did through <c>System.Web.Http.Dispatcher.IHttpControllerSelector</c>.
    /// </summary>
    /// <remarks>
    /// Two modules may both ship a controller with the same short name (for example
    /// <c>ServicesController</c>). ASP.NET Core matches a conventional <c>{controller}</c> route value
    /// against the controller's short name only, so without this policy both controllers would be
    /// candidates for both modules' routes. The policy invalidates every candidate whose controller type
    /// does not live in the namespaces of the matched <see cref="DnnServiceRoute"/>, and records the
    /// not-found/ambiguous outcome for <see cref="DnnControllerSelectionMiddleware"/> so that the exact
    /// legacy status codes and localized messages are still produced.
    /// </remarks>
    internal sealed class DnnControllerNamespaceMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
    {
        /// <summary>The <see cref="HttpContext.Items"/> key under which a failed selection is recorded.</summary>
        internal const string SelectionFailureKey = "DotNetNuke.Web.Api.ControllerSelectionFailure";

        private const string ControllerKey = "controller";

        private readonly DnnControllerSelector selector;

        /// <summary>Initializes a new instance of the <see cref="DnnControllerNamespaceMatcherPolicy"/> class.</summary>
        /// <param name="selector">The controller selector.</param>
        public DnnControllerNamespaceMatcherPolicy(DnnControllerSelector selector)
        {
            ArgumentNullException.ThrowIfNull(selector);

            this.selector = selector;
        }

        /// <inheritdoc />
        public override int Order => 0;

        /// <inheritdoc />
        public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            return endpoints.Any(endpoint => endpoint.Metadata.GetMetadata<DnnServiceRoute>() != null);
        }

        /// <inheritdoc />
        public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            ArgumentNullException.ThrowIfNull(candidates);

            for (var i = 0; i < candidates.Count; i++)
            {
                if (!candidates.IsValidCandidate(i))
                {
                    continue;
                }

                var endpoint = candidates[i].Endpoint;
                var route = endpoint.Metadata.GetMetadata<DnnServiceRoute>();
                if (route is null)
                {
                    continue;
                }

                var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
                if (action?.ControllerTypeInfo?.FullName is null)
                {
                    continue;
                }

                candidates[i].Values.TryGetValue(ControllerKey, out var controllerName);
                var result = this.selector.SelectController(
                    controllerName as string,
                    route.GetNameSpaces(),
                    httpContext.Request.GetDisplayUrl());

                if (!result.Success)
                {
                    httpContext.Items[SelectionFailureKey] = result;
                    candidates.SetValidity(i, false);
                    continue;
                }

                if (!string.Equals(action.ControllerTypeInfo.FullName, result.ControllerTypeFullName, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.SetValidity(i, false);
                }
            }

            return Task.CompletedTask;
        }
    }
}
