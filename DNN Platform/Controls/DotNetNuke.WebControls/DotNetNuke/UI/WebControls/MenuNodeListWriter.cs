// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.IO;

    /// <summary>
    /// Renders a <see cref="MenuNode"/> as a nested unordered-list item.
    /// </summary>
    /// <remarks>
    /// The original wrote through <c>System.Web.UI.HtmlTextWriter</c> and composed
    /// <c>System.Web.UI.WebControls</c> primitives into a <c>HyperLink</c>'s control collection;
    /// both are replaced by this project's <see cref="HtmlMarkupWriter"/> and
    /// <see cref="MarkupControl"/> primitives, which emit the identical markup.
    /// </remarks>
    internal sealed class MenuNodeListWriter : IMenuNodeWriter
    {
        private MenuNode node;

        /// <summary>Initializes a new instance of the <see cref="MenuNodeListWriter"/> class.</summary>
        internal MenuNodeListWriter()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MenuNodeListWriter"/> class.</summary>
        /// <param name="orientation">The orientation the menu renders in.</param>
        internal MenuNodeListWriter(Orientation orientation)
        {
            this.Orientation = orientation;
        }

        /// <summary>Gets or sets the orientation the menu renders in.</summary>
        internal Orientation Orientation { get; set; }

        /// <inheritdoc/>
        public void RenderNode(TextWriter writer, MenuNode node)
        {
            this.node = node;
            this.Render(writer);
        }

        /// <summary>Gets the client side identifier of one of the node's rendered parts.</summary>
        /// <param name="nodeId">The identifier of the node.</param>
        /// <param name="prefix">The prefix identifying the part.</param>
        /// <returns>The composed client side identifier.</returns>
        private string GetChildControlID(string nodeId, string prefix)
        {
            return $"{this.node.DNNMenu.ClientID}{prefix}{nodeId}";
        }

        /// <summary>Creates the anchor of the node.</summary>
        /// <returns>The anchor, with its target URL set when the node navigates.</returns>
        private MarkupHyperLink GetLink()
        {
            var link = new MarkupHyperLink();
            if (this.node.Enabled && this.node.ClickAction == eClickAction.Navigate)
            {
                link.NavigateUrl = this.node.NavigateURL;
            }

            return link;
        }

        /// <summary>Renders the list item of the node and the nested list of its children.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void Render(TextWriter writer)
        {
            var html = HtmlMarkupWriter.For(writer);
            this.RenderOpenTag(html);

            var link = this.GetLink();
            this.RenderNodeIcon(link);
            this.RenderNodeText(link);
            this.RenderNodeArrow(link);
            link.RenderControl(html);

            if (this.node.HasNodes)
            {
                html.AddAttribute("id", this.GetChildControlID(this.node.ID, "sub"));
                html.RenderBeginTag("ul");
                this.RenderChildren(html);
                html.RenderEndTag();
            }

            html.RenderEndTag();
        }

        /// <summary>Renders the child nodes of the node.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderChildren(TextWriter writer)
        {
            var enumerator = this.node.MenuNodes.GetEnumerator();
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

        /// <summary>Adds the sub-menu arrow of the node to its anchor.</summary>
        /// <param name="parent">The anchor to add the arrow to.</param>
        private void RenderNodeArrow(MarkupControl parent)
        {
            if (!this.node.HasNodes)
            {
                return;
            }

            MarkupImage image = null;
            if (this.node.Level == 0)
            {
                if (!string.IsNullOrEmpty(this.node.DNNMenu.RootArrowImage))
                {
                    image = new MarkupImage { ImageUrl = this.node.DNNMenu.RootArrowImage };
                }
            }
            else if (!string.IsNullOrEmpty(this.node.DNNMenu.ChildArrowImage))
            {
                image = new MarkupImage { ImageUrl = this.node.DNNMenu.ChildArrowImage };
            }

            if (image is not null)
            {
                parent.Controls.Add(image);
            }
        }

        /// <summary>Adds the icon of the node to its anchor.</summary>
        /// <param name="parent">The anchor to add the icon to.</param>
        private void RenderNodeIcon(MarkupControl parent)
        {
            var cssIcon = string.Empty;
            if (!string.IsNullOrEmpty(this.node.CSSIcon))
            {
                cssIcon = this.node.CSSIcon;
            }
            else if (!string.IsNullOrEmpty(this.node.DNNMenu.DefaultIconCssClass))
            {
                cssIcon = this.node.DNNMenu.DefaultIconCssClass;
            }

            if (this.node.ImageIndex > -1)
            {
                var nodeImage = this.node.DNNMenu.ImageList[this.node.ImageIndex];
                if (nodeImage is not null)
                {
                    parent.Controls.Add(new MarkupImage
                    {
                        ImageUrl = nodeImage.ImageUrl,
                        AlternateText = " ",
                        CssClass = cssIcon,
                    });
                }
            }
        }

        /// <summary>Adds the text of the node to its anchor.</summary>
        /// <param name="parent">The anchor to add the text to.</param>
        private void RenderNodeText(MarkupControl parent)
        {
            var label = new MarkupLabel { Text = this.node.Text };
            if (!string.IsNullOrEmpty(this.node.ToolTip))
            {
                label.ToolTip = this.node.ToolTip;
            }

            parent.Controls.Add(label);
        }

        /// <summary>Renders the opening tag of the node's list item.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderOpenTag(HtmlMarkupWriter writer)
        {
            writer.AddAttribute("id", this.GetChildControlID(this.node.ID, "ctr"));
            writer.RenderBeginTag("li");
        }
    }
}
