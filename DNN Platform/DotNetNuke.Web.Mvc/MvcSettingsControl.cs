// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc
{
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.UI.Modules;

    /// <summary>Hosts the settings control of an MVC module.</summary>
    public class MvcSettingsControl : MvcHostControl, ISettingsControl
    {
        /// <summary>Initializes a new instance of the <see cref="MvcSettingsControl"/> class.</summary>
        public MvcSettingsControl()
            : base("Settings")
        {
            this.ExecuteModuleImmediately = false;
        }

        /// <inheritdoc />
        /// <remarks>
        /// The module execution is asynchronous on ASP.NET Core, but this is a synchronous contract:
        /// the work is run to completion before returning, so the caller can persist and redirect.
        /// </remarks>
        public void LoadSettings()
        {
            this.RunOperation(this.ExecuteModuleAsync);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The module execution is asynchronous on ASP.NET Core, but this is a synchronous contract:
        /// the work is run to completion before returning, so the caller can persist and redirect.
        /// </remarks>
        public void UpdateSettings()
        {
            this.RunOperation(this.UpdateSettingsAsync);
        }

        private async Task UpdateSettingsAsync(CancellationToken cancellationToken)
        {
            await this.ExecuteModuleAsync(cancellationToken);

            ModuleController.Instance.UpdateModule(this.ModuleContext.Configuration);
        }
    }
}
