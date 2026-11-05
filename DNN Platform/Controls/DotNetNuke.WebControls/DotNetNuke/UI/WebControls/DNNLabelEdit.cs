// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.IO;

    using DotNetNuke.UI.Utilities;
    using DotNetNuke.Web.Client;

    /// <summary>
    /// A label whose text can be edited in place and saved back through a callback.
    /// </summary>
    /// <remarks>
    /// Converted from <c>System.Web.UI.WebControls.Label</c> to <see cref="BaseWebControl"/>, which
    /// renders the same <c>span</c> (or <c>div</c>) element. The state-bag backed properties
    /// became ordinary backing fields keeping the very same defaults the view state getters
    /// returned, and <c>IPostBackEventHandler</c> became
    /// <see cref="IClientApiPostBackEventHandler"/>.
    /// </remarks>
    public class DNNLabelEdit : BaseWebControl, IClientApiPostBackEventHandler, IClientAPICallbackEventHandler, IDNNToolBar, IDNNToolBarSupportedActions
    {
        private static readonly string[] ToolBarActions =
        {
            "edit", "save", "cancel", "bold", "italic", "underline", "justifyleft", "justifycenter",
            "justifyright", "insertorderedlist", "insertunorderedlist", "outdent", "indent", "createlink",
        };

        private string toolBarId = string.Empty;

        private bool? lostFocusSave;
        private bool? editEnabled;
        private string labelEditScriptPath;
        private string systemImagesPath = "images/";
        private string eventName = "onclick";
        private bool? saveOnEnter;
        private UrlFormatType? urlFormat;
        private ClientAPICallBackResponse.CallBackTypeCode? callBackType;
        private string showToolBarEventName = "onmousemove";

        /// <summary>Initializes a new instance of the <see cref="DNNLabelEdit"/> class.</summary>
        public DNNLabelEdit()
        {
            this.PreRender += this.DNNLabelEdit_PreRender;
        }

        /// <summary>Handles the edited text being saved.</summary>
        /// <param name="source">The label raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        public delegate void DNNLabelEditEventHandler(object source, DNNLabelEditEventArgs e);

        /// <summary>Occurs when the edited text is saved.</summary>
        public event DNNLabelEditEventHandler UpdateLabel;

        /// <summary>Gets or sets the text of the label.</summary>
        /// <remarks>Replaces <c>Label.Text</c>.</remarks>
        [DefaultValue("")]
        [Localizable(true)]
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the client side behavior identifier of the label.</summary>
        [Bindable(true)]
        [ClientProperty]
        [DefaultValue("")]
        [ClientPropertyName("bid")]
        public string BehaviorID { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether losing focus saves the edit.</summary>
        [ClientProperty]
        [Category("Behavior")]
        [ClientPropertyName("blursave")]
        [DefaultValue(true)]
        public bool LostFocusSave
        {
            get
            {
                // A label driven by a tool bar never saves on blur; the tool bar's save button does.
                return !string.IsNullOrEmpty(this.ToolBarId) ? false : this.lostFocusSave ?? true;
            }

            set
            {
                this.lostFocusSave = value;
            }
        }

        /// <summary>Gets a value indicating whether the label renders without its editor.</summary>
        [Category("Appearance")]
        [Browsable(false)]
        public bool IsDownLevel
        {
            get
            {
                return !this.EditEnabled
                    || !ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.DHTML)
                    || !ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.XML);
            }
        }

        /// <summary>Gets or sets a value indicating whether the text can be edited.</summary>
        [Category("Behavior")]
        [DefaultValue(true)]
        public bool EditEnabled
        {
            get { return this.editEnabled ?? true; }
            set { this.editEnabled = value; }
        }

        /// <summary>Gets or sets the directory the Client API scripts are served from.</summary>
        [Category("Paths")]
        public string ClientAPIScriptPath
        {
            get { return ClientAPI.ScriptPath; }
            set { ClientAPI.ScriptPath = value; }
        }

        /// <summary>Gets or sets the directory the label's script is served from.</summary>
        [Category("Paths")]
        public string LabelEditScriptPath
        {
            get
            {
                return !string.IsNullOrEmpty(this.labelEditScriptPath)
                    ? this.labelEditScriptPath
                    : this.ClientAPIScriptPath;
            }

            set
            {
                this.labelEditScriptPath = value;
            }
        }

        /// <summary>Gets or sets the directory holding the label's system images.</summary>
        [Category("Paths")]
        [ClientProperty]
        [DefaultValue("images/")]
        [Description("Directory to find the images for the LabelEdit.  Need to have spacer.gif here!")]
        [ClientPropertyName("sysimgpath")]
        public string SystemImagesPath
        {
            get { return !string.IsNullOrEmpty(this.systemImagesPath) ? this.systemImagesPath : "images/"; }
            set { this.systemImagesPath = value; }
        }

        /// <summary>Gets or sets the client side event that starts an edit.</summary>
        [Description("Client-side event that will trigger an edit.  (onclick, ondblclick, none)")]
        [Bindable(true)]
        [ClientPropertyName("eventName")]
        [ClientProperty]
        [Category("Behavior")]
        [DefaultValue("onclick")]
        public string EventName
        {
            get { return !string.IsNullOrEmpty(this.eventName) ? this.eventName : "onclick"; }
            set { this.eventName = value; }
        }

        /// <summary>Gets or sets a value indicating whether pressing enter saves the edit.</summary>
        [Category("Behavior")]
        [DefaultValue(true)]
        [ClientPropertyName("saveonenter")]
        [ClientProperty]
        public bool SaveOnEnter
        {
            get { return this.saveOnEnter ?? true; }
            set { this.saveOnEnter = value; }
        }

        /// <summary>Gets or sets a value indicating whether the editor accepts multiple lines.</summary>
        [ClientProperty]
        [DefaultValue(false)]
        [Category("Behavior")]
        [ClientPropertyName("multiline")]
        public bool MultiLine { get; set; }

        /// <summary>Gets or sets how much of the server part of a URL is stripped when saving.</summary>
        [Category("Behavior")]
        [DefaultValue(1)]
        public UrlFormatType UrlFormat
        {
            get { return this.urlFormat ?? UrlFormatType.AbsoluteWithoutServer; }
            set { this.urlFormat = value; }
        }

        /// <summary>Gets or sets a value indicating whether the editor is a rich text editor.</summary>
        [ClientPropertyName("richtext")]
        [DefaultValue(false)]
        [ClientProperty]
        [Category("Behavior")]
        public bool RichTextEnabled { get; set; }

        /// <summary>Gets or sets the script invoked on each callback status change.</summary>
        [Bindable(true)]
        [ClientPropertyName("callbackSF")]
        [ClientProperty]
        [Category("Behavior")]
        public string CallbackStatusFunction { get; set; } = string.Empty;

        /// <summary>Gets or sets the kind of callback the label uses.</summary>
        [Category("Behavior")]
        [Description("Image to display during a callback")]
        [DefaultValue(0)]
        [Bindable(true)]
        public ClientAPICallBackResponse.CallBackTypeCode CallBackType
        {
            get { return this.callBackType ?? ClientAPICallBackResponse.CallBackTypeCode.Simple; }
            set { this.callBackType = value; }
        }

        /// <summary>Gets or sets the control whose children are posted with a callback.</summary>
        [Bindable(true)]
        [Description("This property allows for data on page to be posted in callback")]
        [Category("Behavior")]
        public string CallBackPostChildrenOf { get; set; } = string.Empty;

        /// <inheritdoc/>
        [Category("Behavior")]
        public string ToolBarId
        {
            get
            {
                return this.toolBarId;
            }

            set
            {
                this.toolBarId = value;
                TypeDescriptor.Refresh(this);
            }
        }

        /// <summary>Gets or sets the client side event that shows the tool bar.</summary>
        [DefaultValue("onmousemove")]
        [ClientPropertyName("tbEvent")]
        [ClientProperty]
        [Description("Allows the client-side event that displays the toolbar to be configured (onmouseover, onclick)")]
        [Category("Behavior")]
        public string ShowToolBarEventName
        {
            get { return !string.IsNullOrEmpty(this.showToolBarEventName) ? this.showToolBarEventName : "onmousemove"; }
            set { this.showToolBarEventName = value; }
        }

        /// <summary>Gets or sets the script invoked right before the edit is saved.</summary>
        [ClientProperty]
        [Bindable(true)]
        [Category("Behavior")]
        [ClientPropertyName("beforeSaveF")]
        public string BeforeSaveFunction { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class of the editor.</summary>
        [DefaultValue("")]
        [Bindable(true)]
        [ClientPropertyName("cssEdit")]
        [ClientProperty]
        [Category("Appearance")]
        public string LabelEditCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied while a save is in flight.</summary>
        [Category("Appearance")]
        [DefaultValue("")]
        [Bindable(true)]
        [ClientPropertyName("cssWork")]
        [ClientProperty]
        public string WorkCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied while the pointer is over the label.</summary>
        [ClientProperty]
        [ClientPropertyName("cssOver")]
        [DefaultValue("")]
        [Category("Appearance")]
        public string MouseOverCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether the label renders as a <c>div</c>.</summary>
        [DefaultValue(false)]
        [Category("Misc")]
        public bool RenderAsDiv { get; set; }

        /// <inheritdoc/>
        [Browsable(false)]
        public string[] Actions
        {
            get { return (string[])ToolBarActions.Clone(); }
        }

        /// <summary>Gets the tool bar actions this label understands.</summary>
        /// <remarks>
        /// The original built the array with an initializer of nine items and then assigned indices
        /// 9 through 13, which in VB silently resized nothing — it relied on the array literal being
        /// large enough. The full set of fourteen actions it meant to expose is listed here.
        /// </remarks>
        [Browsable(false)]
        internal string[] ToolBarSupportedActions
        {
            get { return this.Actions; }
        }

        /// <inheritdoc/>
        protected override string TagName
        {
            get { return this.RenderAsDiv ? "div" : "span"; }
        }

        /// <inheritdoc/>
        public string RaiseClientAPICallbackEvent(string eventArgument)
        {
            if (this.UrlFormat != UrlFormatType.None)
            {
                eventArgument = WebControls.StripUrlPaths(eventArgument, this.UrlFormat, this.Page);
            }

            var e = new DNNLabelEditEventArgs(eventArgument);
            this.UpdateLabel?.Invoke(this, e);
            return e.ReturnedText;
        }

        /// <inheritdoc/>
        public virtual void RaisePostBackEvent(string eventArgument)
        {
            this.UpdateLabel?.Invoke(this, new DNNLabelEditEventArgs(eventArgument));
        }

        /// <summary>Registers the client side scripts the label needs.</summary>
        public void RegisterClientScript()
        {
            if (this.IsDownLevel || !this.EditEnabled)
            {
                return;
            }

            ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_dom);
            ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_dom_positioning);
            ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_xmlhttp);
#pragma warning disable CS0618 // the obsolete overloads are kept until the host injects IClientResourceController
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.js", DotNetNuke.Web.Client.FileOrder.Js.DnnControlsLabelEdit);
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.dnnlabeledit.js", DotNetNuke.Web.Client.FileOrder.Js.DnnModalPopup);
#pragma warning restore CS0618

            if (!ClientAPI.UseExternalScripts)
            {
                // The two scripts used to be embedded assembly resources; they are now static web
                // assets of this Razor Class Library, referenced by URL.
                this.Page?.RegisterClientScriptInclude(
                    "dnn.controls.dnninputtext.js", WebControls.GetScriptUrl("dnn.controls.dnninputtext.js"));
                this.Page?.RegisterClientScriptInclude(
                    "dnn.controls.dnnrichtext.js", WebControls.GetScriptUrl("dnn.controls.dnnrichtext.js"));
            }
        }

        /// <summary>Collects the properties the client script needs to drive the editor.</summary>
        /// <returns>The marshalled properties.</returns>
        protected Hashtable MarshalledProperties()
        {
            var properties = new Hashtable();
            if (!this.EditEnabled)
            {
                return properties;
            }

            if (ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.XMLHTTP))
            {
                properties.Add(
                    "callback",
                    ClientAPI.GetCallbackEventReference(
                        this,
                        "'[TEXT]'",
                        "this.callBackSuccess",
                        "this",
                        "this.callBackFail",
                        "this.callBackStatus",
                        this.CallBackPostChildrenOf,
                        this.CallBackType));
            }

            if (!string.IsNullOrEmpty(this.ToolBarId))
            {
                properties.Add("tbId", this.ToolBarId);
                if (!string.IsNullOrEmpty(this.UniqueID.Replace(this.ID ?? string.Empty, string.Empty, StringComparison.Ordinal)))
                {
                    properties.Add("nsPrefix", this.NamingContainer?.UniqueId);
                }
            }

            return properties;
        }

        /// <inheritdoc/>
        protected override void Render(TextWriter writer)
        {
            base.Render(writer);
            if (!this.IsDownLevel && this.EditEnabled)
            {
                var properties = this.MarshalledProperties();
                var json = ScriptGenerator.GetMarshalledPropertyJSON(this, properties);
                RegisterInitialize(this, "initLabelEdit", json);
            }
        }

        /// <inheritdoc/>
        protected override void RenderContents(TextWriter writer)
        {
            // Replaces Label.RenderContents, which wrote the Text into the element.
            if (!string.IsNullOrEmpty(this.Text))
            {
                writer.Write(this.Text);
            }

            base.RenderContents(writer);
        }

        /// <summary>Prepares the label for rendering.</summary>
        /// <param name="sender">The label raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        private void DNNLabelEdit_PreRender(object sender, EventArgs e)
        {
            this.RegisterClientScript();
        }
    }
}
