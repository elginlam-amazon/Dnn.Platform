// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Authentication
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;

    /// <summary>
    /// The AuthenticationLogoffBase class provides a base class for Authentiication
    /// Logoff controls.
    /// </summary>
    public abstract class AuthenticationLogoffBase : UserModuleBase
    {
        /// <summary>Initializes a new instance of the <see cref="AuthenticationLogoffBase"/> class.</summary>
        public AuthenticationLogoffBase()
        {
        }

        /// <summary>Fires when a LogOff occurs.</summary>
        public event EventHandler LogOff;

        /// <summary>Fires when a redirect occurs.</summary>
        public event EventHandler Redirect;

        /// <summary>Gets or sets the Type of Authentication associated with this control.</summary>
        /// <remarks>
        /// A Blazor <see cref="Microsoft.AspNetCore.Components.ParameterAttribute"/>: the logoff
        /// host resolves the provider's component type and renders it by type.
        /// </remarks>
        [Microsoft.AspNetCore.Components.Parameter]
        public string AuthenticationType { get; set; } = Null.NullString;

        /// <summary>Gets or sets the parameter form of the <see cref="LogOff"/> event.</summary>
        /// <remarks>
        /// A CLR event on the instance cannot be subscribed by a host that renders this control by
        /// type (see <c>AuthenticationLoginBase.UserAuthenticatedCallback</c> for the full
        /// rationale). <see cref="OnLogOff"/> raises both.
        /// </remarks>
        [Microsoft.AspNetCore.Components.Parameter]
        public Microsoft.AspNetCore.Components.EventCallback LogOffCallback { get; set; }

        /// <summary>Gets or sets the parameter form of the <see cref="Redirect"/> event.</summary>
        [Microsoft.AspNetCore.Components.Parameter]
        public Microsoft.AspNetCore.Components.EventCallback RedirectCallback { get; set; }

        /// <summary>
        /// Gets the Dependency Provider to resolve registered
        /// services with the container.
        /// </summary>
        /// <value>
        /// The Dependency Service.
        /// </value>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        protected new IServiceProvider DependencyProvider => Globals.GetCurrentServiceProvider();

        /// <summary>Handles the <see cref="LogOff"/> event.</summary>
        /// <param name="a">The event arguments.</param>
        protected virtual void OnLogOff(EventArgs a)
        {
            if (this.LogOff != null)
            {
                this.LogOff(null, a);
            }

            if (this.LogOffCallback.HasDelegate)
            {
                this.LogOffCallback.InvokeAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>Handles the <see cref="Redirect"/> event.</summary>
        /// <param name="a">The event arguments.</param>
        protected virtual void OnRedirect(EventArgs a)
        {
            if (this.Redirect != null)
            {
                this.Redirect(null, a);
            }

            if (this.RedirectCallback.HasDelegate)
            {
                this.RedirectCallback.InvokeAsync().GetAwaiter().GetResult();
            }
        }
    }
}
