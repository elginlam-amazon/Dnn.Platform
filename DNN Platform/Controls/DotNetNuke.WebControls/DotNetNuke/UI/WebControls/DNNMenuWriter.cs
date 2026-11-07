// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.IO;

    /// <summary>
    /// Renders a <see cref="DNNMenu"/> as down-level (plain HTML) markup, including its nodes.
    /// </summary>
    /// <remarks>
    /// The original derived from <c>WebControl</c> only to reach its render pipeline and wrote
    /// through <c>HtmlTextWriter</c>; both are replaced by this project's
    /// <see cref="HtmlMarkupWriter"/>, which emits the identical markup.
    /// </remarks>
    internal sealed class DNNMenuWriter : IDNNMenuWriter
    {
        private DNNMenu menu;

        /// <summary>Initializes a new instance of the <see cref="DNNMenuWriter"/> class.</summary>
        internal DNNMenuWriter()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNMenuWriter"/> class.</summary>
        /// <param name="forceFullMenu">Whether the whole menu is rendered rather than one level.</param>
        internal DNNMenuWriter(bool forceFullMenu)
        {
            this.ForceFullMenu = forceFullMenu;
        }

        /// <summary>Gets or sets a value indicating whether the whole menu is rendered.</summary>
        internal bool ForceFullMenu { get; set; }

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

        /// <summary>Renders the container of the menu and its nodes.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderContents(HtmlMarkupWriter writer)
        {
            var cssClass = "DNNMenu ";
            if (this.menu.SelectedMenuNodes.Count == 0)
            {
                if (!string.IsNullOrEmpty(this.menu.MenuBarCssClass))
                {
                    cssClass += this.menu.MenuBarCssClass;
                }
            }
            else if (!string.IsNullOrEmpty(this.menu.MenuCssClass))
            {
                cssClass += this.menu.MenuCssClass;
            }

            writer.AddAttribute("width", "100%");
            writer.AddAttribute("class", cssClass);
            writer.AddAttribute("name", this.menu.UniqueID);
            writer.AddAttribute("id", this.menu.ClientID);
            writer.RenderBeginTag("div");
            this.RenderChildren(writer);
            writer.RenderEndTag();
        }
    }
}
