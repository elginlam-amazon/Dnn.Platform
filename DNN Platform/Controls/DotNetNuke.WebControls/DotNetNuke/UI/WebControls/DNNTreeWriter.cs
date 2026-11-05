// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.IO;

    /// <summary>
    /// Renders a <see cref="DnnTree"/> as down-level (plain HTML) markup, including its nodes.
    /// </summary>
    /// <remarks>
    /// The original derived from <c>WebControl</c> only to reach its <c>RenderControl</c> /
    /// <c>RenderContents</c> pipeline and wrote through <c>HtmlTextWriter</c>. Both are replaced by
    /// this project's <see cref="HtmlMarkupWriter"/>, which emits the identical markup.
    /// </remarks>
    internal sealed class DNNTreeWriter : IDNNTreeWriter
    {
        private DnnTree tree;

        /// <inheritdoc/>
        public Hashtable MarshalledProperties()
        {
            return new Hashtable();
        }

        /// <inheritdoc/>
        public void RenderTree(TextWriter writer, DnnTree tree)
        {
            this.tree = tree;

            // The original derived from WebControl with the default tag, so RenderControl wrapped
            // the contents in a bare <span>; that wrapper is reproduced here.
            var html = HtmlMarkupWriter.For(writer);
            html.RenderBeginTag("span");
            this.RenderContents(html);
            html.RenderEndTag();
        }

        /// <summary>Renders the nodes of the tree.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderChildren(TextWriter writer)
        {
            var enumerator = this.tree.TreeNodes.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    ((TreeNode)enumerator.Current).Render(writer);
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }

        /// <summary>Renders the container of the tree and its nodes.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderContents(HtmlMarkupWriter writer)
        {
            writer.AddAttribute("width", "100%");
            writer.AddAttribute("class", "DNNTree");
            writer.AddAttribute("name", this.tree.UniqueID);
            writer.AddAttribute("id", this.tree.ClientID);
            writer.RenderBeginTag("div");
            this.RenderChildren(writer);
            writer.RenderEndTag();
        }
    }
}
