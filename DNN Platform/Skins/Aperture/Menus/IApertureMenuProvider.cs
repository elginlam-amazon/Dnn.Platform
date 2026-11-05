// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Skins.Aperture.Menus
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Supplies the navigation tree rendered by the Aperture menu components.
    /// </summary>
    /// <remarks>
    /// The legacy theme obtained its nodes from the DDRMenu <c>dnn:MENU</c> skin object, which
    /// resolved a node source from the <c>NodeSelector</c> attribute. The host application
    /// implements this abstraction so the theme stays free of any WebForms or page-lifecycle
    /// dependency.
    /// </remarks>
    public interface IApertureMenuProvider
    {
        /// <summary>Gets the root-level navigation nodes for a menu.</summary>
        /// <param name="nodeSelector">
        /// The DDRMenu node selector that was declared on the original <c>dnn:MENU</c> skin object,
        /// for example <c>*,0,2</c> for the header menus or <c>*,0,0</c> for the footer menu. The
        /// first segment selects the starting node, the second the number of levels to skip and the
        /// third the maximum depth to include (0 meaning root level only).
        /// </param>
        /// <param name="cancellationToken">A token used to cancel the operation.</param>
        /// <returns>The root nodes to render; an empty sequence when the menu has no nodes.</returns>
        ValueTask<IReadOnlyList<ApertureMenuNode>> GetNodesAsync(string nodeSelector, CancellationToken cancellationToken = default);
    }
}
