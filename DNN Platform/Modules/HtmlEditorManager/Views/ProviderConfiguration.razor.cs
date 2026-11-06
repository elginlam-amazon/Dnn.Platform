// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.HtmlEditorManager.Views
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Modules.HtmlEditorManager.Presenters;
    using DotNetNuke.Modules.HtmlEditorManager.ViewModels;
    using DotNetNuke.UI;
    using DotNetNuke.Web.Mvp;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>View control for selecting an HTML provider.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>ProviderConfiguration.ascx</c> + <c>.ascx.cs</c> + <c>.ascx.designer.cs</c>; the
    /// markup is now <c>ProviderConfiguration.razor</c>. It is still a
    /// <see cref="ModuleView{TModel}"/> implementing <see cref="IProviderConfigurationView"/>, so
    /// <see cref="ProviderConfigurationPresenter"/> and its event wiring are unchanged.
    /// </para>
    /// <para>
    /// The three designer-generated server controls became this component's own state:
    /// <c>ProvidersDropDownList</c> is <see cref="Editors"/>/<see cref="SelectedEditorValue"/>,
    /// <c>SaveButton</c> is the button bound to <c>Model.CanSave</c> and
    /// <see cref="SaveButtonClick"/>, and <c>EditorPanel</c> is the <see cref="DnnPlaceHolder"/>
    /// behind <see cref="Editor"/>, whose children <see cref="EditorPanelContent"/> renders.
    /// </para>
    /// <para>
    /// The <see cref="PresenterBindingAttribute"/> is required on net10.0:
    /// <c>MvpPresenterBinder</c> reads the declared bindings instead of applying WebFormsMvp's
    /// naming convention, and a view with no declared presenter throws
    /// <c>MvpPresenterBindingException</c>. It declares the same presenter the convention resolved.
    /// </para>
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    [PresenterBinding(typeof(ProviderConfigurationPresenter))]
    public partial class ProviderConfiguration : ModuleView<ProviderConfigurationViewModel>, IProviderConfigurationView
    {
        /// <summary>
        /// Replaces the <c>EditorPanel</c> <c>asp:PlaceHolder</c>. It is created eagerly, because
        /// the presenter adds the loaded provider settings control to it while the view
        /// initializes, which is before the first render.
        /// </summary>
        private DnnPlaceHolder editorPanel = new DnnPlaceHolder { ID = "EditorPanel" };

        private string selectedEditorValue;

        /// <summary>Occurs when the save button is clicked.</summary>
        public event EventHandler<EditorEventArgs> SaveEditorChoice;

        /// <summary>Occurs when the editor is changed.</summary>
        public event EventHandler<EditorEventArgs> EditorChanged;

        /// <summary>Gets or sets the editor panel.</summary>
        /// <value>The editor panel.</value>
        public DnnPlaceHolder Editor
        {
            get
            {
                return this.editorPanel;
            }

            set
            {
                this.editorPanel = value;
            }
        }

        /// <summary>Gets the editors offered by the drop down list.</summary>
        /// <remarks>
        /// Replaces <c>ProvidersDropDownList.DataSource = this.Model.Editors</c>. A null model list
        /// produced an empty list in WebForms as well.
        /// </remarks>
        protected IEnumerable<string> Editors => this.Model.Editors ?? Enumerable.Empty<string>();

        /// <summary>Gets the selected value of the drop down list.</summary>
        /// <remarks>Replaces <c>ProvidersDropDownList.SelectedValue</c>.</remarks>
        protected string SelectedEditorValue => this.selectedEditorValue ?? this.Model.SelectedEditor;

        /// <summary>Gets the content of the editor panel.</summary>
        /// <remarks>
        /// Renders the children the presenter added to <see cref="Editor"/>, which is what the
        /// <c>asp:PlaceHolder</c> did. <see cref="DnnComponentBase.RenderControl"/> is the
        /// platform's child renderer, so a child keeps the parameters the presenter configured on it
        /// (see <see cref="EditorControlHost"/>).
        /// </remarks>
        protected RenderFragment EditorPanelContent => builder =>
        {
            var sequence = 0;
            foreach (var child in this.editorPanel.Controls.ToList())
            {
                RenderControl(builder, sequence++, child);
            }
        };

        /// <inheritdoc />
        public void Refresh()
        {
            this.Response.Redirect(this.Request.GetRawUrl(), true);
        }

        /// <summary>Handles the Click event of the SaveButton control.</summary>
        protected void SaveButtonClick()
        {
            this.SaveEditorChoice(this, new EditorEventArgs(this.SelectedEditorValue));
        }

        /// <summary>Handles the SelectedIndexChanged event of the ProvidersDropDownList control.</summary>
        /// <param name="args">The arguments of the change event, carrying the newly selected value.</param>
        protected void ProvidersDropDownListSelectedIndexChanged(ChangeEventArgs args)
        {
            this.selectedEditorValue = args?.Value?.ToString();
            this.EditorChanged(this, new EditorEventArgs(this.SelectedEditorValue));
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            var currentUser = UserController.Instance.GetCurrentUserInfo();
            if (currentUser == null || !currentUser.IsSuperUser)
            {
                this.LocalResourceFile = "/DesktopModules/Admin/HtmlEditorManager/App_LocalResources/ProviderConfiguration.ascx.resx";
                Globals.Redirect(Globals.AccessDeniedURL(this.LocalizeString("CannotManageHTMLEditorProviders")), true);
            }

            base.OnInit(e);
        }
    }
}
