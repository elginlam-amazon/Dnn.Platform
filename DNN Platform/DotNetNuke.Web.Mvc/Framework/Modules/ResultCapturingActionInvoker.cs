// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Modules
{
    using System;
    using System.Threading.Tasks;

    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>
    /// Captures the <see cref="IActionResult"/> produced by a module's controller instead of letting
    /// MVC execute it, so that the module host can render it into the DNN page at the right moment.
    /// </summary>
    /// <remarks>
    /// The legacy implementation derived from <c>System.Web.Mvc.AsyncControllerActionInvoker</c> and
    /// overrode <c>InvokeActionResult</c> to store (rather than execute) the result. ASP.NET Core has no
    /// action invoker to subclass, so the same semantics are implemented with filters:
    /// <list type="bullet">
    /// <item>as an <see cref="IAsyncActionFilter"/> it runs outermost, configures the module controller
    /// instance MVC created and records the result returned by the action (including a result produced by
    /// an exception filter that flipped <c>ExceptionHandled</c>);</item>
    /// <item>as an <see cref="IAsyncResultFilter"/> it stores the final result and cancels result
    /// execution, which is the Core equivalent of not calling <c>InvokeActionResult</c>.</item>
    /// </list>
    /// The instance is supplied to the filter pipeline per request by <see cref="ModuleFilterProvider"/>.
    /// </remarks>
    public class ResultCapturingActionInvoker : IAsyncActionFilter, IAsyncResultFilter, IAsyncExceptionFilter, IOrderedFilter
    {
        /// <summary>The <see cref="HttpContext.Items"/> key under which the current capturing filter is stored.</summary>
        internal const string HttpContextItemKey = "DotNetNuke.Web.Mvc.ResultCapturingActionInvoker";

        /// <summary>Gets or sets the result produced by the last invoked action.</summary>
        public IActionResult ResultOfLastInvoke { get; set; }

        /// <summary>Gets the module controller instance that MVC executed.</summary>
        public IDnnController Controller { get; private set; }

        /// <summary>Gets or sets a callback used to configure the controller instance before the action executes.</summary>
        public Action<object> ConfigureController { get; set; }

        /// <summary>Gets the filter order. The capturing filter must be the outermost filter so that it can
        /// configure the controller before any module filter runs and observe the final result.</summary>
        public int Order => int.MinValue;

        /// <summary>Gets the capturing filter for the current request, if any.</summary>
        /// <param name="httpContext">The current HTTP context.</param>
        /// <returns>The capturing filter or <see langword="null"/>.</returns>
        public static ResultCapturingActionInvoker GetCurrent(HttpContext httpContext)
        {
            return httpContext is not null && httpContext.Items.TryGetValue(HttpContextItemKey, out var filter)
                ? filter as ResultCapturingActionInvoker
                : null;
        }

        /// <summary>Removes the capturing filter from the given request.</summary>
        /// <param name="httpContext">The current HTTP context.</param>
        public static void DetachFrom(HttpContext httpContext)
        {
            httpContext?.Items.Remove(HttpContextItemKey);
        }

        /// <summary>Stores this filter as the capturing filter of the given request.</summary>
        /// <param name="httpContext">The current HTTP context.</param>
        public void AttachTo(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            httpContext.Items[HttpContextItemKey] = this;
        }

        /// <inheritdoc />
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            this.Controller = context.Controller as IDnnController;
            this.ConfigureController?.Invoke(context.Controller);

            if (context.Result is not null)
            {
                // A filter short-circuited the action (e.g. an authorization failure result).
                this.ResultOfLastInvoke = context.Result;
                return;
            }

            var executed = await next();

            // If our ActionFilter is executed after the ActionResult has triggered an Exception the filter
            // MUST explicitly flip the ExceptionHandled bit otherwise the view will not render.
            if (executed.Result is not null)
            {
                this.ResultOfLastInvoke = executed.Result;
            }
        }

        /// <inheritdoc />
        public Task OnExceptionAsync(ExceptionContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Result is not null)
            {
                this.ResultOfLastInvoke = context.Result;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);

            // Do not invoke the action result. Instead, store it for later retrieval by the module host,
            // which renders it into the DNN page through IModuleExecutionEngine.ExecuteModuleResultAsync.
            this.ResultOfLastInvoke ??= context.Result;
            context.Cancel = true;
            return Task.CompletedTask;
        }
    }
}
