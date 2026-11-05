// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>Base class for the Property Editor suite of controls.</summary>
    /// <remarks>
    /// <para>
    /// These controls were <c>System.Web.UI.WebControls.WebControl</c>/<c>INamingContainer</c>
    /// implementations that built a child-control tree imperatively (<c>CreateChildControls</c>,
    /// <c>Controls.Add</c>, <c>DataBind</c>) and rendered through <c>HtmlTextWriter</c>. The DNN
    /// public API — and third-party code that extends it — depends on being able to
    /// <em>construct</em> an editor, configure its properties, subscribe to its events and call
    /// <c>DataBind()</c>, so the imperative object model is preserved.
    /// </para>
    /// <para>
    /// The bridge to Blazor is <see cref="Render"/>: it returns a <see cref="RenderFragment"/> that
    /// renders <em>this very instance</em>. That means a control created imperatively renders with
    /// its own state and its own event subscriptions — there is no second, hidden instance. The
    /// same control can also be used declaratively as a Blazor component (<c>&lt;FieldEditorControl
    /// ... /&gt;</c>), because <see cref="BuildRenderTree"/> renders the identical content.
    /// </para>
    /// <para>
    /// Because an imperatively created instance never receives the Blazor lifecycle callbacks,
    /// <see cref="EnsureInitialized"/> raises <see cref="OnInit"/>/<see cref="OnLoad"/> on first use
    /// and <see cref="OnPreRender"/> is raised immediately before each render. The ordering is
    /// therefore the same as it was under Web Forms regardless of how the instance was created.
    /// </para>
    /// </remarks>
    public abstract class PropertyEditorComponentBase : ComponentBase
    {
        private static int autoIdCounter;

        private readonly string autoId = "ctl" + Interlocked.Increment(ref autoIdCounter).ToString(CultureInfo.InvariantCulture);

        private string id;
        private bool initialized;
        private bool loaded;

        /// <summary>Gets or sets the identifier of this control.</summary>
        /// <remarks>Replaces <c>Control.ID</c>. It is still used to build the form field names that
        /// the DNN client scripts and <see cref="EditControl.LoadPostData"/> rely upon.</remarks>
        [Parameter]
        public string ID
        {
            get => this.id;
            set => this.id = value;
        }

        /// <summary>Gets the unique name used for this control's form fields.</summary>
        /// <remarks>Replaces <c>Control.UniqueID</c>.</remarks>
        public string UniqueID => string.IsNullOrEmpty(this.id) ? this.autoId : this.id;

        /// <summary>Gets the identifier emitted onto this control's element.</summary>
        /// <remarks>Replaces <c>Control.ClientID</c>.</remarks>
        public string ClientID => this.UniqueID.Replace('$', '_').Replace(' ', '_');

        /// <summary>Gets or sets a value indicating whether this control renders.</summary>
        /// <remarks>Replaces <c>Control.Visible</c>; it is honoured by <see cref="BuildRenderTree"/>
        /// and by <see cref="Render"/>.</remarks>
        [Parameter]
        public bool Visible { get; set; } = true;

        /// <summary>Gets the style applied to this control's element.</summary>
        /// <remarks>Replaces <c>WebControl.ControlStyle</c>.</remarks>
        public Style ControlStyle { get; } = new Style();

        /// <summary>Gets or sets the css class applied to this control's element.</summary>
        public string CssClass
        {
            get => this.ControlStyle.CssClass;
            set => this.ControlStyle.CssClass = value;
        }

        /// <summary>Gets or sets the width of this control's element.</summary>
        public Unit Width
        {
            get => this.ControlStyle.Width;
            set => this.ControlStyle.Width = value;
        }

        /// <summary>Gets or sets the height of this control's element.</summary>
        public Unit Height
        {
            get => this.ControlStyle.Height;
            set => this.ControlStyle.Height = value;
        }

        /// <summary>Gets or sets any additional attributes to apply to this control's element.</summary>
        /// <remarks>Replaces <c>WebControl.Attributes</c>.</remarks>
        [Parameter(CaptureUnmatchedValues = true)]
        public IDictionary<string, object> AdditionalAttributes { get; set; }

        /// <summary>Gets a <see cref="RenderFragment"/> that renders this control instance.</summary>
        /// <returns>A render fragment bound to this instance.</returns>
        /// <remarks>
        /// This is the seam that lets Blazor markup render a control that was created and configured
        /// in C#: <c>@myEditor.Render()</c>. The fragment renders this instance, so state changes and
        /// events observed through the instance are the real ones.
        /// </remarks>
        public RenderFragment Render()
        {
            return builder =>
            {
                this.EnsureInitialized();
                if (!this.Visible)
                {
                    return;
                }

                this.OnPreRender(EventArgs.Empty);
                this.RenderContent(builder);
            };
        }

        /// <summary>Binds this control to its data source, rebuilding its content.</summary>
        /// <remarks>Replaces <c>Control.DataBind()</c>.</remarks>
        public virtual void DataBind()
        {
            this.OnDataBinding(EventArgs.Empty);
        }

        /// <summary>Raises <see cref="OnInit"/> and <see cref="OnLoad"/> if they have not run yet.</summary>
        protected void EnsureInitialized()
        {
            if (!this.initialized)
            {
                this.initialized = true;
                this.OnInit(EventArgs.Empty);
            }

            if (!this.loaded)
            {
                this.loaded = true;
                this.OnLoad(EventArgs.Empty);
            }
        }

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            base.OnInitialized();
            this.EnsureInitialized();
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.EnsureInitialized();
            if (!this.Visible)
            {
                return;
            }

            this.OnPreRender(EventArgs.Empty);
            this.RenderContent(builder);
        }

        /// <summary>Renders this control's content.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        /// <remarks>Replaces <c>Render(HtmlTextWriter)</c>.</remarks>
        protected abstract void RenderContent(RenderTreeBuilder builder);

        /// <summary>Runs when this control is initialized.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnInit(EventArgs e)
        {
        }

        /// <summary>Runs when this control is loaded.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnLoad(EventArgs e)
        {
        }

        /// <summary>Runs immediately before this control renders.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnPreRender(EventArgs e)
        {
        }

        /// <summary>Runs when this control is being data bound.</summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnDataBinding(EventArgs e)
        {
        }

        /// <summary>Adds this control's style and additional attributes onto the element being built.</summary>
        /// <param name="builder">The render tree builder, positioned inside an open element.</param>
        /// <param name="sequence">The render tree sequence number to use.</param>
        protected void AddStyleAttributes(RenderTreeBuilder builder, int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.ControlStyle.AddAttributesToRender(builder, sequence);
            if (this.AdditionalAttributes != null)
            {
                builder.AddMultipleAttributes(sequence + 2, this.AdditionalAttributes);
            }
        }
    }
}
