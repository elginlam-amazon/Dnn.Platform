// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.IO;

    /// <summary>
    /// Renders the up-level (script driven) container of a <see cref="DNNMenu"/>.
    /// </summary>
    /// <remarks>
    /// The original derived from <c>WebControl</c> only to reach its render pipeline and wrote
    /// through <c>HtmlTextWriter</c>; both are replaced by this project's
    /// <see cref="HtmlMarkupWriter"/>, which emits the identical markup.
    /// </remarks>
    internal sealed class DNNMenuUpLevelWriter : IDNNMenuWriter
    {
        private DNNMenu menu;

        /// <inheritdoc/>
        public void RenderMenu(TextWriter writer, DNNMenu menu)
        {
            this.menu = menu;

            // The original derived from WebControl with the default tag, so RenderControl wrapped
            // the contents in a bare <span>; that wrapper is reproduced here.
            var html = HtmlMarkupWriter.For(writer);
            html.RenderBeginTag("span");
            this.RenderContents(html);
            html.RenderEndTag();
        }

        /// <summary>
        /// Renders the nodes of the menu.
        /// </summary>
        /// <param name="writer">The writer to render to.</param>
        /// <remarks>
        /// Kept from the original even though <see cref="RenderContents"/> deliberately does not
        /// call it: in up-level mode the client script builds the nodes from the marshalled JSON.
        /// </remarks>
        internal void RenderChildren(TextWriter writer)
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

        /// <summary>Renders the empty container the client script fills in.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderContents(HtmlMarkupWriter writer)
        {
            writer.AddAttribute("class", this.menu.CssClass);
            writer.AddAttribute("id", this.menu.ClientID);
            writer.RenderBeginTag("span");
            writer.RenderEndTag();
        }
    }
}
