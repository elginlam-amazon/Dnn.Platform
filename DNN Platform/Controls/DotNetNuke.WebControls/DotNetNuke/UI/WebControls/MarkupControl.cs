// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Base class of the lightweight markup primitives the render writers compose their output from.
    /// </summary>
    /// <remarks>
    /// Replaces the <c>System.Web.UI.WebControls</c> primitives (<c>Label</c>, <c>HyperLink</c>,
    /// <c>Image</c>, <c>CheckBox</c>) and <c>System.Web.UI.LiteralControl</c> that the original
    /// render code instantiated, populated and then called <c>RenderControl</c> on. Each subclass
    /// emits exactly the markup its WebForms counterpart emitted, so the HTML produced by the
    /// writers is unchanged.
    /// </remarks>
    internal abstract class MarkupControl
    {
        private readonly List<MarkupControl> controls = new List<MarkupControl>();
        private readonly Dictionary<string, string> attributes = new Dictionary<string, string>();
        private readonly Dictionary<string, string> style = new Dictionary<string, string>();

        /// <summary>Gets or sets the identifier rendered as the <c>id</c> attribute.</summary>
        internal string Id { get; set; }

        /// <summary>Gets or sets the value rendered as the <c>class</c> attribute.</summary>
        internal string CssClass { get; set; }

        /// <summary>Gets or sets the value rendered as the <c>title</c> attribute.</summary>
        internal string ToolTip { get; set; }

        /// <summary>Gets the additional attributes to render.</summary>
        internal IDictionary<string, string> Attributes => this.attributes;

        /// <summary>Gets the inline style declarations to render.</summary>
        internal IDictionary<string, string> Style => this.style;

        /// <summary>Gets the child markup rendered inside this control.</summary>
        internal IList<MarkupControl> Controls => this.controls;

        /// <summary>Gets the name of the tag this control renders as.</summary>
        protected abstract string TagName { get; }

        /// <summary>Gets a value indicating whether the tag renders without any content.</summary>
        protected virtual bool IsSelfClosing => false;

        /// <summary>Renders the whole control, its attributes and its content.</summary>
        /// <param name="writer">The writer to render to.</param>
        internal void RenderControl(TextWriter writer)
        {
            var html = HtmlMarkupWriter.For(writer);
            if (this.IsSelfClosing)
            {
                this.AddAttributesToRender(html);
                html.RenderSelfClosingTag(this.TagName);
                return;
            }

            this.RenderBeginTag(html);
            this.RenderContents(html);
            this.RenderEndTag(html);
        }

        /// <summary>Renders the opening tag only, leaving the element open for content.</summary>
        /// <param name="writer">The writer to render to.</param>
        internal void RenderBeginTag(TextWriter writer)
        {
            var html = HtmlMarkupWriter.For(writer);
            this.AddAttributesToRender(html);
            html.RenderBeginTag(this.TagName);
        }

        /// <summary>Renders the closing tag only.</summary>
        /// <param name="writer">The writer to render to.</param>
        internal void RenderEndTag(TextWriter writer)
        {
            HtmlMarkupWriter.For(writer).RenderEndTag();
        }

        /// <summary>Renders the content of the element.</summary>
        /// <param name="writer">The writer to render to.</param>
        protected virtual void RenderContents(TextWriter writer)
        {
            foreach (var child in this.controls)
            {
                child.RenderControl(writer);
            }
        }

        /// <summary>Buffers the attributes of this control onto the writer.</summary>
        /// <param name="writer">The writer to add the attributes to.</param>
        protected virtual void AddAttributesToRender(HtmlMarkupWriter writer)
        {
            if (!string.IsNullOrEmpty(this.Id))
            {
                writer.AddAttribute("id", this.Id);
            }

            if (!string.IsNullOrEmpty(this.CssClass))
            {
                writer.AddAttribute("class", this.CssClass);
            }

            if (!string.IsNullOrEmpty(this.ToolTip))
            {
                writer.AddAttribute("title", this.ToolTip);
            }

            foreach (var attribute in this.attributes)
            {
                writer.AddAttribute(attribute.Key, attribute.Value);
            }

            foreach (var declaration in this.style)
            {
                writer.AddStyleAttribute(declaration.Key, declaration.Value);
            }
        }
    }
}
