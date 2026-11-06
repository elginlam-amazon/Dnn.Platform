// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider.Helper
{
    using System;
    using System.Collections.Generic;

    /// <summary>Lookup helpers over a collection of <see cref="DnnTreeNode"/>.</summary>
    /// <remarks>
    /// Replaces the <c>TreeNodeCollection</c> walks the original pages performed (their
    /// <c>FindNodeByValue</c> helpers and the <c>SelectedNode</c> bookkeeping).
    /// </remarks>
    public static class DnnTreeNodeExtensions
    {
        /// <summary>Finds the first node with the given value, searching the whole sub tree.</summary>
        /// <param name="nodes">The nodes to search.</param>
        /// <param name="value">The value to look for.</param>
        /// <returns>The matching node, or <see langword="null"/> when there is none.</returns>
        public static DnnTreeNode FindByValue(this IEnumerable<DnnTreeNode> nodes, string value)
        {
            if (nodes is null)
            {
                return null;
            }

            foreach (var node in nodes)
            {
                if (string.Equals(node.Value, value, StringComparison.Ordinal))
                {
                    return node;
                }

                var found = node.ChildNodes.FindByValue(value);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>Clears the <see cref="DnnTreeNode.Selected"/> flag of every node of the sub tree.</summary>
        /// <param name="nodes">The nodes to clear the selection of.</param>
        public static void ClearSelection(this IEnumerable<DnnTreeNode> nodes)
        {
            if (nodes is null)
            {
                return;
            }

            foreach (var node in nodes)
            {
                node.Selected = false;
                node.ChildNodes.ClearSelection();
            }
        }

        /// <summary>Expands every node of the sub tree down to the given depth.</summary>
        /// <param name="nodes">The nodes to expand.</param>
        /// <param name="depth">The number of levels to expand, or a negative value to expand everything.</param>
        /// <remarks>Replaces <c>TreeView.ExpandDepth</c>.</remarks>
        public static void ExpandToDepth(this IEnumerable<DnnTreeNode> nodes, int depth)
        {
            if (nodes is null)
            {
                return;
            }

            foreach (var node in nodes)
            {
                node.Expanded = depth != 0;
                node.ChildNodes.ExpandToDepth(depth < 0 ? depth : depth - 1);
            }
        }

        /// <summary>Adds a node as a child of the given node, keeping <see cref="DnnTreeNode.Parent"/> up to date.</summary>
        /// <param name="nodes">The collection to add to.</param>
        /// <param name="parent">The node owning <paramref name="nodes"/>, or <see langword="null"/> for the root collection.</param>
        /// <param name="node">The node to add.</param>
        public static void AddChild(this IList<DnnTreeNode> nodes, DnnTreeNode parent, DnnTreeNode node)
        {
            if (nodes is null || node is null)
            {
                return;
            }

            node.Parent = parent;
            nodes.Add(node);
        }
    }
}
