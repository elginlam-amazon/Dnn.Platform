// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionResults
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewEngines;

    /// <summary>A <see cref="ViewResult"/> which can render itself into a caller supplied <see cref="TextWriter"/>.</summary>
    public class DnnViewResult : ViewResult, IDnnViewResult
    {
        /// <summary>Gets or sets the already resolved view to render, if any.</summary>
        public IView View { get; set; }

        /// <summary>Gets or sets the name of the master page (layout) to use.</summary>
        public string MasterName { get; set; }

        /// <summary>Gets or sets the view engines of the module which produced this result.</summary>
        public IReadOnlyList<IViewEngine> ViewEngines { get; set; }

        /// <inheritdoc />
        public async Task ExecuteResultAsync(ActionContext context, TextWriter writer)
        {
            Requires.NotNull("context", context);
            Requires.NotNull("writer", writer);

            if (string.IsNullOrEmpty(this.ViewName))
            {
                this.ViewName = context.RouteData.Values["action"] as string;
            }

            this.View = await DnnViewResultExecutor.RenderAsync(
                context,
                writer,
                this.View,
                this.ViewName,
                this.MasterName,
                true,
                this.ViewData,
                this.TempData,
                this.ViewEngine,
                this.ViewEngines);
        }

        /// <inheritdoc />
        public override async Task ExecuteResultAsync(ActionContext context)
        {
            Requires.NotNull("context", context);

            if (this.View is null && this.ViewEngines is null)
            {
                // Not a module request: let MVC execute the result the regular way.
                await base.ExecuteResultAsync(context);
                return;
            }

            // Render into the response body through the DNN view lookup, so that the module's own
            // view engines are honored even when the result is executed outside of the module host.
            var response = context.HttpContext.Response;
            if (!string.IsNullOrEmpty(this.ContentType))
            {
                response.ContentType = this.ContentType;
            }

            if (this.StatusCode.HasValue)
            {
                response.StatusCode = this.StatusCode.Value;
            }

            await using var writer = new StreamWriter(response.Body, leaveOpen: true);
            await this.ExecuteResultAsync(context, writer);
            await writer.FlushAsync();
        }
    }
}
