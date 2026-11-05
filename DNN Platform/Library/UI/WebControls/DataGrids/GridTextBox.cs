// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    /// <summary>The state of a text box rendered in a grid cell.</summary>
    /// <remarks>
    /// Replaces the <c>System.Web.UI.WebControls.TextBox</c> the WebForms <see cref="TextColumn"/>
    /// template added to the cell's control tree, so that the edited text can still be read back
    /// through <c>cell.Controls[0]</c>.
    /// </remarks>
    public class GridTextBox
    {
        /// <summary>Gets or sets the text of the text box.</summary>
        public string Text { get; set; } = string.Empty;
    }
}
