// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionResults
{
    using System.IO;
    using System.Threading.Tasks;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Mvc;

    /// <summary>A <see cref="PartialViewResult"/> that can also render itself into an arbitrary <see cref="TextWriter"/>.</summary>
    public class DnnPartialViewResult : PartialViewResult, IDnnViewResult
    {
        /// <inheritdoc />
        public Task ExecuteResultAsync(ActionContext context, TextWriter writer)
        {
            Requires.NotNull("context", context);
            Requires.NotNull("writer", writer);

            return DnnViewRenderer.RenderAsync(context, writer, this.ViewName, this.ViewData, this.TempData, isPartial: true);
        }
    }
}
