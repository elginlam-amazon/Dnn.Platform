// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;

    using DotNetNuke.UI.Utilities;
    using DotNetNuke.Web.Client;

    /// <summary>
    /// A strip of <see cref="DNNTab"/> children, of which one is selected at a time.
    /// </summary>
    /// <remarks>
    /// Converted from <c>WebControl</c> to <see cref="BaseWebControl"/>; the state-bag backed
    /// properties became ordinary backing fields keeping the very same defaults the view state
    /// getters returned, and <c>IPostBackEventHandler</c> became
    /// <see cref="IClientApiPostBackEventHandler"/>. The <c>ParseChildren</c> /
    /// <c>PersistenceMode</c> / <c>ToolboxData</c> / <c>Designer</c> annotations and the
    /// <c>AddParsedSubObject</c> override were <c>System.Web.UI</c> ASPX-parser directives with no
    /// .NET 10 equivalent and are dropped; children are added through <see cref="Tabs"/>.
    /// </remarks>
    [DefaultProperty("Tabs")]
    public class DNNTabStrip : BaseWebControl, IClientAPICallbackEventHandler, IClientApiPostBackEventHandler
    {
        private const string TABCONTENTDELIMITER = "~`~`~`~`~`~`";
        private const int TABRENDERED = 1;
        private const int TABSELECTED = 2;
        private const int TABENABLED = 4;

        private readonly Hashtable postedTabData;
        private readonly DNNTabLabel defaultLabel;
        private TabStripTabCollection tabs;
        private int selectedIndex;
        private int selectedIndexState;
        private bool? isCrawler;
        private eTabRenderMode? tabRenderMode;
        private ClientAPICallBackResponse.CallBackTypeCode? callBackType;
        private Alignment tabAlignment = Alignment.Top;
        private string tabStripScriptPath;

        /// <summary>Initializes a new instance of the <see cref="DNNTabStrip"/> class.</summary>
        public DNNTabStrip()
            : base("div")
        {
            this.PreRender += this.DNNTabStrip_PreRender;
            this.postedTabData = new Hashtable();
            this.defaultLabel = new DNNTabLabel();
            this.selectedIndex = -1;
            this.DNNTabStrip_Init();
        }

        /// <summary>How the content of the tabs reaches the browser.</summary>
        public enum eTabRenderMode
        {
            /// <summary>Every tab is rendered up front.</summary>
            All = 0,

            /// <summary>A tab is rendered on a post back.</summary>
            PostBack = 1,

            /// <summary>A tab is fetched through a callback.</summary>
            CallBack = 2,
        }

        /// <summary>Gets or sets the client side behavior identifier of the strip.</summary>
        [Bindable(true)]
        [ClientProperty]
        [DefaultValue("")]
        [ClientPropertyName("bid")]
        public string BehaviorID { get; set; } = string.Empty;

        /// <summary>Gets or sets the script invoked on each callback status change.</summary>
        [Bindable(true)]
        [ClientPropertyName("callbackSF")]
        [ClientProperty]
        [Category("Behavior")]
        public string CallbackStatusFunction { get; set; } = string.Empty;

        /// <summary>Gets or sets the script invoked when a tab is clicked.</summary>
        [DefaultValue("")]
        [Bindable(true)]
        [ClientPropertyName("tabClickF")]
        [ClientProperty]
        [Category("Behavior")]
        public string TabClickFunction { get; set; } = string.Empty;

        /// <summary>Gets or sets the script invoked when the selected tab changes.</summary>
        [Bindable(true)]
        [Category("Behavior")]
        [ClientProperty]
        [DefaultValue("")]
        [ClientPropertyName("selIdxF")]
        public string SelectedIndexChangedFunction { get; set; } = string.Empty;

        /// <summary>Gets or sets the directory the Client API scripts are served from.</summary>
        public string ClientAPIScriptPath
        {
            get { return ClientAPI.ScriptPath; }
            set { ClientAPI.ScriptPath = value; }
        }

        /// <summary>Gets or sets the directory the strip's script is served from.</summary>
        public string TabStripScriptPath
        {
            get { return !string.IsNullOrEmpty(this.tabStripScriptPath) ? this.tabStripScriptPath : this.ClientAPIScriptPath; }
            set { this.tabStripScriptPath = value; }
        }

        /// <summary>Gets or sets a value indicating whether down-level rendering is forced.</summary>
        [DefaultValue(false)]
        [Category("Behavior")]
        public bool ForceDownLevel { get; set; }

        /// <summary>Gets or sets a value indicating whether the request comes from a crawler.</summary>
        /// <remarks>
        /// The original fell back to the WebForms <c>Request.Browser.Crawler</c> capability when there
        /// was a request; the fallback now goes through <see cref="WebControls.IsCrawlerRequest"/>.
        /// </remarks>
        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsCrawler
        {
            get { return this.isCrawler ?? WebControls.IsCrawlerRequest(); }
            set { this.isCrawler = value; }
        }

        /// <summary>Gets a value indicating whether the strip renders as plain HTML.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public bool IsDownLevel
        {
            get
            {
                return this.ForceDownLevel
                    || this.IsCrawler
                    || !ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.DHTML);
            }
        }

        /// <summary>Gets or sets how the content of the tabs reaches the browser.</summary>
        [Category("Behavior")]
        [ClientProperty]
        [DefaultValue(2)]
        [ClientPropertyName("trm")]
        public eTabRenderMode TabRenderMode
        {
            get { return this.tabRenderMode ?? eTabRenderMode.CallBack; }
            set { this.tabRenderMode = value; }
        }

        /// <summary>Gets or sets the index of the selected tab.</summary>
        [RefreshProperties(RefreshProperties.All)]
        [DefaultValue(0)]
        public int SelectedIndex
        {
            get
            {
                return this.selectedIndex == -1 ? this.selectedIndexState : this.selectedIndex;
            }

            set
            {
                if (this.tabs is not null && value > this.Tabs.Count - 1)
                {
                    throw new InvalidOperationException("Invalid SelectedIndex");
                }

                this.selectedIndex = value;
                this.selectedIndexState = value;
            }
        }

        /// <summary>Gets or sets the selected tab.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public DNNTab SelectedTab
        {
            get
            {
                return this.Tabs.Count <= this.SelectedIndex ? null : this.Tabs[this.SelectedIndex];
            }

            set
            {
                ArgumentNullException.ThrowIfNull(value);

                for (var index = 0; index <= this.Tabs.Count - 1; index++)
                {
                    if (this.Tabs[index].ID == value.ID)
                    {
                        this.SelectedIndex = index;
                        return;
                    }
                }
            }
        }

        /// <summary>Gets the tabs of the strip.</summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [MergableProperty(false)]
        public virtual TabStripTabCollection Tabs
        {
            get
            {
                if (this.tabs is null)
                {
                    this.tabs = new TabStripTabCollection(this);
                }

                return this.tabs;
            }
        }

        /// <summary>Gets the appearance the labels of the tabs fall back to.</summary>
        [Description("The style to be applied to Label")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Category("Appearance")]
        [NotifyParentProperty(true)]
        public DNNTabLabel DefaultLabel
        {
            get { return this.defaultLabel; }
        }

        /// <summary>Gets or sets the CSS class the content container of a tab falls back to.</summary>
        [Category("Appearance")]
        public string DefaultContainerCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class of the container holding the tab labels.</summary>
        [Category("Appearance")]
        public string CssTabContainer { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class of the container holding the tab contents.</summary>
        [Category("Appearance")]
        public string CssContentContainer { get; set; } = string.Empty;

        /// <summary>Gets or sets the image shown while a callback is in flight.</summary>
        [DefaultValue("")]
        [Description("Image to display during a callback")]
        [Bindable(true)]
        [ClientPropertyName("workImage")]
        [ClientProperty]
        public string WorkImage { get; set; } = string.Empty;

        /// <summary>Gets or sets the kind of callback the tabs use.</summary>
        [ClientPropertyName("cbtype")]
        [Category("Behavior")]
        [DefaultValue(0)]
        [ClientProperty]
        [Bindable(true)]
        [Description("Image to display during a callback")]
        public ClientAPICallBackResponse.CallBackTypeCode CallBackType
        {
            get { return this.callBackType ?? ClientAPICallBackResponse.CallBackTypeCode.Simple; }
            set { this.callBackType = value; }
        }

        /// <summary>Gets or sets the edge the tab labels are rendered along.</summary>
        [DefaultValue(0)]
        [Bindable(true)]
        public Alignment TabAlignment
        {
            get { return this.tabAlignment; }
            set { this.tabAlignment = value; }
        }

        /// <summary>
        /// Loads the posted value of the strip.
        /// </summary>
        /// <param name="postDataKey">The key the value was posted under.</param>
        /// <param name="postCollection">The posted values.</param>
        /// <returns>Always <see langword="false"/>, as in the original.</returns>
        /// <remarks>Replaces <c>IPostBackDataHandler.LoadPostData</c>.</remarks>
        public bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            return false;
        }

        /// <summary>Signals that the posted value of the strip changed.</summary>
        /// <remarks>Replaces <c>IPostBackDataHandler.RaisePostDataChangedEvent</c>; it was empty.</remarks>
        public void RaisePostDataChangedEvent()
        {
        }

        /// <inheritdoc/>
        public string RaiseClientAPICallbackEvent(string eventArgument)
        {
            var tab = this.Tabs.FindTab(eventArgument);
            return tab is null ? null : this.GetTabContents(tab);
        }

        /// <inheritdoc/>
        public virtual void RaisePostBackEvent(string eventArgument)
        {
            var parts = (eventArgument ?? string.Empty).Split(ClientAPI.COLUMN_DELIMITER);
            var tab = this.Tabs.FindTab(parts[0]);
            if (tab is null)
            {
                return;
            }

            if (parts.Length <= 1 || parts[1] == "OnDemand")
            {
                this.SelectedTab = tab;
            }
        }

        /// <summary>Collects the properties the client script needs to drive the strip.</summary>
        /// <returns>The marshalled properties.</returns>
        protected Hashtable MarshalledProperties()
        {
            var properties = new Hashtable { { "tabs", this.GetTabIds() } };
            if (this.IsDownLevel)
            {
                return properties;
            }

            switch (this.TabRenderMode)
            {
                case eTabRenderMode.PostBack:
                    properties.Add(
                        "callback",
                        ClientAPI.GetPostBackClientHyperlink(this, "[TABID]" + ClientAPI.COLUMN_DELIMITER + "OnDemand"));
                    break;

                case eTabRenderMode.CallBack:
                    if (ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.XMLHTTP))
                    {
                        var reference = ClientAPI.GetCallbackEventReference(
                            this,
                            "'[TABID]'",
                            "this.callBackSuccess",
                            "this",
                            "this.callBackFail",
                            "this.callBackStatus",
                            "[POST]",
                            ClientAPICallBackResponse.CallBackTypeCode.Simple);
                        var length = reference.IndexOf("'[POST]'", StringComparison.Ordinal) + "'[POST]'".Length + 1;
                        reference = string.Concat(
                            reference.AsSpan(0, length), "'[CBTYPE]'", reference.AsSpan(length + 1));
                        properties.Add("callback", reference);
                    }
                    else if (this.IsDownLevel)
                    {
                        properties.Add(
                            "callback",
                            ClientAPI.GetPostBackClientHyperlink(this, "[TABID]" + ClientAPI.COLUMN_DELIMITER + "OnDemand"));
                    }

                    break;

                default:
                    break;
            }

            return properties;
        }

        /// <inheritdoc/>
        protected override void Render(TextWriter writer)
        {
            base.Render(writer);
            if (!this.IsDownLevel)
            {
                var properties = this.MarshalledProperties();
                ScriptGenerator.GetMarshalledPropertyJSON(this.defaultLabel, properties);
                var json = ScriptGenerator.GetMarshalledPropertyJSON(this, properties);
                RegisterInitialize(this, "initTabStrip", json);
            }
        }

        /// <inheritdoc/>
        protected override void RenderContents(TextWriter writer)
        {
            var html = HtmlMarkupWriter.For(writer);
            switch (this.TabAlignment)
            {
                case Alignment.Top:
                case Alignment.Left:
                    this.RenderTabs(html);
                    break;
                default:
                    break;
            }

            html.AddAttribute("id", this.ClientID + "_c");
            if (!string.IsNullOrEmpty(this.CssContentContainer))
            {
                html.AddAttribute("class", this.CssContentContainer);
            }

            html.RenderBeginTag("div");

            var enumerator = this.Tabs.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    var current = (DNNTab)enumerator.Current;
                    if (current.IsPostBack || current.IsSelected || current.TabRenderMode == eTabRenderMode.All)
                    {
                        current.RaiseSetupDefaultsEvent(false);
                        if (!current.Visible)
                        {
                            current.Visible = true;
                            current.Style["display"] = "none";
                        }
                        else if (current.IsSelected && current.Style.ContainsKey("display"))
                        {
                            current.Style.Remove("display");
                        }

                        current.RenderControl(html);
                    }
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }

            html.RenderEndTag();

            switch (this.TabAlignment)
            {
                case Alignment.Bottom:
                case Alignment.Right:
                    this.RenderTabs(html);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Finds the form the strip lives in.
        /// </summary>
        /// <param name="tabStrip">The strip to start at.</param>
        /// <returns>The form, or <see langword="null"/> when the strip is not on a page.</returns>
        /// <remarks>
        /// Replaces the walk up the control tree looking for an <c>HtmlForm</c>; the page
        /// abstraction exposes its form directly through <see cref="IClientApiPage.Form"/>.
        /// </remarks>
        private static IClientApiControl FindForm(DNNTabStrip tabStrip)
        {
            return tabStrip?.Page?.Form;
        }

        /// <summary>
        /// Extracts the script elements of the given markup.
        /// </summary>
        /// <param name="html">The markup to scan.</param>
        /// <returns>The script elements found.</returns>
        /// <remarks>
        /// REVIEW: behavior inferred from usage — verify. The decompiled VB body never assigned the
        /// element it created to the variable it then populated and dereferenced, which would have
        /// thrown; the intent (one <see cref="HTMLElement"/> per match, carrying the raw markup, the
        /// inner text and the attribute name/value pairs, with <c>src</c> HTML-decoded) is
        /// implemented here. The regular expression itself is carried over unchanged.
        /// </remarks>
        private static HTMLElementCollection GetScripts(string html)
        {
            var regex = new Regex(
                "(?i:\t(?<element>(?:<script\t\t(?:\\s*\t\t(?:\t\t\t(?<attr>[^=>]*?)\t\t\t=(?:\"|')\t\t\t" +
                "(?<attrv>[^\"|']*?)\t\t\t(?:\"|')\t\t))*        )\t((?(?=\\s*?/>)\\s*?/>|                " +
                "(?:\\s*?>\t(?:[\\s\\r\\n]*?<!--)?(?<text>[\\s\\S]*?)                </script>))\t)))",
                RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.IgnoreCase);

            var elements = new HTMLElementCollection();
            foreach (Match match in regex.Matches(html))
            {
                var elementGroup = match.Groups["element"];
                var element = new HTMLElement(elementGroup.Success ? elementGroup.Value : match.Value)
                {
                    Raw = match.Value,
                };

                var textGroup = match.Groups["text"];
                if (textGroup.Success)
                {
                    element.Text = textGroup.Value;
                }

                var names = match.Groups["attr"].Captures;
                var values = match.Groups["attrv"].Captures;
                for (var index = 0; index < names.Count && index < values.Count; index++)
                {
                    var name = names[index].Value;
                    var value = values[index].Value;
                    var isSource = string.Equals(name, "src", StringComparison.OrdinalIgnoreCase);
                    element.Attributes.Add(name, isSource ? System.Net.WebUtility.HtmlDecode(value) : value);
                }

                elements.Add(element);
            }

            return elements;
        }

        /// <summary>Runs the initialization the original performed on <c>Init</c>.</summary>
        /// <remarks>
        /// REVIEW: behavior inferred from usage — verify. The original deferred the registration of
        /// the client state field to <c>Page.Load</c>; the page abstraction only exposes a
        /// <c>PreRender</c> event, which is the closest later lifecycle hook available.
        /// </remarks>
        private void DNNTabStrip_Init()
        {
            if (ClientAPI.NeedsDNNVariable(this))
            {
                if (this.Page is not null)
                {
                    this.Page.PreRender += this.ParentOnLoad;
                }
                else
                {
                    this.ParentOnLoad(this, EventArgs.Empty);
                }
            }

            this.LoadPostedVars();
            this.RaisePreLoadPostDataEvents();
        }

        /// <summary>Prepares the strip for rendering.</summary>
        /// <param name="sender">The strip raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        private void DNNTabStrip_PreRender(object sender, EventArgs e)
        {
            this.RegisterClientScript();
            RegisterRequiresPostBack();
            if (ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.DHTML))
            {
                ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn);
                ClientAPI.RegisterClientVariable(this.Page, this.ClientID + "_json", "{" + this.Tabs.ToJSON() + "}", true);
            }
        }

        /// <summary>Registers the client state field of the strip.</summary>
        /// <param name="sender">The page raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        private void ParentOnLoad(object sender, EventArgs e)
        {
            ClientAPI.RegisterDNNVariableControl(this);
        }

        /// <summary>Applies the posted per-tab state to the tabs.</summary>
        private void AssignSelectedIndex()
        {
            for (var index = 0; index <= this.Tabs.Count - 1; index++)
            {
                var id = this.Tabs[index].ID;
                if (!this.postedTabData.ContainsKey(id))
                {
                    continue;
                }

                var flags = Convert.ToInt32(this.postedTabData[id], CultureInfo.InvariantCulture);
                if ((flags & TABSELECTED) != 0)
                {
                    this.SelectedIndex = index;
                }

                if ((flags & TABRENDERED) != 0)
                {
                    this.Tabs[index].SetTabState(true);
                }

                this.Tabs[index].Enabled = (flags & TABENABLED) != 0;
            }
        }

        /// <summary>Reads the per-tab state posted back by the client script.</summary>
        private void LoadPostedVars()
        {
            var posted = ClientAPI.GetClientVariable(this.Page, this.ClientID + "_tabs") ?? string.Empty;
            foreach (var entry in posted.Split(','))
            {
                var parts = entry.Split('=');
                if (parts.Length == 2)
                {
                    this.postedTabData[parts[0]] = parts[1];
                }
            }

            this.AssignSelectedIndex();
        }

        /// <summary>
        /// Renders the whole form of the page to markup.
        /// </summary>
        /// <returns>The rendered markup, or an empty string when there is no form.</returns>
        /// <remarks>Kept from the original, which no longer called it either.</remarks>
        private string GetFormHTML()
        {
            var builder = new StringBuilder();
            var writer = new HtmlMarkupWriter(new StringWriter(builder, CultureInfo.InvariantCulture));
            if (FindForm(this) is BaseWebControl form)
            {
                form.Children.Clear();
                form.RenderControl(writer);
                return builder.ToString();
            }

            return string.Empty;
        }

        /// <summary>
        /// Renders a single tab and the client state that goes with it, for a callback.
        /// </summary>
        /// <param name="tab">The tab to render.</param>
        /// <returns>The serialized payload sent back to the client script.</returns>
        /// <remarks>
        /// The original reached the tab's markup by emptying the page's form, re-adding the tab
        /// between two delimiter literals, rendering the form and splitting the result on the
        /// delimiter. Rendering the tab straight into a writer yields the very same fragment without
        /// mutating the page, which the ASP.NET Core control model does not allow anyway.
        /// </remarks>
        private string GetTabContents(DNNTab tab)
        {
            tab.RaiseSetupDefaultsEvent(true);

            var builder = new StringBuilder();
            var writer = new HtmlMarkupWriter(new StringWriter(builder, CultureInfo.InvariantCulture));

            tab.ID = tab.UniqueID;
            tab.SetParent(this);
            tab.Enabled = true;
            tab.RenderControl(writer);

            var markup = builder.ToString();
            var payload = new Dictionary<string, string> { { "text", markup } };

            var scripts = GetScripts(markup);
            if (scripts.Count > 0)
            {
                payload.Add("scripts", scripts.ToJSON());
            }

            payload.Add("vars", MSAJAX.Serialize(ClientAPI.GetClientVariableList(this.Page)));
            return MSAJAX.Serialize(payload);
        }

        /// <summary>Gets the comma separated client side identifiers of the tabs.</summary>
        /// <returns>The identifiers of the tabs.</returns>
        private string GetTabIds()
        {
            var expression = string.Empty;
            var enumerator = this.Tabs.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    expression += ((DNNTab)enumerator.Current).ClientID + ",";
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }

            return expression.Length <= 0 ? string.Empty : expression.Substring(0, expression.Length - 1);
        }

        /// <summary>Lets every rendered tab prepare its posted data.</summary>
        private void RaisePreLoadPostDataEvents()
        {
            var enumerator = this.Tabs.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    var current = (DNNTab)enumerator.Current;
                    if (current.IsPostBack)
                    {
                        current.RaisePreLoadPostData();
                    }
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }

        /// <summary>Registers the client side scripts the strip needs.</summary>
        private void RegisterClientScript()
        {
            if (this.IsDownLevel)
            {
                return;
            }

            ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_dom);

            var needsCallback = this.TabRenderMode == eTabRenderMode.CallBack;
            var enumerator = this.Tabs.GetEnumerator();
            try
            {
                while (!needsCallback && enumerator.MoveNext())
                {
                    if (((DNNTab)enumerator.Current).TabRenderMode == eTabRenderMode.CallBack)
                    {
                        needsCallback = true;
                    }
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }

            if (needsCallback && ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.XMLHTTP))
            {
                ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_xmlhttp);
            }

#pragma warning disable CS0618 // the obsolete overloads are kept until the host injects IClientResourceController
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.js", DotNetNuke.Web.Client.FileOrder.Js.DnnControlsLabelEdit);
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.dnntabstrip.js");
#pragma warning restore CS0618
        }

        /// <summary>Renders the container holding the labels of the tabs.</summary>
        /// <param name="writer">The writer to render to.</param>
        private void RenderTabs(HtmlMarkupWriter writer)
        {
            writer.AddAttribute("id", this.ClientID + "_lc");
            if (!string.IsNullOrEmpty(this.CssTabContainer))
            {
                writer.AddAttribute("class", this.CssTabContainer);
            }

            writer.RenderBeginTag("div");

            var enumerator = this.Tabs.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    ((DNNTab)enumerator.Current).RenderLabel(writer);
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }

            writer.RenderEndTag();
        }
    }
}
