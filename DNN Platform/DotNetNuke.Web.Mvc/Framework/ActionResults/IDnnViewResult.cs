// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionResults
{
    using System.IO;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;

    /// <summary>A DNN action result that can render itself into an arbitrary <see cref="TextWriter"/>.</summary>
    public interface IDnnViewResult
    {
        /// <summary>Renders the result into the supplied <paramref name="writer"/>.</summary>
        /// <param name="context">The action context in which to execute.</param>
        /// <param name="writer">The writer into which to render.</param>
        /// <returns>A task that completes when rendering is done.</returns>
        Task ExecuteResultAsync(ActionContext context, TextWriter writer);
    }
}
