// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;

    /// <summary>The <see cref="DNNTextSuggestEventArgs"/> class.</summary>
    public class DNNTextSuggestEventArgs : EventArgs
    {
        private readonly DNNNodeCollection nodesField;
        private readonly string textField;

        /// <summary>Initializes a new instance of the <see cref="DNNTextSuggestEventArgs"/> class.</summary>
        /// <param name="nodes">The nodes.</param>
        /// <param name="text">The text.</param>
        public DNNTextSuggestEventArgs(DNNNodeCollection nodes, string text)
        {
            this.nodesField = nodes;
            this.textField = text;
        }

        /// <summary>Gets the nodes.</summary>
        public DNNNodeCollection Nodes
        {
            get { return this.nodesField; }
        }

        /// <summary>Gets the text.</summary>
        public string Text
        {
            get { return this.textField; }
        }
    }
}
