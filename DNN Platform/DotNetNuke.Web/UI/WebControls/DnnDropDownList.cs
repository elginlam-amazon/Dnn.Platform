// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Framework;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;
    using DotNetNuke.UI.Utilities;
    using DotNetNuke.Web.UI.WebControls.Extensions;
    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    using Globals = DotNetNuke.Common.Globals;

    /// <summary>A dropdown list control.</summary>
    /// <remarks>
    /// This was a <c>Panel, INamingContainer</c> whose markup was hydrated by
    /// <c>dnn.DropDownList.js</c>. On ASP.NET Core it is a <see cref="DnnComponentBase"/> that
    /// renders the same DOM (a <c>div.dnnDropDownList</c> containing <c>div.selected-item &gt;
    /// a.selected-value</c> and the state hidden field) and calls the same
    /// <c>dnn.createDropDownList</c> client factory through <see cref="IJSRuntime"/> instead of
    /// <c>ScriptManager.RegisterStartupScript</c>. The client still talks to the
    /// <c>InternalServices/ItemListServiceController</c> endpoints named by <see cref="Services"/>.
    /// </remarks>
    public class DnnDropDownList : DnnComponentBase
    {
        private readonly Lazy<DnnDropDownListOptions> options = new Lazy<DnnDropDownListOptions>(() => new DnnDropDownListOptions());
        private readonly IClientResourceController clientResourceController;
        private readonly IServicesFramework servicesFramework;

        private DnnGenericHiddenField<DnnDropDownListState> stateControl;
        private DnnGenericControl selectedValue;
        private bool clientInitialized;

        /// <summary>Initializes a new instance of the <see cref="DnnDropDownList"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public DnnDropDownList()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnDropDownList"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        public DnnDropDownList(IClientResourceController clientResourceController, IServicesFramework servicesFramework)
        {
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
            this.servicesFramework = servicesFramework ?? Globals.GetCurrentServiceProvider().GetRequiredService<IServicesFramework>();
        }

        /// <summary>Occurs when the selection from the list control changes.</summary>
        public event EventHandler SelectionChanged;

        /// <summary>
        /// Gets when this method returns, contains the 32-bit signed integer value equivalent to the number contained in
        /// SelectedItem.Value, if the conversion succeeded, or Null.NullInteger if the conversion failed.
        /// </summary>
        public int SelectedItemValueAsInt
        {
            get
            {
                if (this.SelectedItem != null && !string.IsNullOrEmpty(this.SelectedItem.Value))
                {
                    var parsed = int.TryParse(this.SelectedItem.Value, out var valueAsInt);
                    return parsed ? valueAsInt : Null.NullInteger;
                }

                return Null.NullInteger;
            }
        }

        /// <summary>Gets the services options.</summary>
        public ItemListServicesOptions Services => this.Options.Services;

        /// <summary>Gets register a list of JavaScript methods that are executed when the selection from the list control changes on the client.</summary>
        public List<string> OnClientSelectionChanged => this.Options.OnClientSelectionChanged;

        /// <summary>Gets or sets the selected item in the control, or selects the item in the control.</summary>
        public DnnListItem SelectedItem
        {
            get
            {
                if (this.StateControl.TypedValue != null && this.StateControl.TypedValue.SelectedItem != null)
                {
                    return new DnnListItem { Text = this.StateControl.TypedValue.SelectedItem.Value, Value = this.StateControl.TypedValue.SelectedItem.Key };
                }

                return null;
            }

            set
            {
                this.StateControl.TypedValueOrDefault.SelectedItem = (value == null) ? null : new SerializableKeyValuePair<string, string>(value.Value, value.Text);
            }
        }

        /// <summary>
        /// Gets or sets selectedItem's value when SelectedItem is not explicitly specified (i.e. equals <see langword="null"/>);
        /// Always displayed as first option in the list.
        /// </summary>
        public DnnListItem UndefinedItem
        {
            get
            {
                return this.FirstItem;
            }

            set
            {
                this.FirstItem = value;
                this.UseUndefinedItem = true;
            }
        }

        /// <summary>Gets or sets item to be displayed as first item.</summary>
        public DnnListItem FirstItem
        {
            get
            {
                return (this.Options.ItemList.FirstItem == null) ? null : new DnnListItem(this.Options.ItemList.FirstItem.Value, this.Options.ItemList.FirstItem.Key);
            }

            set
            {
                this.Options.ItemList.FirstItem = (value == null) ? null : new SerializableKeyValuePair<string, string>(value.Value, value.Text);
                this.UseUndefinedItem = false;
            }
        }

        /// <summary>Sets dropDownList Caption when no Item is selected.</summary>
        public string SelectItemDefaultText
        {
            set => this.Options.SelectItemDefaultText = value;
        }

        /// <summary>Gets or sets the skin.</summary>
        [Parameter]
        public string Skin { get; set; }

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <summary>Gets or sets the CSS width of this control, e.g. <c>"300px"</c>.</summary>
        /// <remarks>Replaces <c>WebControl.Width</c>, which was a <c>System.Web.UI.WebControls.Unit</c>.</remarks>
        [Parameter]
        public string Width
        {
            get => this.Style.TryGetValue("width", out var width) ? width : null;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    this.Style.Remove("width");
                }
                else
                {
                    this.Style["width"] = value;
                }
            }
        }
        #pragma warning restore BL0007

        /// <summary>Gets or sets a value indicating whether this control accepts input.</summary>
        /// <remarks>Replaces <c>WebControl.Enabled</c>.</remarks>
        [Parameter]
        public bool Enabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the server is notified when the user changes the list selection.</summary>
        /// <remarks>
        /// Replaces the Web Forms auto-postback: when it is set, a client side selection change is
        /// surfaced to the server as <see cref="SelectionChanged"/>/<see cref="SelectionChangedCallback"/>.
        /// </remarks>
        [Parameter]
        public bool AutoPostBack { get; set; }

        /// <summary>Gets or sets a value indicating whether validation is performed when a control is clicked.</summary>
        [Parameter]
        public virtual bool CausesValidation { get; set; }

        /// <summary>Gets or sets the group of controls for which the control causes validation.</summary>
        [Parameter]
        public virtual string ValidationGroup { get; set; } = string.Empty;

        /// <summary>Gets or sets a callback raised in addition to the <see cref="SelectionChanged"/> event.</summary>
        [Parameter]
        public EventCallback<EventArgs> SelectionChangedCallback { get; set; }

        /// <summary>
        /// Gets or sets when the tree view in drop down has multiple level nodes, and the initial selected item is a child node.
        /// we need expand its parent nodes to make it selected.
        /// </summary>
        public string ExpandPath
        {
            get => ClientAPI.GetClientVariable(this.Page, this.ClientId + "_expandPath");
            set => ClientAPI.RegisterClientVariable(this.Page, this.ClientId + "_expandPath", value, true);
        }

        /// <summary>Gets the options.</summary>
        internal DnnDropDownListOptions Options => this.options.Value;

        /// <summary>Gets or sets the JavaScript runtime used to initialize the client component.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>Gets the state control.</summary>
        protected DnnGenericHiddenField<DnnDropDownListState> StateControl
        {
            get
            {
                this.EnsureChildControls();
                return this.stateControl;
            }
        }

        /// <inheritdoc />
        protected override string TagName => "div";

        private DnnGenericControl SelectedValue
        {
            get
            {
                this.EnsureChildControls();
                return this.selectedValue;
            }
        }

        private bool UseUndefinedItem { get; set; }

        /// <summary>Registers scripts.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="skin">The skin.</param>
        internal static void RegisterClientScript(IClientResourceController clientResourceController, string skin)
        {
            clientResourceController.RegisterStylesheet("~/Resources/Shared/components/DropDownList/dnn.DropDownList.css", FileOrder.Css.ResourceCss);
            if (!string.IsNullOrEmpty(skin))
            {
                clientResourceController.RegisterStylesheet("~/Resources/Shared/components/DropDownList/dnn.DropDownList." + skin + ".css", FileOrder.Css.ResourceCss);
            }

            clientResourceController.RegisterStylesheet("~/Resources/Shared/scripts/jquery/dnn.jScrollBar.css", FileOrder.Css.ResourceCss);

            clientResourceController.RegisterScript("~/Resources/Shared/scripts/dnn.extensions.js");
            clientResourceController.RegisterScript("~/Resources/Shared/scripts/dnn.jquery.extensions.js");
            clientResourceController.RegisterScript("~/Resources/Shared/scripts/dnn.DataStructures.js");
            clientResourceController.RegisterScript("~/Resources/Shared/scripts/jquery/jquery.mousewheel.js");
            clientResourceController.RegisterScript("~/Resources/Shared/scripts/jquery/dnn.jScrollBar.js");
            clientResourceController.RegisterScript("~/Resources/Shared/scripts/TreeView/dnn.TreeView.js");
            clientResourceController.RegisterScript("~/Resources/Shared/scripts/TreeView/dnn.DynamicTreeView.js");
            clientResourceController.RegisterScript("~/Resources/Shared/Components/DropDownList/dnn.DropDownList.js");
        }

        /// <inheritdoc />
        protected override void CreateChildControls()
        {
            this.Controls.Clear();

            var selectedItemPanel = new DnnPanel { CssClass = "selected-item", };

            this.selectedValue = new DnnGenericControl("a");
            this.selectedValue.Attributes["href"] = "javascript:void(0);";
            this.selectedValue.Attributes["title"] = LocalizeString("DropDownList.SelectedItemExpandTooltip");
            this.selectedValue.Attributes["class"] = "selected-value";
            selectedItemPanel.Controls.Add(this.selectedValue);
            this.Controls.Add(selectedItemPanel);

            this.stateControl = new DnnGenericHiddenField<DnnDropDownListState> { ID = "state", };
            this.stateControl.ValueChanged += (sender, args) => this.OnSelectionChanged(EventArgs.Empty);
            this.Controls.Add(this.stateControl);
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            this.StateControl.Value = string.Empty; // for state persistence (stateControl)
            this.servicesFramework.RequestAjaxAntiForgerySupport();
        }
        #pragma warning restore BL0005

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            RegisterClientScript(this.clientResourceController, this.Skin);

            this.AddCssClass("dnnDropDownList");

            base.OnPreRender(e);
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (this.clientInitialized || this.JsRuntime == null)
            {
                return;
            }

            this.clientInitialized = true;
            await this.InitializeClientComponentAsync();
        }

        /// <summary>A method called when the <see cref="SelectionChanged"/> event triggers.</summary>
        /// <param name="e">The event args.</param>
        protected virtual void OnSelectionChanged(EventArgs e)
        {
            this.SelectionChanged?.Invoke(this, e);
            if (this.AutoPostBack && this.SelectionChangedCallback.HasDelegate)
            {
                this.SelectionChangedCallback.InvokeAsync(e);
            }
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>Creates the client side component over the markup this control rendered.</summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// Replaces <c>ScriptManager.RegisterStartupScript</c>: the very same
        /// <c>dnn.createDropDownList(selector, options, methods)</c> call is made, but through the
        /// JS interop channel. <c>onSelectionChangedBackScript</c> is no longer needed — the client
        /// writes the new selection into the state field and the resulting <c>change</c> event is
        /// handled server side by <see cref="DnnGenericHiddenField{T}"/>.
        /// </remarks>
        protected virtual async Task InitializeClientComponentAsync()
        {
            this.Options.InternalStateFieldId = this.StateControl.ClientId;

            if (this.SelectedItem == null && this.UseUndefinedItem)
            {
                this.SelectedItem = this.UndefinedItem;
            }

            this.Options.InitialState = new DnnDropDownListState
            {
                SelectedItem = this.StateControl.TypedValue?.SelectedItem,
            };

            this.SelectedValue.InnerHtml = (this.SelectedItem != null) ? this.SelectedItem.Text : this.Options.SelectItemDefaultText;

            this.Options.Disabled = !this.Enabled;

            // The options are serialized by DNN's own serializer so that the client keeps seeing the
            // camel-cased [DataMember] names; the resulting document is handed to interop verbatim.
            using var optionsDocument = JsonDocument.Parse(Json.Serialize(this.Options));
            using var methodsDocument = JsonDocument.Parse(new JavaScriptObjectDictionary().ToJsonString());

            await this.JsRuntime.InvokeVoidAsync(
                "dnn.createDropDownList",
                "#" + this.ClientId,
                optionsDocument.RootElement,
                methodsDocument.RootElement);
        }
        #pragma warning restore BL0005

        private static string LocalizeString(string key)
        {
            return Localization.GetString(key, Localization.SharedResourceFile);
        }
    }
}
