// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.IO;

    /// <summary>Gets the iDNN Menu Writer.</summary>
    internal interface IDNNMenuWriter
    {
        /// <summary>Renders the given menu to the given writer.</summary>
        /// <param name="writer">The writer to render to.</param>
        /// <param name="menu">The menu to render.</param>
        void RenderMenu(TextWriter writer, DNNMenu menu);
    }
}
