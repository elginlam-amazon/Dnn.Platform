// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Globalization;
    using System.Net;

    using DotNetNuke.Common;
    using DotNetNuke.Modules.HTMLEditorProvider;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>
    /// The DNNRichTextEditControl control provides a standard UI component for editing
    /// RichText.
    /// </summary>
    /// <remarks>
    /// The configured <see cref="HtmlEditorProvider"/> now hands over its markup as a
    /// <see cref="RenderFragment"/> (<see cref="HtmlEditorProvider.HtmlEditorControl"/>) instead of
    /// as a server control to add to a control collection; when no provider is configured a
    /// <c>textarea</c> is rendered as before. <c>Server.HtmlEncode</c>/<c>Server.HtmlDecode</c> are
    /// now <see cref="WebUtility"/> calls.
    /// </remarks>
    public class DNNRichTextEditControl : TextEditControl
    {
        private HtmlEditorProvider richTextEditor;
        private string defaultEditorText = string.Empty;
        private bool editorCreated;

        /// <summary>Gets the markup of the editor that is rendered.</summary>
        /// <remarks>Replaces the <c>Control TextEditControl</c> property, which returned either the
        /// provider's control or the fallback <c>TextBox</c>.</remarks>
        protected RenderFragment TextEditControl
        {
            get
            {
                this.EnsureEditorCreated();
                if (this.richTextEditor != null)
                {
                    return this.richTextEditor.HtmlEditorControl;
                }

                return this.RenderDefaultEditor;
            }
        }

        /// <summary>Gets or sets the text held by the editor.</summary>
        protected string EditorText
        {
            get
            {
                this.EnsureEditorCreated();
                if (this.richTextEditor != null)
                {
                    return this.richTextEditor.Text;
                }

                return this.defaultEditorText;
            }

            set
            {
                this.EnsureEditorCreated();
                if (this.richTextEditor != null)
                {
                    this.richTextEditor.Text = value;
                }
                else
                {
                    this.defaultEditorText = value;
                }
            }
        }

        /// <inheritdoc />
        public override bool LoadPostData(string postDataKey, System.Collections.Specialized.NameValueCollection postCollection)
        {
            var dataChanged = false;
            var presentValue = this.StringValue;
            var postedValue = this.EditorText;
            if (!presentValue.Equals(postedValue, StringComparison.Ordinal))
            {
                this.Value = postedValue;
                dataChanged = true;
            }

            return dataChanged;
        }

        /// <inheritdoc />
        protected override void OnDataChanged(EventArgs e)
        {
            var strValue = RemoveBaseTags(Convert.ToString(this.Value, CultureInfo.InvariantCulture));
            var strOldValue = RemoveBaseTags(Convert.ToString(this.OldValue, CultureInfo.InvariantCulture));
            var args = new PropertyEditorEventArgs(this.Name)
            {
                Value = WebUtility.HtmlEncode(strValue),
                OldValue = WebUtility.HtmlEncode(strOldValue),
                StringValue = WebUtility.HtmlEncode(RemoveBaseTags(this.StringValue)),
            };
            this.OnValueChanged(args);
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            this.EnsureEditorCreated();
            base.OnInit(e);
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            if (this.EditMode == PropertyEditorMode.Edit)
            {
                this.EditorText = WebUtility.HtmlDecode(Convert.ToString(this.Value, CultureInfo.InvariantCulture));
            }
        }

        /// <inheritdoc />
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.EnsureEditorCreated();

            builder.OpenElement(0, "div");
            builder.AddAttribute(
                1,
                "class",
                string.IsNullOrEmpty(this.CssClass) ? "dnnLeft" : $"{this.CssClass} dnnLeft");
            builder.AddContent(2, this.TextEditControl);
            builder.CloseElement();
        }

        /// <inheritdoc />
        protected override void RenderViewMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            string propValue = WebUtility.HtmlDecode(Convert.ToString(this.Value, CultureInfo.InvariantCulture));

            builder.OpenElement(0, "span");
            this.AddStyleAttributes(builder, 1);

            // The value is editor html, so it is emitted as markup, which is what
            // HtmlTextWriter.Write(propValue) did.
            builder.AddMarkupContent(5, propValue);
            builder.CloseElement();
        }

        private static string RemoveBaseTags(string strInput)
        {
            return Globals.BaseTagRegex.Replace(strInput, " ");
        }

        /// <summary>Resolves and configures the editor provider, once.</summary>
        /// <remarks>Replaces <c>CreateChildControls</c>/<c>EnsureChildControls</c>.</remarks>
        private void EnsureEditorCreated()
        {
            if (this.editorCreated || this.EditMode != PropertyEditorMode.Edit)
            {
                return;
            }

            this.editorCreated = true;

            this.richTextEditor = HtmlEditorProvider.Instance();
            if (this.richTextEditor != null)
            {
                this.richTextEditor.ControlID = this.ID + "edit";
                this.richTextEditor.Initialize();
                this.richTextEditor.Height = this.ControlStyle.Height;
                this.richTextEditor.Width = this.ControlStyle.Width;
                if (this.richTextEditor.Height.IsEmpty)
                {
                    this.richTextEditor.Height = new Unit(250);
                }

                this.richTextEditor.Width = new Unit(400);
            }
        }

        private void RenderDefaultEditor(RenderTreeBuilder builder)
        {
            var width = this.ControlStyle.Width.IsEmpty ? new Unit(300) : this.ControlStyle.Width;
            var height = this.ControlStyle.Height.IsEmpty ? new Unit(250) : this.ControlStyle.Height;

            builder.OpenElement(0, "textarea");
            builder.AddAttribute(1, "id", this.ClientID + "edit");
            builder.AddAttribute(2, "name", this.UniqueID);
            builder.AddAttribute(3, "aria-label", "editor");
            builder.AddAttribute(
                4,
                "style",
                $"width:{width.ToString(CultureInfo.InvariantCulture)};height:{height.ToString(CultureInfo.InvariantCulture)};");
            builder.AddAttribute(5, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnEditorTextChanged));
            builder.AddContent(6, this.defaultEditorText);
            builder.CloseElement();
        }

        private void OnEditorTextChanged(ChangeEventArgs args)
        {
            this.defaultEditorText = args?.Value?.ToString() ?? string.Empty;
            this.CommitPostData();
        }
    }
}
