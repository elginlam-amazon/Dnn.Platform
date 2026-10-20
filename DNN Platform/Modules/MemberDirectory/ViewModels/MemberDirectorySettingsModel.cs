// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.MemberDirectory.ViewModels
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.Entities.Profile;
    using DotNetNuke.Entities.Users.Social;
    using DotNetNuke.Security.Roles;

    /// <summary>The model for <see cref="Settings"/>.</summary>
    /// <remarks>
    /// Originally derived from the WebFormsMvp based <c>DotNetNuke.Web.Mvp.SettingsModel</c>, which was
    /// removed. The <see cref="ModuleSettings"/> and <see cref="TabModuleSettings"/> members that the base
    /// class previously provided are now declared directly here to preserve the original behavior.
    /// </remarks>
    public partial class MemberDirectorySettingsModel
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public IList<ProfilePropertyDefinition> ProfileProperties;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public IList<Relationship> Relationships;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public IList<RoleInfo> Groups;

        /// <summary>Gets or sets the module settings.</summary>
        public Dictionary<string, string> ModuleSettings { get; set; }

        /// <summary>Gets or sets the tab-module settings.</summary>
        public Dictionary<string, string> TabModuleSettings { get; set; }
    }
}
