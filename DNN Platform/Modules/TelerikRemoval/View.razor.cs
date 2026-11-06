// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Modules.TelerikRemoval
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Maintenance.Telerik;
    using DotNetNuke.Maintenance.Telerik.Removal;

    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    /// <summary>The main view of the DNN Telerik Removal module.</summary>
    /// <remarks>
    /// Was <c>View.ascx</c> + <c>View.ascx.cs</c> + <c>View.ascx.designer.cs</c>; the markup now
    /// lives in <c>View.razor</c>. The designer file's server-control fields have no Blazor
    /// equivalent and are replaced as follows:
    /// <list type="bullet">
    /// <item><description>
    /// <c>MainUpdatePanel</c> (an <c>asp:UpdatePanel</c>) is dropped: a component re-renders its
    /// own diff, and the manifest already declared <c>supportsPartialRendering="False"</c>.
    /// </description></item>
    /// <item><description>
    /// <c>MainMultiView.ActiveViewIndex</c> and <c>InstalledMultiView.ActiveViewIndex</c> become
    /// <see cref="ActiveViewIndex"/> and <see cref="InstalledViewIndex"/>, with the same indices.
    /// </description></item>
    /// <item><description>
    /// The labels whose <c>Text</c> the code behind assigned become
    /// <see cref="TelerikInstalledVersion"/>, <see cref="TelerikInstalledAndUsedInfo"/> and
    /// <see cref="TelerikInstalledAndUsedWarning"/>; the labels that carried a literal resource
    /// key resolve it in the markup through <c>LocalizeText</c>.
    /// </description></item>
    /// <item><description>
    /// <c>RemoveTelerikButton.Enabled</c> becomes <see cref="RemoveTelerikButtonEnabled"/> and
    /// <c>BackupConfirmationCheckBox.Checked</c> becomes <see cref="BackupConfirmed"/>.
    /// </description></item>
    /// <item><description>
    /// <c>AssemblyListPlaceHolder</c> plus <c>CreateTable</c>'s
    /// <c>Table</c>/<c>TableRow</c>/<c>TableCell</c>/<c>BulletedList</c> tree becomes
    /// <see cref="AssemblyColumns"/> and <see cref="AssemblyCellStyle"/>, which the markup renders
    /// with the same structure, the same capacity rules and the same cell widths.
    /// </description></item>
    /// <item><description>
    /// <c>UninstallReportRepeater.DataSource</c> becomes <see cref="UninstallReport"/>, which the
    /// markup iterates in place of the repeater's <c>ItemTemplate</c>.
    /// </description></item>
    /// </list>
    /// </remarks>
    public partial class View : PortalModuleBase
    {
        private readonly IServiceProvider serviceProvider;

        private int activeViewIndex;
        private int installedViewIndex;
        private string telerikInstalledVersion = string.Empty;
        private string telerikInstalledAndUsedInfo = string.Empty;
        private string telerikInstalledAndUsedWarning = string.Empty;
        private string uninstallReportDetail = string.Empty;
        private bool backupConfirmed;
        private bool removeTelerikButtonEnabled = true;
        private bool removalRunning;
        private List<List<string>> assemblyColumns = new List<List<string>>();
        private IEnumerable<UninstallSummaryItem> uninstallReport = Array.Empty<UninstallSummaryItem>();
        private IJSObjectReference interopModule;

        /// <summary>Initializes a new instance of the <see cref="View"/> class.</summary>
        public View()
        {
            this.serviceProvider = this.DependencyProvider;
        }

        /// <summary>Initializes a new instance of the <see cref="View"/> class.</summary>
        /// <param name="serviceProvider">An instance of <see cref="IServiceProvider"/>.</param>
        internal View(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider ??
                throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>Gets the index of the visible outer view.</summary>
        /// <remarks>
        /// Replaces <c>MainMultiView.ActiveViewIndex</c>: 0 <c>RegularUserView</c>,
        /// 1 <c>NotInstalledView</c>, 2 <c>InstalledView</c>, 3 <c>UninstallReportView</c>.
        /// </remarks>
        protected int ActiveViewIndex => this.activeViewIndex;

        /// <summary>Gets the index of the visible inner view of <c>InstalledView</c>.</summary>
        /// <remarks>
        /// Replaces <c>InstalledMultiView.ActiveViewIndex</c>: 0 <c>InstalledButNotUsedView</c>,
        /// 1 <c>InstalledAndUsedView</c>.
        /// </remarks>
        protected int InstalledViewIndex => this.installedViewIndex;

        /// <summary>Gets the installed Telerik version.</summary>
        /// <remarks>Replaces <c>TelerikInstalledVersionLabel.Text</c>.</remarks>
        protected string TelerikInstalledVersion => this.telerikInstalledVersion;

        /// <summary>Gets the text of the "Telerik is installed and used" notice.</summary>
        /// <remarks>Replaces <c>TelerikInstalledAndUsedInfoLabel.Text</c>.</remarks>
        protected string TelerikInstalledAndUsedInfo => this.telerikInstalledAndUsedInfo;

        /// <summary>Gets the text of the "Telerik is installed and used" warning.</summary>
        /// <remarks>Replaces <c>TelerikInstalledAndUsedWarningLabel.Text</c>.</remarks>
        protected string TelerikInstalledAndUsedWarning => this.telerikInstalledAndUsedWarning;

        /// <summary>Gets the detail text of the uninstall report.</summary>
        /// <remarks>
        /// Replaces <c>UninstallReportDetailLabel.Text</c>. The label was declared
        /// <c>Localize="False"</c> and the code behind never assigned it, so it renders empty, as
        /// it did on .NET Framework.
        /// </remarks>
        protected string UninstallReportDetail => this.uninstallReportDetail;

        /// <summary>Gets a value indicating whether the backup confirmation is checked.</summary>
        /// <remarks>Replaces <c>BackupConfirmationCheckBox.Checked</c>.</remarks>
        protected bool BackupConfirmed => this.backupConfirmed;

        /// <summary>Gets a value indicating whether the remove button accepts input.</summary>
        /// <remarks>Replaces <c>RemoveTelerikButton.Enabled</c>.</remarks>
        protected bool RemoveTelerikButtonEnabled => this.removeTelerikButtonEnabled;

        /// <summary>Gets the assemblies that depend on Telerik, laid out in columns.</summary>
        /// <remarks>Replaces the <c>BulletedList</c> of each cell built by <c>CreateTable</c>.</remarks>
        protected List<List<string>> AssemblyColumns => this.assemblyColumns;

        /// <summary>Gets the inline style of each assembly list cell.</summary>
        /// <remarks>Replaces <c>cell.Width = Unit.Percentage(100.0 / row.Cells.Count)</c>.</remarks>
        protected string AssemblyCellStyle =>
            this.assemblyColumns.Count == 0
                ? string.Empty
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "width:{0}%;",
                    100.0 / this.assemblyColumns.Count);

        /// <summary>Gets the uninstall progress rendered by the report table.</summary>
        /// <remarks>Replaces <c>UninstallReportRepeater.DataSource</c>.</remarks>
        protected IEnumerable<UninstallSummaryItem> UninstallReport => this.uninstallReport;

        /// <summary>Gets or sets the JavaScript runtime used to drive the "running" overlay.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>
        /// Converts a boolean value into a check mark if <see langword="true"/>,
        /// a cross mark if <see langword="false"/>, or an empty string if null.
        /// If the input value is not of <c>Boolean</c> type or <c>Null</c>,
        /// the unmodified input value is returned.
        /// </summary>
        /// <param name="value">The input value to convert.</param>
        /// <returns>
        /// The check mark button icon ✅ (&amp;#9989;) if <c>value</c> is <see langword="true"/>,
        /// the cross mark icon ❌ (&amp;#10060;) if <c>value</c> is <see langword="false"/>,
        /// an empty <c>String</c> if <c>value</c> is <c>Null</c>,
        /// or the unmodified input value otherwise.
        /// </returns>
        protected string ConvertBooleanToIcon(object value)
        {
            if (value is null)
            {
                return string.Empty;
            }

            if (typeof(bool) != value.GetType())
            {
                return $"{value}";
            }

            return ((bool)value) ? "&#9989;" : "&#10060;";
        }

        /// <summary>Renders <see cref="ConvertBooleanToIcon(object)"/> as markup.</summary>
        /// <param name="value">The input value to convert.</param>
        /// <returns>The icon as markup.</returns>
        /// <remarks>
        /// The repeater emitted the result of <c>ConvertBooleanToIcon</c> through
        /// <c>&lt;%# … %&gt;</c>, which did not HTML-encode it, so the <c>&amp;#9989;</c> /
        /// <c>&amp;#10060;</c> entities displayed as ✅ / ❌. Razor encodes string output, so the
        /// value is wrapped in a <see cref="MarkupString"/> to keep that rendering.
        /// </remarks>
        protected MarkupString ConvertBooleanToIconMarkup(object value) =>
            (MarkupString)this.ConvertBooleanToIcon(value);

        /// <inheritdoc />
        /// <remarks>
        /// Replaces the <c>if (!this.Page.IsPostBack)</c> body of <c>Page_Load</c>: the Init phase
        /// of a module control runs once per circuit, which is when the WebForms control ran this
        /// work, and it is also the only point at which <c>HttpContext</c> (and therefore the
        /// portal context behind <c>UserInfo</c>) is available in a Blazor Server circuit, so the
        /// view state it computes is cached in fields.
        /// </remarks>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.InitMainMultiView();
            this.InitRemoveTelerikButton();
        }

        /// <inheritdoc />
        /// <remarks>
        /// Imports the module that replaces <c>RegisterClientScripts()</c>'s
        /// <c>RegisterOnSubmitStatement</c>. A <c>&lt;script&gt;</c> element written by a render
        /// tree is never executed, and JS interop is only legal once the component has rendered.
        /// </remarks>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (!firstRender || this.JsRuntime is null)
            {
                return;
            }

            try
            {
                this.interopModule = await this.JsRuntime.InvokeAsync<IJSObjectReference>(
                    "import",
                    Globals.ResolveUrl("~/DesktopModules/TelerikRemoval/Scripts/telerikRemovalInterop.js"));
            }
            catch (JSException)
            {
                // the extension's assets are not reachable; the module degrades to plain markup
                // without the "running" overlay. The removal itself is unaffected.
            }
        }

        /// <summary>Lays out the given items in columns.</summary>
        /// <param name="items">The items to lay out.</param>
        /// <param name="maxRows">The maximum number of items per column.</param>
        /// <param name="maxColumns">The maximum number of columns.</param>
        /// <returns>The columns, each holding the items of one list.</returns>
        /// <remarks>
        /// This is <c>CreateTable(IEnumerable&lt;string&gt;, int, int)</c>, which built a single
        /// <c>TableRow</c> of <c>TableCell</c>s each containing a <c>BulletedList</c>. The layout
        /// rules are preserved exactly: a column is opened before the first item, a new column is
        /// opened every <paramref name="maxRows"/> items, at most
        /// <c>maxRows * maxColumns - 1</c> items are listed, and <c>"..."</c> is appended when the
        /// capacity is smaller than the number of items.
        /// </remarks>
        private static List<List<string>> CreateColumns(IEnumerable<string> items, int maxRows, int maxColumns)
        {
            var capacity = maxRows * maxColumns;

            var columns = new List<List<string>>();
            var list = StartNewColumn(columns);

            foreach (var item in items.Take(capacity - 1))
            {
                list.Add(item);

                if (list.Count == maxRows)
                {
                    list = StartNewColumn(columns);
                }
            }

            if (capacity < items.Count())
            {
                list.Add("...");
            }

            return columns;
        }

        /// <summary>Appends a new column to the layout.</summary>
        /// <param name="columns">The columns built so far.</param>
        /// <returns>The new column.</returns>
        /// <remarks>Replaces <c>StartNewColumn(TableRow)</c>.</remarks>
        private static List<string> StartNewColumn(List<List<string>> columns)
        {
            var list = new List<string>();
            columns.Add(list);
            return list;
        }

        /// <summary>Event handler for the backup confirmation checkbox.</summary>
        /// <param name="isChecked">A value indicating whether the checkbox is checked.</param>
        /// <remarks>
        /// Replaces <c>BackupConfirmationCheckBox_CheckedChanged</c>; the checkbox's
        /// <c>AutoPostBack="True"</c> is now the change callback that invokes this method.
        /// </remarks>
        private async Task BackupConfirmationCheckBox_CheckedChanged(bool isChecked)
        {
            this.backupConfirmed = isChecked;
            this.removeTelerikButtonEnabled = this.backupConfirmed;

            // RegisterOnSubmitStatement attached the overlay to EVERY postback of the page,
            // including this checkbox's AutoPostBack, so it is applied around this round trip too.
            await this.SetRunningAsync(true);
            await this.SetRunningAsync(false);

            this.StateHasChanged();
        }

        /// <summary>Click event handler for <c>RemoveTelerikButton</c>.</summary>
        /// <param name="e">An <see cref="EventArgs"/> that contains the event data.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task RemoveTelerikButton_Click(EventArgs e)
        {
            if (!this.backupConfirmed)
            {
                return;
            }

            // Replaces OnClientClick="this.disabled = true;": in WebForms the button was disabled
            // in the BROWSER before the postback left, so a second click was impossible. A
            // component's disabled state only reaches the client with the next render batch, so the
            // guard is a server side latch, which closes the same window.
            if (this.removalRunning)
            {
                return;
            }

            this.removalRunning = true;
            this.removeTelerikButtonEnabled = false;

            // Replaces RegisterClientScripts()'s "$('#telerikRemoval').addClass('running');",
            // which the form's submit statement ran as the postback left the browser.
            await this.SetRunningAsync(true);

            try
            {
                var uninstaller = this.GetService<ITelerikUninstaller>();

                uninstaller.Execute();

                this.uninstallReport = uninstaller.Progress;

                this.activeViewIndex = 3; // UninstallReportView
            }
            finally
            {
                await this.SetRunningAsync(false);
                this.removalRunning = false;
            }

            // Replaces this.DataBind().
            this.StateHasChanged();
        }

        /// <summary>Adds or removes the <c>running</c> class of the module's root element.</summary>
        /// <param name="running">Whether the removal is running.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private async Task SetRunningAsync(bool running)
        {
            if (this.interopModule is null)
            {
                return;
            }

            try
            {
                await this.interopModule.InvokeVoidAsync("setRunning", "telerikRemoval", running);
            }
            catch (JSException)
            {
                // the overlay is cosmetic; a failure to toggle it must not stop the removal.
            }
        }

        /// <summary>Selects the view to display, as <c>InitMainMultiView()</c> did.</summary>
        private void InitMainMultiView()
        {
            if (!this.UserInfo.IsSuperUser)
            {
                this.activeViewIndex = 0; // RegularUserView
                return;
            }

            var telerikUtils = this.GetService<ITelerikUtils>();

            if (!telerikUtils.TelerikIsInstalled())
            {
                this.activeViewIndex = 1; // NotInstalledView
                return;
            }

            this.activeViewIndex = 2; // InstalledView

            var version = telerikUtils.GetTelerikVersion().ToString();
            this.telerikInstalledVersion = version;

            var assemblies = telerikUtils.GetAssembliesThatDependOnTelerik()
                .Select(a => Path.GetFileName(a));

            if (!assemblies.Any())
            {
                this.installedViewIndex = 0; // InstalledButNotUsedView
                return;
            }

            this.installedViewIndex = 1; // InstalledAndUsedView

            // The .ascx assigned these labels the RESOURCE KEY and the labels, which had no
            // Localize attribute (so Localize was true), resolved it through
            // DnnLabel.LocalizeStrings. The lookup is done here instead, against the very same
            // App_LocalResources/View.resx keys, because the markup pins Localize="false" to keep
            // label text stable across re-renders.
            this.telerikInstalledAndUsedInfo = this.LocalizeText("TelerikInstalledAndUsedInfo");
            this.telerikInstalledAndUsedWarning = this.LocalizeText("TelerikInstalledAndUsedWarning");

            this.assemblyColumns = CreateColumns(assemblies, maxRows: 3, maxColumns: 3);
        }

        /// <summary>Disables the remove button, as <c>InitRemoveTelerikButton()</c> did.</summary>
        private void InitRemoveTelerikButton()
        {
            this.removeTelerikButtonEnabled = false;
        }

        private T GetService<T>()
            where T : class
        {
            return (T)this.serviceProvider.GetService(typeof(T));
        }
    }
}
