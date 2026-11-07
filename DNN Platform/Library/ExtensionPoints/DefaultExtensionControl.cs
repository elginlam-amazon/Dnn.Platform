// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.ExtensionPoints
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;

    using DotNetNuke.Framework;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>The base class of the controls that render DNN's extension points.</summary>
    /// <remarks>
    /// <para>
    /// This was a <c>System.Web.UI.WebControls.WebControl</c>. On ASP.NET Core the platform renders
    /// with Blazor, so it is now a component deriving from <see cref="UserControlBase"/> (which owns
    /// the <c>OnInit</c>/<c>OnLoad</c>/<c>OnPreRender</c> lifecycle protocol the extension controls
    /// were written against). <c>ViewState["X"]</c> backing is replaced by
    /// <see cref="ParameterAttribute"/>-annotated properties, which is how a host page passes the
    /// same values today.
    /// </para>
    /// <para>
    /// The WebForms controls built their output by adding children to <c>Controls</c> and then read
    /// that collection back to dispatch <c>BindAction</c>/<c>SaveAction</c>/<c>CancelAction</c> to
    /// them. Blazor has no mutable control tree, so a child extension is rendered through
    /// <see cref="DynamicComponent"/> and the component instance it creates is captured; the
    /// captured instances are exposed through <see cref="Controls"/>, which keeps the action
    /// dispatch loops in the derived controls behaving exactly as before.
    /// </para>
    /// </remarks>
    public class DefaultExtensionControl : UserControlBase
    {
        private readonly List<DynamicComponent> loadedControls = new List<DynamicComponent>();

        /// <summary>Gets or sets the name of the module whose extension points should be rendered.</summary>
        [Parameter]
        [Bindable(true)]
        [DefaultValue("")]
        public string Module { get; set; } = string.Empty;

        /// <summary>Gets or sets the extension point group to render.</summary>
        [Parameter]
        [Bindable(true)]
        [DefaultValue("")]
        public string Group { get; set; } = string.Empty;

        /// <summary>Gets or sets the name of the single extension point to render, if any.</summary>
        [Parameter]
        [Bindable(true)]
        [DefaultValue("")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class to render on the control's wrapper.</summary>
        /// <remarks>Replaces <c>WebControl.CssClass</c>.</remarks>
        [Parameter]
        public string CssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the module context of the module hosting this extension point.</summary>
        [Parameter]
        public ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets the extension controls that have been loaded by this control.</summary>
        /// <remarks>
        /// Replaces <c>Control.Controls</c>. The instances only exist once the child components have
        /// been created by the renderer, which is the case by the time a user action is dispatched.
        /// </remarks>
        public IEnumerable<object> Controls
            => this.loadedControls.Select(c => c.Instance).Where(i => i != null);

        /// <summary>Gets the control sources that this control renders, in order.</summary>
        protected List<string> ControlSources { get; } = new List<string>();

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            base.BuildRenderTree(builder);
            this.RenderExtensionControls(builder, 0);
        }

        /// <summary>Renders each loaded extension control through a <see cref="DynamicComponent"/>.</summary>
        /// <param name="builder">The render tree builder.</param>
        /// <param name="sequence">The starting sequence number.</param>
        protected void RenderExtensionControls(RenderTreeBuilder builder, int sequence)
        {
            // Note: no clearing of the captured instances here. A reference capture fires once, when
            // the child component is created, so clearing on each re-render would permanently lose
            // the references the action dispatch in the derived controls depends on.
            foreach (var controlSrc in this.ControlSources)
            {
                this.RenderExtensionControl(builder, sequence, controlSrc);
                sequence += 3;
            }
        }

        /// <summary>Renders a single extension control through a <see cref="DynamicComponent"/>.</summary>
        /// <param name="builder">The render tree builder.</param>
        /// <param name="sequence">The sequence number.</param>
        /// <param name="controlSrc">The control source declared by the extension point.</param>
        protected void RenderExtensionControl(RenderTreeBuilder builder, int sequence, string controlSrc)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var componentType = ExtensionControlResolver.Resolve(controlSrc);
            builder.OpenComponent<DynamicComponent>(sequence);
            builder.AddComponentParameter(sequence + 1, nameof(DynamicComponent.Type), componentType);
            builder.AddComponentReferenceCapture(sequence + 2, instance => this.loadedControls.Add((DynamicComponent)instance));
            builder.CloseComponent();
        }
    }
}
