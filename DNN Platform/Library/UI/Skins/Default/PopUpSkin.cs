// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Skins.Default
{
    /// <summary>The host's fallback skin for modal/popup pages.</summary>
    /// <remarks>
    /// <para>
    /// Replaces <c>Portals/_default/Skins/_default/popUpSkin.ascx</c>, which declared a
    /// <c>&lt;style type="text/css"&gt;body#Body {background:#fff;}&lt;/style&gt;</c> block followed
    /// by <c>&lt;div id="ContentPane" runat="server" /&gt;</c>. It is loaded by
    /// <see cref="Skin.GetPopUpSkin"/> whenever the active theme ships no <c>popUpSkin</c> of its
    /// own.
    /// </para>
    /// <para>
    /// The style block is kept as a <see cref="DnnLiteralControl"/> so the rendered markup is
    /// byte-for-byte what the <c>.ascx</c> emitted and in the same position. It is deliberately not
    /// moved into <c>HeadContent</c>: the original rule is scoped to this skin only, and the
    /// literal is not a <see cref="DnnGenericControl"/>, so <c>LoadPanes</c> skips it and
    /// <c>ContentPane</c> remains the only pane.
    /// </para>
    /// <para>
    /// This is the <i>host</i> fallback and is distinct from
    /// <c>Dnn.Skins.Aperture.PopUpSkinLayout</c>, which is the Aperture theme's own popup layout
    /// and a Blazor layout rather than a <see cref="Skin"/>.
    /// </para>
    /// </remarks>
    public class PopUpSkin : Skin
    {
        /// <summary>Initializes a new instance of the <see cref="PopUpSkin"/> class.</summary>
        public PopUpSkin()
        {
            this.Controls.Add(new DnnLiteralControl("<style type=\"text/css\">\r\n    body#Body {background:#fff;}\r\n</style>"));
            this.Controls.Add(new DnnGenericControl("div") { ID = Common.Globals.glbDefaultPane });
        }
    }
}
