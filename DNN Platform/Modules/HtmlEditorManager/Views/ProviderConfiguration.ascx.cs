// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.HtmlEditorManager.Views
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Web.UI;
    using System.Web.UI.WebControls;
    using System.Xml;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Modules.HtmlEditorManager.ViewModels;
    using DotNetNuke.Services.ClientDependency;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>View control for selecting an HTML provider.</summary>
    /// <remarks>
    /// This control was originally implemented using the WebFormsMvp based <c>DotNetNuke.Web.Mvp</c>
    /// pattern (an <c>IProviderConfigurationView</c> plus a <c>ProviderConfigurationPresenter</c>).
    /// That framework was removed, so the presenter logic has been folded directly into this
    /// <see cref="PortalModuleBase"/> code-behind while preserving the original behavior.
    /// </remarks>
    public partial class ProviderConfiguration : PortalModuleBase
    {
        /// <summary>The HTML editor node.</summary>
        private const string HtmlEditorNode = "/configuration/dotnetnuke/htmlEditor";

        /// <summary>The dot net nuke document.</summary>
        private XmlDocument dotnetNukeDocument;

        /// <summary>Gets the view model used by the data-bound markup.</summary>
        public ProviderConfigurationViewModel Model { get; } = new ProviderConfigurationViewModel();

        /// <summary>Gets or sets the editor panel.</summary>
        /// <value>The editor panel.</value>
        public PlaceHolder Editor
        {
            get
            {
                return this.EditorPanel;
            }

            set
            {
                this.EditorPanel = value;
            }
        }

        /// <summary>Gets the DNN configuration.</summary>
        /// <value>The DNN configuration.</value>
        protected XmlDocument DNNConfiguration
        {
            get
            {
                if (this.dotnetNukeDocument == null)
                {
                    UserInfo currentUser = UserController.Instance.GetCurrentUserInfo();
                    if (currentUser != null && currentUser.IsSuperUser)
                    {
                        this.dotnetNukeDocument = Config.Load(this.AppStatus);
                    }
                }

                return this.dotnetNukeDocument;
            }
        }

        /// <summary>Gets the client resource controller.</summary>
        private IClientResourceController ClientResourceController => this.DependencyProvider.GetRequiredService<IClientResourceController>();

        /// <summary>Gets the application status info.</summary>
        private IApplicationStatusInfo AppStatus => this.DependencyProvider.GetRequiredService<IApplicationStatusInfo>();

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            var currentUser = UserController.Instance.GetCurrentUserInfo();
            if (currentUser == null || !currentUser.IsSuperUser)
            {
                this.LocalResourceFile = "/DesktopModules/Admin/HtmlEditorManager/App_LocalResources/ProviderConfiguration.ascx.resx";
                Globals.Redirect(Globals.AccessDeniedURL(this.LocalizeText("CannotManageHTMLEditorProviders")), true);
            }

            base.OnInit(e);

            this.ProvidersDropDownList.SelectedIndexChanged += this.ProvidersDropDownList_SelectedIndexChanged;
            this.SaveButton.Click += this.SaveButton_Click;

            this.Initialize();
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            // Replicates the WebFormsMvp AutoDataBind behavior which data-bound the view before rendering.
            this.DataBind();
        }

        /// <summary>Handles the Click event of the SaveButton control.</summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void SaveButton_Click(object sender, EventArgs e)
        {
            this.SaveEditorChoice(this.ProvidersDropDownList.SelectedValue);
        }

        /// <summary>Handles the SelectedIndexChanged event of the ProvidersDropDownList control.</summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void ProvidersDropDownList_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.EditorChanged(this.ProvidersDropDownList.SelectedValue);
        }

        /// <summary>Redirects to the current URL, reloading the page.</summary>
        private void Refresh()
        {
            this.Response.Redirect(this.Request.RawUrl, true);
        }

        /// <summary>Initializes the view model and loads the currently configured editor.</summary>
        private void Initialize()
        {
            this.Model.Editors = this.GetAvailableEditors();
            this.Model.SelectedEditor = this.GetSelectedEditor();
            this.Editor.Controls.Add(this.LoadCurrentEditor());

            // hack: force DNN to load the Telerik Combobox skin. Needed for the RadEditor provider
            // TODO: Remove as the included file path isn't distributed with installs
            this.ClientResourceController.RegisterStylesheet("~/Portals/_default/Skins/_default/WebControlSkin/Default/ComboBox.Default.css");
        }

        /// <summary>Handles the editor being changed in the dropdown.</summary>
        /// <param name="editor">The newly selected editor name.</param>
        private void EditorChanged(string editor)
        {
            this.Editor.Controls.Clear();
            this.Model.SelectedEditor = editor;
            this.Model.CanSave = true;
        }

        /// <summary>Saves the editor choice to the configuration and refreshes the view.</summary>
        /// <param name="editor">The editor name to save.</param>
        private void SaveEditorChoice(string editor)
        {
            this.SaveEditorInConfiguration(editor);
            this.Refresh();
            this.Editor.Controls.Clear();
            this.Editor.Controls.Add(this.LoadCurrentEditor());
            this.Model.SelectedEditor = editor;
        }

        /// <summary>Loads the current editor.</summary>
        /// <param name="editorName">Name of the editor.</param>
        /// <returns>The editor based on the current editor settings in the web configuration.</returns>
        private Control LoadCurrentEditor(string editorName)
        {
            XmlDocument dnnConfiguration = this.DNNConfiguration;
            XmlNode htmlProviderNode = this.GetHtmlEditorProviderNode(dnnConfiguration);
            XmlNode currentProvider = htmlProviderNode.SelectSingleNode("providers/add[@name='" + editorName + "']");

            var settingsAttribute = currentProvider.Attributes["settingsControlPath"];
            if (settingsAttribute != null)
            {
                try
                {
                    var controlPath = settingsAttribute.Value;
                    var control = (PortalModuleBase)this.LoadControl(controlPath);
                    control.ModuleConfiguration = this.ModuleContext.Configuration;
                    control.ID = Path.GetFileNameWithoutExtension(controlPath);
                    this.Model.CanSave = true;
                    return control;
                }
                catch (Exception)
                {
                    this.Model.CanSave = false;
                }
            }

            // Display a nice message to the user that this provider is not supported.
            return this.LoadControl("~/DesktopModules/Admin/HtmlEditorManager/Controls/InvalidConfiguration.ascx");
        }

        /// <summary>Loads the current editor.</summary>
        /// <returns>The editor based on the current editor settings in the web configuration.</returns>
        private Control LoadCurrentEditor()
        {
            return this.LoadCurrentEditor(this.GetSelectedEditor());
        }

        /// <summary>Saves the editor in configuration.</summary>
        /// <param name="name">The name.</param>
        private void SaveEditorInConfiguration(string name)
        {
            XmlDocument dnnConfiguration = this.DNNConfiguration;
            XmlNode providerNode = this.GetHtmlEditorProviderNode(dnnConfiguration);

            if (providerNode != null)
            {
                providerNode.Attributes["defaultProvider"].Value = name;
                Config.Save(this.AppStatus, dnnConfiguration);
            }
        }

        /// <summary>Gets the HTML provider node.</summary>
        /// <param name="dnnConfiguration">The DNN configuration.</param>
        /// <returns>The XmlNode for the htmlEditor provider.</returns>
        private XmlNode GetHtmlEditorProviderNode(XmlDocument dnnConfiguration)
        {
            if (dnnConfiguration != null && dnnConfiguration.DocumentElement != null)
            {
                return dnnConfiguration.DocumentElement.SelectSingleNode(HtmlEditorNode);
            }

            return null;
        }

        /// <summary>Gets the provider list.</summary>
        /// <returns>A list of the installed providers.</returns>
        private List<string> GetAvailableEditors()
        {
            var editors = new List<string>();
            XmlDocument xmlConfig = this.DNNConfiguration;
            if (xmlConfig != null && xmlConfig.DocumentElement != null)
            {
                XmlNodeList editorNodes = xmlConfig.DocumentElement.SelectNodes(HtmlEditorNode + "/providers/add");
                if (editorNodes != null)
                {
                    int i = 0;
                    while (i < editorNodes.Count)
                    {
                        XmlNode node = editorNodes[i];
                        if (node.Attributes["name"] != null)
                        {
                            editors.Add(node.Attributes["name"].Value);
                        }

                        i = i + 1;
                    }
                }
            }

            return editors;
        }

        /// <summary>Gets the selected editor.</summary>
        /// <returns>The currently configured editor.</returns>
        private string GetSelectedEditor()
        {
            XmlDocument xmlConfig = this.DNNConfiguration;

            if (xmlConfig != null && xmlConfig.DocumentElement != null)
            {
                XmlNode editorProviderNode = xmlConfig.DocumentElement.SelectSingleNode(HtmlEditorNode);
                return editorProviderNode.Attributes["defaultProvider"].Value;
            }

            return string.Empty;
        }
    }
}
