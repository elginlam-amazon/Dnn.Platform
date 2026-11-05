// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Skins
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Personalization;
    using DotNetNuke.UI.Containers;
    using DotNetNuke.UI.Utilities;

    using Microsoft.AspNetCore.Http;

    using Globals = DotNetNuke.Common.Globals;

    /// <summary>The Pane class represents a Pane within the Skin.</summary>
    /// <remarks>
    /// A pane used to be an HTML element with <c>runat="server"</c> inside the skin's <c>.ascx</c>
    /// file; it is now a DNN skinning component (<see cref="IDnnControl"/>) declared by the skin
    /// component, so the pane's markup element, its attributes and its children are all addressable
    /// exactly as before.
    /// </remarks>
    public class Pane : IDisposable
    {
        private const string CPaneOutline = "paneOutline";
        private IDnnControl containerWrapperControl;
        private Dictionary<string, Containers.Container> containers;

        /// <summary>
        /// Initializes a new instance of the <see cref="Pane"/> class.
        /// Constructs a new Pane object from the Control in the Skin.
        /// </summary>
        /// <param name="pane">The pane control in the Skin.</param>
        public Pane(IDnnControl pane)
        {
            this.PaneControl = pane;

            // Was: PaneControl.ViewStateMode = ViewStateMode.Disabled. There is no view state on
            // ASP.NET Core, so there is nothing to disable.
            this.Name = pane.Id;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pane"/> class.
        /// Constructs a new Pane object from the Control in the Skin.
        /// </summary>
        /// <param name="name">The name (ID) of the pane control.</param>
        /// <param name="pane">The pane control in the Skin.</param>
        public Pane(string name, IDnnControl pane)
        {
            this.PaneControl = pane;
            this.Name = name;
        }

        /// <summary>Gets a Dictionary of Containers.</summary>
        protected Dictionary<string, Containers.Container> Containers => this.containers ??= new Dictionary<string, Containers.Container>();

        /// <summary>Gets the PortalSettings of the Portal.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        protected PortalSettings PortalSettings => PortalSettings.Current;

        /// <summary>Gets or sets the name (ID) of the Pane.</summary>
        protected string Name { get; set; }

        /// <summary>Gets or sets the pane control.</summary>
        protected IDnnControl PaneControl { get; set; }

        /// <summary>InjectModule injects a Module (and its container) into the Pane.</summary>
        /// <param name="module">The Module.</param>
        public void InjectModule(ModuleInfo module)
        {
            this.containerWrapperControl = new DnnGenericControl("div");
            this.PaneControl.Controls.Add(this.containerWrapperControl);

            // inject module classes
            string classFormatString = "DnnModule DnnModule-{0} DnnModule-{1}";
            string sanitizedModuleName = Null.NullString;

            if (!string.IsNullOrEmpty(module.DesktopModule.ModuleName))
            {
                sanitizedModuleName = Globals.CreateValidClass(module.DesktopModule.ModuleName, false);
            }

            if (IsVersionableModule(module))
            {
                classFormatString += " DnnVersionableControl";
            }

            this.containerWrapperControl.Attributes["class"] = string.Format(CultureInfo.InvariantCulture, classFormatString, sanitizedModuleName, module.ModuleID);

            try
            {
                if (!Globals.IsAdminControl() && (this.PortalSettings.InjectModuleHyperLink || Personalization.GetUserMode() != PortalSettings.Mode.View))
                {
                    this.containerWrapperControl.Controls.Add(new DnnLiteralControl($"<a name=\"{module.ModuleID}\"></a>"));
                }

                // Load container control
                Containers.Container container = this.LoadModuleContainer(module);

                // Add Container to Dictionary
                this.Containers.Add(container.ID, container);

                // hide anything of type ActionsMenu - as we're injecting our own menu now.
                container.InjectActionMenu = !container.Controls.OfType<ActionBase>().Any();
                if (!container.InjectActionMenu)
                {
                    foreach (var actionControl in container.Controls.OfType<IActionControl>())
                    {
                        if (actionControl is ActionsMenu and IDnnControl control)
                        {
                            control.Visible = false;
                            container.InjectActionMenu = true;
                        }
                    }
                }

                if (Globals.IsLayoutMode() && !Globals.IsAdminControl())
                {
                    // provide Drag-N-Drop capabilities
                    var dragDropContainer = new DnnPanel();
                    IClientApiControl title = container.FindControl("dnnTitle");

                    // Assume that the title control is named dnnTitle.  If this becomes an issue we could loop through the controls looking for the title type of skin object
                    dragDropContainer.ID = container.ID + "_DD";
                    this.containerWrapperControl.Controls.Add(dragDropContainer);

                    // inject the container into the page pane - this triggers the initialization of the container component
                    dragDropContainer.Controls.Add(container);

                    if (title != null)
                    {
                        if (title.Children.Count > 0)
                        {
                            title = title.Children[0];
                        }
                    }

                    // enable drag and drop
                    if (title != null)
                    {
                        // The title ID is actually the first child so we need to make sure at least one child exists
                        DNNClientAPI.EnableContainerDragAndDrop(title, dragDropContainer, module.ModuleID);
                        ClientAPI.RegisterPostBackEventHandler(this.PaneControl, "MoveToPane", this.ModuleMoveToPanePostBack, false);
                    }
                }
                else
                {
                    this.containerWrapperControl.Controls.Add(container);
                    if (Globals.IsAdminControl())
                    {
                        this.containerWrapperControl.Attributes["class"] += " DnnModule-Admin";
                    }
                }

                // Attach Module to Container
                container.SetModuleConfiguration(module);

                // display collapsible page panes
                if (!this.PaneControl.Visible)
                {
                    this.PaneControl.Visible = true;
                }
            }
            catch (Exception exc)
            {
                var lex = new ModuleLoadException(string.Format(CultureInfo.InvariantCulture, Skin.MODULEADD_ERROR, this.PaneControl.Id), exc);
                if (TabPermissionController.CanAdminPage())
                {
                    // only display the error to administrators
                    this.containerWrapperControl.Controls.Add(new ErrorContainer(this.PortalSettings, Skin.MODULELOAD_ERROR, lex).Container);
                }

                Exceptions.LogException(exc);
                throw lex;
            }
        }

        /// <summary>ProcessPane processes the Attributes for the PaneControl.</summary>
        public void ProcessPane()
        {
            if (this.PaneControl != null)
            {
                // remove excess skin non-validating attributes
                this.PaneControl.Attributes.Remove("ContainerType");
                this.PaneControl.Attributes.Remove("ContainerName");
                this.PaneControl.Attributes.Remove("ContainerSrc");

                if (Globals.IsLayoutMode())
                {
                    this.PaneControl.Visible = true;

                    // display pane border
                    this.PaneControl.Attributes.TryGetValue("class", out var cssclass);
                    if (string.IsNullOrEmpty(cssclass))
                    {
                        this.PaneControl.Attributes["class"] = CPaneOutline;
                    }
                    else
                    {
                        this.PaneControl.Attributes["class"] = cssclass.Replace(CPaneOutline, string.Empty).Trim().Replace("  ", " ") + " " + CPaneOutline;
                    }

                    // display pane name
                    var ctlLabel = new DnnLabel { Text = "<center>" + this.Name + "</center><br />", CssClass = "SubHead" };
                    this.PaneControl.Controls.AddAt(0, ctlLabel);
                }
                else
                {
                    if (this.PaneControl.Visible == false && TabPermissionController.CanAddContentToPage())
                    {
                        this.PaneControl.Visible = true;
                    }

                    if (this.CanCollapsePane())
                    {
                        // This pane has no controls so set the width to 0
                        if (this.PaneControl.Attributes.ContainsKey("style"))
                        {
                            this.PaneControl.Attributes.Remove("style");
                        }

                        if (this.PaneControl.Attributes.TryGetValue("class", out var paneClass))
                        {
                            this.PaneControl.Attributes["class"] = paneClass + " DNNEmptyPane";
                        }
                        else
                        {
                            this.PaneControl.Attributes["class"] = "DNNEmptyPane";
                        }
                    }

                    // Add support for drag and drop
                    if (Globals.IsEditMode())
                    {
                        // this call also checks for permission
                        if (this.PaneControl.Attributes.TryGetValue("class", out var sortableClass))
                        {
                            this.PaneControl.Attributes["class"] = sortableClass + " dnnSortable";
                        }
                        else
                        {
                            this.PaneControl.Attributes["class"] = "dnnSortable";
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Releases the resources held by this pane.</summary>
        /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                (this.containerWrapperControl as IDisposable)?.Dispose();
                (this.PaneControl as IDisposable)?.Dispose();
            }
        }

        private static bool IsVersionableModule(ModuleInfo moduleInfo)
        {
            if (string.IsNullOrEmpty(moduleInfo.DesktopModule.BusinessControllerClass))
            {
                return false;
            }

            var controllerType = Framework.Reflection.CreateType(moduleInfo.DesktopModule.BusinessControllerClass);
            return typeof(IVersionable).IsAssignableFrom(controllerType);
        }

        private bool CanCollapsePane()
        {
            // This section sets the width to "0" on panes that have no modules.
            // This preserves the integrity of the HTML syntax so we don't have to set
            // the visiblity of a pane to false. Setting the visibility of a pane to
            // false where there are colspans and rowspans can render the skin incorrectly.
            bool canCollapsePane = true;
            if (this.Containers.Count > 0)
            {
                canCollapsePane = false;
            }
            else if (this.PaneControl.Controls.Count == 1)
            {
                // Pane contains 1 control
                canCollapsePane = false;
                var literal = this.PaneControl.Controls[0] as DnnLiteralControl;
                if (literal != null)
                {
                    // Check  if the literal control is just whitespace - if so we can collapse panes
                    if (string.IsNullOrEmpty(HtmlUtils.StripWhiteSpace(literal.Text, false)))
                    {
                        canCollapsePane = true;
                    }
                }
            }
            else if (this.PaneControl.Controls.Count > 1)
            {
                // Pane contains more than 1 control
                canCollapsePane = false;
            }

            return canCollapsePane;
        }

        /// <summary>LoadContainerByPath gets the Container from its Url(Path).</summary>
        /// <param name="containerPath">The Url to the Container control.</param>
        /// <returns>A Container.</returns>
        private Containers.Container LoadContainerByPath(string containerPath)
        {
            if (containerPath.IndexOf("/skins/", StringComparison.InvariantCultureIgnoreCase) != -1
                || containerPath.IndexOf(@"/skins\", StringComparison.InvariantCultureIgnoreCase) != -1
                || containerPath.IndexOf(@"\skins\", StringComparison.InvariantCultureIgnoreCase) != -1
                || containerPath.IndexOf(@"\skins/", StringComparison.InvariantCultureIgnoreCase) != -1)
            {
                throw new InvalidContainerPathException($"Invalid container path: {containerPath}");
            }

            Containers.Container container = null;

            try
            {
                string containerSrc = containerPath;
                if (containerPath.IndexOf(Globals.ApplicationPath, StringComparison.InvariantCultureIgnoreCase) != -1)
                {
                    containerPath = containerPath.Remove(0, Globals.ApplicationPath.Length);
                }

                container = ControlUtilities.LoadControl<Containers.Container>(this.PaneControl, containerPath);
                container.ContainerSrc = containerSrc;

                // Was: container.DataBind(), which executed any server logic declared in the .ascx.
                // A Blazor component's logic runs in its own lifecycle when it is rendered.
            }
            catch (Exception exc)
            {
                // could not load the container component
                var lex = new ModuleLoadException(Skin.MODULELOAD_ERROR, exc);
                if (TabPermissionController.CanAdminPage())
                {
                    // only display the error to administrators
                    this.containerWrapperControl.Controls.Add(new ErrorContainer(this.PortalSettings, string.Format(CultureInfo.InvariantCulture, Skin.CONTAINERLOAD_ERROR, containerPath), lex).Container);
                }

                Exceptions.LogException(lex);
            }

            return container;
        }

        /// <summary>LoadModuleContainer gets the Container for cookie.</summary>
        /// <param name="request">Current Http Request.</param>
        /// <returns>A Container.</returns>
        private Containers.Container LoadContainerFromCookie(HttpRequest request)
        {
            Containers.Container container = null;
            var cookieValue = request?.Cookies["_ContainerSrc" + this.PortalSettings.PortalId];
            if (!string.IsNullOrEmpty(cookieValue))
            {
                container = this.LoadContainerByPath(SkinController.FormatSkinSrc(cookieValue + ".ascx", this.PortalSettings));
            }

            return container;
        }

        private Containers.Container LoadContainerFromPane()
        {
            Containers.Container container = null;
            string containerSrc;
            var validSrc = false;

            this.PaneControl.Attributes.TryGetValue("ContainerType", out var paneContainerType);
            this.PaneControl.Attributes.TryGetValue("ContainerName", out var paneContainerName);
            this.PaneControl.Attributes.TryGetValue("ContainerSrc", out var paneContainerSrc);

            if (paneContainerType != null && paneContainerName != null)
            {
                containerSrc = "[" + paneContainerType + "]" + SkinController.RootContainer + "/" + paneContainerName + "/" +
                               paneContainerSrc;
                validSrc = true;
            }
            else
            {
                containerSrc = paneContainerSrc;
                if (containerSrc != null && containerSrc.Contains("/") && !(containerSrc.StartsWith("[g]", StringComparison.InvariantCultureIgnoreCase) || containerSrc.StartsWith("[l]", StringComparison.InvariantCultureIgnoreCase)))
                {
                    containerSrc = string.Format(CultureInfo.InvariantCulture, SkinController.IsGlobalSkin(this.PortalSettings.ActiveTab.SkinSrc) ? "[G]containers/{0}" : "[L]containers/{0}", containerSrc.TrimStart('/'));
                    validSrc = true;
                }
            }

            if (validSrc)
            {
                containerSrc = SkinController.FormatSkinSrc(containerSrc, this.PortalSettings);
                container = this.LoadContainerByPath(containerSrc);
            }

            return container;
        }

        private Containers.Container LoadContainerFromQueryString(ModuleInfo module, HttpRequest request)
        {
            Containers.Container container = null;
            int previewModuleId = -1;
            var queryString = request.GetQueryString();
            if (queryString["ModuleId"] != null)
            {
                if (!int.TryParse(queryString["ModuleId"], out previewModuleId))
                {
                    previewModuleId = -1;
                }
            }

            // load user container ( based on cookie )
            if (queryString["ContainerSrc"] != null && (module.ModuleID == previewModuleId || previewModuleId == -1))
            {
                string containerSrc = SkinController.FormatSkinSrc(Globals.QueryStringDecode(queryString["ContainerSrc"]) + ".ascx", this.PortalSettings);
                container = this.LoadContainerByPath(containerSrc);
            }

            return container;
        }

        private Containers.Container LoadNoContainer(ModuleInfo module)
        {
            string noContainerSrc = "[G]" + SkinController.RootContainer + "/_default/No Container.ascx";
            Containers.Container container = null;

            // if the module specifies that no container should be used
            if (module.DisplayTitle == false)
            {
                // always display container if the current user is the administrator or the module is being used in an admin case
                bool displayTitle = ModulePermissionController.CanEditModuleContent(module) || Globals.IsAdminSkin();

                // unless the administrator is in view mode
                if (displayTitle)
                {
                    displayTitle = Personalization.GetUserMode() != PortalSettings.Mode.View;
                }

                if (displayTitle == false)
                {
                    container = this.LoadContainerByPath(SkinController.FormatSkinSrc(noContainerSrc, this.PortalSettings));
                }
            }

            return container;
        }

        private Containers.Container LoadModuleContainer(ModuleInfo module)
        {
            var containerSrc = Null.NullString;
            var request = HttpContextSource.Current?.Request;
            Containers.Container container = null;

            if (this.PortalSettings.EnablePopUps && UrlUtils.InPopUp())
            {
                containerSrc = module.ContainerPath + "popUpContainer.ascx";

                // Check Skin for a popup Container
                if (module.ContainerSrc == this.PortalSettings.ActiveTab.ContainerSrc)
                {
                    if (File.Exists(HttpContextSource.MapPath(containerSrc)))
                    {
                        container = this.LoadContainerByPath(containerSrc);
                    }
                }

                // error loading container - load default popup container
                if (container == null)
                {
                    containerSrc = Globals.HostPath + "Containers/_default/popUpContainer.ascx";
                    container = this.LoadContainerByPath(containerSrc);
                }
            }
            else
            {
                container = (this.LoadContainerFromQueryString(module, request) ?? this.LoadContainerFromCookie(request)) ?? this.LoadNoContainer(module);
                if (container == null)
                {
                    // Check Skin for Container
                    var masterModules = this.PortalSettings.ActiveTab.ChildModules;
                    if (masterModules.TryGetValue(module.ModuleID, out var masterModule) && string.IsNullOrEmpty(masterModule.ContainerSrc))
                    {
                        // look for a container specification in the skin pane
                        if (this.PaneControl != null && this.PaneControl.Attributes.ContainsKey("ContainerSrc"))
                        {
                            container = this.LoadContainerFromPane();
                        }
                    }
                }

                // else load assigned container
                if (container == null)
                {
                    containerSrc = module.ContainerSrc;
                    if (!string.IsNullOrEmpty(containerSrc))
                    {
                        containerSrc = SkinController.FormatSkinSrc(containerSrc, this.PortalSettings);
                        container = this.LoadContainerByPath(containerSrc);
                    }
                }

                // error loading container - load from tab
                if (container == null)
                {
                    containerSrc = this.PortalSettings.ActiveTab.ContainerSrc;
                    if (!string.IsNullOrEmpty(containerSrc))
                    {
                        containerSrc = SkinController.FormatSkinSrc(containerSrc, this.PortalSettings);
                        container = this.LoadContainerByPath(containerSrc);
                    }
                }

                // error loading container - load default
                if (container == null)
                {
                    containerSrc = SkinController.FormatSkinSrc(SkinController.GetDefaultPortalContainer(), this.PortalSettings);
                    container = this.LoadContainerByPath(containerSrc);
                }
            }

            if (container == null)
            {
                // Every container source failed. Rendering nothing here would silently drop the
                // module from the page, so this is surfaced instead.
                throw new ModuleLoadException(
                    string.Format(CultureInfo.InvariantCulture, Skin.CONTAINERLOAD_ERROR, containerSrc));
            }

            // Set container path
            module.ContainerPath = SkinController.FormatSkinPath(containerSrc);

            // set container id to an explicit short name to reduce page payload
            container.ID = "ctr";

            // make the container id unique for the page
            if (module.ModuleID > -1)
            {
                container.ID += module.ModuleID.ToString(CultureInfo.InvariantCulture);
            }

            return container;
        }

        /// <summary>ModuleMoveToPanePostBack executes when a module is moved by Drag-and-Drop.</summary>
        /// <param name="args">A ClientAPIPostBackEventArgs object.</param>
        private void ModuleMoveToPanePostBack(ClientAPIPostBackEventArgs args)
        {
            var portalSettings = PortalSettings.Current;
            if (TabPermissionController.CanAdminPage())
            {
                var moduleId = Convert.ToInt32(args.EventArguments["moduleid"], CultureInfo.InvariantCulture);
                var paneName = Convert.ToString(args.EventArguments["pane"], CultureInfo.InvariantCulture);
                var moduleOrder = Convert.ToInt32(args.EventArguments["order"], CultureInfo.InvariantCulture);

                ModuleController.Instance.UpdateModuleOrder(portalSettings.ActiveTab.TabID, moduleId, moduleOrder, paneName);
                ModuleController.Instance.UpdateTabModuleOrder(portalSettings.ActiveTab.TabID);

                // Redirect to the same page to pick up changes
                var context = HttpContextSource.CurrentOrThrow;
                context.Response.Redirect(context.Request.GetRawUrl(), true);
            }
        }
    }
}
