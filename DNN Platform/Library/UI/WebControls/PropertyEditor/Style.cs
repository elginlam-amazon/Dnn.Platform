// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>Represents the style (css class and dimensions) applied to a DNN web control.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.Style</c>, which does not exist on ASP.NET Core. Only the
    /// members DNN actually used are carried over — <see cref="CssClass"/>, <see cref="Width"/>,
    /// <see cref="Height"/>, <see cref="CopyFrom"/> and <see cref="IsEmpty"/> — plus the Blazor
    /// equivalents of <c>AddAttributesToRender</c>.
    /// </remarks>
    public class Style
    {
        private string cssClass = string.Empty;

        /// <summary>Gets or sets the css class applied to the control's element.</summary>
        public string CssClass
        {
            get => this.cssClass;
            set => this.cssClass = value ?? string.Empty;
        }

        /// <summary>Gets or sets the width of the control's element.</summary>
        public Unit Width { get; set; }

        /// <summary>Gets or sets the height of the control's element.</summary>
        public Unit Height { get; set; }

        /// <summary>Gets a value indicating whether no style information has been set.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(this.cssClass) && this.Width.IsEmpty && this.Height.IsEmpty;

        /// <summary>Copies the style information of <paramref name="source"/> onto this instance.</summary>
        /// <param name="source">The style to copy from. A <see langword="null"/> source is a no-op.</param>
        /// <remarks>As with <c>System.Web.UI.WebControls.Style.CopyFrom</c>, only properties that are
        /// actually set on <paramref name="source"/> overwrite the values on this instance.</remarks>
        public void CopyFrom(Style source)
        {
            if (source == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(source.CssClass))
            {
                this.CssClass = source.CssClass;
            }

            if (!source.Width.IsEmpty)
            {
                this.Width = source.Width;
            }

            if (!source.Height.IsEmpty)
            {
                this.Height = source.Height;
            }
        }

        /// <summary>Gets the inline <c>style</c> attribute value for the dimensions, if any.</summary>
        /// <returns>The css declarations, or <see langword="null"/> when no dimension is set.</returns>
        public string GetInlineStyle()
        {
            if (this.Width.IsEmpty && this.Height.IsEmpty)
            {
                return null;
            }

            var builder = new StringBuilder();
            if (!this.Width.IsEmpty)
            {
                builder.Append(CultureInfo.InvariantCulture, $"width:{this.Width.ToString(CultureInfo.InvariantCulture)};");
            }

            if (!this.Height.IsEmpty)
            {
                builder.Append(CultureInfo.InvariantCulture, $"height:{this.Height.ToString(CultureInfo.InvariantCulture)};");
            }

            return builder.ToString();
        }

        /// <summary>Adds the <c>class</c> and <c>style</c> attributes for this style onto the element being built.</summary>
        /// <param name="builder">The render tree builder, positioned inside an open element.</param>
        /// <param name="sequence">The render tree sequence number to use.</param>
        /// <remarks>Replaces <c>Style.AddAttributesToRender(HtmlTextWriter)</c>.</remarks>
        public void AddAttributesToRender(RenderTreeBuilder builder, int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (!string.IsNullOrEmpty(this.cssClass))
            {
                builder.AddAttribute(sequence, "class", this.cssClass);
            }

            var inline = this.GetInlineStyle();
            if (inline != null)
            {
                builder.AddAttribute(sequence + 1, "style", inline);
            }
        }

        /// <summary>Adds the <c>class</c> and <c>style</c> attributes for this style into an attribute dictionary.</summary>
        /// <param name="attributes">The attribute dictionary to add to.</param>
        public void AddAttributesToRender(IDictionary<string, object> attributes)
        {
            ArgumentNullException.ThrowIfNull(attributes);

            if (!string.IsNullOrEmpty(this.cssClass))
            {
                attributes["class"] = this.cssClass;
            }

            var inline = this.GetInlineStyle();
            if (inline != null)
            {
                attributes["style"] = inline;
            }
        }
    }
}
