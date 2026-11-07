// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    /// <summary>The state of a check box rendered in a grid cell.</summary>
    /// <remarks>
    /// Replaces the <c>System.Web.UI.WebControls.CheckBox</c> the WebForms column templates added
    /// to the cell's control tree and read back afterwards. It carries the two values that were
    /// ever read off that control, so <c>cell.Controls[0]</c> keeps working.
    /// </remarks>
    public class GridCheckBox
    {
        /// <summary>Gets or sets a value indicating whether the check box is checked.</summary>
        public bool Checked { get; set; }

        /// <summary>Gets or sets a value indicating whether the check box is enabled.</summary>
        public bool Enabled { get; set; } = true;
    }
}
