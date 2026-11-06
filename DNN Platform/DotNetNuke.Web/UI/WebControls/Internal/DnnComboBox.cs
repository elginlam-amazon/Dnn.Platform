// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls.Internal
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.UI;
    using DotNetNuke.Web.UI.WebControls.Extensions;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    using Newtonsoft.Json;

    /// <summary>This control is only for internal use, please don't reference it in any other place as it may be removed in the future.</summary>
    /// <remarks>
    /// This was a <c>DropDownList</c> whose markup the <c>Selectize</c> based <c>dnn.combobox.js</c>
    /// enhanced. On ASP.NET Core it derives from <see cref="DnnListControl"/> and keeps the same
    /// two rendering shapes: a <c>select</c> carrying the options, or — when
    /// <see cref="CheckBoxes"/>/<see cref="DnnListControl.MultipleSelect"/> is set — a hidden-style <c>input</c>
    /// whose <c>value</c> is the comma separated list of selected values, which is what the client
    /// component reads. The init call moved from <c>ScriptManager.RegisterStartupScript</c> to
    /// <c>dnn.createComboBox</c> over JS interop.
    /// </remarks>
    public class DnnComboBox : DnnListControl
    {
        private string initValue;
        private string multipleValue;
        private bool clientInitialized;

        /// <summary>Initializes a new instance of the <see cref="DnnComboBox"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IApplicationStatusInfo. Scheduled removal in v12.0.0.")]
        public DnnComboBox()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnComboBox"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        public DnnComboBox(IApplicationStatusInfo appStatus, IEventLogger eventLogger, IClientResourceController clientResourceController)
        {
            this.AppStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.EventLogger = eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            this.ClientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
        }

        /// <inheritdoc />
        public override string SelectedValue
        {
            get
            {
                return base.SelectedValue;
            }

            set
            {
                if (this.RequiresDataBinding)
                {
                    this.initValue = value;
                }

                if (this.Items.Any(i => i.Value == value))
                {
                    base.SelectedValue = value;
                }
            }
        }

        /// <summary>Gets or sets a value indicating whether to show checkboxes.</summary>
        [Parameter]
        public virtual bool CheckBoxes { get; set; }

        /// <summary>Gets or sets the client code to run when the selected index changes.</summary>
        [Parameter]
        public virtual string OnClientSelectedIndexChanged { get; set; }

        /// <summary>Gets or sets the value.</summary>
        public string Value
        {
            get
            {
                if (this.RendersAsInput)
                {
                    return this.multipleValue ?? string.Empty;
                }

                return this.SelectedValue ?? string.Empty;
            }

            set
            {
                if (this.RendersAsInput)
                {
                    this.multipleValue = value;
                    this.Attributes["value"] = value;
                }

                this.SelectedValue = value;
            }
        }

        /// <summary>Gets or sets the options.</summary>
        public DnnComboBoxOption Options { get; set; } = new DnnComboBoxOption();

        /// <summary>Gets the application status.</summary>
        protected IApplicationStatusInfo AppStatus { get; }

        /// <summary>Gets the event logger.</summary>
        protected IEventLogger EventLogger { get; }

        /// <summary>Gets the client resource controller.</summary>
        protected IClientResourceController ClientResourceController { get; }

        /// <summary>Gets or sets the JavaScript runtime used to create the client component.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>Gets a value indicating whether this control renders an <c>input</c> rather than a <c>select</c>.</summary>
        /// <remarks>Replaces the <c>TagKey</c> switch of the Web Forms control.</remarks>
        protected bool RendersAsInput => this.MultipleSelect || this.CheckBoxes;

        /// <inheritdoc />
        public override void DataBind()
        {
            if (!string.IsNullOrEmpty(this.initValue))
            {
                this.DataBind(this.initValue);
            }
            else
            {
                base.DataBind();
            }
        }

        /// <summary>Adds an item to the list.</summary>
        /// <param name="text">The item text.</param>
        /// <param name="value">The item value.</param>
        public void AddItem(string text, string value)
        {
            this.Items.Add(new DnnListItem(text, value));
        }

        /// <summary>Inserts an item into the list.</summary>
        /// <param name="index">The location in the collection to insert the item.</param>
        /// <param name="text">The item text.</param>
        /// <param name="value">The item value.</param>
        public void InsertItem(int index, string text, string value)
        {
            this.Items.Insert(index, new DnnListItem(text, value));
        }

        /// <summary>Binds a data source to the invoked server control and all its child controls.</summary>
        /// <param name="initialValue">The initial value.</param>
        public void DataBind(string initialValue)
        {
            this.DataBind(initialValue, false);
        }

        /// <summary>Binds a data source to the invoked server control and all its child controls.</summary>
        /// <param name="initial">The initial value or text.</param>
        /// <param name="findByText">Whether <paramref name="initial"/> is the text or value.</param>
        public void DataBind(string initial, bool findByText)
        {
            base.DataBind();

            this.Select(initial, findByText);
        }

        /// <summary>Selects an item.</summary>
        /// <param name="initial">The item's value or text.</param>
        /// <param name="findByText">Whether <paramref name="initial"/> is the text or value.</param>
        public void Select(string initial, bool findByText)
        {
            if (findByText)
            {
                var byText = this.FindItemByText(initial, true);
                if (byText != null)
                {
                    byText.Selected = true;
                }
            }
            else
            {
                var byValue = this.FindItemByValue(initial, true);
                if (byValue != null)
                {
                    byValue.Selected = true;
                }
            }
        }

        /// <summary>Finds an item by its text.</summary>
        /// <param name="text">The item's text.</param>
        /// <param name="ignoreCase">Whether to do a case-insensitive search.</param>
        /// <returns>The list item or <see langword="null"/>.</returns>
        public DnnListItem FindItemByText(string text, bool ignoreCase = false)
        {
            return ignoreCase ? this.Items.FindByText(text) : this.Items.FindByTextWithIgnoreCase(text);
        }

        /// <summary>Finds an item by its value.</summary>
        /// <param name="value">The item's value.</param>
        /// <param name="ignoreCase">Whether to do a case-insensitive search.</param>
        /// <returns>The list item or <see langword="null"/>.</returns>
        public DnnListItem FindItemByValue(string value, bool ignoreCase = false)
        {
            return ignoreCase ? this.Items.FindByValue(value) : this.Items.FindByValueWithIgnoreCase(value);
        }

        /// <summary>Finds an item's index by its value.</summary>
        /// <param name="value">The item's value.</param>
        /// <returns>The index or <c>-1</c>.</returns>
        public int FindItemIndexByValue(string value)
        {
            var item = this.FindItemByValue(value);
            return item == null ? -1 : this.Items.IndexOf(item);
        }

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (!this.RendersAsInput)
            {
                base.BuildRenderTree(builder);
                return;
            }

            // The multiple-select / checkbox flavour renders a single input whose value carries the
            // selection; the client component builds the visible list from the options it is given.
            this.EnsureDataBound();

            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;
            builder.OpenElement(sequence++, "input");
            builder.AddAttribute(sequence++, "type", "text");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
                builder.AddAttribute(sequence++, "name", this.UniqueId);
            }

            foreach (var attribute in this.Attributes)
            {
                builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
            }

            var inlineStyle = this.BuildStyleAttribute();
            if (!string.IsNullOrEmpty(inlineStyle))
            {
                builder.AddAttribute(sequence++, "style", inlineStyle);
            }

            if (!this.Enabled)
            {
                builder.AddAttribute(sequence++, "disabled", true);
            }

            builder.AddAttribute(sequence++, "value", this.Value);
            builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.Create<Microsoft.AspNetCore.Components.ChangeEventArgs>(this, this.OnSelectionChanged));
            builder.CloseElement();
        }
        #pragma warning restore ASP0006

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            Utilities.ApplyControlSkin(this.ClientResourceController, this, string.Empty, string.Empty);

            if (this.RendersAsInput)
            {
                this.Options.Items = this.Items;
                this.Value = string.Join(",", this.Options.Items.Where(i => i.Selected).Select(i => i.Value));
            }
            else
            {
                if (this.Items.Any(i => string.IsNullOrEmpty(i.Value)))
                {
                    this.Options.AllowEmptyOption = true;
                }
            }

            if (!this.Options.Localization.ContainsKey("ItemsChecked"))
            {
                this.Options.Localization.Add("ItemsChecked", Utilities.GetLocalizedString("ItemsCheckedString"));
            }

            if (!this.Options.Localization.ContainsKey("AllItemsChecked"))
            {
                this.Options.Localization.Add("AllItemsChecked", Utilities.GetLocalizedString("AllItemsCheckedString"));
            }

            this.Options.Checkbox = this.CheckBoxes;
            this.Options.OnChangeEvent = this.OnClientSelectedIndexChanged;

            this.RegisterRequestResources();

            base.OnPreRender(e);
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (this.clientInitialized || this.JsRuntime == null || this.AppStatus.Status != UpgradeStatus.None)
            {
                return;
            }

            var package = JavaScriptLibraryController.Instance.GetLibrary(l => l.LibraryName == "Selectize");
            if (package == null)
            {
                return;
            }

            this.clientInitialized = true;

            var options = JsonConvert.SerializeObject(this.Options, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, });
            using var optionsDocument = System.Text.Json.JsonDocument.Parse(options);

            await this.JsRuntime.InvokeVoidAsync("dnn.createComboBox", "#" + this.ClientId, optionsDocument.RootElement);
        }

        private void RegisterRequestResources()
        {
            JavaScript.RequestRegistration(this.AppStatus, this.EventLogger, PortalSettings.Current, CommonJs.DnnPlugins);

            if (this.AppStatus.Status == UpgradeStatus.None)
            {
                var package = JavaScriptLibraryController.Instance.GetLibrary(l => l.LibraryName == "Selectize");
                if (package != null)
                {
                    JavaScript.RequestRegistration(this.AppStatus, this.EventLogger, PortalSettings.Current, "Selectize");

                    var libraryPath =
                        $"~/Resources/Libraries/{package.LibraryName}/{Globals.FormatVersion(package.Version, "00", 3, "_")}/";

                    this.ClientResourceController.RegisterScript($"{libraryPath}dnn.combobox.js");
                    this.ClientResourceController.RegisterStylesheet($"{libraryPath}selectize.css");
                    this.ClientResourceController.RegisterStylesheet($"{libraryPath}selectize.default.css");
                    this.ClientResourceController.RegisterScript("~/Resources/Shared/scripts/dnn.ControlsInterop.js");
                }
            }
        }
    }
}
