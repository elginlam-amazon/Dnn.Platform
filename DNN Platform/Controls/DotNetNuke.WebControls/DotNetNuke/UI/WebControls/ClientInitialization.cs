// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;

    /// <summary>
    /// One client side initialization a control asked for.
    /// </summary>
    /// <remarks>
    /// Replaces the start-up script the Client API start-up script registration used to inject: a
    /// <see cref="Function"/> entry is the <c>dnn.setVar</c> / <c>dnn.controls.&lt;func&gt;</c> pair
    /// of <see cref="BaseWebControl.RegisterInitialize"/>, a <see cref="Script"/> entry is a
    /// one-off statement (such as the tool bar registering its JSON). Both are performed through
    /// <see cref="Microsoft.JSInterop.IJSRuntime"/> by the Blazor components, which is the only way
    /// to reach the browser over a SignalR circuit.
    /// </remarks>
    public sealed class ClientInitialization
    {
        /// <summary>Initializes a new instance of the <see cref="ClientInitialization"/> class.</summary>
        /// <param name="clientId">The client side identifier of the control being initialized.</param>
        /// <param name="function">The name of the <c>dnn.controls</c> initialization function.</param>
        /// <param name="propertyJson">The marshalled client properties of the control.</param>
        public ClientInitialization(string clientId, string function, string propertyJson)
        {
            this.ClientId = clientId;
            this.Function = function;
            this.PropertyJson = propertyJson;
        }

        /// <summary>Initializes a new instance of the <see cref="ClientInitialization"/> class.</summary>
        /// <param name="clientId">The client side identifier of the control being initialized.</param>
        /// <param name="script">The statement to run in the browser.</param>
        public ClientInitialization(string clientId, string script)
        {
            this.ClientId = clientId;
            this.Script = script;
        }

        /// <summary>Gets the client side identifier of the control being initialized.</summary>
        public string ClientId { get; }

        /// <summary>Gets the name of the <c>dnn.controls</c> initialization function.</summary>
        public string Function { get; }

        /// <summary>Gets the marshalled client properties of the control.</summary>
        public string PropertyJson { get; }

        /// <summary>Gets the statement to run in the browser.</summary>
        public string Script { get; }
    }
}
