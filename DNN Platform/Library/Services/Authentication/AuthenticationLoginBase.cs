// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Authentication
{
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.UserRequest;

    /// <summary>
    /// The AuthenticationLoginBase class provides a base class for Authentication
    /// Login components.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This was a <c>System.Web.UI.UserControl</c> on ASP.NET; it is now a Blazor component base
    /// (through <see cref="UserModuleBase"/>, which derives from
    /// <c>Microsoft.AspNetCore.Components.ComponentBase</c>).
    /// </para>
    /// <para>
    /// <b>Blazor Server:</b> <c>HttpContext</c> is <see langword="null"/> during interactive
    /// (SignalR) rendering, so every value that comes from the request — here the client IP
    /// address — is read once in <see cref="OnInitialized"/> and kept in a field. Never read it
    /// from an <c>@onclick</c> handler.
    /// </para>
    /// </remarks>
    public abstract partial class AuthenticationLoginBase : UserModuleBase
    {
        private string ipAddress;

        /// <summary>Initializes a new instance of the <see cref="AuthenticationLoginBase"/> class.</summary>
        protected AuthenticationLoginBase()
        {
            this.RedirectURL = Null.NullString;
            this.AuthenticationType = Null.NullString;
            this.Mode = AuthMode.Login;
            this.ipAddress = Null.NullString;
        }

        [SuppressMessage("Microsoft.Design", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "Breaking change")]
        public delegate void UserAuthenticatedEventHandler(object sender, UserAuthenticatedEventArgs e);

        public event UserAuthenticatedEventHandler UserAuthenticated;

        /// <summary>Gets a value indicating whether the control is Enabled.</summary>
        /// <remarks>This property must be overriden in the inherited class.</remarks>
        public abstract bool Enabled { get; }

        /// <summary>Gets the IP address associated with the request.</summary>
        /// <remarks>
        /// Captured during initial (static) rendering, because the HTTP request is not available
        /// once the component is interactive. When the component was never initialized within a
        /// request (for example when the type is instantiated purely to read
        /// <see cref="Enabled"/>), the value is resolved on demand through the seam instead.
        /// </remarks>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string IPAddress
        {
            get
            {
                if (!string.IsNullOrEmpty(this.ipAddress))
                {
                    return this.ipAddress;
                }

                return UserRequestIPAddressController.Instance.GetUserRequestIPAddress();
            }
        }

        /// <summary>Gets a value indicating whether the control supports Registration.</summary>
        /// <remarks>This property may be overriden in the inherited class.</remarks>
        public virtual bool SupportsRegistration
        {
            get { return false; }
        }

        /// <summary>Gets or sets the Type of Authentication associated with this control.</summary>
        /// <remarks>
        /// A Blazor <see cref="Microsoft.AspNetCore.Components.ParameterAttribute"/> for the same
        /// reason as <c>PortalModuleBase.ModuleConfiguration</c>: the login host resolves the
        /// provider's component type and renders it by type, so a value assigned on an instance the
        /// host created would not reach the instance the renderer creates.
        /// </remarks>
        [Microsoft.AspNetCore.Components.Parameter]
        public string AuthenticationType { get; set; }

        /// <summary>
        /// Gets or sets the callback raised when this control has authenticated (or failed to
        /// authenticate) the user.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The parameter form of <see cref="UserAuthenticated"/>. The event is a CLR event on the
        /// control <i>instance</i>, which the Blazor renderer does not carry over when it renders a
        /// dynamically resolved provider component by type (and which cannot be subscribed before
        /// the component's own lifecycle runs during a static server render). The host therefore
        /// subscribes through this parameter, and <see cref="OnUserAuthenticated"/> raises both.
        /// </para>
        /// </remarks>
        [Microsoft.AspNetCore.Components.Parameter]
        public Microsoft.AspNetCore.Components.EventCallback<UserAuthenticatedEventArgs> UserAuthenticatedCallback { get; set; }

        /// <summary>Gets or sets the Authentication mode of the control (Login or Register).</summary>
        /// <remarks>This property may be overriden in the inherited class.</remarks>
        public virtual AuthMode Mode { get; set; }

        /// <summary>Gets or sets the Redirect Url for this control.</summary>
        [Microsoft.AspNetCore.Components.Parameter]
        public string RedirectURL { get; set; }

        /// <inheritdoc cref="IUserRequestIPAddressController.GetUserRequestIPAddress(Microsoft.AspNetCore.Http.HttpRequest)"/>
        [DnnDeprecated(9, 2, 0, "Use UserRequestIPAddressController.Instance.GetUserRequestIPAddress")]
        public static partial string GetIPAddress()
        {
            return UserRequestIPAddressController.Instance.GetUserRequestIPAddress();
        }

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            base.OnInitialized();

            // Capture everything that depends on the HTTP request while there still is one.
            this.ipAddress = UserRequestIPAddressController.Instance.GetUserRequestIPAddress();
        }

        protected virtual void OnUserAuthenticated(UserAuthenticatedEventArgs ea)
        {
            if (this.UserAuthenticated != null)
            {
                this.UserAuthenticated(null, ea);
            }

            if (this.UserAuthenticatedCallback.HasDelegate)
            {
                // Synchronous: the host's handler signs the user in, which has to happen while the
                // HTTP request that carried the credentials is still in flight.
                this.UserAuthenticatedCallback.InvokeAsync(ea).GetAwaiter().GetResult();
            }
        }
    }
}
