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
    /// A text box that suggests completions fetched from the server as the user types.
    /// </summary>
    /// <remarks>
    /// Converted from <c>System.Web.UI.WebControls.TextBox</c> to <see cref="BaseWebControl"/>: the
    /// control renders its <c>input</c> element itself, since the WebForms text box has no .NET 10
    /// equivalent. The state-bag backed properties became ordinary backing fields keeping the
    /// very same defaults the view state getters returned, and <c>IPostBackEventHandler</c> became
    /// <see cref="IClientApiPostBackEventHandler"/>.
    /// </remarks>
    public class DNNTextSuggest : BaseWebControl, IClientApiPostBackEventHandler, IClientAPICallbackEventHandler
    {
        private DNNNodeCollection nodes;
        private string textSuggestScriptPath;
        private string systemImagesPath = "images/";
        private int minCharacterLookup = 1;
        private int maxSuggestRows = 10;
        private int lookupDelay = 500;
        private int lostFocusDelay = 500;
        private ClientAPICallBackResponse.CallBackTypeCode? callBackType;

        /// <summary>Initializes a new instance of the <see cref="DNNTextSuggest"/> class.</summary>
        public DNNTextSuggest()
            : base("input")
        {
            this.PreRender += this.DNNTextSuggest_PreRender;
        }

        /// <summary>Handles a suggestion being clicked.</summary>
        /// <param name="source">The control raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        public delegate void DNNDNNNodeClickHandler(object source, DNNTextSuggestEventArgs e);

        /// <summary>Handles the suggestions having to be supplied.</summary>
        /// <param name="source">The control raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        public delegate void DNNTextSuggestEventHandler(object source, DNNTextSuggestEventArgs e);

        /// <summary>Occurs when a suggestion is clicked.</summary>
        public event DNNDNNNodeClickHandler NodeClick;

        /// <summary>Occurs when the suggestions have to be supplied.</summary>
        public event DNNTextSuggestEventHandler PopulateOnDemand;

        /// <summary>The character pair that surrounds the identifier of a selected suggestion.</summary>
        public enum eIDTokenChar
        {
            /// <summary>No identifier is appended.</summary>
            None = 0,

            /// <summary>The identifier is surrounded by square brackets.</summary>
            Brackets = 1,

            /// <summary>The identifier is surrounded by parentheses.</summary>
            Paranthesis = 2,
        }

        /// <summary>Gets or sets the text of the box.</summary>
        /// <remarks>Replaces <c>TextBox.Text</c>.</remarks>
        [DefaultValue("")]
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the client side behavior identifier of the box.</summary>
        [DefaultValue("")]
        [ClientProperty]
        [Bindable(true)]
        [ClientPropertyName("bid")]
        public string BehaviorID { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether down-level rendering is forced.</summary>
        [DefaultValue(false)]
        [Description("Allows developer to force the rendering of the Menu in DownLevel mode")]
        [Category("Behavior")]
        public bool ForceDownLevel { get; set; }

        /// <summary>Gets a value indicating whether the box renders without its script.</summary>
        [Browsable(false)]
        public bool IsDownLevel
        {
            get
            {
                return this.ForceDownLevel
                    || !ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.DHTML);
            }
        }

        /// <summary>Gets or sets the directory the Client API scripts are served from.</summary>
        [Description("Location of ClientAPI js files")]
        [Category("Paths")]
        [DefaultValue("")]
        public string ClientAPIScriptPath
        {
            get { return ClientAPI.ScriptPath; }
            set { ClientAPI.ScriptPath = value; }
        }

        /// <summary>Gets or sets the directory the box's script is served from.</summary>
        [Category("Paths")]
        [Description("Location of dnn.controls.DNNTextSuggest.js file")]
        [DefaultValue("")]
        public string TextSuggestScriptPath
        {
            get
            {
                return !string.IsNullOrEmpty(this.textSuggestScriptPath)
                    ? this.textSuggestScriptPath
                    : this.ClientAPIScriptPath;
            }

            set
            {
                this.textSuggestScriptPath = value;
            }
        }

        /// <summary>Gets or sets the directory holding the box's system images.</summary>
        [ClientProperty]
        [Description("Directory to find the images for the TextSuggest.")]
        [ClientPropertyName("sysimgpath")]
        [DefaultValue("images/")]
        [Category("Paths")]
        public string SystemImagesPath
        {
            get { return !string.IsNullOrEmpty(this.systemImagesPath) ? this.systemImagesPath : "images/"; }
            set { this.systemImagesPath = value; }
        }

        /// <summary>Gets the suggestions of the box.</summary>
        public DNNNodeCollection DNNNodes
        {
            get
            {
                if (this.nodes is null)
                {
                    this.nodes = new DNNNodeCollection(this.ClientID);
                }

                return this.nodes;
            }
        }

        /// <summary>Gets the suggestions the user picked, parsed back out of the text.</summary>
        [Browsable(false)]
        public DNNNodeCollection SelectedNodes
        {
            get
            {
                var selected = new DNNNodeCollection(string.Empty);

                // The original only parsed on a post back; the posted text is now simply whatever
                // Text holds, which the host assigns from the request.
                foreach (var entry in (this.Text ?? string.Empty).Split(this.Delimiter))
                {
                    if (entry.Length <= 0)
                    {
                        continue;
                    }

                    var text = entry;
                    var key = string.Empty;
                    if (this.IDToken == eIDTokenChar.Brackets)
                    {
                        var openIndex = entry.LastIndexOf(" [", StringComparison.Ordinal);
                        var startIndex = openIndex + " [".Length;
                        var closeIndex = entry.LastIndexOf(']');
                        if (openIndex > -1 && closeIndex > startIndex)
                        {
                            text = entry.Substring(0, openIndex);
                            key = entry.Substring(startIndex, closeIndex - startIndex);
                        }
                    }

                    selected.Add(new DNNNode(text) { Key = key });
                }

                return selected;
            }
        }

        /// <summary>Gets or sets the frame navigated when a suggestion navigates.</summary>
        [ClientPropertyName("target")]
        [Description("If ClickAction for a node is set to navigate this is the target frame that will do the navigating.")]
        [Category("Behavior")]
        [ClientProperty]
        [DefaultValue("")]
        public string Target { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class of a suggestion.</summary>
        [ClientProperty]
        [Description("Default Classname for node.")]
        [Category("Appearance")]
        [ClientPropertyName("css")]
        [DefaultValue("")]
        public string DefaultNodeCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class of a child suggestion.</summary>
        [Category("Appearance")]
        [ClientPropertyName("csschild")]
        [ClientProperty]
        [DefaultValue("")]
        [Description("Default Classname for child node.")]
        public string DefaultChildNodeCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied while the pointer is over a suggestion.</summary>
        [Category("Appearance")]
        [DefaultValue("")]
        [Description("Default Classname for node when hovered.")]
        [ClientPropertyName("csshover")]
        [ClientProperty]
        public string DefaultNodeCssClassOver { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied to the selected suggestion.</summary>
        [Description("Default Classname for node when selected.")]
        [Category("Appearance")]
        [DefaultValue("")]
        [ClientProperty]
        [ClientPropertyName("csssel")]
        public string DefaultNodeCssClassSelected { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class of the container holding the suggestions.</summary>
        [Description("Default Classname container holding all of suggestion nodes.")]
        [Category("Appearance")]
        [ClientProperty]
        [DefaultValue("")]
        [ClientPropertyName("tscss")]
        public string TextSuggestCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the script run when a suggestion is clicked.</summary>
        [DefaultValue("")]
        [ClientProperty]
        [ClientPropertyName("js")]
        [Description("Allows you to have a common JS function be invoked for all nodes, unless a different JS function is provided on the node level.")]
        [Category("Behavior")]
        public string JSFunction { get; set; } = string.Empty;

        /// <summary>Gets or sets the script invoked on each callback status change.</summary>
        [ClientProperty]
        [Category("Behavior")]
        [ClientPropertyName("callbackSF")]
        [DefaultValue("")]
        [Description("If callbacks are supported/enabled, this javascript function will be invoked with each status change of the xmlhttp request.")]
        public string CallbackStatusFunction { get; set; } = string.Empty;

        /// <summary>Gets or sets the character separating multiple entries in the text.</summary>
        [ClientPropertyName("del")]
        [DefaultValue("")]
        [Description("Specifies a delimiter to be used to allow for multiple entries to be added.")]
        [Category("Behavior")]
        [ClientProperty]
        public char Delimiter { get; set; }

        /// <summary>Gets or sets the character pair surrounding the identifier of a suggestion.</summary>
        [Category("Behavior")]
        [DefaultValue(0)]
        [Description("Specifies a type of character to be used to surround/delimit the underlying value/id of the selected item.")]
        public eIDTokenChar IDToken { get; set; }

        /// <summary>Gets or sets the number of characters typed before a lookup runs.</summary>
        [Description("Minimum number of characters typed before a lookup will be invoked")]
        [ClientProperty]
        [DefaultValue(1)]
        [ClientPropertyName("minChar")]
        [Category("Behavior")]
        public int MinCharacterLookup
        {
            get { return this.minCharacterLookup; }
            set { this.minCharacterLookup = value; }
        }

        /// <summary>Gets or sets the maximum number of suggestions shown.</summary>
        [Category("Behavior")]
        [Description("Maximum number of rows to display.")]
        [ClientPropertyName("maxRows")]
        [ClientProperty]
        [DefaultValue(10)]
        public int MaxSuggestRows
        {
            get { return this.maxSuggestRows; }
            set { this.maxSuggestRows = value; }
        }

        /// <summary>Gets or sets the delay, in milliseconds, before a lookup runs.</summary>
        [DefaultValue(500)]
        [Description("Number of milliseconds to wait after keypress before a lookup takes place.")]
        [ClientProperty]
        [ClientPropertyName("ludelay")]
        [Category("Behavior")]
        public int LookupDelay
        {
            get { return this.lookupDelay; }
            set { this.lookupDelay = value; }
        }

        /// <summary>Gets or sets the delay, in milliseconds, before the suggestions hide.</summary>
        [Description("Number of milliseconds to wait after focus is lost before a hiding the results.")]
        [DefaultValue(500)]
        [Category("Behavior")]
        [ClientPropertyName("lfdelay")]
        [ClientProperty]
        public int LostFocusDelay
        {
            get { return this.lostFocusDelay; }
            set { this.lostFocusDelay = value; }
        }

        /// <summary>Gets or sets a value indicating whether the lookup is case sensitive.</summary>
        [DefaultValue(false)]
        [ClientPropertyName("casesens")]
        [Description("Determines if lookup uses a case sensitve match.")]
        [Category("Behavior")]
        [ClientProperty]
        public bool CaseSensitive { get; set; }

        /// <summary>Gets or sets the kind of callback the box uses.</summary>
        [DefaultValue(0)]
        [Description("Image to display during a callback")]
        [Bindable(true)]
        [Category("Behavior")]
        public ClientAPICallBackResponse.CallBackTypeCode CallBackType
        {
            get { return this.callBackType ?? ClientAPICallBackResponse.CallBackTypeCode.Simple; }
            set { this.callBackType = value; }
        }

        /// <summary>Gets or sets the control whose children are posted with a callback.</summary>
        [Bindable(true)]
        [Category("Behavior")]
        [Description("This property allows for data on page to be posted in callback")]
        public string CallBackPostChildrenOf { get; set; } = string.Empty;

        /// <summary>Finds a suggestion by its identifier.</summary>
        /// <param name="id">The identifier to look for.</param>
        /// <returns>The matching suggestion, or <see langword="null"/>.</returns>
        public DNNNode FindNode(string id)
        {
            return this.DNNNodes.FindNode(id);
        }

        /// <summary>Finds a suggestion by its key.</summary>
        /// <param name="key">The key to look for.</param>
        /// <returns>The matching suggestion, or <see langword="null"/>.</returns>
        public DNNNode FindNodeByKey(string key)
        {
            return this.DNNNodes.FindNodeByKey(key);
        }

        /// <summary>Replaces the suggestions with the ones of the given XML payload.</summary>
        /// <param name="strXml">The XML payload.</param>
        public void LoadXml(string strXml)
        {
            this.nodes = new DNNNodeCollection(strXml, string.Empty);
        }

        /// <summary>Raises the <see cref="NodeClick"/> event.</summary>
        /// <param name="e">The arguments of the event.</param>
        public virtual void OnNodeClick(DNNTextSuggestEventArgs e)
        {
            this.NodeClick?.Invoke(this, e);
        }

        /// <inheritdoc/>
        public string RaiseClientAPICallbackEvent(string eventArgument)
        {
            this.PopulateOnDemand?.Invoke(this, new DNNTextSuggestEventArgs(this.DNNNodes, eventArgument));
            return this.DNNNodes.ToJSON();
        }

        /// <inheritdoc/>
        public virtual void RaisePostBackEvent(string eventArgument)
        {
            var parts = (eventArgument ?? string.Empty).Split(ClientAPI.COLUMN_DELIMITER);
            if (parts.Length > 1 && parts[1] == "Click")
            {
                this.OnNodeClick(new DNNTextSuggestEventArgs(this.DNNNodes, parts[0]));
            }
        }

        /// <summary>Registers the client side scripts the box needs.</summary>
        public void RegisterClientScript()
        {
            if (this.IsDownLevel)
            {
                return;
            }

            ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_dom);
            ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_dom_positioning);
            ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_xmlhttp);
#pragma warning disable CS0618 // the obsolete overloads are kept until the host injects IClientResourceController
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.js", DotNetNuke.Web.Client.FileOrder.Js.DnnControlsLabelEdit);
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.dnntextsuggest.js");
#pragma warning restore CS0618
        }

        /// <summary>Collects the properties the client script needs to drive the box.</summary>
        /// <returns>The marshalled properties.</returns>
        protected Hashtable MarshalledProperties()
        {
            var properties = new Hashtable();
            switch (this.IDToken)
            {
                case eIDTokenChar.Brackets:
                    properties.Add("idtok", "[~]");
                    break;
                case eIDTokenChar.Paranthesis:
                    properties.Add("idtok", "(~)");
                    break;
                default:
                    break;
            }

            properties.Add(
                "postback",
                ClientAPI.GetPostBackEventReference(this, "[TEXT]" + ClientAPI.COLUMN_DELIMITER + "Click"));

            if (ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.XMLHTTP))
            {
                properties.Add(
                    "callback",
                    ClientAPI.GetCallbackEventReference(
                        this,
                        "this.getText()",
                        "this.callBackSuccess",
                        "this",
                        "this.callBackFail",
                        "this.callBackStatus",
                        this.CallBackPostChildrenOf,
                        this.CallBackType));
            }

            return properties;
        }

        /// <inheritdoc/>
        protected override void Render(TextWriter writer)
        {
            var html = HtmlMarkupWriter.For(writer);

            // Replaces TextBox.Render, which emitted the very same input element.
            html.AddAttribute("type", "text");
            html.AddAttribute("id", this.ClientID);
            html.AddAttribute("name", this.UniqueID);
            html.AddAttribute("value", this.Text);
            if (!this.Enabled)
            {
                html.AddAttribute("disabled", "disabled");
            }

            if (!string.IsNullOrEmpty(this.CssClass))
            {
                html.AddAttribute("class", this.CssClass);
            }

            if (!string.IsNullOrEmpty(this.ToolTip))
            {
                html.AddAttribute("title", this.ToolTip);
            }

            foreach (var attribute in this.Attributes)
            {
                html.AddAttribute(attribute.Key, attribute.Value);
            }

            foreach (var declaration in this.Style)
            {
                html.AddStyleAttribute(declaration.Key, declaration.Value);
            }

            html.RenderSelfClosingTag("input");

            if (!this.IsDownLevel)
            {
                var properties = this.MarshalledProperties();
                var json = ScriptGenerator.GetMarshalledPropertyJSON(this, properties);
                RegisterInitialize(this, "initTextSuggest", json);
            }
        }

        /// <summary>Prepares the box for rendering.</summary>
        /// <param name="sender">The box raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        private void DNNTextSuggest_PreRender(object sender, EventArgs e)
        {
            this.RegisterClientScript();
            RegisterRequiresPostBack();
        }
    }
}
