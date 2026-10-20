// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Framework.Modules
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Common;

    /// <summary>Executes an MVC module and renders its result.</summary>
    public class ModuleExecutionEngine : IModuleExecutionEngine
    {
        /// <inheritdoc />
        public ModuleRequestResult ExecuteModule(ModuleRequestContext moduleRequestContext)
        {
            Requires.NotNull("moduleRequestContext", moduleRequestContext);

            if (moduleRequestContext.ModuleApplication != null)
            {
                return moduleRequestContext.ModuleApplication.ExecuteRequest(moduleRequestContext);
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<ModuleRequestResult> ExecuteModuleAsync(ModuleRequestContext moduleRequestContext, CancellationToken cancellationToken)
        {
            Requires.NotNull("moduleRequestContext", moduleRequestContext);

            if (moduleRequestContext.ModuleApplication != null)
            {
                return await moduleRequestContext.ModuleApplication.ExecuteRequestAsync(moduleRequestContext, cancellationToken);
            }

            return null;
        }

        /// <inheritdoc />
        public virtual void ExecuteModuleResult(ModuleRequestResult moduleResult, TextWriter writer)
        {
            Requires.NotNull("moduleResult", moduleResult);
            Requires.NotNull("writer", writer);

            if (moduleResult.RenderedOutput != null)
            {
                writer.Write(moduleResult.RenderedOutput);
            }
        }
    }
}
