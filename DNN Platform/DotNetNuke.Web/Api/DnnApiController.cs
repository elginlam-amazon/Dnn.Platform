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

    /// <summary>The base class of the services framework (Web API) controllers, an ASP.NET Core <see cref="ControllerBase"/>.</summary>
    [DnnExceptionFilter]
    public abstract class DnnApiController : ControllerBase
    {
        private readonly Lazy<ModuleInfo> activeModule;

        /// <summary>Initializes a new instance of the <see cref="DnnApiController"/> class.</summary>
        protected DnnApiController()
        {
            this.activeModule = new Lazy<ModuleInfo>(this.InitModuleInfo);
        }

        /// <summary>Gets the settings of the current portal.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public PortalSettings PortalSettings => PortalSettings.Current;

        /// <summary>Gets the current user.</summary>
        public UserInfo UserInfo => this.PortalSettings.UserInfo;

        /// <summary>Gets the module associated with the request (identified by the <c>ModuleId</c>/<c>TabId</c> headers or query string), or <see langword="null"/>.</summary>
        public ModuleInfo ActiveModule => this.activeModule.Value;

        private ModuleInfo InitModuleInfo()
        {
            return this.Request.FindModuleInfo();
        }
    }
}
