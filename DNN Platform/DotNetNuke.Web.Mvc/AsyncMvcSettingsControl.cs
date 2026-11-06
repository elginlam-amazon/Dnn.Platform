// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.UI.Modules;

    /// <summary>Hosts the settings control of an MVC module whose action executes asynchronously.</summary>
    public class AsyncMvcSettingsControl : AsyncMvcHostControl, IAsyncSettingsControl
    {
        /// <summary>Initializes a new instance of the <see cref="AsyncMvcSettingsControl"/> class.</summary>
        public AsyncMvcSettingsControl()
            : base("Settings")
        {
            this.ExecuteModuleImmediately = false;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Callers of an asynchronous settings control must use <see cref="LoadSettingsAsync"/>. This
        /// deliberate guard is preserved from the original control: silently running the asynchronous
        /// work from here would hide a caller that never awaits the result.
        /// </remarks>
        public void LoadSettings()
        {
            throw new NotSupportedException("Async controls need to call LoadSettingsAsync.");
        }

        /// <inheritdoc/>
        public Task LoadSettingsAsync(CancellationToken cancellationToken)
        {
            return this.ExecuteModuleAsync(cancellationToken);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Callers of an asynchronous settings control must use <see cref="UpdateSettingsAsync"/>. This
        /// deliberate guard is preserved from the original control: silently running the asynchronous
        /// work from here would hide a caller that never awaits the result.
        /// </remarks>
        public void UpdateSettings()
        {
            throw new NotSupportedException("Async controls need to call UpdateSettingsAsync.");
        }

        /// <inheritdoc/>
        public async Task UpdateSettingsAsync(CancellationToken cancellationToken)
        {
            await this.ExecuteModuleAsync(cancellationToken);

            ModuleController.Instance.UpdateModule(this.ModuleContext.Configuration);
        }
    }
}
