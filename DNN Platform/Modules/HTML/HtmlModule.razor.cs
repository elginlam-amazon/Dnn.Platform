// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Html
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Content.Workflow;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Modules.Html.Components;
    using DotNetNuke.Security;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Personalization;
    using DotNetNuke.UI.WebControls;
    using DotNetNuke.UI.WebControls.Components;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    /// <summary>The HtmlModule Class provides the UI for displaying the HTML.</summary>
    /// <remarks>
    /// Was <c>HtmlModule.ascx</c> + <c>HtmlModule.ascx.cs</c> +
    /// <c>HtmlModule.ascx.designer.cs</c>; the markup is <c>HtmlModule.razor</c> and the two
    /// designer-generated control fields are the <c>lblContent</c> / <c>editorDnnToobar</c>
    /// members below (the 14 tool bar buttons are the <see cref="ToolBarButtons"/> collection,
    /// which is what the WebForms <c>Buttons</c> child collection was).
    /// </remarks>
    public partial class HtmlModule : HtmlModuleBase, IActionable
    {
        /// <summary>
        /// Path of the JS module of this extension, served as an RCL static web asset.
        /// </summary>
        private const string InteropModulePath = "./_content/DotNetNuke.Modules.Html/js/dnn.modules.html.js";

        private readonly IWorkflowManager workflowManager = WorkflowManager.Instance;
        private readonly INavigationManager navigationManager;
        private readonly HtmlTextController htmlTextController;

        /// <summary>The tool bar the 14 buttons of the inline editor belong to.</summary>
        /// <remarks>Replaces the designer's <c>editorDnnToobar</c> field.</remarks>
        private DnnToolBar editorDnnToobar;

        private DnnLabelEdit lblContent;
        private IJSObjectReference interopModule;
        private Microsoft.AspNetCore.Http.HttpContext capturedContext;
        private List<DNNToolBarButton> toolBarButtons;
        private bool editorEnabled;
        private bool inlineEditing;
        private bool beginEditRequested;
        private int workflowId;
        private string contentString = string.Empty;
        private string formattedContent = string.Empty;
        private string labelCssClass = "Normal";

        /// <summary>Initializes a new instance of the <see cref="HtmlModule"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with INavigationManager. Scheduled removal in v12.0.0.")]
        public HtmlModule()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="HtmlModule"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="htmlTextController">The HTML/Text controller.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with HtmlModuleSettingsRepository. Scheduled removal in v12.0.0.")]
        public HtmlModule(INavigationManager navigationManager, HtmlTextController htmlTextController)
            : this(null, navigationManager, htmlTextController)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="HtmlModule"/> class.</summary>
        /// <param name="settingsRepository">The settings repository.</param>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="htmlTextController">The HTML/Text controller.</param>
        public HtmlModule(HtmlModuleSettingsRepository settingsRepository, INavigationManager navigationManager, HtmlTextController htmlTextController)
            : base(settingsRepository)
        {
            this.navigationManager = navigationManager ?? this.DependencyProvider.GetRequiredService<INavigationManager>();
            this.htmlTextController = htmlTextController ?? this.DependencyProvider.GetRequiredService<HtmlTextController>();
        }

        /// <summary>Gets moduleActions is an interface property that returns the module actions collection for the module.</summary>
        public ModuleActionCollection ModuleActions =>
            new()
            {
                {
                    this.GetNextActionID(),
                    Localization.GetString(ModuleActionType.AddContent, this.LocalResourceFile),
                    ModuleActionType.AddContent,
                    string.Empty,
                    string.Empty,
                    this.EditUrl(),
                    false,
                    SecurityAccessLevel.Edit,
                    true,
                    false
                },
                {
                    this.GetNextActionID(),
                    Localization.GetString("MyWork.Action", this.LocalResourceFile),
                    "MyWork.Action",
                    string.Empty,
                    "view.gif",
                    this.EditUrl("MyWork"),
                    false,
                    SecurityAccessLevel.Edit,
                    true,
                    false
                },
            };

        /// <summary>Gets the 14 buttons of the inline editor's tool bar.</summary>
        /// <remarks>
        /// Replaces the <c>DNN:DNNToolBarButton</c> children declared in the markup (and the
        /// designer's <c>tbEdit</c> … <c>tbCreateLink</c> fields): every ID, control action,
        /// tooltip and CSS class is preserved, which is what keeps the
        /// <c>&lt;ToolTip&gt;.ToolTip</c> resource keys of <c>HtmlModule.resx</c> resolving.
        /// </remarks>
        protected IReadOnlyList<DNNToolBarButton> ToolBarButtons => this.toolBarButtons ??= BuildToolBarButtons();

        /// <summary>Gets the actions the inline editor understands.</summary>
        /// <remarks>
        /// Replaces the <c>ToolBarId="editorDnnToobar"</c> attachment of the markup, through which
        /// the WebForms tool bar read <c>DNNLabelEdit</c>'s
        /// <see cref="IDNNToolBarSupportedActions.Actions"/> to decide which buttons to render.
        /// </remarks>
        protected string[] SupportedToolBarActions { get; } =
        {
            "edit", "save", "cancel", "bold", "italic", "underline", "justifyleft", "justifycenter",
            "justifyright", "insertorderedlist", "insertunorderedlist", "outdent", "indent", "createlink",
        };

        /// <summary>Gets the CSS class of the content wrapper.</summary>
        /// <remarks>
        /// Was <c>lblContent.CssClass</c>: <c>Normal</c>, with <c>normalCheckBox</c> appended when
        /// form decoration is disabled.
        /// </remarks>
        protected string LabelCssClass => this.labelCssClass;

        /// <summary>Gets or sets the inline editor component, replacing the designer's <c>lblContent</c> field.</summary>
        /// <remarks>
        /// The setter is the <c>@ref</c> capture of the markup. It starts the edit the tool bar's
        /// "edit" action asked for, which is what <c>dnn.controls.dnnlabeledit.js</c> did when the
        /// tool bar dispatched that action to it.
        /// </remarks>
        protected DnnLabelEdit LabelEditor
        {
            get => this.lblContent;

            set
            {
                this.lblContent = value;
                if (value is not null && this.beginEditRequested)
                {
                    this.beginEditRequested = false;
                    value.BeginEdit();
                }
            }
        }

        /// <summary>Page_Init runs when the control is initialized.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            // The UpdateLabel handler is wired through the component's OnUpdateLabel callback in
            // the markup; the tool bar buttons (whose ToolTips OnLoad localizes) are built here,
            // which is the phase the markup's DNNToolBarButton children were created in.
            _ = this.ToolBarButtons;

            // HttpContext is null for the rest of a Blazor Server circuit, so it is captured while
            // a request is still in flight; this is the instance FormatHtmlText is given below.
            this.capturedContext ??= HttpContextSource.Current;

            this.editorEnabled = this.PortalSettings.InlineEditorEnabled;
            try
            {
                this.workflowId = this.htmlTextController.GetWorkflow(this.ModuleId, this.TabId, this.PortalId).Value;

                // Add an Action Event Handler to the Skin
                this.AddActionHandler(this.ModuleAction_Click);
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Page_Load runs when the control is loaded.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                // edit in place
                if (this.editorEnabled && this.IsEditable && Personalization.GetUserMode() == PortalSettings.Mode.Edit)
                {
                    this.editorEnabled = true;
                }
                else
                {
                    this.editorEnabled = false;
                }

                // get content
                HtmlTextInfo htmlTextInfo = null;
                this.contentString = string.Empty;

                htmlTextInfo = this.htmlTextController.GetTopHtmlText(this.ModuleId, !this.IsEditable, this.workflowId);

                if (htmlTextInfo != null)
                {
                    // don't decode yet (this is done in FormatHtmlText)
                    this.contentString = htmlTextInfo.Content;
                }
                else
                {
                    // get default content from resource file
                    if (Personalization.GetUserMode() == PortalSettings.Mode.Edit)
                    {
                        if (this.editorEnabled)
                        {
                            this.contentString = Localization.GetString("AddContentFromToolBar.Text", this.LocalResourceFile);
                        }
                        else
                        {
                            this.contentString = Localization.GetString("AddContentFromActionMenu.Text", this.LocalResourceFile);
                        }
                    }
                    else
                    {
                        // hide the module if no content and in view mode
                        if (this.ContainerControl is DotNetNuke.UI.IDnnControl container)
                        {
                            container.Visible = false;
                        }
                    }
                }

                // token replace
                this.editorEnabled = this.editorEnabled && !this.Settings.ReplaceTokens;

                // localize toolbar
                if (this.editorEnabled)
                {
                    foreach (DNNToolBarButton button in this.ToolBarButtons)
                    {
                        button.ToolTip = Localization.GetString(button.ToolTip + ".ToolTip", this.LocalResourceFile);
                    }
                }
                else
                {
                    // The tool bar is not rendered at all, which is what Visible = false did.
                    this.inlineEditing = false;
                }

                // add content to module
                this.formattedContent = HtmlTextController.FormatHtmlText(this.ModuleId, this.contentString, this.Settings, this.PortalSettings, this.capturedContext ?? HttpContextSource.Current);

                // set normalCheckBox on the content wrapper to prevent form decoration if its disabled.
                this.labelCssClass = "Normal";
                if (!this.Settings.UseDecorate)
                {
                    this.labelCssClass = string.Format(CultureInfo.InvariantCulture, "{0} normalCheckBox", this.labelCssClass);
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Replaces the WebForms re-initialization block, which on an asynchronous post back
        /// registered a client script resetting <c>dnn.controls.controls[ClientID]</c> so that the
        /// inline editor rebound to the markup the partial update had just replaced. A Blazor
        /// re-render is the equivalent of that partial update, so the same reset - carrying the
        /// very same <c>dnn.controls</c> guard, see <c>wwwroot/js/dnn.modules.html.js</c> - is
        /// issued through JS interop after every render but the first.
        /// </remarks>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (firstRender)
            {
                return;
            }

            try
            {
                var module = await this.GetInteropModuleAsync();
                if (module is not null)
                {
                    await module.InvokeVoidAsync("resetInlineEditor", this.lblContent?.ClientId ?? "lblContent");
                }
            }
            catch (JSException)
            {
                // The client script is not present (the original's guard on dnn.controls); there
                // is nothing to reset.
            }
        }

        /// <summary>Builds the 14 buttons the markup declared, in their original order.</summary>
        /// <returns>The buttons of the tool bar.</returns>
        private static List<DNNToolBarButton> BuildToolBarButtons()
        {
            return new List<DNNToolBarButton>
            {
                new DNNToolBarButton { ID = "tbEdit", ControlAction = "edit", ToolTip = "Edit", CssClass = "eipbutton_edit" },
                new DNNToolBarButton { ID = "tbSave", ControlAction = "save", ToolTip = "Save", CssClass = "eipbutton_save" },
                new DNNToolBarButton { ID = "tbCancel", ControlAction = "cancel", ToolTip = "Cancel", CssClass = "eipbutton_cancel" },
                new DNNToolBarButton { ID = "tbBold", ControlAction = "bold", ToolTip = "Bold", CssClass = "eipbutton_bold" },
                new DNNToolBarButton { ID = "tbItalic", ControlAction = "italic", ToolTip = "Italic", CssClass = "eipbutton_italic" },
                new DNNToolBarButton { ID = "tbUnderline", ControlAction = "underline", ToolTip = "Underline", CssClass = "eipbutton_underline" },
                new DNNToolBarButton { ID = "tbJustifyLeft", ControlAction = "justifyleft", ToolTip = "JustifyLeft", CssClass = "eipbutton_justifyleft" },
                new DNNToolBarButton { ID = "tbJustifyCenter", ControlAction = "justifycenter", ToolTip = "JustifyCenter", CssClass = "eipbutton_justifycenter" },
                new DNNToolBarButton { ID = "tbJustifyRight", ControlAction = "justifyright", ToolTip = "JustifyRight", CssClass = "eipbutton_justifyright" },
                new DNNToolBarButton { ID = "tbOrderedList", ControlAction = "insertorderedlist", ToolTip = "OrderedList", CssClass = "eipbutton_orderedlist" },
                new DNNToolBarButton { ID = "tbUnorderedList", ControlAction = "insertunorderedlist", ToolTip = "UnorderedList", CssClass = "eipbutton_unorderedlist" },
                new DNNToolBarButton { ID = "tbOutdent", ControlAction = "outdent", ToolTip = "Outdent", CssClass = "eipbutton_outdent" },
                new DNNToolBarButton { ID = "tbIndent", ControlAction = "indent", ToolTip = "Indent", CssClass = "eipbutton_indent" },
                new DNNToolBarButton { ID = "tbCreateLink", ControlAction = "createlink", ToolTip = "CreateLink", CssClass = "eipbutton_createlink" },
            };
        }

        /// <summary>Handles a button of the inline editor's tool bar being clicked.</summary>
        /// <param name="button">The button that was clicked.</param>
        /// <returns>A task that completes once the action has run.</returns>
        /// <remarks>
        /// Replaces the client side dispatch of <c>dnn.controls.dnntoolbarstub.js</c>, which handed
        /// the control action of the clicked button to the label edit behavior: <c>edit</c> started
        /// the in-place edit, <c>save</c> committed it (raising <c>UpdateLabel</c> on the server),
        /// <c>cancel</c> abandoned it, and the eleven formatting actions were
        /// <c>document.execCommand</c> calls on the edited element, which is what
        /// <c>execEditorAction</c> performs in <c>wwwroot/js/dnn.modules.html.js</c>.
        /// </remarks>
        private async Task ToolBarButtonClickAsync(DNNToolBarButton button)
        {
            if (button is null)
            {
                return;
            }

            switch (button.ControlAction)
            {
                case "edit":
                    this.inlineEditing = true;

                    // The editor component does not exist yet; its @ref capture starts the edit.
                    this.beginEditRequested = true;
                    this.lblContent?.BeginEdit();
                    break;

                case "save":
                    if (this.lblContent is not null)
                    {
                        await this.lblContent.SaveAsync();
                    }

                    this.inlineEditing = false;

                    // Re-read the saved content, as the full page the original's save produced did.
                    this.OnLoad(EventArgs.Empty);
                    break;

                case "cancel":
                    this.lblContent?.CancelEdit();
                    this.inlineEditing = false;
                    break;

                default:
                    try
                    {
                        var module = await this.GetInteropModuleAsync();
                        if (module is not null)
                        {
                            await module.InvokeVoidAsync(
                                "execEditorAction",
                                this.lblContent?.ClientId ?? "lblContent",
                                button.ControlAction);
                        }
                    }
                    catch (JSException)
                    {
                        // The client script is not present (the original's guard on dnn.controls).
                    }

                    break;
            }
        }

        /// <summary>  lblContent_UpdateLabel allows for inline editing of content.</summary>
        /// <param name="e">The arguments carrying the edited text.</param>
        /// <returns>A task that completes once the content has been saved.</returns>
        private Task LblContentUpdateLabelAsync(DNNLabelEditEventArgs e)
        {
            this.LblContent_UpdateLabel(this.lblContent, e);
            return Task.CompletedTask;
        }

        /// <summary>  lblContent_UpdateLabel allows for inline editing of content.</summary>
        private void LblContent_UpdateLabel(object source, DNNLabelEditEventArgs e)
        {
            try
            {
                // verify security
                if (!this.PortalSettings.AllowJsInHtmlModule && HtmlUtils.ContainsJavaScript(e.Text))
                {
                    throw new SecurityException();
                }
                else if (this.editorEnabled && this.IsEditable && Personalization.GetUserMode() == PortalSettings.Mode.Edit)
                {
                    // get content
                    HtmlTextInfo objContent = this.htmlTextController.GetTopHtmlText(this.ModuleId, false, this.workflowId);
                    if (objContent == null)
                    {
                        objContent = new HtmlTextInfo();
                        objContent.ItemID = -1;
                    }

                    // set content attributes
                    objContent.ModuleID = this.ModuleId;

                    // Was Server.HtmlEncode; identical semantics without System.Web.
                    objContent.Content = System.Net.WebUtility.HtmlEncode(e.Text);
                    objContent.WorkflowID = this.workflowId;
                    objContent.StateID = this.workflowManager.GetWorkflow(this.workflowId).FirstState.StateID;

                    // save the content
                    this.htmlTextController.UpdateHtmlText(objContent, this.htmlTextController.GetMaximumVersionHistory(this.PortalId));
                }
                else
                {
                    throw new SecurityException();
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>  ModuleAction_Click handles all ModuleAction events raised from the action menu.</summary>
        private void ModuleAction_Click(object sender, ActionEventArgs e)
        {
            try
            {
                if (e.Action.CommandArgument == "publish")
                {
                    // verify security
                    if (this.IsEditable && Personalization.GetUserMode() == PortalSettings.Mode.Edit)
                    {
                        // get content
                        HtmlTextInfo objContent = this.htmlTextController.GetTopHtmlText(this.ModuleId, false, this.workflowId);
                        var workflow = this.workflowManager.GetWorkflow(this.workflowId);
                        if (objContent.StateID == workflow.FirstState.StateID)
                        {
                            // publish content
                            objContent.StateID = workflow.LastState.StateID;

                            // save the content
                            this.htmlTextController.UpdateHtmlText(objContent, this.htmlTextController.GetMaximumVersionHistory(this.PortalId));

                            // refresh page (was Response.Redirect(url, true))
                            this.Navigation.NavigateTo(this.navigationManager.NavigateURL(), forceLoad: true);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Imports the JS module of this extension on first use and caches it.</summary>
        /// <returns>The module reference, or <see langword="null"/> when there is no JS runtime.</returns>
        private async Task<IJSObjectReference> GetInteropModuleAsync()
        {
            if (this.JsRuntime is null)
            {
                return null;
            }

            return this.interopModule ??= await this.JsRuntime.InvokeAsync<IJSObjectReference>("import", InteropModulePath);
        }

        /// <summary>Gets or sets the JS runtime the inline editor's client seam is called through.</summary>
        [Inject]
        private IJSRuntime JsRuntime { get; set; }

        /// <summary>Gets or sets the navigator the publish action redirects through.</summary>
        [Inject]
        private Microsoft.AspNetCore.Components.NavigationManager Navigation { get; set; }
    }
}
