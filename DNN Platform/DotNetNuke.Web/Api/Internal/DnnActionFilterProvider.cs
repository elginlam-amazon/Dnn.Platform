// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System.Linq;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>Gets action filters for web API actions.</summary>
    /// <remarks>
    /// Replaces the Web API 2 <c>DnnActionFilterProvider</c> (an
    /// <c>System.Web.Http.Filters.IFilterProvider</c> which replaced <c>ActionDescriptorFilterProvider</c>).
    /// The ASP.NET Core <see cref="IFilterProvider"/> is additive: the framework's
    /// <c>DefaultFilterProvider</c> (<c>Order</c> <c>-1000</c>) has already contributed the controller
    /// filters followed by the action filters by the time this provider runs, which is the order the legacy
    /// provider produced them in. The only thing the legacy provider added on top was the default
    /// host-level authorization, which is appended here unless an <see cref="IOverrideDefaultAuthLevel"/>
    /// filter is present. Property injection into the filters (the former <c>container.BuildUp(filter)</c>)
    /// is no longer needed: filters resolved through <c>[ServiceFilter]</c>/<c>[TypeFilter]</c> are created
    /// by <c>ActivatorUtilities</c> from the request container.
    /// </remarks>
    internal class DnnActionFilterProvider : IFilterProvider
    {
        /// <inheritdoc />
        /// <remarks>Runs after the MVC <c>DefaultFilterProvider</c>, whose order is <c>-1000</c>.</remarks>
        public int Order => 0;

        /// <inheritdoc />
        public void OnProvidersExecuting(FilterProviderContext context)
        {
            Requires.NotNull("context", context);

            // SCOPE GATE. On .NET Framework this provider was registered into HttpConfiguration
            // (ServicesRoutingManager: Configuration.Services.Add(typeof(IFilterProvider), ...)), so it only
            // ever saw services-framework actions. ASP.NET Core has a SINGLE filter pipeline, so without this
            // gate the default host-level authorization below would also be appended to the DotNetNuke.Web.Mvc
            // module-host controllers and every other non-DNN-API MVC action, silently making them
            // super-user-only. Restrict the provider to actions on a DnnApiController, which is exactly the
            // set the legacy HttpConfiguration pipeline covered.
            if (context.ActionContext.ActionDescriptor is not ControllerActionDescriptor descriptor ||
                !typeof(DnnApiController).IsAssignableFrom(descriptor.ControllerTypeInfo))
            {
                return;
            }

            var overrideFilterPresent = context.Results.Any(
                item => item.Filter is IOverrideDefaultAuthLevel || item.Descriptor.Filter is IOverrideDefaultAuthLevel);

            if (!overrideFilterPresent)
            {
                var requireHost = new RequireHostAttribute();
                context.Results.Add(new FilterItem(new FilterDescriptor(requireHost, FilterScope.Action), requireHost));
            }
        }

        /// <inheritdoc />
        public void OnProvidersExecuted(FilterProviderContext context)
        {
            // left empty by design, all filters are contributed by OnProvidersExecuting
        }
    }
}
