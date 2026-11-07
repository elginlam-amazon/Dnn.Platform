// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Globalization;
    using System.IO;

    using DotNetNuke.UI.Utilities;

    /// <summary>
    /// Renders a <see cref="MenuNode"/> as the down-level (plain HTML) markup of a
    /// <see cref="DNNMenu"/>.
    /// </summary>
    /// <remarks>
    /// The original wrote through <c>System.Web.UI.HtmlTextWriter</c> and composed
    /// <c>System.Web.UI.WebControls</c> primitives; both are replaced by this project's
    /// <see cref="HtmlMarkupWriter"/> and <see cref="MarkupControl"/> primitives, which emit the
    /// identical markup.
    /// </remarks>
    internal sealed class MenuNodeWriter : IMenuNodeWriter
    {
        private MenuNode node;

        /// <summary>Initializes a new instance of the <see cref="MenuNodeWriter"/> class.</summary>
        internal MenuNodeWriter()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MenuNodeWriter"/> class.</summary>
        /// <param name="forceFullMenu">Whether the whole menu is rendered rather than one level.</param>
        internal MenuNodeWriter(bool forceFullMenu)
        {
            this.ForceFullMenu = forceFullMenu;
        }

        /// <summary>Initializes a new instance of the <see cref="MenuNodeWriter"/> class.</summary>
        /// <param name="forceFullMenu">Whether the whole menu is rendered rather than one level.</param>
        /// <param name="orientation">The orientation the menu renders in.</param>
        internal MenuNodeWriter(bool forceFullMenu, Orientation orientation)
        {
            this.ForceFullMenu = forceFullMenu;
            this.Orientation = orientation;
        }

        /// <summary>Gets or sets a value indicating whether the whole menu is rendered.</summary>
        internal bool ForceFullMenu { get; set; }

        /// <summary>Gets or sets the orientation the menu renders in.</summary>
        internal Orientation Orientation { get; set; }

        /// <inheritdoc/>
        public void RenderNode(TextWriter writer, MenuNode node)
        {
            this.node = node;
            this.Render(writer);
        }

        /// <summary>Gets the CSS class the given node renders with.</summary>
        /// <param name="menuNode">The node to inspect.</param>
        /// <returns>The CSS class of the node.</returns>
        private static string GetNodeCss(MenuNode menuNode)
        {
            var cssClass = menuNode.DNNMenu.CssClass;
            if (menuNode.Level > 0)
            {
                cssClass = menuNode.DNNMenu.DefaultChildNodeCssClass;
            }

            if (!string.IsNullOrEmpty(menuNode.CSSClass))
            {
                cssClass = menuNode.CSSClass;
            }

            if (menuNode.Selected)
            {
                cssClass = string.IsNullOrEmpty(menuNode.CSSClassSelected)
                    ? cssClass + " " + menuNode.DNNMenu.DefaultNodeCssClassSelected
                    : cssClass + " " + menuNode.CSSClassSelected;
            }

            return cssClass;
        }

        /// <summary>Renders the node, the walk-up link and the child level when applicable.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void Render(TextWriter writer)
        {
            var html = HtmlMarkupWriter.For(writer);
            MenuNode selected = null;
            string parentId = null;

            var selectedNodes = this.node.DNNMenu.SelectedMenuNodes;
            if (selectedNodes.Count > 0)
            {
                // The VB original indexed a 1-based Microsoft.VisualBasic.Collection with Count,
                // i.e. it took the last selected node; the migrated collection is a 0-based
                // ArrayList, so the same element is at Count - 1.
                selected = (MenuNode)selectedNodes[selectedNodes.Count - 1];
            }

            if (this.node.Parent is not null)
            {
                parentId = this.node.Parent.ID;
            }

            if (selected is null
                || this.node.Selected
                || selected.ID == this.node.ID
                || selected.ID == parentId
                || (selected.Parent is not null && selected.Parent.ID == parentId)
                || this.ForceFullMenu)
            {
                if (selected is not null
                    && selected.ID == this.node.ID
                    && !string.IsNullOrEmpty(parentId)
                    && parentId != this.node.DNNMenu.ClientID)
                {
                    this.RenderParentLink(html, parentId);
                }

                this.RenderContents(html);
            }

            if (this.node.HasNodes && (this.node.Selected || this.ForceFullMenu))
            {
                html.AddAttribute("class", "Child");
                html.AddAttribute("width", "100%");
                if (this.ForceFullMenu || this.Orientation == Orientation.Vertical)
                {
                    html.RenderBeginTag("div");
                }
                else
                {
                    html.RenderBeginTag("span");
                }

                this.RenderChildren(html);
                html.RenderEndTag();
            }
            else if (selected is not null && selected.Level > this.node.Level)
            {
                this.RenderChildren(html);
            }
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

        /// <summary>Renders the row of the node itself.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderContents(HtmlMarkupWriter writer)
        {
            this.RenderOpenTag(writer);
            this.RenderNodeIcon(writer);
            this.RenderNodeText(writer);
            this.RenderNodeArrow(writer);
            writer.RenderEndTag();
        }

        /// <summary>Renders the sub-menu arrow of the node.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderNodeArrow(HtmlMarkupWriter writer)
        {
            if (!this.node.HasNodes)
            {
                return;
            }

            var label = new MarkupLabel();
            label.RenderBeginTag(writer);
            if (this.node.Level == 0)
            {
                if (!string.IsNullOrEmpty(this.node.DNNMenu.RootArrowImage))
                {
                    new MarkupImage { ImageUrl = this.node.DNNMenu.RootArrowImage }.RenderControl(writer);
                }
            }
            else if (!string.IsNullOrEmpty(this.node.DNNMenu.ChildArrowImage))
            {
                new MarkupImage { ImageUrl = this.node.DNNMenu.ChildArrowImage }.RenderControl(writer);
            }

            label.RenderEndTag(writer);
        }

        /// <summary>Renders the icon of the node.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderNodeIcon(HtmlMarkupWriter writer)
        {
            var label = new MarkupLabel();
            if (!string.IsNullOrEmpty(this.node.CSSIcon))
            {
                label.CssClass = this.node.CSSIcon;
            }
            else if (!string.IsNullOrEmpty(this.node.DNNMenu.DefaultIconCssClass))
            {
                label.CssClass = this.node.DNNMenu.DefaultIconCssClass;
            }

            label.RenderBeginTag(writer);
            if (this.node.ImageIndex > -1)
            {
                var nodeImage = this.node.DNNMenu.ImageList[this.node.ImageIndex];
                if (nodeImage is not null)
                {
                    new MarkupImage { ImageUrl = nodeImage.ImageUrl }.RenderControl(writer);
                    writer.Write("&nbsp;");
                }
            }

            label.RenderEndTag(writer);
        }

        /// <summary>Renders the clickable text of the node.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderNodeText(HtmlMarkupWriter writer)
        {
            var link = new MarkupHyperLink();
            var menu = this.node.DNNMenu;
            var script = WebControls.GetNodeJS(menu, this.node, menu.JSFunction, menu.Target, true);
            link.Text = this.node.Text;
            if (!this.node.Enabled)
            {
                link.NavigateUrl = ClientAPI.GetPostBackClientHyperlink(
                    menu, this.node.ID + ClientAPI.COLUMN_DELIMITER + "Click");
            }
            else
            {
                link.NavigateUrl = !string.IsNullOrEmpty(script) ? "javascript:" + script : this.node.NavigateURL;
            }

            if (!string.IsNullOrEmpty(this.node.ToolTip))
            {
                link.ToolTip = this.node.ToolTip;
            }

            var nodeCss = GetNodeCss(this.node);
            if (!string.IsNullOrEmpty(nodeCss))
            {
                link.CssClass = nodeCss;
            }

            link.RenderControl(writer);
        }

        /// <summary>Renders the opening tag of the node's row.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderOpenTag(HtmlMarkupWriter writer)
        {
            writer.AddAttribute("name", this.node.ID);
            writer.AddAttribute("id", this.node.ID);
            if (this.ForceFullMenu || this.Orientation == Orientation.Vertical)
            {
                writer.AddStyleAttribute(
                    "padding-left",
                    (this.node.Level * 20).ToString(CultureInfo.InvariantCulture) + "px");
                writer.RenderBeginTag("div");
            }
            else
            {
                writer.RenderBeginTag("span");
            }
        }

        /// <summary>Renders the link that walks back up to the parent level.</summary>
        /// <param name="writer">The writer to render to.</param>
        /// <param name="id">The identifier of the parent node.</param>
        private void RenderParentLink(HtmlMarkupWriter writer, string id)
        {
            var link = new MarkupHyperLink
            {
                Text = "..\\",
                NavigateUrl = ClientAPI.GetPostBackClientHyperlink(
                    this.node.DNNMenu, id + ClientAPI.COLUMN_DELIMITER + "Click"),
            };
            link.RenderControl(writer);
        }
    }
}
