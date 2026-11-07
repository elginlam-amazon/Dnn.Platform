// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Skins.Default
{
    /// <summary>The host's chromeless fallback skin: a single content pane and nothing else.</summary>
    /// <remarks>
    /// <para>
    /// Replaces <c>Portals/_default/Skins/_default/No Skin.ascx</c>, whose entire body was
    /// <c>&lt;div runat="server" id="ContentPane" /&gt;</c> over a <c>Inherits="DotNetNuke.UI.Skins.Skin"</c>
    /// directive. It is the skin DNN falls back to when a module is rendered without chrome — see
    /// <c>DotNetNuke.UI.Modules.ModuleInstanceContext</c>, which builds a
    /// <c>SkinSrc=[G]Skins/_default/No Skin</c> query string for module edit/preview URLs.
    /// </para>
    /// <para>
    /// This is NOT the Aperture theme's popup layout: <see cref="Skin"/> derives from
    /// <c>DnnComponentBase</c> and <see cref="Skin.LoadPanes"/> discovers panes by walking
    /// <c>Controls</c> for a <see cref="DnnGenericControl"/> that carries an ID and a block-level
    /// tag, so a skin must be a <see cref="Skin"/> subclass that declares its panes as child
    /// controls. A Blazor layout component cannot satisfy <c>LoadSkin</c>'s <see cref="Skin"/>
    /// contract.
    /// </para>
    /// <para>
    /// The pane is created in the constructor rather than in <c>CreateChildControls</c> because
    /// <see cref="Skin.OnInit"/> calls <c>LoadPanes</c>, which runs before the component is first
    /// rendered and therefore before <c>EnsureChildControls</c> would have run.
    /// </para>
    /// </remarks>
    public class NoSkin : Skin
    {
        /// <summary>Initializes a new instance of the <see cref="NoSkin"/> class.</summary>
        public NoSkin()
        {
            this.Controls.Add(new DnnGenericControl("div") { ID = Common.Globals.glbDefaultPane });
        }
    }
}
