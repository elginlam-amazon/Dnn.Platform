// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;

    /// <summary>The <see cref="DNNTreeEventArgs"/> class.</summary>
    public class DNNTreeEventArgs : EventArgs
    {
        private readonly TreeNode nodeField;

        /// <summary>Initializes a new instance of the <see cref="DNNTreeEventArgs"/> class.</summary>
        /// <param name="node">The node.</param>
        public DNNTreeEventArgs(TreeNode node)
        {
            this.nodeField = node;
        }

        /// <summary>Gets the node.</summary>
        public TreeNode Node
        {
            get { return this.nodeField; }
        }
    }
}
