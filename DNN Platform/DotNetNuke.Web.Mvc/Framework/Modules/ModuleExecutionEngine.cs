// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Modules
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Web.Mvc.Framework.ActionResults;

    /// <inheritdoc cref="IModuleExecutionEngine" />
    public class ModuleExecutionEngine : IModuleExecutionEngine
    {
        /// <inheritdoc />
        public Task<ModuleRequestResult> ExecuteModule(ModuleRequestContext moduleRequestContext)
        {
            return this.ExecuteModuleAsync(moduleRequestContext, CancellationToken.None);
        }

        /// <inheritdoc />
        public async Task<ModuleRequestResult> ExecuteModuleAsync(ModuleRequestContext moduleRequestContext, CancellationToken cancellationToken)
        {
            Requires.NotNull("moduleRequestContext", moduleRequestContext);

            if (moduleRequestContext.ModuleApplication != null)
            {
                // Run the module
                return await moduleRequestContext.ModuleApplication.ExecuteRequestAsync(moduleRequestContext, cancellationToken);
            }

            return null;
        }

        /// <inheritdoc />
        public virtual async Task ExecuteModuleResultAsync(ModuleRequestResult moduleResult, TextWriter writer)
        {
            Requires.NotNull("moduleResult", moduleResult);

            if (moduleResult.ActionResult is null)
            {
                return;
            }

            if (moduleResult.ActionResult is IDnnViewResult dnnViewResult)
            {
                // DNN view results render into the supplied writer so the output can be injected in the page.
                await dnnViewResult.ExecuteResultAsync(moduleResult.ActionContext, writer);
            }
            else
            {
                await moduleResult.ActionResult.ExecuteResultAsync(moduleResult.ActionContext);
            }
        }
    }
}
