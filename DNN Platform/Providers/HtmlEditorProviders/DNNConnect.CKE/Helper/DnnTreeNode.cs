// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider.Helper
{
    using System;
    using System.Collections.Generic;

    /// <summary>A node of the tree rendered by <c>DNNConnect.CKEditorProvider.Controls.DnnTreeView</c>.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.TreeNode</c>, which does not exist on ASP.NET Core and
    /// for which the platform offers no replacement. The member shape the provider's two trees
    /// (the Browser folder/tab tree and the <c>EditorConfigManager</c> portal/tab/module tree) used
    /// — <see cref="Text"/>, <see cref="Value"/>, <see cref="ImageUrl"/>, <see cref="ToolTip"/>,
    /// <see cref="NavigateUrl"/>, <see cref="Expanded"/>, <see cref="Selected"/>,
    /// <see cref="Checked"/> and <see cref="ChildNodes"/> — is preserved verbatim so the node
    /// building logic reads the same.
    /// </remarks>
    public class DnnTreeNode
    {
        private readonly List<DnnTreeNode> childNodes = new List<DnnTreeNode>();

        /// <summary>Initializes a new instance of the <see cref="DnnTreeNode"/> class.</summary>
        public DnnTreeNode()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnTreeNode"/> class.</summary>
        /// <param name="text">The display text of the node.</param>
        public DnnTreeNode(string text)
            : this(text, text)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnTreeNode"/> class.</summary>
        /// <param name="text">The display text of the node.</param>
        /// <param name="value">The value of the node.</param>
        public DnnTreeNode(string text, string value)
        {
            this.Text = text;
            this.Value = value;
        }

        /// <summary>Gets or sets the display text of the node.</summary>
        public string Text { get; set; }

        /// <summary>Gets or sets the value of the node.</summary>
        public string Value { get; set; }

        /// <summary>Gets or sets the URL of the image rendered in front of the node's text.</summary>
        public string ImageUrl { get; set; }

        /// <summary>Gets or sets the tool tip of the node.</summary>
        public string ToolTip { get; set; }

        /// <summary>Gets or sets the URL the node navigates to, or <see langword="null"/> when the node only raises a click.</summary>
        public string NavigateUrl { get; set; }

        /// <summary>Gets or sets a value indicating whether the node's children are shown.</summary>
        public bool Expanded { get; set; }

        /// <summary>Gets or sets a value indicating whether the node is the selected one.</summary>
        public bool Selected { get; set; }

        /// <summary>Gets or sets a value indicating whether the node's check box is checked.</summary>
        public bool Checked { get; set; }

        /// <summary>Gets or sets the parent of this node, or <see langword="null"/> for a root node.</summary>
        public DnnTreeNode Parent { get; set; }

        /// <summary>Gets the children of this node.</summary>
        /// <remarks>Replaces <c>TreeNode.ChildNodes</c>; adding to it parents the added node.</remarks>
        public IList<DnnTreeNode> ChildNodes => this.childNodes;

        /// <summary>Gets a value indicating whether this node has any children.</summary>
        public bool HasChildNodes => this.childNodes.Count > 0;
    }
}
