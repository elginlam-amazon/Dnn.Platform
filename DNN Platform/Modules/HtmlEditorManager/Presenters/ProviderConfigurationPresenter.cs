// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.HtmlEditorManager.Presenters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Modules.HtmlEditorManager.ViewModels;
    using DotNetNuke.Modules.HtmlEditorManager.Views;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;
    using DotNetNuke.UI.Utilities;
    using DotNetNuke.Web.Client.ClientResourceManagement;
    using DotNetNuke.Web.Mvp;
    using DotNetNuke.Web.UI;
    using DotNetNuke.Web.UI.WebControls;

    /// <summary>Presenter for Provider Configuration.</summary>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public partial class ProviderConfigurationPresenter : ModulePresenter<IProviderConfigurationView, ProviderConfigurationViewModel>
    {
        /// <summary>The HTML editor node.</summary>
        private const string HtmlEditorNode = "/configuration/dotnetnuke/htmlEditor";

        /// <summary>The dot net nuke document.</summary>
        private XmlDocument dotnetNukeDocument;

        /// <summary>Initializes a new instance of the <see cref="ProviderConfigurationPresenter" /> class.</summary>
        /// <param name="view">the interface provider view.</param>
        public ProviderConfigurationPresenter(IProviderConfigurationView view)
            : base(view)
        {
            this.View.Initialize += this.View_Initialize;
            this.View.SaveEditorChoice += this.View_SaveEditorChoice;
            this.View.EditorChanged += this.View_EditorChanged;
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
                        this.dotnetNukeDocument = Config.Load();
                    }
                }

                return this.dotnetNukeDocument;
            }
        }

        /// <summary>
        /// Builds the local resource file the loaded provider settings control reads, from the
        /// virtual path it was loaded from.
        /// </summary>
        /// <param name="controlPath">The <c>settingsControlPath</c> the control was loaded from.</param>
        /// <returns>The extension-less resource file root, e.g. <c>~/Providers/X/App_LocalResources/Y</c>.</returns>
        /// <remarks>
        /// Reproduces what <c>PortalModuleBase.LocalResourceFile</c> computed in WebForms from the
        /// loaded control's own <c>TemplateSourceDirectory</c> and the <c>ID</c> the presenter
        /// assigned: the directory of <paramref name="controlPath"/>, the
        /// <c>App_LocalResources</c> folder and the control's file name without its extension.
        /// </remarks>
        private static string GetLocalResourceFile(string controlPath)
        {
            var normalized = controlPath.Replace('\\', '/');
            var lastSlash = normalized.LastIndexOf('/');
            var directory = lastSlash > 0 ? normalized.Substring(0, lastSlash) : string.Empty;
            var name = normalized.Substring(lastSlash + 1);
            var dot = name.LastIndexOf('.');
            if (dot > 0)
            {
                name = name.Substring(0, dot);
            }

            return $"{directory}/{Localization.LocalResourceDirectory}/{name}";
        }

        /// <summary>Handles the Initialize event of the View control.</summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void View_Initialize(object sender, EventArgs e)
        {
            this.View.Model.Editors = this.GetAvailableEditors();
            this.View.Model.SelectedEditor = this.GetSelectedEditor();
            this.View.Editor.Controls.Add(this.LoadCurrentEditor());

            // hack: force DNN to load the Telerik Combobox skin. Needed for the RadEditor provider
            // TODO: Remove as the included file path isn't distributed with installs
            // ClientResourceManager.RegisterStyleSheet no longer takes a Page (there are no
            // WebForms pages on net10.0); HttpContextSource.Current is the DNN accessor for the
            // request the view is rendered for. The null check matters: View_Initialize also runs
            // when the component initializes on an interactive circuit, where there is no
            // HttpContext, and the registration is only meaningful on the render that emits the
            // page's stylesheets anyway.
            var context = HttpContextSource.Current;
            if (context != null)
            {
                ClientResourceManager.RegisterStyleSheet(context, "~/portals/_default/Skins/_default/WebControlSkin/Default/ComboBox.Default.css");
            }
        }

        /// <summary>Loads the current editor.</summary>
        /// <param name="editorName">Name of the editor.</param>
        /// <returns>The editor based on the current editor settings in the web configuration.</returns>
        /// <remarks>
        /// <c>((TemplateControl)this.View).LoadControl(path)</c> compiled the provider's
        /// <c>.ascx</c> at runtime. ASP.NET Core has no runtime markup compiler, so
        /// <see cref="ControlUtilities.LoadControl{T}"/> resolves the stored
        /// <c>settingsControlPath</c> (still the legacy <c>~/...ascx</c> value in
        /// <c>release.config</c>/<c>development.config</c>, which the resolver accepts by design)
        /// to the Blazor component that replaced that file and activates it from the request's
        /// service provider. The loaded control is wrapped in an <see cref="EditorControlHost"/>
        /// because a <see cref="PortalModuleBase"/> is not part of the DNN component tree and so
        /// cannot be added to the editor panel's child collection directly.
        /// </remarks>
        private IClientApiControl LoadCurrentEditor(string editorName)
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
                    var control = ControlUtilities.LoadControl<PortalModuleBase>(this.View.Editor, controlPath);
                    control.ModuleConfiguration = this.ModuleContext.Configuration;

                    // control.ID = Path.GetFileNameWithoutExtension(controlPath) existed for one
                    // reason: PortalModuleBase.LocalResourceFile was built from ControlPath (the
                    // loaded .ascx's own directory) plus the ID, so the provider's settings control
                    // read its OWN App_LocalResources file. On net10.0 PortalModuleBase has no ID
                    // and derives ControlPath from this module's configuration instead of from the
                    // loaded control's path, which would point a third party provider's
                    // LocalizeString calls at HtmlEditorManager's resource folder. Assigning
                    // LocalResourceFile explicitly from controlPath reproduces the original value
                    // exactly ("<provider dir>/App_LocalResources/<control name>", extension-less,
                    // as Localization expects).
                    control.LocalResourceFile = GetLocalResourceFile(controlPath);
                    this.View.Model.CanSave = true;
                    return this.HostControl(control);
                }
                catch (Exception)
                {
                    this.View.Model.CanSave = false;
                }
            }

            // Display a nice message to the user that this provider is not supported.
            return this.HostControl(ControlUtilities.LoadControl<PortalModuleBase>(
                this.View.Editor,
                "~/DesktopModules/Admin/HtmlEditorManager/Controls/InvalidConfiguration.razor"));
        }

        /// <summary>Loads the current editor.</summary>
        /// <returns>The editor based on the current editor settings in the web configuration.</returns>
        private IClientApiControl LoadCurrentEditor()
        {
            return this.LoadCurrentEditor(this.GetSelectedEditor());
        }

        /// <summary>Wraps a loaded provider settings control so the editor panel can carry it.</summary>
        /// <param name="control">The loaded control.</param>
        /// <returns>The holder rendering the control.</returns>
        private IClientApiControl HostControl(PortalModuleBase control)
        {
            return new EditorControlHost { HostedComponent = control };
        }

        /// <summary>Handles the Editor was changed event of the View control.</summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EditorEventArgs"/> instance containing the event data.</param>
        private void View_EditorChanged(object sender, EditorEventArgs e)
        {
            this.View.Editor.Controls.Clear();
            this.View.Model.SelectedEditor = e.Editor;
            this.View.Model.CanSave = true;
        }

        /// <summary>Handles the SaveEditorChoice event of the View control.</summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EditorEventArgs"/> instance containing the event data.</param>
        private void View_SaveEditorChoice(object sender, EditorEventArgs e)
        {
            this.SaveEditorInConfiguration(e.Editor);
            this.View.Refresh();
            this.View.Editor.Controls.Clear();
            this.View.Editor.Controls.Add(this.LoadCurrentEditor());
            this.View.Model.SelectedEditor = e.Editor;
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
                Config.Save(dnnConfiguration);
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
