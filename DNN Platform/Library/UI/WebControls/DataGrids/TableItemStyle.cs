// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;

    /// <summary>The style of a grid row or cell.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.TableItemStyle</c> (and <c>TableStyle</c>). It extends
    /// the <see cref="Style"/> that replaces <c>System.Web.UI.WebControls.Style</c> with the
    /// table-cell members DNN used, and instead of writing itself onto an <c>HtmlTextWriter</c> it
    /// produces the attribute bag a Blazor render tree takes, see <see cref="GetAttributes"/>.
    /// </remarks>
    public class TableItemStyle : Style
    {
        /// <summary>Gets or sets the horizontal alignment of the content.</summary>
        public HorizontalAlign HorizontalAlign { get; set; } = HorizontalAlign.NotSet;

        /// <summary>Gets or sets the vertical alignment of the content.</summary>
        public VerticalAlign VerticalAlign { get; set; } = VerticalAlign.NotSet;

        /// <summary>Gets or sets a value indicating whether the content wraps.</summary>
        public bool Wrap { get; set; } = true;

        /// <summary>Gets the font of the element.</summary>
        public GridFontStyle Font { get; } = new GridFontStyle();

        /// <summary>Gets the attributes this style renders as.</summary>
        /// <returns>The attribute bag, ready to be handed to <c>RenderTreeBuilder.AddMultipleAttributes</c>.</returns>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "The bag is composed on every call from the current style values.")]
        public IDictionary<string, object> GetAttributes()
        {
            var attributes = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(this.CssClass))
            {
                attributes["class"] = this.CssClass;
            }

            if (this.HorizontalAlign != HorizontalAlign.NotSet)
            {
                attributes["align"] = this.HorizontalAlign.ToString().ToLowerInvariant();
            }

            if (this.VerticalAlign != VerticalAlign.NotSet)
            {
                attributes["valign"] = this.VerticalAlign.ToString().ToLowerInvariant();
            }

            var style = this.GetFullInlineStyle();
            if (!string.IsNullOrEmpty(style))
            {
                attributes["style"] = style;
            }

            return attributes;
        }

        /// <summary>Gets the inline CSS declarations this style renders as, dimensions included.</summary>
        /// <returns>The value of the <c>style</c> attribute, which may be empty.</returns>
        [SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "The declarations are composed on every call from the current style values.")]
        public string GetFullInlineStyle()
        {
            var declarations = new List<string>();
            var dimensions = this.GetInlineStyle();
            if (!string.IsNullOrEmpty(dimensions))
            {
                declarations.Add(dimensions.TrimEnd(';'));
            }

            if (!this.Wrap)
            {
                declarations.Add("white-space:nowrap");
            }

            if (this.Font.Names.Count > 0)
            {
                declarations.Add("font-family:" + string.Join(",", this.Font.Names));
            }

            if (!string.IsNullOrEmpty(this.Font.Size))
            {
                declarations.Add("font-size:" + this.Font.Size);
            }

            if (this.Font.Bold)
            {
                declarations.Add("font-weight:bold");
            }

            if (this.Font.Italic)
            {
                declarations.Add("font-style:italic");
            }

            return string.Join(";", declarations);
        }

        /// <summary>Copies the values of another table style onto this one.</summary>
        /// <param name="other">The style to copy, which may be <see langword="null"/>.</param>
        public void CopyFrom(TableItemStyle other)
        {
            if (other == null)
            {
                return;
            }

            base.CopyFrom(other);

            if (other.HorizontalAlign != HorizontalAlign.NotSet)
            {
                this.HorizontalAlign = other.HorizontalAlign;
            }

            if (other.VerticalAlign != VerticalAlign.NotSet)
            {
                this.VerticalAlign = other.VerticalAlign;
            }

            this.Wrap = other.Wrap;
            this.Font.Bold = this.Font.Bold || other.Font.Bold;
            this.Font.Italic = this.Font.Italic || other.Font.Italic;
            if (!string.IsNullOrEmpty(other.Font.Size))
            {
                this.Font.Size = other.Font.Size;
            }

            foreach (var name in other.Font.Names)
            {
                if (!this.Font.Names.Contains(name))
                {
                    this.Font.Names.Add(name);
                }
            }
        }

        /// <inheritdoc />
        public override string ToString() => string.Format(
            CultureInfo.InvariantCulture,
            "class='{0}' style='{1}'",
            this.CssClass,
            this.GetFullInlineStyle());
    }
}
