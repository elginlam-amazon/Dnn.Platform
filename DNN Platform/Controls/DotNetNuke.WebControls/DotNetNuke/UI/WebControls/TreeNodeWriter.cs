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
    /// Renders a <see cref="TreeNode"/> as the down-level (plain HTML) markup of a
    /// <see cref="DnnTree"/>.
    /// </summary>
    /// <remarks>
    /// The original wrote through <c>System.Web.UI.HtmlTextWriter</c> and composed
    /// <c>System.Web.UI.WebControls</c> primitives. Both are replaced by this project's
    /// <see cref="HtmlMarkupWriter"/> and <see cref="MarkupControl"/> primitives, which emit the
    /// identical markup, so the writer stays usable as the server rendered fallback.
    /// </remarks>
    internal sealed class TreeNodeWriter : ITreeNodeWriter
    {
        private static readonly string[] ExpandCollapse = { "+", "-" };

        private TreeNode node;

        /// <inheritdoc/>
        public void RenderNode(TextWriter writer, TreeNode node)
        {
            this.node = node;
            this.Render(writer);
        }

        /// <summary>Gets the CSS class the given node renders with.</summary>
        /// <param name="treeNode">The node to inspect.</param>
        /// <returns>The CSS class of the node.</returns>
        private static string GetNodeCss(TreeNode treeNode)
        {
            var cssClass = treeNode.DNNTree.CssClass;
            if (treeNode.Level > 0)
            {
                cssClass = treeNode.DNNTree.DefaultChildNodeCssClass;
            }

            if (!string.IsNullOrEmpty(treeNode.CssClass))
            {
                cssClass = treeNode.CssClass;
            }

            if (treeNode.Selected)
            {
                cssClass = string.IsNullOrEmpty(treeNode.CSSClassSelected)
                    ? cssClass + " " + treeNode.DNNTree.DefaultNodeCssClassSelected
                    : cssClass + " " + treeNode.CSSClassSelected;
            }

            return cssClass;
        }

        /// <summary>Renders the node and, when it is expanded, its children.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void Render(TextWriter writer)
        {
            var html = HtmlMarkupWriter.For(writer);
            this.RenderContents(html);
            if (this.node.HasNodes && (this.node.IsExpanded || this.node.DNNTree.IsCrawler))
            {
                html.AddAttribute("class", "Child");
                html.AddAttribute("width", "100%");
                html.RenderBeginTag("div");
                this.RenderChildren(html);
                html.RenderEndTag();
            }
        }

        /// <summary>Renders the child nodes of the node.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderChildren(TextWriter writer)
        {
            var enumerator = this.node.TreeNodes.GetEnumerator();
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

        /// <summary>Renders the row of the node itself.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderContents(HtmlMarkupWriter writer)
        {
            this.RenderOpenTag(writer);
            if (this.node.DNNTree.IndentWidth == 0)
            {
                this.node.DNNTree.IndentWidth = 9;
            }

            if (this.node.Level > 0)
            {
                this.RenderSpacer(writer, this.node.Level * this.node.DNNTree.IndentWidth);
            }

            this.RenderExpandNodeIcon(writer);
            this.RenderNodeCheckbox(writer);
            this.RenderNodeIcon(writer);
            this.RenderNodeText(writer);
            writer.RenderEndTag();
        }

        /// <summary>Renders the expand/collapse affordance of the node.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderExpandNodeIcon(HtmlMarkupWriter writer)
        {
            if (!this.node.HasNodes)
            {
                this.RenderSpacer(writer, this.node.DNNTree.ExpandCollapseImageWidth);
            }
            else
            {
                var link = new MarkupHyperLink();
                var image = new MarkupImage();
                if (this.node.IsExpanded || this.node.DNNTree.IsCrawler)
                {
                    link.Text = ExpandCollapse[1];
                    if (!string.IsNullOrEmpty(this.node.DNNTree.ExpandedNodeImage))
                    {
                        image.ImageUrl = this.node.DNNTree.ExpandedNodeImage;
                    }
                }
                else
                {
                    link.Text = ExpandCollapse[0];
                    if (!string.IsNullOrEmpty(this.node.DNNTree.CollapsedNodeImage))
                    {
                        image.ImageUrl = this.node.DNNTree.CollapsedNodeImage;
                    }
                }

                link.NavigateUrl = ClientAPI.GetPostBackClientHyperlink(this.node.DNNTree, this.node.ID);
                if (string.IsNullOrEmpty(image.ImageUrl))
                {
                    link.RenderControl(writer);
                }
                else
                {
                    link.RenderBeginTag(writer);
                    image.RenderControl(writer);
                    link.RenderEndTag(writer);
                }
            }

            writer.Write("&nbsp;");
        }

        /// <summary>Renders the check box of the node, when the tree shows check boxes.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderNodeCheckbox(HtmlMarkupWriter writer)
        {
            if (!this.node.DNNTree.CheckBoxes)
            {
                return;
            }

            var box = new MarkupCheckBox
            {
                Id = this.node.ID + TreeNode.Separator + TreeNode.CheckboxIdSuffix,
                Checked = this.node.Selected,
            };

            var expression = string.Empty;
            if (!string.IsNullOrEmpty(this.node.JSFunction))
            {
                if (!this.node.JSFunction.EndsWith(';'))
                {
                    this.node.JSFunction += ";";
                }

                expression += this.node.JSFunction;
            }

            if (!string.IsNullOrEmpty(this.node.DNNTree.JSFunction))
            {
                if (!this.node.DNNTree.JSFunction.EndsWith(';'))
                {
                    this.node.DNNTree.JSFunction += ";";
                }

                expression += this.node.DNNTree.JSFunction;
            }

            var clickScript = ClientAPI
                .GetPostBackClientHyperlink(this.node.DNNTree, this.node.ID + ClientAPI.COLUMN_DELIMITER + "Click")
                .Replace("javascript:", string.Empty, StringComparison.Ordinal) + ";";
            var checkedScript = ClientAPI
                .GetPostBackClientHyperlink(this.node.DNNTree, this.node.ID + ClientAPI.COLUMN_DELIMITER + "Checked")
                .Replace("javascript:", string.Empty, StringComparison.Ordinal) + ";";

            if (this.node.Selected)
            {
                expression = checkedScript;
            }
            else if (expression.Length <= 0)
            {
                expression += clickScript;
            }
            else
            {
                expression = "if (eval(\"" + expression.Replace("\"", "\"\"", StringComparison.Ordinal) + "\") != false) "
                    + clickScript + " else " + checkedScript;
            }

            box.Attributes.Add("onclick", expression);
            box.RenderControl(writer);
            writer.Write("&nbsp;");
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
            else if (!string.IsNullOrEmpty(this.node.DNNTree.DefaultIconCssClass))
            {
                label.CssClass = this.node.DNNTree.DefaultIconCssClass;
            }

            label.RenderBeginTag(writer);
            if (this.node.ImageIndex > -1)
            {
                var nodeImage = this.node.DNNTree.ImageList[this.node.ImageIndex];
                if (nodeImage is not null)
                {
                    var image = new MarkupImage { ImageUrl = nodeImage.ImageUrl };
                    image.RenderControl(writer);
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
            var expression = string.Empty;
            link.Text = this.node.Text;
            if (!string.IsNullOrEmpty(this.node.JSFunction))
            {
                if (!this.node.JSFunction.EndsWith(';'))
                {
                    this.node.JSFunction += ";";
                }

                expression += this.node.JSFunction;
            }
            else if (!string.IsNullOrEmpty(this.node.DNNTree.JSFunction))
            {
                if (!this.node.DNNTree.JSFunction.EndsWith(';'))
                {
                    this.node.DNNTree.JSFunction += ";";
                }

                expression += this.node.DNNTree.JSFunction;
            }

            if (this.node.Enabled)
            {
                switch (this.node.ClickAction)
                {
                    case eClickAction.PostBack:
                    case eClickAction.Expand:
                        if (expression.Length > 0)
                        {
                            expression = "if (eval(\"" + expression.Replace("\"", "\"\"", StringComparison.Ordinal) + "\") != false) ";
                        }

                        expression += ClientAPI
                            .GetPostBackClientHyperlink(this.node.DNNTree, this.node.ID + ClientAPI.COLUMN_DELIMITER + "Click")
                            .Replace("javascript:", string.Empty, StringComparison.Ordinal);
                        break;

                    case eClickAction.Navigate:
                        if (expression.Length > 0)
                        {
                            expression = "if (eval(\"" + expression.Replace("\"", "\"\"", StringComparison.Ordinal) + "\") != false) ";
                        }

                        if (string.IsNullOrEmpty(this.node.DNNTree.Target))
                        {
                            expression = expression + "window.location.href='" + this.node.NavigateUrl + "';";
                        }
                        else
                        {
                            expression = string.Concat(
                                expression,
                                "window.frames.",
                                this.node.DNNTree.Target,
                                ".location.href='",
                                this.node.NavigateUrl,
                                "'; void(0);");
                        }

                        break;

                    default:
                        break;
                }

                link.NavigateUrl = "javascript:" + expression;
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
            writer.AddAttribute("id", this.node.ID.Replace(TreeNode.Separator, "_", StringComparison.Ordinal));
            writer.RenderBeginTag("div");
        }

        /// <summary>Renders the spacer image used to indent the node.</summary>
        /// <param name="writer">The writer to render to.</param>
        /// <param name="width">The width of the spacer, in pixels.</param>
        private void RenderSpacer(HtmlMarkupWriter writer, int width)
        {
            writer.AddStyleAttribute("width", width.ToString(CultureInfo.InvariantCulture) + "px");
            writer.AddStyleAttribute("height", "1px");
            writer.AddAttribute("src", this.node.DNNTree.SystemImagesPath + "spacer.gif");
            writer.RenderBeginTag("img");
            writer.RenderEndTag();
        }
    }
}
