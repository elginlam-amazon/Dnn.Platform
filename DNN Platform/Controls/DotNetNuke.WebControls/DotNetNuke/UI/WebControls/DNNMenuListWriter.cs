// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.IO;

    /// <summary>
    /// Renders a <see cref="DNNMenu"/> as a nested unordered list, including its nodes.
    /// </summary>
    /// <remarks>
    /// The original derived from <c>WebControl</c> with the <c>Div</c> tag and wrote through
    /// <c>HtmlTextWriter</c>; both are replaced by this project's <see cref="HtmlMarkupWriter"/>,
    /// which emits the identical markup.
    /// </remarks>
    internal sealed class DNNMenuListWriter : IDNNMenuWriter
    {
        private DNNMenu menu;

        /// <inheritdoc/>
        public void RenderMenu(TextWriter writer, DNNMenu menu)
        {
            this.menu = menu;

            // The original passed HtmlTextWriterTag.Div to the WebControl constructor, so
            // RenderControl wrapped the contents in a bare <div>.
            var html = HtmlMarkupWriter.For(writer);
            html.RenderBeginTag("div");
            this.RenderContents(html);
            html.RenderEndTag();
        }

        /// <summary>Renders the nodes of the menu.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderChildren(TextWriter writer)
        {
            var enumerator = this.menu.MenuNodes.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    ((MenuNode)enumerator.Current).Render(writer);
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }

        /// <summary>Renders the list of the menu and its nodes.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderContents(HtmlMarkupWriter writer)
        {
            writer.AddAttribute("class", this.menu.CssClass);
            writer.AddAttribute("id", this.menu.ClientID);
            writer.RenderBeginTag("ul");
            this.RenderChildren(writer);
            writer.RenderEndTag();
        }
    }
}
