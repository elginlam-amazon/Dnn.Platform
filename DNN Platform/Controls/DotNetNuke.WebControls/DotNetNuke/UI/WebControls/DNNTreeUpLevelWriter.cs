// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.IO;

    using DotNetNuke.UI.Utilities;

    /// <summary>
    /// Renders the up-level (script driven) container of a <see cref="DnnTree"/> and marshals the
    /// properties the client script needs to build the nodes itself.
    /// </summary>
    /// <remarks>
    /// The original derived from <c>WebControl</c> only to reach its render pipeline and wrote
    /// through <c>HtmlTextWriter</c>; both are replaced by this project's
    /// <see cref="HtmlMarkupWriter"/>, which emits the identical markup.
    /// </remarks>
    internal sealed class DNNTreeUpLevelWriter : IDNNTreeWriter
    {
        private DnnTree tree;

        /// <inheritdoc/>
        public Hashtable MarshalledProperties()
        {
            var properties = new Hashtable();
            if (this.tree.ImageList.Count > 0)
            {
                var imageList = string.Empty;
                var enumerator = this.tree.ImageList.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext())
                    {
                        var current = (NodeImage)enumerator.Current;
                        imageList += (imageList.Length > 0 ? "," : string.Empty) + current.ImageUrl;
                    }
                }
                finally
                {
                    (enumerator as IDisposable)?.Dispose();
                }

                properties.Add("imagelist", imageList);
            }

            properties.Add(
                "postback",
                ClientAPI.GetPostBackEventReference(this.tree, "[NODEID]" + ClientAPI.COLUMN_DELIMITER + "Click"));

            if (this.tree.PopulateNodesFromClient)
            {
                if (ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.XMLHTTP))
                {
                    properties.Add(
                        "callback",
                        ClientAPI.GetCallbackEventReference(
                            this.tree,
                            "'[NODEXML]'",
                            "this.callBackSuccess",
                            "tNode",
                            "this.callBackFail",
                            "this.callBackStatus"));
                }
                else
                {
                    properties.Add(
                        "callback",
                        ClientAPI.GetPostBackClientHyperlink(this.tree, "[NODEID]" + ClientAPI.COLUMN_DELIMITER + "OnDemand"));
                }

                if (!string.IsNullOrEmpty(this.tree.CallbackStatusFunction))
                {
                    properties.Add("callbackSF", this.tree.CallbackStatusFunction);
                }
            }

            return properties;
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

        /// <summary>
        /// Renders the nodes of the tree.
        /// </summary>
        /// <param name="writer">The writer to render to.</param>
        /// <remarks>
        /// Kept from the original even though <see cref="RenderContents"/> deliberately does not
        /// call it: in up-level mode the client script builds the nodes from the marshalled JSON.
        /// </remarks>
        internal void RenderChildren(TextWriter writer)
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

        /// <summary>Renders the empty container the client script fills in.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderContents(HtmlMarkupWriter writer)
        {
            writer.AddAttribute("width", "100%");
            writer.AddAttribute("class", "DNNTree");
            writer.AddAttribute("name", this.tree.UniqueID);
            writer.AddAttribute("id", this.tree.ClientID);
            writer.RenderBeginTag("div");
            writer.RenderEndTag();
        }
    }
}
