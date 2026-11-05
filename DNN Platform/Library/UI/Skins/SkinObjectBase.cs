// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Skins
{
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.UI.Modules;

    /// <summary>
    /// The SkinObject class defines a custom base class inherited by all
    /// skin and container objects within the Portal.
    /// </summary>
    /// <remarks>
    /// Was a <c>System.Web.UI.UserControl</c>; skin objects are DNN skinning components
    /// (<see cref="DnnComponentBase"/>) now, which keeps the control-tree surface
    /// (<c>ID</c>/<c>Visible</c>/<c>Controls</c>/<c>FindControl</c>) and the
    /// <c>OnInit</c>/<c>OnLoad</c>/<c>OnPreRender</c> lifecycle the skin objects are written against.
    /// </remarks>
    public class SkinObjectBase : DnnComponentBase, ISkinControl
    {
        /// <summary>Gets the portal Settings for this Skin Control.</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public PortalSettings PortalSettings => PortalSettings.Current;

        /// <summary>Gets a value indicating whether we are in Admin Mode.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public bool AdminMode => TabPermissionController.CanAdminPage();

        /// <summary>Gets or sets the associated ModuleControl for this SkinControl.</summary>
        public IModuleControl ModuleControl { get; set; }

        /// <summary>
        /// Gets or sets the version of the rendering to use.
        /// </summary>
        /// <remarks>
        /// This allows opting-in into new more modern rendering behaviors.
        /// </remarks>
        public int RenderingVersion { get; set; }
    }
}
