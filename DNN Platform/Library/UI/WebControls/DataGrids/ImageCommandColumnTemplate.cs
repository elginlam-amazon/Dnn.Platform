// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;

    using DotNetNuke.Common.Utilities;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>The ImageCommandColumnTemplate provides a Template for the ImageCommandColumn.</summary>
    /// <remarks>
    /// Was an <c>ITemplate</c> that instantiated a <c>HyperLink</c>, an <c>ImageButton</c> or a
    /// <c>LinkButton</c> into the cell and bound it through its <c>DataBinding</c> event. It now
    /// renders the anchor or the button itself; the command the buttons used to bubble up the
    /// control tree is raised on the owning grid through
    /// <see cref="DNNDataGrid.RaiseItemCommand"/>, and the confirmation prompt
    /// <c>ClientAPI.AddButtonConfirm</c> attached is rendered as the very same inline
    /// <c>onclick</c> attribute.
    /// </remarks>
    public class ImageCommandColumnTemplate : IGridTemplate
    {
        /// <summary>Initializes a new instance of the <see cref="ImageCommandColumnTemplate"/> class.</summary>
        public ImageCommandColumnTemplate()
            : this(ListItemType.Item)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ImageCommandColumnTemplate"/> class.</summary>
        /// <param name="itemType">The list item type.</param>
        public ImageCommandColumnTemplate(ListItemType itemType)
        {
            this.ItemType = itemType;
        }

        /// <summary>Gets or sets the CommandName for the Column.</summary>
        /// <value>A String.</value>
        public string CommandName { get; set; }

        /// <summary>Gets or sets a value indicating whether the Column is in design mode.</summary>
        /// <value>A Boolean.</value>
        public bool DesignMode { get; set; }

        /// <summary>Gets or sets the CommandName for the Column.</summary>
        /// <value>A String.</value>
        public ImageCommandColumnEditMode EditMode { get; set; } = ImageCommandColumnEditMode.Command;

        /// <summary>Gets or sets the URL of the Image.</summary>
        /// <value>A String.</value>
        public string ImageURL { get; set; }

        /// <summary>Gets or sets the type of Template to Create.</summary>
        /// <value>A String.</value>
        public ListItemType ItemType { get; set; } = ListItemType.Item;

        /// <summary>Gets or sets the Key Field that provides a Unique key to the data Item.</summary>
        /// <value>A String.</value>
        public string KeyField { get; set; }

        /// <summary>Gets or sets the URL of the Link (unless DataBinding through KeyField).</summary>
        /// <value>A String.</value>
        public string NavigateURL { get; set; }

        /// <summary>Gets or sets the URL Formatting string.</summary>
        /// <value>A String.</value>
        public string NavigateURLFormatString { get; set; }

        /// <summary>Gets or sets javascript text to attach to the OnClick Event.</summary>
        /// <value>A String.</value>
        public string OnClickJS { get; set; }

        /// <summary>Gets or sets a value indicating whether an Image is displayed.</summary>
        /// <remarks>Defaults to True.</remarks>
        /// <value>A Boolean.</value>
        public bool ShowImage { get; set; } = true;

        /// <summary>Gets or sets the Text (for Header/Footer Templates).</summary>
        /// <value>A String.</value>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the buttons are visible (this is overridden if
        /// the VisibleField is set).
        /// </summary>
        /// <value>A Boolean.</value>
        public bool Visible { get; set; } = true;

        /// <summary>Gets or sets an flag that indicates whether the buttons are visible.</summary>
        /// <value>A String.</value>
        public string VisibleField { get; set; }

        /// <inheritdoc />
        public RenderFragment Render(DataGridCell cell)
        {
            ArgumentNullException.ThrowIfNull(cell);

            switch (this.ItemType)
            {
                case ListItemType.Item:
                case ListItemType.AlternatingItem:
                case ListItemType.SelectedItem:
                case ListItemType.EditItem:
                    return this.EditMode == ImageCommandColumnEditMode.URL
                        ? this.RenderHyperLink(cell)
                        : this.RenderCommand(cell);
                case ListItemType.Footer:
                case ListItemType.Header:
                    cell.Text = this.Text;
                    var text = this.Text;
                    return builder => builder.AddMarkupContent(0, text);
                default:
                    return static builder => { };
            }
        }

        /// <summary>Gets whether theButton is visible.</summary>
        /// <param name="container">The parent container (DataGridItem).</param>
        private bool GetIsVisible(DataGridItem container)
        {
            bool isVisible;
            if (!string.IsNullOrEmpty(this.VisibleField))
            {
                isVisible = Convert.ToBoolean(GridDataBinder.Eval(container?.DataItem, this.VisibleField), CultureInfo.InvariantCulture);
            }
            else
            {
                isVisible = this.Visible;
            }

            return isVisible;
        }

        /// <summary>Gets the value of the key.</summary>
        /// <param name="container">The parent container (DataGridItem).</param>
        private int GetValue(DataGridItem container)
        {
            int keyValue = Null.NullInteger;
            if (!string.IsNullOrEmpty(this.KeyField))
            {
                keyValue = Convert.ToInt32(GridDataBinder.Eval(container?.DataItem, this.KeyField), CultureInfo.InvariantCulture);
            }

            return keyValue;
        }

        private string GetImageUrl() =>
            this.DesignMode ? this.ImageURL.Replace("~/", "../../") : this.ImageURL;

        private RenderFragment RenderHyperLink(DataGridCell cell)
        {
            var keyValue = this.GetValue(cell.Item);
            var url = !string.IsNullOrEmpty(this.NavigateURLFormatString)
                ? string.Format(CultureInfo.InvariantCulture, this.NavigateURLFormatString, keyValue)
                : keyValue.ToString(CultureInfo.InvariantCulture);
            var showImage = !string.IsNullOrEmpty(this.ImageURL) && this.ShowImage;
            var imageUrl = showImage ? this.GetImageUrl() : null;
            var text = this.Text;

            return builder =>
            {
                builder.OpenElement(0, "a");
                builder.AddAttribute(1, "href", url);
                if (!string.IsNullOrEmpty(text))
                {
                    builder.AddAttribute(2, "title", text);
                }

                if (showImage)
                {
                    builder.OpenElement(3, "img");
                    builder.AddAttribute(4, "src", imageUrl);
                    if (!string.IsNullOrEmpty(text))
                    {
                        builder.AddAttribute(5, "title", text);
                        builder.AddAttribute(6, "alt", text);
                    }

                    builder.CloseElement();
                }
                else
                {
                    builder.AddContent(7, text);
                }

                builder.CloseElement();
            };
        }

        private RenderFragment RenderCommand(DataGridCell cell)
        {
            var keyValue = this.GetValue(cell.Item);
            var commandArgument = keyValue.ToString(CultureInfo.InvariantCulture);
            var isVisible = this.GetIsVisible(cell.Item);
            var showImage = !string.IsNullOrEmpty(this.ImageURL) && this.ShowImage;
            var showLink = !string.IsNullOrEmpty(this.Text) && !this.ShowImage;
            var imageUrl = showImage ? this.GetImageUrl() : null;
            var text = this.Text;
            var onClick = EventCallback.Factory.Create<MouseEventArgs>(
                this,
                () => this.RaiseCommandAsync(cell, commandArgument));

            return builder =>
            {
                if (!isVisible)
                {
                    return;
                }

                if (showImage)
                {
                    builder.OpenElement(0, "input");
                    builder.AddAttribute(1, "type", "image");
                    builder.AddAttribute(2, "src", imageUrl);
                    if (!string.IsNullOrEmpty(text))
                    {
                        builder.AddAttribute(3, "title", text);
                        builder.AddAttribute(4, "alt", text);
                    }

                    builder.AddAttribute(6, "onclick", onClick);
                    builder.CloseElement();
                }

                if (showLink)
                {
                    builder.OpenElement(7, "button");
                    builder.AddAttribute(8, "type", "button");
                    builder.AddAttribute(9, "title", text);
                    builder.AddAttribute(11, "onclick", onClick);
                    builder.AddContent(12, text);
                    builder.CloseElement();
                }
            };
        }

        private Task RaiseCommandAsync(DataGridCell cell, string commandArgument)
        {
            var args = new DataGridCommandEventArgs(cell.Item, this, this.CommandName, commandArgument);
            if (cell.Grid == null)
            {
                return Task.CompletedTask;
            }

            // The WebForms column attached the confirmation prompt as an inline onclick through
            // ClientAPI.AddButtonConfirm. A Blazor element cannot carry both an inline onclick and a
            // handler, so the very same prompt is issued through the grid's JS interop instead.
            return cell.Grid.RaiseItemCommandAsync(args, this.OnClickJS);
        }
    }
}
