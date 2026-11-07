// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.IO;

    /// <summary>Gets the i Tree Node Writer.</summary>
    internal interface ITreeNodeWriter
    {
        /// <summary>Renders the given tree node to the given writer.</summary>
        /// <param name="writer">The writer to render to.</param>
        /// <param name="node">The node to render.</param>
        void RenderNode(TextWriter writer, TreeNode node);
    }
}
