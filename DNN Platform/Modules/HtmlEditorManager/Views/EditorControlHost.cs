// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.HtmlEditorManager.Views
{
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>
    /// Carries a provider settings control that is not part of the DNN component tree inside the
    /// editor panel's ordered child collection, rendering it as a component of its own type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The WebForms presenter did <c>this.View.Editor.Controls.Add(LoadControl(path))</c>, where the
    /// loaded control was a <see cref="PortalModuleBase"/>. On net10.0 a
    /// <see cref="PortalModuleBase"/> is a Blazor <see cref="ComponentBase"/> but <b>not</b> an
    /// <see cref="UI.Utilities.IClientApiControl"/>, so it cannot be put into a
    /// <see cref="DnnControlCollection"/> directly. This holder is the platform's own answer to
    /// exactly that problem (see the private <c>ComponentHolder</c> in
    /// <c>DotNetNuke.UI.Modules.ModuleHost</c>): it is a <see cref="DnnComponentBase"/>, so it fits
    /// the collection, and it renders the hosted component by its type.
    /// </para>
    /// <para>
    /// <see cref="HostedComponent"/> is a <see cref="ParameterAttribute"/> on purpose:
    /// <see cref="DnnComponentBase"/> renders a child of its <c>Controls</c> collection with
    /// <c>OpenComponent(child.GetType())</c> plus <c>DnnComponentParameters.Capture(child)</c>,
    /// which carries declared parameters over to the instance the renderer creates. Without it the
    /// holder would lose the control the presenter configured and the editor panel would silently
    /// render nothing.
    /// </para>
    /// <para>
    /// The configuration of the hosted control travels the same way: the presenter assigns
    /// <see cref="PortalModuleBase.ModuleConfiguration"/> on the loaded instance (as the WebForms
    /// code did), and because that property is itself a Blazor parameter,
    /// <c>DnnComponentParameters.Capture</c> carries it onto the rendered instance <i>before</i> it
    /// initializes. It is deliberately NOT applied through
    /// <c>RenderTreeBuilder.AddComponentReferenceCapture</c>: a reference capture frame is
    /// processed after the component frame has already been initialized and its parameters set, so
    /// the control would run its <c>OnInit</c>/<c>OnLoad</c> against a null configuration.
    /// </para>
    /// </remarks>
    public sealed class EditorControlHost : DnnComponentBase
    {
        /// <summary>Gets or sets the provider settings control this holder renders.</summary>
        [Parameter]
        public IComponent HostedComponent { get; set; }

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            if (this.HostedComponent == null)
            {
                return;
            }

            builder.OpenRegion(sequence++);
            builder.OpenComponent(0, this.HostedComponent.GetType());

            var parameters = DnnComponentParameters.Capture(this.HostedComponent);
            if (parameters.Count > 0)
            {
                builder.AddMultipleAttributes(1, parameters);
            }

            builder.CloseComponent();
            builder.CloseRegion();
        }
    }
}
