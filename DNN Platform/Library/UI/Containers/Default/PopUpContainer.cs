// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Containers.Default
{
    using DotNetNuke.UI;

    /// <summary>The fallback module container used for modal/popup pages.</summary>
    /// <remarks>
    /// <para>
    /// Replaces <c>Portals/_default/Containers/_default/popUpContainer.ascx</c>, whose entire body
    /// was <c>&lt;div id="ContentPane" runat="server" /&gt;</c> over an
    /// <c>Inherits="DotNetNuke.UI.Containers.Container"</c> directive. The file was a separate
    /// container from <c>No Container.ascx</c> even though the markup matched, because
    /// <c>DotNetNuke.UI.Skins.Pane.LoadModuleContainer</c> resolves it by its own path when the
    /// portal has pop-ups enabled and the request is in a pop-up: it first probes the active
    /// theme's <c>popUpContainer.ascx</c> and falls back to
    /// <c>Containers/_default/popUpContainer.ascx</c>. Keeping it a distinct type preserves that
    /// two-step fallback and lets the two containers diverge as they did before.
    /// </para>
    /// <para>
    /// The pane is created in the constructor because <c>Container.ContentPane</c> is resolved with
    /// <c>FindControl(Globals.glbDefaultPane)</c> before the first render.
    /// </para>
    /// </remarks>
    public class PopUpContainer : Container
    {
        /// <summary>Initializes a new instance of the <see cref="PopUpContainer"/> class.</summary>
        public PopUpContainer()
        {
            this.Controls.Add(new DnnGenericControl("div") { ID = Common.Globals.glbDefaultPane });
        }
    }
}
