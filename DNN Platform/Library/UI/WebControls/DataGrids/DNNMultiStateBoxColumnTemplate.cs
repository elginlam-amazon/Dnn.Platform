// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Globalization;
    using System.IO;

    using DotNetNuke.Common.Utilities;

    using Microsoft.AspNetCore.Components;

    /// <summary>The DNNMultiStateBoxColumnTemplate provides a Template for the DNNMultiStateBoxColumn.</summary>
    /// <remarks>
    /// Was an <c>ITemplate</c> that added a <see cref="DNNMultiStateBox"/> to the cell's control
    /// tree and bound it through its <c>DataBinding</c> event. The box itself was migrated by the
    /// DotNetNuke.WebControls library to a control that renders itself onto a
    /// <see cref="TextWriter"/>, so the template configures it, registers it in the cell (keeping
    /// <c>cell.Controls[0]</c> working) and emits its markup into the render tree.
    /// </remarks>
    public class DNNMultiStateBoxColumnTemplate : IGridTemplate
    {
        private string mDataField = Null.NullString;
        private bool mEnabled = true;
        private string mEnabledField = Null.NullString;
        private string mImagePath = string.Empty;
        private ListItemType mItemType = ListItemType.Item;
        private string mSelectedStateKey = string.Empty;
        private DNNMultiStateCollection mStates;
        private string mText = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="DNNMultiStateBoxColumnTemplate"/> class.</summary>
        public DNNMultiStateBoxColumnTemplate()
            : this(ListItemType.Item)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNMultiStateBoxColumnTemplate"/> class.</summary>
        /// <param name="itemType">The list item type.</param>
        public DNNMultiStateBoxColumnTemplate(ListItemType itemType)
        {
            this.ItemType = itemType;
        }

        /// <summary>Gets or sets a value indicating whether the column fires a postback when the control changes.</summary>
        /// <value>A Boolean.</value>
        public bool AutoPostBack { get; set; }

        /// <summary>Gets or sets the selected state of the DNNMultiStateBox (unless DataBound).</summary>
        /// <value>A Boolean.</value>
        public string SelectedStateKey
        {
            get
            {
                return this.mSelectedStateKey;
            }

            set
            {
                this.mSelectedStateKey = value;
            }
        }

        /// <summary>Gets or sets the Data Field that the column should bind to.</summary>
        /// <value>A String.</value>
        public string DataField
        {
            get
            {
                return this.mDataField;
            }

            set
            {
                this.mDataField = value;
            }
        }

        /// <summary>Gets or sets a value indicating whether the Column is in Design Mode.</summary>
        /// <value>A Boolean.</value>
        public bool DesignMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the control is enabled (this is overridden if
        /// the EnabledField is set).
        /// </summary>
        /// <value>A Boolean.</value>
        public bool Enabled
        {
            get
            {
                return this.mEnabled;
            }

            set
            {
                this.mEnabled = value;
            }
        }

        /// <summary>
        /// Gets or sets the Data Field that determines whether the control is Enabled
        /// changed.
        /// </summary>
        /// <value>A String.</value>
        public string EnabledField
        {
            get
            {
                return this.mEnabledField;
            }

            set
            {
                this.mEnabledField = value;
            }
        }

        /// <summary>Gets or sets the type of Template to Create.</summary>
        /// <value>A String.</value>
        public ListItemType ItemType
        {
            get
            {
                return this.mItemType;
            }

            set
            {
                this.mItemType = value;
            }
        }

        /// <summary>Gets or sets the Text to display in a Header Template.</summary>
        /// <value>A String.</value>
        public string Text
        {
            get
            {
                return this.mText;
            }

            set
            {
                this.mText = value;
            }
        }

        /// <summary>Gets or sets the image path of the DNNMultiStateBox.</summary>
        /// <value>A Boolean.</value>
        public string ImagePath
        {
            get
            {
                return this.mImagePath;
            }

            set
            {
                this.mImagePath = value;
            }
        }

        /// <summary>Gets or sets the state collection of the DNNMultiStateBox.</summary>
        /// <value>A Boolean.</value>
        public DNNMultiStateCollection States
        {
            get
            {
                if (this.mStates == null)
                {
                    this.mStates = new DNNMultiStateCollection(new DNNMultiStateBox());
                }

                return this.mStates;
            }

            set
            {
                this.mStates = value;
            }
        }

        /// <inheritdoc />
        public RenderFragment Render(DataGridCell cell)
        {
            ArgumentNullException.ThrowIfNull(cell);

            var markup = string.Empty;
            if (this.ItemType != ListItemType.Header)
            {
                // DNNMultiStateBox no longer exposes AutoPostBack: the control it inherited it
                // from is gone and it cycles its state client side, so there is no post back to
                // trigger. AutoPostBack is kept on this template because it is public API.
                var box = new DNNMultiStateBox();
                box.ImagePath = this.ImagePath;
                foreach (DNNMultiState objState in this.States)
                {
                    box.States.Add(objState);
                }

                this.BindBox(box, cell);
                cell.Controls.Clear();
                cell.Controls.Add(box);

                using (var writer = new StringWriter(CultureInfo.InvariantCulture))
                {
                    box.RenderControl(writer);
                    markup = writer.ToString();
                }
            }

            var text = this.Text;
            return builder =>
            {
                var sequence = 0;
                if (!string.IsNullOrEmpty(text))
                {
                    builder.AddMarkupContent(sequence++, text + "<br/>");
                }

                if (!string.IsNullOrEmpty(markup))
                {
                    builder.AddMarkupContent(sequence, markup);
                }
            };
        }

        /// <summary>Called when the template item is Data Bound.</summary>
        private void BindBox(DNNMultiStateBox box, DataGridCell cell)
        {
            var dataItem = cell.Item?.DataItem;
            if (!string.IsNullOrEmpty(this.DataField) && this.ItemType != ListItemType.Header)
            {
                if (this.DesignMode)
                {
                    box.SelectedStateKey = string.Empty;
                }
                else
                {
                    box.SelectedStateKey = Convert.ToString(GridDataBinder.Eval(dataItem, this.DataField), CultureInfo.InvariantCulture);
                }
            }
            else
            {
                box.SelectedStateKey = this.SelectedStateKey;
            }

            if (!string.IsNullOrEmpty(this.EnabledField))
            {
                if (this.DesignMode)
                {
                    box.Enabled = false;
                }
                else
                {
                    box.Enabled = Convert.ToBoolean(GridDataBinder.Eval(dataItem, this.EnabledField), CultureInfo.InvariantCulture);
                }
            }
            else
            {
                box.Enabled = this.Enabled;
            }
        }
    }
}
