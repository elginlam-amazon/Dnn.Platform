// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.Containers.Default
{
    using DotNetNuke.UI;

    /// <summary>The chromeless fallback module container: a content pane and nothing else.</summary>
    /// <remarks>
    /// <para>
    /// Replaces <c>Portals/_default/Containers/_default/No Container.ascx</c>, whose entire body was
    /// <c>&lt;div runat="server" id="ContentPane" /&gt;</c> over an
    /// <c>Inherits="DotNetNuke.UI.Containers.Container"</c> directive.
    /// </para>
    /// <para>
    /// It is still resolved by path at runtime: <c>DotNetNuke.UI.Skins.Pane.LoadNoContainer</c>
    /// loads <c>[G]Containers/_default/No Container.ascx</c> for a module whose
    /// <c>DisplayTitle</c> is false, and <c>ModuleInstanceContext</c> emits
    /// <c>ContainerSrc=[G]Containers/_default/No Container</c> on module edit/preview URLs.
    /// </para>
    /// <para>
    /// A container must be a <see cref="Container"/> subclass that declares a child control whose
    /// ID is <c>ContentPane</c>, because <c>Container.ContentPane</c> resolves it with
    /// <c>FindControl(Globals.glbDefaultPane)</c> and the container then adds the module host,
    /// action menu, header and footer into it. The pane is therefore created in the constructor,
    /// before the first render.
    /// </para>
    /// <para>
    /// Distinct from <c>Dnn.Skins.Aperture.NoneContainer</c>, which is the Aperture theme's own
    /// chromeless container (a Blazor component, not a <see cref="Container"/>) and only applies to
    /// pages skinned with Aperture.
    /// </para>
    /// </remarks>
    public class NoContainer : Container
    {
        /// <summary>Initializes a new instance of the <see cref="NoContainer"/> class.</summary>
        public NoContainer()
        {
            this.Controls.Add(new DnnGenericControl("div") { ID = Common.Globals.glbDefaultPane });
        }
    }
}
