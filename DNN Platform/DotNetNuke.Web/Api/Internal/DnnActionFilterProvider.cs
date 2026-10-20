// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Linq;

    using DotNetNuke.Common;
    using DotNetNuke.DependencyInjection.Extensions;

    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>
    /// Adds the default authorization level (<see cref="RequireHostAttribute"/>) to the services framework actions which have no
    /// <see cref="IOverrideDefaultAuthLevel"/> filter, and injects the <c>[Dependency]</c> properties of the filters.
    /// </summary>
    internal class DnnActionFilterProvider : IFilterProvider
    {
        private readonly IServiceProvider container;

        /// <summary>Initializes a new instance of the <see cref="DnnActionFilterProvider"/> class.</summary>
        /// <param name="container">The service provider.</param>
        public DnnActionFilterProvider(IServiceProvider container)
        {
            this.container = container;
        }

        /// <inheritdoc />
        /// <remarks>Runs after the default filter provider (order -1000), which creates the filter instances.</remarks>
        public int Order => 0;

        /// <inheritdoc />
        public void OnProvidersExecuting(FilterProviderContext context)
        {
            Requires.NotNull("context", context);

            if (context.ActionContext.ActionDescriptor is not ControllerActionDescriptor descriptor
                || !typeof(DnnApiController).IsAssignableFrom(descriptor.ControllerTypeInfo))
            {
                return;
            }

            var overrideFilterPresent = context.Results.Any(item => (item.Filter ?? item.Descriptor.Filter) is IOverrideDefaultAuthLevel);
            if (!overrideFilterPresent)
            {
                var requireHost = new RequireHostAttribute();
                context.Results.Add(new FilterItem(new FilterDescriptor(requireHost, FilterScope.Action), requireHost) { IsReusable = true, });
            }

            foreach (var item in context.Results)
            {
                if (item.Filter != null)
                {
                    this.container.BuildUp(item.Filter);
                }
            }
        }

        /// <inheritdoc />
        public void OnProvidersExecuted(FilterProviderContext context)
        {
        }
    }
}
