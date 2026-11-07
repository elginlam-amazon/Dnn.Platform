// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Modules
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Executes DNN MVC modules and renders their results.</summary>
    public interface IModuleExecutionEngine
    {
        /// <summary>Executes the module described by the given context.</summary>
        /// <param name="moduleRequestContext">The module request context.</param>
        /// <returns>The module request result, or <see langword="null"/> when there is no module application.</returns>
        /// <remarks>Action execution is asynchronous in ASP.NET Core, hence the <see cref="Task{TResult}"/>.</remarks>
        Task<ModuleRequestResult> ExecuteModule(ModuleRequestContext moduleRequestContext);

        /// <summary>Executes the module described by the given context.</summary>
        /// <param name="moduleRequestContext">The module request context.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The module request result, or <see langword="null"/> when there is no module application.</returns>
        Task<ModuleRequestResult> ExecuteModuleAsync(ModuleRequestContext moduleRequestContext, CancellationToken cancellationToken);

        /// <summary>Renders the result captured from a module execution into the given writer.</summary>
        /// <param name="moduleResult">The module request result.</param>
        /// <param name="writer">The writer to render the result into.</param>
        /// <returns>A task.</returns>
        Task ExecuteModuleResultAsync(ModuleRequestResult moduleResult, TextWriter writer);
    }
}
