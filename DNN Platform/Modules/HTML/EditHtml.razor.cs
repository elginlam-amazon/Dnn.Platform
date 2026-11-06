// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Html
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Content.Workflow;
    using DotNetNuke.Entities.Content.Workflow.Entities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Entities.Tabs.TabVersions;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Modules.Html.Components;
    using DotNetNuke.Security;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Skins.Controls;
    using DotNetNuke.UI.UserControls;
    using DotNetNuke.Web.UI.WebControls.Internal;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    /// <summary>The EditHtml PortalModuleBase is used to manage HTML.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>EditHtml.ascx</c> + <c>EditHtml.ascx.cs</c> + <c>EditHtml.ascx.designer.cs</c>; the
    /// markup lives in <c>EditHtml.razor</c>. The designer-generated control fields have no ASP.NET
    /// Core equivalent, so they became component state:
    /// </para>
    /// <list type="bullet">
    /// <item>every <c>PlaceHolder</c>/<c>HtmlGenericControl</c> <c>Visible</c> assignment became a
    /// boolean field driving an <c>@if</c> on the same condition (<c>phEdit</c> →
    /// <see cref="PhEditVisible"/> and so on);</item>
    /// <item>every <c>asp:Label</c>/<c>asp:Literal</c> <c>Text</c> assignment became a string field
    /// the markup renders;</item>
    /// <item><c>txtContent</c> is the migrated <c>TextEditor</c> component, bound two way to
    /// <see cref="contentText"/>, so the places that read <c>txtContent.Text</c> read that field;</item>
    /// <item><c>hfEditor</c> (a <c>HiddenField</c> that stashed the editor content across post backs)
    /// became the <see cref="editorStash"/> field, which a Blazor circuit keeps for free;</item>
    /// <item><c>dgHistory</c>/<c>dgVersions</c> are the migrated <see cref="DnnGrid"/> components,
    /// captured with <c>@ref</c> and configured (columns, events, paging) once they exist;</item>
    /// <item>the <c>ViewState</c> entries (<c>WorkflowID</c>, <c>TempContent</c>,
    /// <c>_currentWorkflowType</c>) became fields of this component instance, which is the per-user,
    /// per-module state container ViewState was.</item>
    /// </list>
    /// </remarks>
    public partial class EditHtml : HtmlModuleBase
    {
        private readonly HtmlTextLogController htmlTextLogController = new HtmlTextLogController();
        private readonly IWorkflowManager workflowManager = WorkflowManager.Instance;
        private readonly INavigationManager navigationManager;
        private readonly HtmlTextController htmlTextController;
        private readonly IPortalAliasService portalAliasService;
        private readonly IHostSettings hostSettings;

        /// <summary>The localized cell texts of the history grid, by row.</summary>
        /// <remarks>
        /// Replaces writing back to <c>GridViewRow.Cells[i].Text</c>, which the migrated
        /// <see cref="DnnGridRow"/> (a data item plus an index, not a cell collection) has no
        /// equivalent for: <see cref="OnHistoryGridItemDataBound"/> still produces the five cell
        /// texts and still localizes cells 2 and 3, and the history columns render them.
        /// </remarks>
        private readonly Dictionary<DnnGridRow, string[]> historyCellTexts = new Dictionary<DnnGridRow, string[]>();

        /// <summary>The per row state of the command buttons of the versions grid.</summary>
        /// <remarks>
        /// Replaces the <c>ImageButton</c> properties <c>OnVersionsGridItemDataBound</c> assigned by
        /// walking the row's cells and controls.
        /// </remarks>
        private readonly Dictionary<DnnGridRow, VersionRowState> versionRowStates = new Dictionary<DnnGridRow, VersionRowState>();

        /// <summary>The items of the <c>ddlRender</c> drop down list.</summary>
        private readonly List<DnnRenderItem> renderItems = new List<DnnRenderItem>();

        private DnnGrid dgHistory;
        private DnnGrid dgVersions;
        private TextEditor txtContent;

        private bool gridsConfigured;
        private bool loaded;

        /// <summary>Whether the request carried the <c>nuru</c> query string flag, captured in OnInit.</summary>
        private bool keepAbsoluteUrls;
        private bool renderModePending;

        private object historyDataSource;
        private object versionsDataSource;
        private bool historyBindPending;
        private bool versionsBindPending;

        // ViewState replacements.
        private int? workflowId;
        private string tempContent;
        private WorkflowType currentWorkflowType;

        // hfEditor replacement.
        private string editorStash = string.Empty;

        // txtContent.Text replacement (two way bound to the TextEditor component).
        private string contentText = string.Empty;

        private string hlCancelUrl;
        private string selectedRenderValue = string.Empty;
        private int versionsPageSize = 5;

        /// <summary>Initializes a new instance of the <see cref="EditHtml"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with INavigationManager. Scheduled removal in v12.0.0.")]
        public EditHtml()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="EditHtml"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="htmlTextController">The HTML/Text controller.</param>
        /// <param name="portalAliasService">The portal alias service.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public EditHtml(INavigationManager navigationManager, HtmlTextController htmlTextController, IPortalAliasService portalAliasService)
            : this(null, navigationManager, htmlTextController, portalAliasService, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="EditHtml"/> class.</summary>
        /// <param name="settingsRepository">The settings repository.</param>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="htmlTextController">The HTML/Text controller.</param>
        /// <param name="portalAliasService">The portal alias service.</param>
        /// <param name="hostSettings">The host settings.</param>
        public EditHtml(HtmlModuleSettingsRepository settingsRepository, INavigationManager navigationManager, HtmlTextController htmlTextController, IPortalAliasService portalAliasService, IHostSettings hostSettings)
            : base(settingsRepository)
        {
            this.navigationManager = navigationManager ?? this.DependencyProvider.GetRequiredService<INavigationManager>();
            this.htmlTextController = htmlTextController ?? this.DependencyProvider.GetRequiredService<HtmlTextController>();
            this.portalAliasService = portalAliasService ?? this.DependencyProvider.GetRequiredService<IPortalAliasService>();
            this.hostSettings = hostSettings ?? this.DependencyProvider.GetRequiredService<IHostSettings>();
        }

        private enum WorkflowType
        {
            DirectPublish = 1,
            SaveDraft = 2,
            ContentApproval = 3,
            CustomWorkflow = 4,
        }

        /// <summary>Gets the name of the view that is currently shown, which is also a CSS class of the content wrapper.</summary>
        protected string CurrentView
        {
            get
            {
                if (this.PhEditVisible)
                {
                    return "EditView";
                }
                else if (this.PhPreviewVisible)
                {
                    return "PreviewView";
                }

                if (this.PhHistoryVisible)
                {
                    return "HistoryView";
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>Gets or sets a value indicating whether the edit view renders. Replaces <c>phEdit.Visible</c>.</summary>
        protected bool PhEditVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the preview view renders. Replaces <c>phPreview.Visible</c>.</summary>
        protected bool PhPreviewVisible { get; set; }

        /// <summary>Gets or sets a value indicating whether the history view renders. Replaces <c>phHistory.Visible</c>.</summary>
        protected bool PhHistoryVisible { get; set; }

        /// <summary>Gets or sets a value indicating whether the master language content renders. Replaces <c>phMasterContent.Visible</c>.</summary>
        protected bool PhMasterContentVisible { get; set; }

        /// <summary>Gets or sets the master language content. Replaces the <c>LiteralControl</c> added to <c>placeMasterContent</c>.</summary>
        protected string MasterContentHtml { get; set; }

        /// <summary>Gets or sets a value indicating whether the text editor renders. Replaces <c>txtContent.Visible</c>.</summary>
        protected bool TxtContentVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the submitted content block renders. Replaces <c>divSubmittedContent.Visible</c>.</summary>
        protected bool DivSubmittedContentVisible { get; set; } = true;

        /// <summary>Gets or sets the preview of the submitted content. Replaces <c>litCurrentContentPreview.Text</c>.</summary>
        protected string LitCurrentContentPreviewText { get; set; }

        /// <summary>Gets or sets a value indicating whether the current version block renders. Replaces <c>phCurrentVersion.Visible</c>.</summary>
        protected bool PhCurrentVersionVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the current workflow state row renders. Replaces <c>divCurrentWorkflowState.Visible</c>.</summary>
        protected bool DivCurrentWorkflowStateVisible { get; set; } = true;

        /// <summary>Gets or sets the current workflow state. Replaces <c>lblCurrentWorkflowState.Text</c>.</summary>
        protected string LblCurrentWorkflowStateText { get; set; }

        /// <summary>Gets or sets the current version. Replaces <c>lblCurrentVersion.Text</c>.</summary>
        protected string LblCurrentVersionText { get; set; }

        /// <summary>Gets or sets the workflow in use. Replaces <c>lblCurrentWorkflowInUse.Text</c>.</summary>
        protected string LblCurrentWorkflowInUseText { get; set; }

        /// <summary>Gets or sets a value indicating whether the preview version block renders. Replaces <c>phPreviewVersion.Visible</c>.</summary>
        protected bool PhPreviewVersionVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the preview version row renders. Replaces <c>divPreviewVersion.Visible</c>.</summary>
        protected bool DivPreviewVersionVisible { get; set; } = true;

        /// <summary>Gets or sets the previewed version. Replaces <c>lblPreviewVersion.Text</c>.</summary>
        protected string LblPreviewVersionText { get; set; }

        /// <summary>Gets or sets a value indicating whether the preview workflow row renders. Replaces <c>divPreviewWorlflow.Visible</c>.</summary>
        protected bool DivPreviewWorlflowVisible { get; set; } = true;

        /// <summary>Gets or sets the previewed workflow. Replaces <c>lblPreviewWorkflowInUse.Text</c>.</summary>
        protected string LblPreviewWorkflowInUseText { get; set; }

        /// <summary>Gets or sets a value indicating whether the preview workflow state row renders. Replaces <c>divPreviewWorkflowState.Visible</c>.</summary>
        protected bool DivPreviewWorkflowStateVisible { get; set; } = true;

        /// <summary>Gets or sets the previewed workflow state. Replaces <c>lblPreviewWorkflowState.Text</c>.</summary>
        protected string LblPreviewWorkflowStateText { get; set; }

        /// <summary>Gets or sets the previewed content. Replaces <c>litPreview.Text</c>.</summary>
        protected string LitPreviewText { get; set; }

        /// <summary>Gets or sets a value indicating whether the history section head renders. Replaces <c>dnnSitePanelEditHTMLHistory.Visible</c>.</summary>
        protected bool DnnSitePanelEditHtmlHistoryVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the history field set renders. Replaces <c>fsEditHtmlHistory.Visible</c>.</summary>
        protected bool FsEditHtmlHistoryVisible { get; set; } = true;

        /// <summary>Gets or sets the maximum number of versions. Replaces <c>lblMaxVersions.Text</c>.</summary>
        protected string LblMaxVersionsText { get; set; }

        /// <summary>Gets or sets a value indicating whether the save action renders. Replaces <c>cmdSave.Visible</c>.</summary>
        protected bool CmdSaveVisible { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the edit action is enabled. Replaces <c>cmdEdit.Enabled</c>.</summary>
        protected bool CmdEditEnabled { get; set; }

        /// <summary>Gets or sets a value indicating whether the preview action is enabled. Replaces <c>cmdPreview.Enabled</c>.</summary>
        protected bool CmdPreviewEnabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the history action is enabled. Replaces <c>cmdHistory.Enabled</c>.</summary>
        protected bool CmdHistoryEnabled { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the master content action renders. Replaces <c>cmdMasterContent.Visible</c>.</summary>
        protected bool CmdMasterContentVisible { get; set; }

        /// <summary>Gets or sets the caption of the master content action. Replaces <c>cmdMasterContent.Text</c>.</summary>
        protected string CmdMasterContentText { get; set; }

        /// <summary>Gets or sets a value indicating whether the render mode drop down renders. Replaces <c>ddlRender.Visible</c>.</summary>
        protected bool DdlRenderVisible { get; set; } = true;

        /// <summary>Gets or sets the Blazor navigation manager, which performs the redirect that was <c>Response.Redirect</c>.</summary>
        [Inject]
        protected Microsoft.AspNetCore.Components.NavigationManager BrowserNavigation { get; set; }

        /// <summary>Gets or sets the JavaScript runtime, which shows the delete confirmation that was <c>ImageButton.OnClientClick</c>.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>Gets or sets the client resource controller the stylesheet is registered on.</summary>
        [Inject]
        private IClientResourceController ClientResourceController { get; set; }

        private int WorkflowID
        {
            get
            {
                int workflowID;

                if (this.workflowId == null)
                {
                    workflowID = this.htmlTextController.GetWorkflow(this.ModuleId, this.TabId, this.PortalId).Value;
                    this.workflowId = workflowID;
                }
                else
                {
                    workflowID = int.Parse(this.workflowId.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                }

                return workflowID;
            }
        }

        private string TempContent
        {
            get
            {
                var content = string.Empty;
                if (this.tempContent != null)
                {
                    content = this.tempContent;
                }

                return content;
            }

            set
            {
                this.tempContent = value;
            }
        }

        private WorkflowType CurrentWorkflowType
        {
            get => this.currentWorkflowType;

            set => this.currentWorkflowType = value;
        }

        private bool IsVersioningEnabled => TabVersionSettings.Instance.IsVersioningEnabled(PortalSettings.Current.PortalId, TabController.CurrentPage.TabID);

        private bool IsWorkflowEnabled => this.IsVersioningEnabled && TabWorkflowSettings.Instance.IsWorkflowEnabled(PortalSettings.Current.PortalId, TabController.CurrentPage.TabID);

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.hlCancelUrl = this.navigationManager.NavigateURL();

            // Request.QueryString["nuru"] has to be read while the HTTP request is still in
            // flight: HttpContextSource.Current is null for the rest of the circuit's life, so
            // reading it from OnSaveClick (which always runs over SignalR) would always miss and
            // the bypass could never trigger. Captured once here, like HtmlModule does.
            this.keepAbsoluteUrls = HttpContextSource.Current?.Request?.Query.ContainsKey("nuru") == true;

            // The event wiring of the Web Forms control (cmdEdit.Click, cmdPreview.Click,
            // cmdHistory.Click, cmdMasterContent.Click, ddlRender.SelectedIndexChanged,
            // cmdSave.Click, dgHistory.RowDataBound, dgVersions.RowCommand/RowDataBound/
            // PageIndexChanged) is expressed by the @onclick/@onchange callbacks of EditHtml.razor
            // for the action links and the drop down, and by ConfigureGrids() for the two grids,
            // which can only subscribe once the @ref captured components exist.
        }

        /// <inheritdoc />
        /// <remarks>
        /// Registers the stylesheet the <c>dnncl:DnnCssInclude</c> of the markup registered. This
        /// is the component's pre-render phase, which is where the tag helper ran, and
        /// <see cref="Globals.ResolveUrl"/> gives it the application-root-relative path the tag
        /// helper produced (a document-relative path would 404 on any nested page url).
        /// </remarks>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            this.ClientResourceController?.CreateStylesheet(Globals.ResolveUrl("~/DesktopModules/HTML/edit.css")).Register();
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                var htmlContentItemID = Null.NullInteger;
                var htmlContent = this.htmlTextController.GetTopHtmlText(this.ModuleId, false, this.WorkflowID);

                if (htmlContent != null)
                {
                    htmlContentItemID = htmlContent.ItemID;
                }

                // The Web Forms control guarded this with !Page.IsPostBack; a Blazor component is
                // not recreated per interaction, so the equivalent guard is to run it once.
                if (!this.loaded)
                {
                    this.loaded = true;

                    var workflow = this.workflowManager.GetWorkflow(this.WorkflowID);
                    var workflowStates = workflow.States.ToList();
                    var maxVersions = this.htmlTextController.GetMaximumVersionHistory(this.PortalId);
                    var userCanEdit = this.UserInfo.IsSuperUser || PortalSecurity.IsInRole(this.PortalSettings.AdministratorRoleName);

                    this.LblMaxVersionsText = maxVersions.ToString(CultureInfo.CurrentCulture);
                    this.versionsPageSize = Math.Min(Math.Max(maxVersions, 5), 10); // min 5, max 10

                    switch (workflow.WorkflowKey)
                    {
                        case SystemWorkflowManager.DirectPublishWorkflowKey:
                            this.CurrentWorkflowType = WorkflowType.DirectPublish;
                            break;
                        case SystemWorkflowManager.SaveDraftWorkflowKey:
                            this.CurrentWorkflowType = WorkflowType.SaveDraft;
                            break;
                        case SystemWorkflowManager.ContentAprovalWorkflowKey:
                            this.CurrentWorkflowType = WorkflowType.ContentApproval;
                            break;
                        default:
                            this.CurrentWorkflowType = WorkflowType.CustomWorkflow;
                            break;
                    }

                    if (htmlContentItemID != -1)
                    {
                        this.DisplayContent(htmlContent);

                        ////DisplayPreview(htmlContent);
                        this.DisplayHistory(htmlContent);
                    }
                    else
                    {
                        this.DisplayInitialContent(workflowStates[0]);
                    }

                    this.PhCurrentVersionVisible = this.CurrentWorkflowType != WorkflowType.DirectPublish;
                    this.PhPreviewVersionVisible = this.CurrentWorkflowType != WorkflowType.DirectPublish;

                    ////DisplayVersions();

                    // BindRenderItems() and the ddlRender.SelectedValue assignment both need the
                    // TextEditor component (IsRichEditorAvailable / Mode), which only exists after
                    // the first render; they run in OnAfterRenderAsync.
                    this.renderModePending = true;
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Completes the initialization that needs the <c>@ref</c> captured components: the grids
        /// get the columns, the paging and the event handlers their <c>.ascx</c> declaration gave
        /// them, and the render mode drop down gets the items and the selected value the tail of
        /// <c>OnLoad</c> assigned.
        /// </remarks>
        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);

            var changed = false;

            if (!this.gridsConfigured && this.dgHistory != null && this.dgVersions != null)
            {
                this.ConfigureGrids();
                changed = true;
            }

            if (this.historyBindPending && this.dgHistory != null)
            {
                this.historyBindPending = false;
                this.dgHistory.DataSource = this.historyDataSource;
                this.dgHistory.DataBind();
                changed = true;
            }

            if (this.versionsBindPending && this.dgVersions != null)
            {
                this.versionsBindPending = false;
                this.dgVersions.DataSource = this.versionsDataSource;
                this.dgVersions.DataBind();
                changed = true;
            }

            if (this.renderModePending && this.txtContent != null)
            {
                this.renderModePending = false;
                this.BindRenderItems();
                this.selectedRenderValue = this.txtContent.Mode;
                changed = true;
            }

            if (changed)
            {
                this.StateHasChanged();
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Runs the client side setup of the editor panels after every render, which is what the
        /// <c>$(document).ready</c> plus <c>PageRequestManager.add_endRequest</c> pair of the
        /// original inline script did (see <c>edit.js</c>).
        /// </remarks>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (this.JsRuntime == null)
            {
                return;
            }

            try
            {
                await this.JsRuntime.InvokeVoidAsync("dnn.htmlModule.setupEditHtml");
            }
            catch (JSException)
            {
                // The script is not loaded yet (or jQuery's dnnPanels plugin is missing); the panels
                // then simply stay expanded, exactly as they did when the original script failed.
            }
            catch (InvalidOperationException)
            {
                // JavaScript interop is not available during pre-rendering.
            }
        }

        /// <summary>Saves the content and redirects back to the portal page.</summary>
        protected void OnSaveClick()
        {
            const bool redirect = true;

            try
            {
                // get content
                var htmlContent = this.GetLatestHTMLContent();

                var aliases = this.portalAliasService.GetPortalAliasesByPortalId(this.PortalSettings.PortalId).Select(pa => pa.HttpAlias);
                var content = this.PhEditVisible ? this.contentText : this.editorStash;

                // Request.QueryString["nuru"] is captured in OnInit, while the HTTP request is
                // still available (see OnInit); HttpContextSource.Current is null here.
                if (!this.keepAbsoluteUrls)
                {
                    content = HtmlUtils.AbsoluteToRelativeUrls(content, aliases);
                }

                htmlContent.Content = HtmlUtils.SanitizeHtmlIfNeeded(content, this.PortalSettings.AllowJsInHtmlModule);
                var workflow = this.workflowManager.GetWorkflow(this.WorkflowID);
                var draftStateID = workflow.FirstState.StateID;
                var publishedStateID = workflow.LastState.StateID;

                switch (this.CurrentWorkflowType)
                {
                    case WorkflowType.DirectPublish:
                        this.htmlTextController.UpdateHtmlText(htmlContent, this.htmlTextController.GetMaximumVersionHistory(this.PortalId));

                        break;
                    default:
                        // if it's already published set it back to draft
                        if (htmlContent.StateID == publishedStateID)
                        {
                            htmlContent.StateID = draftStateID;
                        }

                        this.htmlTextController.UpdateHtmlText(htmlContent, this.htmlTextController.GetMaximumVersionHistory(this.PortalId));
                        break;
                }
            }
            catch (Exception exc)
            {
                Exceptions.LogException(exc);
                UI.Skins.Skin.AddModuleMessage(this, "Error occurred: ", exc.Message, ModuleMessage.ModuleMessageType.RedError);
                return;
            }

            // redirect back to portal
            if (redirect)
            {
                // Was Response.Redirect(url, true).
                this.BrowserNavigation.NavigateTo(this.navigationManager.NavigateURL(), true);
            }
        }

        /// <summary>Switches to the edit view.</summary>
        protected void OnEditClick()
        {
            try
            {
                this.DisplayEdit(this.editorStash);

                if (this.PhMasterContentVisible)
                {
                    this.DisplayMasterLanguageContent();
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Switches to the preview view.</summary>
        protected void OnPreviewClick()
        {
            try
            {
                if (this.PhEditVisible)
                {
                    this.editorStash = this.contentText;
                }

                this.DisplayPreview(this.PhEditVisible ? this.contentText : this.editorStash);
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Produces the cell texts of a history row, localizing the state and approved columns.</summary>
        /// <param name="sender">The grid.</param>
        /// <param name="e">The row event arguments.</param>
        protected void OnHistoryGridItemDataBound(object sender, DnnGridRowEventArgs e)
        {
            var item = e.Row;

            if (item.RowType == DnnGridRowType.DataRow)
            {
                var cells = new[]
                {
                    DnnGrid.FormatFieldValue(item.DataItem, "CreatedOnDate", null),
                    DnnGrid.FormatFieldValue(item.DataItem, "DisplayName", null),
                    DnnGrid.FormatFieldValue(item.DataItem, "StateName", null),
                    DnnGrid.FormatFieldValue(item.DataItem, "Approved", null),
                    DnnGrid.FormatFieldValue(item.DataItem, "Comment", null),
                };

                // Localize columns
                cells[2] = Localization.GetString(cells[2], this.LocalResourceFile);
                cells[3] = Localization.GetString(cells[3], this.LocalResourceFile);

                this.historyCellTexts[item] = cells;
            }
        }

        /// <summary>Handles the remove/rollback/preview commands of the versions grid.</summary>
        /// <param name="source">The grid.</param>
        /// <param name="e">The command event arguments.</param>
        protected void OnVersionsGridItemCommand(object source, DnnGridCommandEventArgs e)
        {
            try
            {
                HtmlTextInfo htmlContent;

                // disable delete button if user doesn't have delete rights???
                switch (e.CommandName.ToLowerInvariant())
                {
                    case "remove":
                        htmlContent = this.GetHTMLContent(e);
                        this.htmlTextController.DeleteHtmlText(this.ModuleId, htmlContent.ItemID);
                        break;
                    case "rollback":
                        htmlContent = this.GetHTMLContent(e);
                        htmlContent.ItemID = -1;
                        htmlContent.ModuleID = this.ModuleId;
                        htmlContent.WorkflowID = this.WorkflowID;
                        htmlContent.StateID = this.workflowManager.GetWorkflow(this.WorkflowID).FirstState.StateID;
                        this.htmlTextController.UpdateHtmlText(htmlContent, this.htmlTextController.GetMaximumVersionHistory(this.PortalId));
                        break;
                    case "preview":
                        htmlContent = this.GetHTMLContent(e);
                        this.DisplayPreview(htmlContent);
                        break;
                }

                if (e.CommandName.ToLowerInvariant() != "preview")
                {
                    var latestContent = this.htmlTextController.GetTopHtmlText(this.ModuleId, false, this.WorkflowID);
                    if (latestContent == null)
                    {
                        this.DisplayInitialContent(this.workflowManager.GetWorkflow(this.WorkflowID).FirstState);
                    }
                    else
                    {
                        this.DisplayContent(latestContent);

                        ////DisplayPreview(latestContent);
                        ////DisplayVersions();
                    }
                }

                // Module failed to load
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Computes the state of the command buttons of a versions row.</summary>
        /// <param name="sender">The grid.</param>
        /// <param name="e">The row event arguments.</param>
        protected void OnVersionsGridItemDataBound(object sender, DnnGridRowEventArgs e)
        {
            if (e.Row.RowType == DnnGridRowType.DataRow)
            {
                var htmlContent = e.Row.DataItem as HtmlTextInfo;
                var createdBy = "Default";

                if (htmlContent.CreatedByUserID != -1)
                {
                    var createdByByUser = UserController.GetUserById(this.hostSettings, this.PortalId, htmlContent.CreatedByUserID);
                    if (createdByByUser != null)
                    {
                        createdBy = createdByByUser.DisplayName;
                    }
                }

                // The three ImageButtons of the template column: the Web Forms handler found them by
                // walking e.Row.Cells[*].Controls, which the migrated grid has no equivalent for, so
                // their CommandArgument / Visible / OnClientClick are computed here and the template
                // of EditHtml.razor applies them.
                var state = new VersionRowState { CommandArgument = htmlContent.ItemID.ToString(CultureInfo.InvariantCulture), };

                foreach (var commandName in new[] { "rollback", "remove" })
                {
                    switch (commandName)
                    {
                        case "rollback":
                            // hide rollback for the first item
                            if (this.dgVersions.CurrentPageIndex == 0)
                            {
                                if (e.Row.RowIndex == 0)
                                {
                                    state.RollbackVisible = false;
                                    break;
                                }
                            }

                            state.RollbackVisible = true;

                            break;
                        case "remove":
                            var msg = this.GetLocalizedString("DeleteVersion.Confirm");
                            msg =
                                msg.Replace("[VERSION]", htmlContent.Version.ToString(CultureInfo.CurrentCulture)).Replace("[STATE]", htmlContent.StateName).Replace("[DATECREATED]", htmlContent.CreatedOnDate.ToString(CultureInfo.CurrentCulture))
                                    .Replace("[USERNAME]", createdBy);
                            state.RemoveConfirmMessage = msg;

                            // hide the delete button
                            var showDelete = this.UserInfo.IsSuperUser || PortalSecurity.IsInRole(this.PortalSettings.AdministratorRoleName);

                            if (!showDelete)
                            {
                                showDelete = htmlContent.IsPublished == false;
                            }

                            state.RemoveVisible = showDelete;
                            break;
                    }
                }

                this.versionRowStates[e.Row] = state;
            }
        }

        /// <summary>Rebinds the versions grid after the page changed.</summary>
        /// <param name="source">The grid.</param>
        /// <param name="e">The event arguments.</param>
        protected void OnVersionsGridPageIndexChanged(object source, EventArgs e)
        {
            this.DisplayVersions();
        }

        /// <summary>Gets the state of the command buttons of a versions row.</summary>
        /// <param name="row">The row.</param>
        /// <returns>The row's state, or <see langword="null"/> when it was not bound.</returns>
        protected VersionRowState GetVersionRowState(DnnGridRow row)
        {
            return row != null && this.versionRowStates.TryGetValue(row, out var state) ? state : null;
        }

        /// <summary>Gets the text of a cell of the history grid.</summary>
        /// <param name="row">The row.</param>
        /// <param name="cellIndex">The zero based index of the cell.</param>
        /// <returns>The (localized, for cells 2 and 3) cell text.</returns>
        protected string GetHistoryCellText(DnnGridRow row, int cellIndex)
        {
            return row != null && this.historyCellTexts.TryGetValue(row, out var cells) && cellIndex < cells.Length
                ? cells[cellIndex]
                : string.Empty;
        }

        /// <summary>Raises a command of the versions grid, confirming a removal first.</summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="row">The row the command comes from.</param>
        /// <returns>A task representing the operation.</returns>
        /// <remarks>
        /// Replaces the command bubbling of the Web Forms grid plus the <c>OnClientClick</c>
        /// <c>confirm()</c> the delete button carried.
        /// </remarks>
        protected async Task OnVersionCommandAsync(string commandName, DnnGridRow row)
        {
            var state = this.GetVersionRowState(row);

            if (string.Equals(commandName, "Remove", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(state?.RemoveConfirmMessage)
                && this.JsRuntime != null)
            {
                var confirmed = await this.JsRuntime.InvokeAsync<bool>("confirm", state.RemoveConfirmMessage);
                if (!confirmed)
                {
                    return;
                }
            }

            this.OnVersionsGridItemCommand(this.dgVersions, new DnnGridCommandEventArgs(commandName, state?.CommandArgument, row));
        }

        /// <summary>Switches the render mode of the text editor.</summary>
        /// <param name="value">The newly selected mode.</param>
        protected void OnRenderSelectedIndexChanged(string value)
        {
            this.selectedRenderValue = value;
            this.txtContent?.ChangeMode(this.selectedRenderValue);
        }

        /// <summary>Switches to the versions (history) view.</summary>
        protected void OnHistoryClick()
        {
            try
            {
                if (this.PhEditVisible)
                {
                    this.editorStash = this.contentText;
                }

                this.DisplayVersions();
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Shows or hides the content of the master language.</summary>
        protected void OnMasterContentClick()
        {
            try
            {
                this.PhMasterContentVisible = !this.PhMasterContentVisible;
                this.CmdMasterContentText = this.PhMasterContentVisible ?
                    Localization.GetString("cmdHideMasterContent", this.LocalResourceFile) :
                    Localization.GetString("cmdShowMasterContent", this.LocalResourceFile);

                if (this.PhMasterContentVisible)
                {
                    this.DisplayMasterLanguageContent();
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Handles a change of the text editor's content. Replaces reading <c>txtContent.Text</c> on post back.</summary>
        /// <param name="value">The new content.</param>
        protected void OnContentChanged(string value)
        {
            this.contentText = value;
        }

        /// <summary>Displays the history of an html content item in a grid in the preview section.</summary>
        /// <param name="htmlContent">Content of the HTML.</param>
        private void DisplayHistory(HtmlTextInfo htmlContent)
        {
            this.DnnSitePanelEditHtmlHistoryVisible = this.CurrentWorkflowType != WorkflowType.DirectPublish;
            this.FsEditHtmlHistoryVisible = this.CurrentWorkflowType != WorkflowType.DirectPublish;

            if (this.CurrentWorkflowType == WorkflowType.DirectPublish)
            {
                return;
            }

            var htmlLogging = this.htmlTextLogController.GetHtmlTextLog(htmlContent.ItemID);
            this.BindHistoryGrid(htmlLogging);

            this.DnnSitePanelEditHtmlHistoryVisible = htmlLogging.Count != 0;
            this.FsEditHtmlHistoryVisible = htmlLogging.Count != 0;
        }

        /// <summary>Displays the versions of the html content in the versions section.</summary>
        private void DisplayVersions()
        {
            var versions = this.htmlTextController.GetAllHtmlText(this.ModuleId);

            foreach (var item in versions)
            {
                item.StateName = this.GetLocalizedString(item.StateName);
            }

            this.BindVersionsGrid(versions);

            this.PhEditVisible = false;
            this.PhPreviewVisible = false;
            this.PhHistoryVisible = true;
            this.CmdEditEnabled = true;
            this.CmdPreviewEnabled = true;
            this.CmdHistoryEnabled = false;
            this.CmdMasterContentVisible = false;
            this.DdlRenderVisible = false;
        }

        /// <summary>Displays the content of the master language if localized content is enabled.</summary>
        private void DisplayMasterLanguageContent()
        {
            // Get master language
            var objModule = ModuleController.Instance.GetModule(this.ModuleId, this.TabId, false);
            if (objModule.DefaultLanguageModule != null)
            {
                var masterContent = this.htmlTextController.GetTopHtmlText(objModule.DefaultLanguageModule.ModuleID, false, this.WorkflowID);
                if (masterContent != null)
                {
                    // Was placeMasterContent.Controls.Add(new LiteralControl(...)); the markup
                    // renders this html as a MarkupString in the same place.
                    this.MasterContentHtml = HtmlTextController.FormatHtmlText(objModule.DefaultLanguageModule.ModuleID, this.FormatContent(masterContent.Content), this.Settings, this.PortalSettings, HttpContextSource.Current);
                }
            }
        }

        /// <summary>Displays the html content in the preview section.</summary>
        /// <param name="htmlContent">Content of the HTML.</param>
        private void DisplayContent(HtmlTextInfo htmlContent)
        {
            this.LblCurrentWorkflowInUseText = this.GetLocalizedString(htmlContent.WorkflowName);
            this.LblCurrentWorkflowStateText = this.GetLocalizedString(htmlContent.StateName);
            this.LblCurrentVersionText = htmlContent.Version.ToString(CultureInfo.CurrentCulture);
            this.contentText = this.FormatContent(htmlContent.Content);
            this.PhEditVisible = true;
            this.PhPreviewVisible = false;
            this.PhHistoryVisible = false;
            this.CmdEditEnabled = false;
            this.CmdPreviewEnabled = true;
            this.CmdHistoryEnabled = true;

            ////DisplayMasterLanguageContent();
            this.DisplayMasterContentButton();
            this.DdlRenderVisible = true;
        }

        private void DisplayMasterContentButton()
        {
            var objModule = ModuleController.Instance.GetModule(this.ModuleId, this.TabId, false);
            if (objModule.DefaultLanguageModule != null)
            {
                this.CmdMasterContentVisible = true;
                this.CmdMasterContentText = Localization.GetString("cmdShowMasterContent", this.LocalResourceFile);

                this.CmdMasterContentText = this.PhMasterContentVisible ?
                    Localization.GetString("cmdHideMasterContent", this.LocalResourceFile) :
                    Localization.GetString("cmdShowMasterContent", this.LocalResourceFile);
            }
        }

        /// <summary>Displays the content preview in the preview section.</summary>
        /// <param name="htmlContent">Content of the HTML.</param>
        private void DisplayPreview(HtmlTextInfo htmlContent)
        {
            this.LblPreviewVersionText = htmlContent.Version.ToString(CultureInfo.CurrentCulture);
            this.LblPreviewWorkflowInUseText = this.GetLocalizedString(htmlContent.WorkflowName);
            this.LblPreviewWorkflowStateText = this.GetLocalizedString(htmlContent.StateName);
            this.LitPreviewText = HtmlTextController.FormatHtmlText(this.ModuleId, htmlContent.Content, this.Settings, this.PortalSettings, HttpContextSource.Current);
            this.PhEditVisible = false;
            this.PhPreviewVisible = true;
            this.PhHistoryVisible = false;
            this.CmdEditEnabled = true;
            this.CmdPreviewEnabled = false;
            this.CmdHistoryEnabled = true;
            this.DisplayHistory(htmlContent);
            this.CmdMasterContentVisible = false;
            this.DdlRenderVisible = false;
        }

        /// <summary>Displays the preview in the preview section.</summary>
        /// <param name="htmlContent">Content of the HTML.</param>
        private void DisplayPreview(string htmlContent)
        {
            this.LitPreviewText = HtmlTextController.FormatHtmlText(this.ModuleId, htmlContent, this.Settings, this.PortalSettings, HttpContextSource.Current);
            this.DivPreviewVersionVisible = false;
            this.DivPreviewWorlflowVisible = false;

            this.DivPreviewWorkflowStateVisible = true;
            this.LblPreviewWorkflowStateText = this.GetLocalizedString("EditPreviewState");

            this.PhEditVisible = false;
            this.PhPreviewVisible = true;
            this.PhHistoryVisible = false;
            this.CmdEditEnabled = true;
            this.CmdPreviewEnabled = false;
            this.CmdHistoryEnabled = true;
            this.CmdMasterContentVisible = false;
            this.DdlRenderVisible = false;
        }

        private void DisplayEdit(string htmlContent)
        {
            this.contentText = htmlContent;
            this.PhEditVisible = true;
            this.PhPreviewVisible = false;
            this.PhHistoryVisible = false;
            this.CmdEditEnabled = false;
            this.CmdPreviewEnabled = true;
            this.CmdHistoryEnabled = true;
            this.DisplayMasterContentButton();
            this.DdlRenderVisible = true;
        }

        /// <summary>Displays the content but hide the editor if editing is locked from the current user.</summary>
        /// <param name="htmlContent">Content of the HTML.</param>
        /// <param name="lastPublishedContent">Last content of the published.</param>
        private void DisplayLockedContent(HtmlTextInfo htmlContent, HtmlTextInfo lastPublishedContent)
        {
            this.TxtContentVisible = false;
            this.CmdSaveVisible = false;

            ////cmdPreview.Enabled = false;
            this.DivSubmittedContentVisible = true;

            this.LblCurrentWorkflowInUseText = this.GetLocalizedString(htmlContent.WorkflowName);
            this.LblCurrentWorkflowStateText = this.GetLocalizedString(htmlContent.StateName);

            this.LitCurrentContentPreviewText = HtmlTextController.FormatHtmlText(this.ModuleId, htmlContent.Content, this.Settings, this.PortalSettings, HttpContextSource.Current);
            this.LblCurrentVersionText = htmlContent.Version.ToString(CultureInfo.CurrentCulture);
            this.DisplayVersions();

            if (lastPublishedContent != null)
            {
                this.DisplayPreview(lastPublishedContent);

                ////DisplayHistory(lastPublishedContent);
            }
            else
            {
                this.DnnSitePanelEditHtmlHistoryVisible = false;
                this.FsEditHtmlHistoryVisible = false;
                this.DisplayPreview(htmlContent.Content);
            }
        }

        /// <summary>Displays the initial content when a module is first added to the page.</summary>
        /// <param name="firstState">The first state.</param>
        private void DisplayInitialContent(WorkflowState firstState)
        {
            this.CmdHistoryEnabled = false;

            this.contentText = this.GetLocalizedString("AddContent");
            this.LitPreviewText = this.GetLocalizedString("AddContent");
            this.LblCurrentWorkflowInUseText = this.workflowManager.GetWorkflow(firstState.WorkflowID).WorkflowName;
            this.LblPreviewWorkflowInUseText = this.workflowManager.GetWorkflow(firstState.WorkflowID).WorkflowName;
            this.DivPreviewVersionVisible = false;

            this.DnnSitePanelEditHtmlHistoryVisible = false;
            this.FsEditHtmlHistoryVisible = false;

            this.DivCurrentWorkflowStateVisible = false;
            this.PhCurrentVersionVisible = false;
            this.DivPreviewWorkflowStateVisible = false;

            this.LblPreviewWorkflowStateText = firstState.StateName;
        }

        /// <summary>Formats the content to make it HTML safe.</summary>
        /// <param name="htmlContent">Content of the HTML.</param>
        /// <returns>The <paramref name="htmlContent"/> with relative paths updated and encoding issues resolved.</returns>
        private string FormatContent(string htmlContent)
        {
            // Was HttpUtility.HtmlDecode / HtmlEncode, which live in System.Web.
            var strContent = WebUtility.HtmlDecode(htmlContent);
            strContent = HtmlTextController.ManageRelativePaths(strContent, this.PortalSettings.HomeDirectory, "src");
            strContent = HtmlTextController.ManageRelativePaths(strContent, this.PortalSettings.HomeDirectory, "background");
            return WebUtility.HtmlEncode(strContent);
        }

        /// <summary>Gets the localized string from a resource file if it exists.</summary>
        /// <param name="str">The resource key.</param>
        /// <returns>The localized text or <paramref name="str"/>.</returns>
        private string GetLocalizedString(string str)
        {
            var localizedString = Localization.GetString(str, this.LocalResourceFile);
            return string.IsNullOrEmpty(localizedString) ? str : localizedString;
        }

        /// <summary>Gets the latest html content of the module.</summary>
        /// <returns>An <see cref="HtmlTextInfo"/> instance (with <see cref="HtmlTextInfo.ItemID"/> set to <c>-1</c> if there's no existing content).</returns>
        private HtmlTextInfo GetLatestHTMLContent()
        {
            var htmlContent = this.htmlTextController.GetTopHtmlText(this.ModuleId, false, this.WorkflowID);
            if (htmlContent == null)
            {
                htmlContent = new HtmlTextInfo();
                htmlContent.ItemID = -1;
                htmlContent.StateID = this.workflowManager.GetWorkflow(this.WorkflowID).FirstState.StateID;
                htmlContent.WorkflowID = this.WorkflowID;
                htmlContent.ModuleID = this.ModuleId;
            }

            return htmlContent;
        }

        /// <summary>Gets the last published version of this module.</summary>
        /// <param name="publishedStateID">The published state ID.</param>
        /// <returns>An <see cref="HtmlTextInfo"/> instance.</returns>
        private HtmlTextInfo GetLastPublishedVersion(int publishedStateID)
        {
            return (from version in this.htmlTextController.GetAllHtmlText(this.ModuleId) where version.StateID == publishedStateID orderby version.Version descending select version).ToList()[0];
        }

        private HtmlTextInfo GetHTMLContent(DnnGridCommandEventArgs e)
        {
            return this.htmlTextController.GetHtmlText(this.ModuleId, int.Parse(e.CommandArgument.ToString(), CultureInfo.InvariantCulture));
        }

        private void BindRenderItems()
        {
            if (this.renderItems.Count > 0)
            {
                return;
            }

            if (this.txtContent.IsRichEditorAvailable)
            {
                this.renderItems.Add(new DnnRenderItem(this.LocalizeText("liRichText"), "RICH"));
            }

            this.renderItems.Add(new DnnRenderItem(this.LocalizeText("liBasicText"), "BASIC"));
        }

        /// <summary>Applies the declaration the two <c>dnnweb:DnnGrid</c> tags carried.</summary>
        private void ConfigureGrids()
        {
            this.gridsConfigured = true;

            // dgHistory: five bound columns. They are template columns here so that the texts
            // OnHistoryGridItemDataBound produces — including the localization of cells 2 and 3 —
            // are what renders, which is what assigning Cells[i].Text did.
            this.dgHistory.Columns.Clear();
            this.dgHistory.Columns.Add(this.CreateHistoryColumn("Date", "CreatedOnDate", 0));
            this.dgHistory.Columns.Add(this.CreateHistoryColumn("User", "DisplayName", 1));
            this.dgHistory.Columns.Add(this.CreateHistoryColumn("State", "StateName", 2));
            this.dgHistory.Columns.Add(this.CreateHistoryColumn("Approved", "Approved", 3));
            this.dgHistory.Columns.Add(this.CreateHistoryColumn("Comment", "Comment", 4));
            this.dgHistory.RowDataBound += this.OnHistoryGridItemDataBound;

            // dgVersions: four bound columns plus the command template column.
            this.dgVersions.Columns.Clear();
            this.dgVersions.Columns.Add(new DnnGridBoundColumn { HeaderText = "Version", DataField = "Version", });
            this.dgVersions.Columns.Add(new DnnGridBoundColumn { HeaderText = "Date", DataField = "LastModifiedOnDate", });
            this.dgVersions.Columns.Add(new DnnGridBoundColumn { HeaderText = "User", DataField = "DisplayName", });
            this.dgVersions.Columns.Add(new DnnGridBoundColumn { HeaderText = "State", DataField = "StateName", });
            this.dgVersions.Columns.Add(new DnnGridTemplateColumn
            {
                HeaderTemplate = this.VersionsCommandHeaderTemplate,
                ItemTemplate = this.VersionsCommandItemTemplate,
            });
            this.dgVersions.RowCommand += this.OnVersionsGridItemCommand;
            this.dgVersions.RowDataBound += this.OnVersionsGridItemDataBound;
            this.dgVersions.PageIndexChanged += this.OnVersionsGridPageIndexChanged;

            if (this.historyBindPending)
            {
                this.historyBindPending = false;
                this.dgHistory.DataSource = this.historyDataSource;
                this.dgHistory.DataBind();
            }

            if (this.versionsBindPending)
            {
                this.versionsBindPending = false;
                this.dgVersions.DataSource = this.versionsDataSource;
                this.dgVersions.DataBind();
            }
        }

        /// <summary>Creates a column of the history grid.</summary>
        /// <param name="headerText">The header text of the column.</param>
        /// <param name="dataField">The bound field, used before the row was bound.</param>
        /// <param name="cellIndex">The index of the cell this column renders.</param>
        /// <returns>The column.</returns>
        private DnnGridColumn CreateHistoryColumn(string headerText, string dataField, int cellIndex)
        {
            return new DnnGridTemplateColumn
            {
                HeaderText = headerText,
                SortExpression = dataField,
                ItemTemplate = row => builder =>
                {
                    var text = this.historyCellTexts.ContainsKey(row)
                        ? this.GetHistoryCellText(row, cellIndex)
                        : DnnGrid.FormatFieldValue(row?.DataItem, dataField, null);
                    builder.AddContent(0, text);
                },
            };
        }

        /// <summary>Replaces <c>dgHistory.DataSource = x; dgHistory.DataBind();</c>.</summary>
        /// <param name="dataSource">The data to show.</param>
        /// <remarks>The grid component only exists after the first render, so a binding requested
        /// before that is applied by <see cref="OnAfterRenderAsync"/>.</remarks>
        private void BindHistoryGrid(object dataSource)
        {
            this.historyCellTexts.Clear();
            this.historyDataSource = dataSource;
            if (this.dgHistory != null && this.gridsConfigured)
            {
                this.dgHistory.DataSource = dataSource;
                this.dgHistory.DataBind();
            }
            else
            {
                this.historyBindPending = true;
            }
        }

        /// <summary>Replaces <c>dgVersions.DataSource = x; dgVersions.DataBind();</c>.</summary>
        /// <param name="dataSource">The data to show.</param>
        private void BindVersionsGrid(object dataSource)
        {
            this.versionRowStates.Clear();
            this.versionsDataSource = dataSource;
            if (this.dgVersions != null && this.gridsConfigured)
            {
                this.dgVersions.DataSource = dataSource;
                this.dgVersions.DataBind();
            }
            else
            {
                this.versionsBindPending = true;
            }
        }

        /// <summary>An item of the <c>ddlRender</c> drop down list. Replaces <c>System.Web.UI.WebControls.ListItem</c>.</summary>
        protected sealed class DnnRenderItem
        {
            /// <summary>Initializes a new instance of the <see cref="DnnRenderItem"/> class.</summary>
            /// <param name="text">The text of the item.</param>
            /// <param name="value">The value of the item.</param>
            public DnnRenderItem(string text, string value)
            {
                this.Text = text;
                this.Value = value;
            }

            /// <summary>Gets the text of the item.</summary>
            public string Text { get; }

            /// <summary>Gets the value of the item.</summary>
            public string Value { get; }
        }

        /// <summary>The state of the command buttons of a row of the versions grid.</summary>
        protected sealed class VersionRowState
        {
            /// <summary>Gets or sets the command argument, which is the item ID of the version.</summary>
            public string CommandArgument { get; set; }

            /// <summary>Gets or sets a value indicating whether the rollback button renders.</summary>
            public bool RollbackVisible { get; set; }

            /// <summary>Gets or sets a value indicating whether the remove button renders.</summary>
            public bool RemoveVisible { get; set; }

            /// <summary>Gets or sets the message confirming a removal.</summary>
            public string RemoveConfirmMessage { get; set; }
        }
    }
}
