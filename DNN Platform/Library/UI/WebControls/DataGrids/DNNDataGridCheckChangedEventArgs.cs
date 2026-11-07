// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>Handles the change of a check box in a <see cref="CheckBoxColumn"/>.</summary>
    /// <param name="sender">The object raising the event.</param>
    /// <param name="e">The event arguments.</param>
    [SuppressMessage("Microsoft.Design", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "Breaking change")]
    public delegate void DNNDataGridCheckedColumnEventHandler(object sender, DNNDataGridCheckChangedEventArgs e);

    /// <summary>
    /// The DNNDataGridCheckChangedEventArgs class is a custom EventArgs class for
    /// handling Event Args from the CheckChanged event in a CheckBox Column.
    /// </summary>
    public class DNNDataGridCheckChangedEventArgs : DataGridItemEventArgs
    {
        /// <summary>Initializes a new instance of the <see cref="DNNDataGridCheckChangedEventArgs"/> class.</summary>
        /// <param name="item">The data grid item.</param>
        /// <param name="isChecked">Whether it's checked.</param>
        /// <param name="field">The field name.</param>
        public DNNDataGridCheckChangedEventArgs(DataGridItem item, bool isChecked, string field)
            : this(item, isChecked, field, false)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNDataGridCheckChangedEventArgs"/> class.</summary>
        /// <param name="item">The data grid item.</param>
        /// <param name="isChecked">Whether it's checked.</param>
        /// <param name="field">The field name.</param>
        /// <param name="isAll">Whether this is the "All" item for the column.</param>
        public DNNDataGridCheckChangedEventArgs(DataGridItem item, bool isChecked, string field, bool isAll)
            : base(item)
        {
            this.Checked = isChecked;
            this.IsAll = isAll;
            this.Field = field;
        }

        /// <summary>Gets or sets a value indicating whether the check box is checked.</summary>
        public bool Checked { get; set; }

        /// <summary>Gets or sets the column the check box belongs to.</summary>
        public CheckBoxColumn Column { get; set; }

        /// <summary>Gets or sets the name of the field the check box is bound to.</summary>
        public string Field { get; set; }

        /// <summary>Gets or sets a value indicating whether the change came from the header check box.</summary>
        public bool IsAll { get; set; }
    }
}
