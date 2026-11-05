// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.Collections;
    using System.IO;

    /// <summary>Gets the iDNN Tree Writer.</summary>
    internal interface IDNNTreeWriter
    {
        /// <summary>Collects the properties the client script needs to drive the tree.</summary>
        /// <returns>The marshalled properties.</returns>
        Hashtable MarshalledProperties();

        /// <summary>Renders the given tree to the given writer.</summary>
        /// <param name="writer">The writer to render to.</param>
        /// <param name="tree">The tree to render.</param>
        void RenderTree(TextWriter writer, DnnTree tree);
    }
}
