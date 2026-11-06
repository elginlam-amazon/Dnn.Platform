// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider
{
    using System.Collections;

    using DNNConnect.CKEditorProvider.Web;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Modules.HTMLEditorProvider;
    using DotNetNuke.UI;
    using DotNetNuke.UI.WebControls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The CKEditor Provider.</summary>
    /// <remarks>
    /// <see cref="HtmlEditorProvider.HtmlEditorControl"/> is a <see cref="RenderFragment"/> now
    /// rather than a <c>System.Web.UI.Control</c>, because ASP.NET Core has no server control tree
    /// for the consumer to add the editor to. <see cref="Initialize"/> still creates the editor out
    /// of the request scope so the services it needs are injected, and the created instance stays
    /// the one this provider's <see cref="Text"/>/<see cref="Width"/>/<see cref="Height"/>
    /// properties read and write: the fragment renders the component with that instance's
    /// parameters and writes the edited html back onto it.
    /// </remarks>
    public class CKHtmlEditorProvider : HtmlEditorProvider
    {
        /// <summary>The _additional toolbars.</summary>
        private ArrayList additionalToolbars = new ArrayList();

        /// <summary>The _html editor control.</summary>
        private EditorControl htmlEditorControl;

        /// <summary>The _root image directory.</summary>
        private string rootImageDirectory;

        // BL0005 warns that a component parameter is assigned from outside the component, which
        // would be overwritten on the next parameter set. That cannot happen here: the
        // EditorControl instance Initialize() creates is never rendered as a child whose parent
        // supplies parameters - RenderEditor() captures the configuration this provider applied to
        // it (DnnComponentParameters.Capture) and hands it to the renderer as the parameters of the
        // component the renderer creates. This is the provider contract HtmlEditorProvider defines
        // and the same seam the platform's own OAuthSettingsBase uses.
#pragma warning disable BL0005
        /// <summary>Gets or sets AdditionalToolbars.</summary>
        public override ArrayList AdditionalToolbars
        {
            get => this.additionalToolbars;
            set => this.additionalToolbars = value;
        }

        /// <summary>Gets or sets ControlID.</summary>
        public override string ControlID { get; set; }

        /// <summary>Gets or sets Height.</summary>
        public override Unit Height
        {
            get => this.htmlEditorControl.Height;
            set => this.htmlEditorControl.Height = value;
        }

        /// <summary>Gets HtmlEditorControl.</summary>
        public override RenderFragment HtmlEditorControl => this.RenderEditor;

        /// <summary>Gets or sets RootImageDirectory.</summary>
        public override string RootImageDirectory
        {
            get
            {
                if (this.rootImageDirectory == string.Empty)
                {
                    // Remove the Application Path from the Home Directory
                    return Globals.ApplicationPath != string.Empty
                               ? this.PortalSettings.HomeDirectory.Replace(Globals.ApplicationPath, string.Empty)
                               : this.PortalSettings.HomeDirectory;
                }

                return this.rootImageDirectory;
            }

            set
            {
                this.rootImageDirectory = value;
            }
        }

        /// <summary>Gets or sets Text.</summary>
        public override string Text
        {
            get => this.htmlEditorControl.Value;
            set => this.htmlEditorControl.Value = value;
        }

        /// <summary>Gets or sets Width.</summary>
        public override Unit Width
        {
            get => this.htmlEditorControl.Width;
            set => this.htmlEditorControl.Width = value;
        }

        /// <summary>The add toolbar.</summary>
        public override void AddToolbar()
        {
            // Must exist because it exists at the base class
        }

        /// <summary>The initialize.</summary>
        public override void Initialize()
        {
            this.htmlEditorControl = ActivatorUtilities.CreateInstance<EditorControl>(HttpContextSource.Current.GetScope().ServiceProvider);
            this.htmlEditorControl.ID = this.ControlID;
        }

        /// <summary>Renders the editor the consumer asked for.</summary>
        /// <param name="builder">The render tree builder of the consumer.</param>
        /// <remarks>
        /// The configuration the consumer applied through this provider lives on the instance
        /// <see cref="Initialize"/> created, so it is handed to the renderer as the component's
        /// parameters (the same seam the platform's skinning engine uses through
        /// <see cref="DnnComponentParameters.Capture"/>). The added
        /// <see cref="EditorControl.ValueChanged"/> callback writes the edited html back onto that
        /// instance, which is what keeps <see cref="Text"/> returning what the user typed, the way
        /// <c>LoadPostData</c> used to.
        /// </remarks>
        private void RenderEditor(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            if (this.htmlEditorControl == null)
            {
                this.Initialize();
            }

            var editor = this.htmlEditorControl;
            var parameters = DnnComponentParameters.Capture(editor);
            parameters[nameof(EditorControl.ValueChanged)] =
                EventCallback.Factory.Create<string>(editor, value => editor.Value = value);

            builder.OpenComponent<EditorControl>(0);
            builder.AddMultipleAttributes(1, parameters);
            builder.CloseComponent();
        }
#pragma warning restore BL0005
    }
}
