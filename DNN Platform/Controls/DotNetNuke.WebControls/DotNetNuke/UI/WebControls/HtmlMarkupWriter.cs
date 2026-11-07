// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Writes HTML markup to an underlying <see cref="TextWriter"/>.
    /// </summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.HtmlTextWriter</c>, which has no .NET 10 equivalent. It keeps the
    /// members the original render code used — buffering attributes and style attributes until the
    /// next <see cref="RenderBeginTag(string)"/> and popping the tag stack on
    /// <see cref="RenderEndTag"/> — so that the emitted markup is unchanged.
    /// Because it derives from <see cref="TextWriter"/> it can be handed to any API that takes a
    /// plain writer (the <see cref="IDNNTreeWriter"/> / <see cref="IDNNMenuWriter"/> contracts), and
    /// <see cref="For(TextWriter)"/> re-uses an existing instance instead of nesting wrappers.
    /// </remarks>
    internal sealed class HtmlMarkupWriter : TextWriter
    {
        private readonly TextWriter inner;
        private readonly List<KeyValuePair<string, string>> attributes = new List<KeyValuePair<string, string>>();
        private readonly List<KeyValuePair<string, string>> styles = new List<KeyValuePair<string, string>>();
        private readonly Stack<string> openTags = new Stack<string>();

        /// <summary>Initializes a new instance of the <see cref="HtmlMarkupWriter"/> class.</summary>
        /// <param name="writer">The writer the markup is emitted to.</param>
        internal HtmlMarkupWriter(TextWriter writer)
        {
            this.inner = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        /// <inheritdoc/>
        public override Encoding Encoding => this.inner.Encoding;

        /// <inheritdoc/>
        public override IFormatProvider FormatProvider => CultureInfo.InvariantCulture;

        /// <summary>Gets the writer the markup is ultimately emitted to.</summary>
        internal TextWriter InnerWriter => this.inner;

        /// <inheritdoc/>
        public override void Write(char value)
        {
            this.inner.Write(value);
        }

        /// <inheritdoc/>
        public override void Write(string value)
        {
            this.inner.Write(value);
        }

        /// <summary>
        /// Gets a markup writer for the given writer, re-using it when it already is one.
        /// </summary>
        /// <param name="writer">The writer to wrap.</param>
        /// <returns>A markup writer emitting to <paramref name="writer"/>.</returns>
        internal static HtmlMarkupWriter For(TextWriter writer)
        {
            return writer as HtmlMarkupWriter ?? new HtmlMarkupWriter(writer);
        }

        /// <summary>HTML-encodes an attribute value the way the original writer did.</summary>
        /// <param name="value">The raw value.</param>
        /// <returns>The encoded value.</returns>
        internal static string EncodeAttribute(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);
        }

        /// <summary>Buffers an attribute to be written with the next begin tag.</summary>
        /// <param name="name">The attribute name.</param>
        /// <param name="value">The attribute value.</param>
        internal void AddAttribute(string name, string value)
        {
            this.attributes.Add(new KeyValuePair<string, string>(name, value));
        }

        /// <summary>Buffers a style declaration to be written with the next begin tag.</summary>
        /// <param name="name">The style property name.</param>
        /// <param name="value">The style property value.</param>
        internal void AddStyleAttribute(string name, string value)
        {
            this.styles.Add(new KeyValuePair<string, string>(name, value));
        }

        /// <summary>Writes an opening tag, emitting and clearing the buffered attributes.</summary>
        /// <param name="tagName">The name of the tag.</param>
        internal void RenderBeginTag(string tagName)
        {
            // The original passed enum values and, in two places, a tag name with a stray trailing
            // space ("div "); trimming keeps the emitted markup valid and matches what the
            // HtmlTextWriter produced for those call sites.
            var tag = (tagName ?? string.Empty).Trim();
            this.inner.Write('<');
            this.inner.Write(tag);
            this.WriteBufferedAttributes();
            this.inner.Write('>');
            this.openTags.Push(tag);
        }

        /// <summary>Writes the closing tag of the innermost open tag.</summary>
        internal void RenderEndTag()
        {
            if (this.openTags.Count == 0)
            {
                return;
            }

            var tag = this.openTags.Pop();
            this.inner.Write("</");
            this.inner.Write(tag);
            this.inner.Write('>');
        }

        /// <summary>Writes a self-closing tag, emitting and clearing the buffered attributes.</summary>
        /// <param name="tagName">The name of the tag.</param>
        internal void RenderSelfClosingTag(string tagName)
        {
            this.inner.Write('<');
            this.inner.Write(tagName);
            this.WriteBufferedAttributes();
            this.inner.Write(" />");
        }

        /// <summary>Discards the buffered attributes and styles without writing them.</summary>
        internal void ClearPendingAttributes()
        {
            this.attributes.Clear();
            this.styles.Clear();
        }

        /// <summary>Writes the buffered attributes and styles into the tag currently being opened.</summary>
        private void WriteBufferedAttributes()
        {
            foreach (var attribute in this.attributes)
            {
                if (attribute.Value is null)
                {
                    continue;
                }

                this.inner.Write(' ');
                this.inner.Write(attribute.Key);
                this.inner.Write("=\"");
                this.inner.Write(EncodeAttribute(attribute.Value));
                this.inner.Write('"');
            }

            if (this.styles.Count > 0)
            {
                var builder = new StringBuilder();
                foreach (var style in this.styles)
                {
                    builder.Append(style.Key).Append(':').Append(style.Value).Append(';');
                }

                this.inner.Write(" style=\"");
                this.inner.Write(EncodeAttribute(builder.ToString()));
                this.inner.Write('"');
            }

            this.attributes.Clear();
            this.styles.Clear();
        }
    }
}
