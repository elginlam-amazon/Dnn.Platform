// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Modules
{
    using System;

    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>
    /// Adds the per-request <see cref="ResultCapturingActionInvoker"/> to the MVC filter pipeline.
    /// </summary>
    /// <remarks>
    /// In ASP.NET MVC 5 the module host swapped the controller's <c>ActionInvoker</c>. ASP.NET Core
    /// resolves the invoker internally, so the capturing behavior is contributed as a filter: this
    /// provider picks the capturing filter the <see cref="ModuleApplication"/> attached to the current
    /// request and adds it to the filter collection of the action about to execute.
    /// </remarks>
    public class ModuleFilterProvider : IFilterProvider
    {
        /// <inheritdoc />
        /// <remarks>Runs after the default filter provider so the capturing filter is always present.</remarks>
        public int Order => int.MaxValue;

        /// <inheritdoc />
        public void OnProvidersExecuting(FilterProviderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var capturingFilter = ResultCapturingActionInvoker.GetCurrent(context.ActionContext?.HttpContext);
            if (capturingFilter is null)
            {
                // Not a DNN MVC module request; leave the pipeline untouched.
                return;
            }

            var descriptor = new FilterDescriptor(capturingFilter, FilterScope.Last);
            context.Results.Add(new FilterItem(descriptor, capturingFilter));
        }

        /// <inheritdoc />
        public void OnProvidersExecuted(FilterProviderContext context)
        {
            // Nothing to post-process: the filter instance is owned by the module application.
        }
    }
}
