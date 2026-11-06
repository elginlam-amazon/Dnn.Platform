// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;

    using Microsoft.AspNetCore.Mvc;

    /// <summary>Defines properties and methods for DNN-specific API controllers.</summary>
    /// <remarks>
    /// Was a Web API 2 <c>ApiController</c>. The <c>[ApiController]</c> attribute is deliberately NOT
    /// applied: it requires every action to be attribute routed, while the DNN services framework maps its
    /// controllers through the conventional routes registered by the <see cref="IServiceRouteMapper"/>s
    /// (see <c>ServicesRoutingManager</c>), which would fail at startup with <c>[ApiController]</c>.
    /// The member surface (<see cref="PortalSettings"/>, <see cref="UserInfo"/>, <see cref="ActiveModule"/>,
    /// <see cref="ServiceProvider"/>) is unchanged, as all service controllers and third-party module
    /// controllers rely on it.
    /// </remarks>
    [DnnExceptionFilter]
    public abstract class DnnApiController : ControllerBase
    {
        private readonly Lazy<ModuleInfo> activeModule;

        /// <summary>Initializes a new instance of the <see cref="DnnApiController"/> class.</summary>
        protected DnnApiController()
        {
            this.activeModule = new Lazy<ModuleInfo>(this.InitModuleInfo);
        }

        /// <summary>Gets portalSettings for the current portal.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public PortalSettings PortalSettings => PortalSettings.Current;

        /// <summary>Gets userInfo for the current user.</summary>
        public UserInfo UserInfo => this.PortalSettings.UserInfo;

        /// <summary>Gets moduleInfo for the current module.</summary>
        /// <remarks>Will be <see langword="null"/> unless a valid pair of module and tab IDs were provided in the request.</remarks>
        public ModuleInfo ActiveModule => this.activeModule.Value;

        /// <summary>Gets the service provider of the current request scope.</summary>
        public IServiceProvider ServiceProvider => this.HttpContext?.RequestServices;

        /// <summary>Gets moduleInfo for the current module.</summary>
        /// <remarks>Kept as an alias of <see cref="ActiveModule"/>, which is how the legacy controller exposed it.</remarks>
        public ModuleInfo ModuleInfo => this.ActiveModule;

        private ModuleInfo InitModuleInfo()
        {
            return this.Request.FindModuleInfo();
        }
    }
}
