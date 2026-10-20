// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.UI
{
    using System.ComponentModel;

    /// <summary>
    /// Replacement for the System.Web.Extensions <c>UpdatePanel</c> on .NET 10.
    /// </summary>
    /// <remarks>
    /// Partial page rendering is not available on ASP.NET Core, the panel renders its content like a regular container and
    /// the postbacks of its child controls are full postbacks.
    /// </remarks>
    [ParseChildren(true)]
    [PersistChildren(false)]
    public class UpdatePanel : Control
    {
        private ITemplate contentTemplate;
        private Control contentTemplateContainer;

        /// <summary>Gets or sets the update mode.</summary>
        [DefaultValue(UpdatePanelUpdateMode.Always)]
        public UpdatePanelUpdateMode UpdateMode { get; set; } = UpdatePanelUpdateMode.Always;

        /// <summary>Gets or sets a value indicating whether postbacks of child controls update the panel.</summary>
        [DefaultValue(true)]
        public bool ChildrenAsTriggers { get; set; } = true;

        /// <summary>Gets or sets the render mode.</summary>
        [DefaultValue(UpdatePanelRenderMode.Block)]
        public UpdatePanelRenderMode RenderMode { get; set; } = UpdatePanelRenderMode.Block;

        /// <summary>Gets or sets the content template.</summary>
        [Browsable(false)]
        [PersistenceMode(PersistenceMode.InnerProperty)]
        [TemplateInstance(TemplateInstance.Single)]
        public ITemplate ContentTemplate
        {
            get => this.contentTemplate;
            set
            {
                this.contentTemplate = value;
                if (value != null && this.contentTemplateContainer != null)
                {
                    value.InstantiateIn(this.contentTemplateContainer);
                }
            }
        }

        /// <summary>Gets the container of the panel content.</summary>
        [Browsable(false)]
        public Control ContentTemplateContainer
        {
            get
            {
                if (this.contentTemplateContainer == null)
                {
                    this.contentTemplateContainer = this.CreateContentTemplateContainer();
                    this.Controls.Add(this.contentTemplateContainer);
                    this.contentTemplate?.InstantiateIn(this.contentTemplateContainer);
                }

                return this.contentTemplateContainer;
            }
        }

        /// <summary>Gets a value indicating whether the panel is updated during the current request. Always <see langword="false"/> as all postbacks are full postbacks.</summary>
        [Browsable(false)]
        public bool IsInPartialRendering => false;

        /// <summary>Requests an update of the panel content (no-op, the whole page is rendered on every postback).</summary>
        public void Update()
        {
        }

        /// <summary>Creates the content template container.</summary>
        /// <returns>The container control.</returns>
        protected virtual Control CreateContentTemplateContainer()
        {
            return new Control();
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            _ = this.ContentTemplateContainer;
        }

        /// <inheritdoc />
        protected override void Render(HtmlTextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.AddAttribute(HtmlTextWriterAttribute.Id, this.ClientID);
            writer.RenderBeginTag(this.RenderMode == UpdatePanelRenderMode.Block ? HtmlTextWriterTag.Div : HtmlTextWriterTag.Span);
            this.RenderChildren(writer);
            writer.RenderEndTag();
        }
    }
}
