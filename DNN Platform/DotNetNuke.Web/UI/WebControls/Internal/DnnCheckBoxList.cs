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

    /// <summary>This control is only for internal use, please don't reference it in any other place as it may be removed in the future.</summary>
    /// <remarks>
    /// This was a <c>CheckBoxList</c>. On ASP.NET Core it derives from <see cref="DnnListControl"/>
    /// and renders the same markup shape — one <c>input type="checkbox"</c> plus <c>label</c> per
    /// item, laid out in <see cref="RepeatColumns"/> columns. The <c>Selectize</c> initialization
    /// moved from an inline startup script to a JS interop call.
    /// </remarks>
    public class DnnCheckBoxList : DnnListControl
    {
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;
        private readonly IClientResourceController clientResourceController;
        private string initValue;
        private bool clientInitialized;

        /// <summary>Initializes a new instance of the <see cref="DnnCheckBoxList"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IApplicationStatusInfo. Scheduled removal in v12.0.0.")]
        public DnnCheckBoxList()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnCheckBoxList"/> class.</summary>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        public DnnCheckBoxList(IApplicationStatusInfo appStatus, IEventLogger eventLogger, IClientResourceController clientResourceController)
        {
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();

            this.MultipleSelect = true;
            this.RepeatColumns = 1;
        }

        /// <summary>Gets or sets the number of columns the items are laid out in.</summary>
        /// <remarks>Replaces <c>CheckBoxList.RepeatColumns</c>.</remarks>
        [Parameter]
        public int RepeatColumns { get; set; }

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
                else
                {
                    base.SelectedValue = value;
                }
            }
        }

        /// <summary>Gets or sets the JavaScript runtime used to create the client component.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

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

            this.EnsureDataBound();

            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;
            builder.OpenElement(sequence++, "div");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
            }

            foreach (var attribute in this.Attributes)
            {
                builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
            }

            var style = this.BuildStyleAttribute();
            if (this.RepeatColumns > 1)
            {
                style += $"display:grid;grid-template-columns:repeat({this.RepeatColumns}, auto);";
            }

            if (!string.IsNullOrEmpty(style))
            {
                builder.AddAttribute(sequence++, "style", style);
            }

            var index = 0;
            foreach (var item in this.Items)
            {
                var currentItem = item;
                var itemId = this.ClientId + "_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

                builder.OpenElement(sequence, "span");
                builder.OpenElement(sequence + 1, "input");
                builder.AddAttribute(sequence + 2, "type", "checkbox");
                builder.AddAttribute(sequence + 3, "id", itemId);
                builder.AddAttribute(sequence + 4, "name", this.UniqueId);
                builder.AddAttribute(sequence + 5, "value", currentItem.Value);
                builder.AddAttribute(sequence + 6, "checked", currentItem.Selected);
                if (!this.Enabled)
                {
                    builder.AddAttribute(sequence + 7, "disabled", true);
                }

                builder.AddAttribute(
                    sequence + 8,
                    "onchange",
                    EventCallback.Factory.Create<ChangeEventArgs>(this, args => this.OnItemToggled(currentItem, args)));
                builder.CloseElement();

                builder.OpenElement(sequence + 9, "label");
                builder.AddAttribute(sequence + 10, "for", itemId);
                builder.AddContent(sequence + 11, currentItem.Text);
                builder.CloseElement();
                builder.CloseElement();

                index++;
            }

            builder.CloseElement();
        }
        #pragma warning restore ASP0006

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            Utilities.ApplyControlSkin(this.clientResourceController, this, string.Empty, string.Empty);
            this.RegisterRequestResources();

            base.OnPreRender(e);
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (this.clientInitialized || this.JsRuntime == null || this.appStatus.Status != UpgradeStatus.None)
            {
                return;
            }

            if (JavaScriptLibraryController.Instance.GetLibrary(l => l.LibraryName == "Selectize") == null)
            {
                return;
            }

            this.clientInitialized = true;

            // Replaces `$('#id').selectize({})`, which used to be an inline startup script.
            await this.JsRuntime.InvokeVoidAsync("dnn.createComboBox", "#" + this.ClientId, new object());
        }

        private void OnItemToggled(DnnListItem item, ChangeEventArgs args)
        {
            item.Selected = args?.Value is bool flag
                ? flag
                : bool.TryParse(args?.Value?.ToString(), out var parsed) && parsed;

            this.OnSelectedIndexChanged(EventArgs.Empty);
        }

        private void RegisterRequestResources()
        {
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, PortalSettings.Current, CommonJs.DnnPlugins);

            if (this.appStatus.Status == UpgradeStatus.None)
            {
                var package = JavaScriptLibraryController.Instance.GetLibrary(l => l.LibraryName == "Selectize");
                if (package != null)
                {
                    JavaScript.RequestRegistration(this.appStatus, this.eventLogger, PortalSettings.Current, "Selectize");

                    var libraryPath = $"~/resources/Libraries/{package.LibraryName}/{Globals.FormatVersion(package.Version, "00", 3, "_")}/";
                    this.clientResourceController.RegisterStylesheet($"{libraryPath}selectize.css");
                    this.clientResourceController.RegisterStylesheet($"{libraryPath}selectize.default.css");
                    this.clientResourceController.RegisterScript("~/resources/Shared/scripts/dnn.ControlsInterop.js");
                }
            }
        }
    }
}
