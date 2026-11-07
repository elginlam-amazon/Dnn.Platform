// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.ActionResults
{
    using System.IO;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;

    /// <summary>A view result which can render itself into a caller supplied <see cref="TextWriter"/>.</summary>
    public interface IDnnViewResult
    {
        /// <summary>Renders the result into the given writer.</summary>
        /// <param name="context">The action context to execute against.</param>
        /// <param name="writer">The writer to render the view into.</param>
        /// <returns>A task.</returns>
        Task ExecuteResultAsync(ActionContext context, TextWriter writer);
    }
}
