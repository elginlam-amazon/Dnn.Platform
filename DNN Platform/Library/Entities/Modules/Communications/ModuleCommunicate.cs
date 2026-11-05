// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Entities.Modules.Communications
{
    using Microsoft.AspNetCore.Components;

    /// <summary>Specifies communications between modules. There are listeners and communicators.</summary>
    public class ModuleCommunicate
    {
        /// <summary>Gets the module communicators.</summary>
        public ModuleCommunicators ModuleCommunicators { get; } = new ModuleCommunicators();

        /// <summary>Gets the module listeners.</summary>
        public ModuleListeners ModuleListeners { get; } = new ModuleListeners();

        /// <summary>Loads the communicator.</summary>
        /// <param name="ctrl">The module component to inspect.</param>
        /// <remarks>
        /// The parameter was a <c>System.Web.UI.Control</c>. DNN modules are Blazor components
        /// now, so the Blazor component contract <see cref="IComponent"/> replaces it; the method
        /// only ever type-tested the instance for the two communication interfaces.
        /// </remarks>
        public void LoadCommunicator(IComponent ctrl)
        {
            // Check and see if the module implements IModuleCommunicator
            if (ctrl is IModuleCommunicator communicator)
            {
                this.Add(communicator);
            }

            // Check and see if the module implements IModuleListener
            if (ctrl is IModuleListener listener)
            {
                this.Add(listener);
            }
        }

        private int Add(IModuleCommunicator item)
        {
            int returnData = this.ModuleCommunicators.Add(item);

            int i = 0;
            for (i = 0; i <= this.ModuleListeners.Count - 1; i++)
            {
                item.ModuleCommunication += this.ModuleListeners[i].OnModuleCommunication;
            }

            return returnData;
        }

        private int Add(IModuleListener item)
        {
            int returnData = this.ModuleListeners.Add(item);

            int i = 0;
            for (i = 0; i <= this.ModuleCommunicators.Count - 1; i++)
            {
                this.ModuleCommunicators[i].ModuleCommunication += item.OnModuleCommunication;
            }

            return returnData;
        }
    }
}
