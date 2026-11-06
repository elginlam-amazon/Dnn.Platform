// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    /// <summary>A web control for editing a form.</summary>
    /// <remarks>
    /// This was a <c>WebControl, INamingContainer</c> with <c>[ParseChildren]</c>. On ASP.NET Core
    /// it is a <see cref="DnnComponentBase"/>: the tab/section/item hierarchy is still built
    /// imperatively into <see cref="DnnComponentBase.Controls"/> by
    /// <see cref="CreateControlHierarchy"/>, the item count that used to live in control state is
    /// now a field of the component, and the inline jQuery bootstrap became a registered resource
    /// (<c>dnn.FormEditor.js</c>) invoked through <see cref="IJSRuntime"/>.
    /// </remarks>
    public partial class DnnFormEditor : DnnComponentBase
    {
        private readonly IHostSettings hostSettings;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;
        private readonly IClientResourceController clientResourceController;
        private object dataSource;
        private int itemCount;
        private bool clientInitialized;

        /// <summary>Initializes a new instance of the <see cref="DnnFormEditor"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public DnnFormEditor()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnFormEditor"/> class.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        public DnnFormEditor(IHostSettings hostSettings, IApplicationStatusInfo appStatus, IEventLogger eventLogger)
        {
            var services = Globals.GetCurrentServiceProvider();
            this.hostSettings = hostSettings ?? services.GetRequiredService<IHostSettings>();
            this.appStatus = appStatus ?? services.GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? services.GetRequiredService<IEventLogger>();
            this.clientResourceController = services.GetRequiredService<IClientResourceController>();

            this.Items = new List<DnnFormItemBase>();
            this.Sections = new List<DnnFormSection>();
            this.Tabs = new List<DnnFormTab>();

            this.FormMode = DnnFormMode.Long;
        }

        /// <summary>Gets a value indicating whether the editor is valid.</summary>
        public bool IsValid
        {
            get
            {
                bool isValid = true;
                foreach (var item in this.GetAllItems())
                {
                    item.CheckIsValid();
                    if (!item.IsValid)
                    {
                        isValid = false;
                        break;
                    }
                }

                return isValid;
            }
        }

        /// <summary>Gets or sets the data source.</summary>
        public object DataSource
        {
            get
            {
                return this.dataSource;
            }

            set
            {
                if (this.dataSource != value)
                {
                    this.dataSource = value;

                    // Under Web Forms the items were re-bound from the (already created) control
                    // tree on a postback. The Blazor equivalent is: whenever the tree already
                    // exists, push the new data source into the items without rebuilding them.
                    if (this.itemCount > 0)
                    {
                        this.DataBindItems(false);
                    }
                }
            }
        }

        /// <summary>Gets or sets the form mode.</summary>
        [Parameter]
        public DnnFormMode FormMode { get; set; }

        /// <summary>Gets or sets a value indicating whether to encrypt IDs.</summary>
        [Parameter]
        public bool EncryptIds { get; set; }

        /// <summary>Gets the items.</summary>
        public List<DnnFormItemBase> Items { get; private set; }

        /// <summary>Gets the sections.</summary>
        public List<DnnFormSection> Sections { get; private set; }

        /// <summary>Gets the tabs.</summary>
        public List<DnnFormTab> Tabs { get; private set; }

        /// <summary>Gets or sets the JavaScript runtime used to initialize the client behaviour.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>Gets the local resource file path.</summary>
        protected string LocalResourceFile
        {
            get
            {
                return Utilities.GetLocalResourceFile(this);
            }
        }

        /// <inheritdoc />
        protected override string TagName => "div";

        /// <summary>Binds this editor to its <see cref="DataSource"/>, rebuilding its content.</summary>
        /// <remarks>Replaces <c>Control.DataBind()</c>.</remarks>
        public virtual void DataBind()
        {
            this.Controls.Clear();
            this.CreateControlHierarchy(true);
            this.NotifyTreeChanged();
        }

        /// <summary>Sets the items up.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="items">The items.</param>
        /// <param name="parentControl">The parent control.</param>
        /// <param name="encryptIds">Whether to encrypt the IDs.</param>
        internal static void SetUpItems(IHostSettings hostSettings, IEnumerable<DnnFormItemBase> items, DnnComponentBase parentControl, bool encryptIds)
        {
            foreach (DnnFormItemBase item in items)
            {
                if (encryptIds)
                {
                    item.ID = (hostSettings.Guid.Substring(0, 7) + item.ID + DateTime.Now.Day).GenerateHash();
                }

                parentControl.Controls.Add(item);
            }
        }

        /// <inheritdoc />
        protected override void CreateChildControls()
        {
            // CreateChildControls re-creates the children (the items).
            // First clear any existing child controls.
            this.Controls.Clear();

            this.CreateControlHierarchy(false);
        }

        /// <summary>Creates the control hierarchy.</summary>
        /// <param name="useDataSource">Whether to use the data source.</param>
        protected virtual void CreateControlHierarchy(bool useDataSource)
        {
            this.CssClass = string.IsNullOrEmpty(this.CssClass)
                ? "dnnForm"
                : this.CssClass.Contains("dnnForm")
                    ? this.CssClass
                    : $"dnnForm {this.CssClass}";

            this.SetUpTabs();

            this.SetUpSections(this.Sections, this);

            SetUpItems(this.hostSettings, this.Items, this, this.EncryptIds);

            this.DataBindItems(useDataSource);
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, PortalSettings.Current, CommonJs.DnnPlugins);
            base.OnInit(e);
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            if (this.Tabs.Count > 0)
            {
                this.clientResourceController.RegisterScript("~/Resources/Shared/scripts/dnn.ControlsInterop.js");
            }
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (this.clientInitialized || this.Tabs.Count == 0 || this.JsRuntime == null)
            {
                return;
            }

            this.clientInitialized = true;

            var expandAllOptions = new List<DnnFormExpandAllOptions>();
            foreach (var formTab in this.Tabs)
            {
                if (formTab.IncludeExpandAll && formTab.ExpandAll != null)
                {
                    expandAllOptions.Add(formTab.ExpandAll);
                }
            }

            await this.JsRuntime.InvokeVoidAsync("dnn.setupFormEditor", "#" + this.ClientId, expandAllOptions);
        }

        private List<DnnFormItemBase> GetAllItems()
        {
            var items = new List<DnnFormItemBase>();

            // iterate over pages
            foreach (DnnFormTab page in this.Tabs)
            {
                foreach (DnnFormSection section in page.Sections)
                {
                    items.AddRange(section.Items);
                }

                items.AddRange(page.Items);
            }

            // iterate over section
            foreach (DnnFormSection section in this.Sections)
            {
                items.AddRange(section.Items);
            }

            // Add base items
            items.AddRange(this.Items);

            return items;
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        private void SetUpSections(List<DnnFormSection> sections, DnnComponentBase parentControl)
        {
            if (sections.Count > 0)
            {
                foreach (DnnFormSection section in sections)
                {
                    var panel = new DnnFormPanel { CssClass = "dnnFormSectionHead" };
                    parentControl.Controls.Add(panel);

                    var resourceKey = section.ResourceKey;
                    if (string.IsNullOrEmpty(resourceKey))
                    {
                        resourceKey = section.ID;
                    }

                    panel.Text = Localization.GetString(resourceKey, this.LocalResourceFile);
                    panel.Expanded = section.Expanded;

                    SetUpItems(this.hostSettings, this.Items, panel, this.EncryptIds);
                }
            }
        }
        #pragma warning restore BL0005

        private void SetUpTabs()
        {
            if (this.Tabs.Count > 0)
            {
                var tabStrip = new DnnFormTabStrip { CssClass = "dnnAdminTabNav dnnClear" };
                this.Controls.Add(tabStrip);
                tabStrip.Items.Clear();

                foreach (DnnFormTab formTab in this.Tabs)
                {
                    var resourceKey = formTab.ResourceKey;
                    if (string.IsNullOrEmpty(resourceKey))
                    {
                        resourceKey = formTab.ID;
                    }

                    var tab = new DnnPanel { CssClass = formTab.ID + " dnnClear", ID = "tab_" + formTab.ID };
                    this.Controls.Add(tab);

                    if (formTab.IncludeExpandAll)
                    {
                        var expandAll = new DnnPanel { CssClass = "dnnFormExpandContent" };
                        string expandAllText = Localization.GetString("ExpandAll", Localization.SharedResourceFile);
                        expandAll.Controls.Add(new DnnLiteralControl("<a href=\"\">" + expandAllText + "</a>"));
                        tab.Controls.Add(expandAll);

                        formTab.ExpandAll = new DnnFormExpandAllOptions
                        {
                            Selector = "#" + tab.ClientId + " .dnnFormExpandContent a",
                            ExpandText = Localization.GetString("ExpandAll", Localization.SharedResourceFile),
                            CollapseText = Localization.GetString("CollapseAll", Localization.SharedResourceFile),
                            TargetArea = "#" + tab.ClientId,
                        };
                    }

                    tabStrip.Items.Add(new DnnListItem(Localization.GetString(resourceKey, this.LocalResourceFile), "#" + tab.ClientId));

                    if (formTab.Sections.Count > 0)
                    {
                        this.SetUpSections(formTab.Sections, tab);
                    }
                    else
                    {
                        tab.CssClass += " dnnFormNoSections";
                    }

                    SetUpItems(this.hostSettings, formTab.Items, tab, this.EncryptIds);
                }
            }
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        private void DataBindItems(bool useDataSource)
        {
            var items = this.GetAllItems();

            foreach (DnnFormItemBase item in items)
            {
                if (string.IsNullOrEmpty(item.LocalResourceFile))
                {
                    item.LocalResourceFile = this.LocalResourceFile;
                }

                if (item.FormMode == DnnFormMode.Inherit)
                {
                    item.FormMode = this.FormMode;
                }

                if (this.DataSource != null)
                {
                    item.DataSource = this.DataSource;
                    item.DataBindItem(useDataSource);
                }
            }

            this.itemCount = this.GetAllItems().Count;
        }
        #pragma warning restore BL0005
    }
}
