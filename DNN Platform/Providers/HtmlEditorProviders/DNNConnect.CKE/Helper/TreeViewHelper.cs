// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider.Helper
{
    using System;
    using System.Collections.Generic;

    /// <summary>Tree view helper.</summary>
    /// <typeparam name="TIdentifier">Type of the ID.</typeparam>
    /// <remarks>
    /// The algorithm is unchanged; only the node type changed from
    /// <c>System.Web.UI.WebControls.TreeNode</c> (and its <c>TreeNodeCollection</c>) to
    /// <see cref="DnnTreeNode"/> and <see cref="IList{T}"/>, because neither WebForms type exists
    /// on ASP.NET Core. Node text/value/image semantics are preserved verbatim.
    /// </remarks>
    internal sealed class TreeViewHelper<TIdentifier>
    {
        private readonly Dictionary<TIdentifier, TreeNodeWithParentId<TIdentifier>> nodes = new Dictionary<TIdentifier, TreeNodeWithParentId<TIdentifier>>();

        /// <summary>
        /// To iterate passed in data collection(treeViewData), with the passed in node collection(treeViewNodes) using Dictionary collection
        /// to get performance for large number of data for the tree view component.
        /// </summary>
        /// <typeparam name="TData">Any object type to use for data to be used in the tree.</typeparam>
        /// <param name="treeViewData">Data collection based on T.</param>
        /// <param name="treeViewNodes">Nodes need to refresh using Dictionary data structure.</param>
        /// <param name="getNodeId">Method to return Text used for NodeId from T based Object.</param>
        /// <param name="getParentId">Method to return Text used for ParentId from T based Object.</param>
        /// <param name="getNodeText">Method to return Text used for the node from T based Object.</param>
        /// <param name="getNodeValue">Method to return test used for node value.</param>
        /// <param name="getNodeImageURL">Method to return Image used for the node from T based Object.</param>
        /// <param name="parentIdCheck">Validate the parentId for T based object if parent should not be -1.</param>
        /// <param name="allLeaves"> if passed would be added as leaves to each node.</param>
        internal void LoadNodes<TData>(IEnumerable<TData> treeViewData, IList<DnnTreeNode> treeViewNodes, Func<TData, TIdentifier> getNodeId, Func<TData, TIdentifier> getParentId, Func<TData, string> getNodeText, Func<TData, string> getNodeValue, Func<TData, string> getNodeImageURL, Func<TIdentifier, bool> parentIdCheck, Dictionary<TIdentifier, HashSet<DnnTreeNode>> allLeaves = null)
        {
            this.FillNodes(treeViewData, getNodeId, getParentId, getNodeText, getNodeValue, getNodeImageURL);
            this.RefreshTreeViewNodes(treeViewNodes, parentIdCheck, allLeaves);
        }

        private void RefreshTreeViewNodes(IList<DnnTreeNode> treeViewNodes, Func<TIdentifier, bool> parentIdCheck, Dictionary<TIdentifier, HashSet<DnnTreeNode>> allLeaves = null)
        {
            treeViewNodes.Clear();
            foreach (var id in this.nodes.Keys)
            {
                var data = this.GetTreeNodeWithParentId(id);
                var node = data.NodeData;
                var parentId = data.ParentId;
                if (parentIdCheck(parentId))
                {
                    var parentNodeWithId = this.GetTreeNodeWithParentId(parentId);
                    if (parentNodeWithId == null)
                    {
                        // if node has an invalid parent (e.g. parent page is deleted), it can't be displayed in the tree
                        continue;
                    }

                    var parentNode = parentNodeWithId.NodeData;
                    parentNode.ChildNodes.AddChild(parentNode, node);
                }
                else
                {
                    treeViewNodes.AddChild(null, node);
                }

                if (allLeaves != null && allLeaves.TryGetValue(id, out var leaves))
                {
                    foreach (var item in leaves)
                    {
                        node.ChildNodes.AddChild(node, item);
                    }
                }
            }
        }

        private void FillNodes<T>(
            IEnumerable<T> treeViewData,
            Func<T, TIdentifier> getNodeId,
            Func<T, TIdentifier> getParentId,
            Func<T, string> getNodeText,
            Func<T, string> getNodeValue,
            Func<T, string> getNodeImageURL)
        {
            this.nodes.Clear();
            foreach (var item in treeViewData)
            {
                var idTab = getNodeId(item);
                var displayName = getNodeText(item);
                var imageUrl = getNodeImageURL(item);
                var nodeValue = getNodeValue(item);

                var nodeData = new TreeNodeWithParentId<TIdentifier>
                {
                    ParentId = getParentId(item),
                    NodeData = new DnnTreeNode
                    {
                        Text = displayName,
                        Value = nodeValue,
                        ImageUrl = imageUrl,
                    },
                };
                this.nodes.Add(idTab, nodeData);
            }
        }

        private TreeNodeWithParentId<TIdentifier> GetTreeNodeWithParentId(TIdentifier key)
        {
            return this.nodes.TryGetValue(key, out var node) ? node : null;
        }

        private sealed class TreeNodeWithParentId<TParentId>
        {
            public TParentId ParentId { get; set; }

            public DnnTreeNode NodeData { get; set; }
        }
    }
}
