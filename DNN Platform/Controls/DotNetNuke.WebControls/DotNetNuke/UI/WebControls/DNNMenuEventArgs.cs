// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;

    /// <summary>The <see cref="DNNMenuEventArgs"/> class.</summary>
    public class DNNMenuEventArgs : EventArgs
    {
        private readonly MenuNode nodeField;

        /// <summary>Initializes a new instance of the <see cref="DNNMenuEventArgs"/> class.</summary>
        /// <param name="node">The node.</param>
        public DNNMenuEventArgs(MenuNode node)
        {
            this.nodeField = node;
        }

        /// <summary>Gets the node.</summary>
        public MenuNode Node
        {
            get { return this.nodeField; }
        }
    }
}
