// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Skins.Aperture.Menus
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A single navigation node rendered by the Aperture menu components.
    /// </summary>
    /// <remarks>
    /// This is the modern replacement for the <c>DotNetNuke.Web.DDRMenu.MenuNode</c> instances that
    /// the legacy <c>menus/*/RazorMenu.cshtml</c> templates bound to through
    /// <c>Model.Source.root.Children</c>. It exposes exactly the members those templates consumed
    /// (text, link, target, enabled, selected, depth and children) so the ported components keep the
    /// original rendering behaviour.
    /// </remarks>
    public sealed class ApertureMenuNode
    {
        private readonly List<ApertureMenuNode> children = new();

        /// <summary>Gets or sets the link text shown for the node.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the navigation link for the node.</summary>
        /// <remarks>
        /// Corresponds to the legacy <c>MenuNode.Url</c>. When <see cref="Enabled"/> is
        /// <see langword="false"/> the menu components render <c>javascript:void(0);</c> instead,
        /// which is the behaviour of the original templates.
        /// </remarks>
        public string Href { get; set; } = string.Empty;

        /// <summary>Gets or sets the anchor target (for example <c>_blank</c>), or <see langword="null"/> for none.</summary>
        public string? Target { get; set; }

        /// <summary>Gets or sets a value indicating whether the node is navigable.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the node is the current page.</summary>
        public bool Selected { get; set; }

        /// <summary>Gets or sets the zero-based depth of the node within the menu tree.</summary>
        public int Depth { get; set; }

        /// <summary>Gets the child nodes of this node.</summary>
        public IReadOnlyList<ApertureMenuNode> Children => this.children;

        /// <summary>Gets a value indicating whether this node has any child nodes.</summary>
        /// <remarks>Replacement for the legacy <c>MenuNode.HasChildren()</c> method call.</remarks>
        public bool HasChildren => this.children.Count > 0;

        /// <summary>Appends a child node to this node.</summary>
        /// <param name="child">The child node to append.</param>
        public void AddChild(ApertureMenuNode child)
        {
            ArgumentNullException.ThrowIfNull(child);
            this.children.Add(child);
        }

        /// <summary>Removes every child node, used when a node selector limits the menu depth.</summary>
        public void ClearChildren()
        {
            this.children.Clear();
        }
    }
}
