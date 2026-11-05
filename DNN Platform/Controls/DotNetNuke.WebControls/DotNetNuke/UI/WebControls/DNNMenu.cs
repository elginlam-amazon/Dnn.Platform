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

    using DotNetNuke.UI.Utilities;
    using DotNetNuke.UI.Utilities.Animation;
    using DotNetNuke.Web.Client;

    /// <summary>
    /// A menu of <see cref="MenuNode"/> instances, rendered either by the client script (up-level)
    /// or as plain HTML / an unordered list (down-level).
    /// </summary>
    /// <remarks>
    /// Converted from <c>WebControl</c> to <see cref="BaseWebControl"/>; the state-bag backed
    /// properties became ordinary backing fields keeping the very same defaults the view state
    /// getters returned, <c>IPostBackEventHandler</c> became
    /// <see cref="IClientApiPostBackEventHandler"/> and the <c>IPostBackDataHandler</c> members are
    /// kept as ordinary methods because ASP.NET Core has no post back data phase.
    /// </remarks>
    public class DNNMenu : BaseWebControl, IClientApiPostBackEventHandler, IClientAPICallbackEventHandler
    {
        private readonly DNNAnimation animation;
        private MenuNodeCollection nodes;
        private NodeImageCollection images;
        private IDNNMenuWriter writer;
        private string xmlState = string.Empty;
        private bool? isCrawler;
        private bool? populateNodesFromClient;
        private string menuScriptPath;
        private string systemImagesPath = "images/";
        private Orientation orientation = Orientation.Horizontal;
        private Orientation subMenuOrientation = Orientation.Vertical;
        private MenuRenderMode renderMode = MenuRenderMode.Normal;
        private int mouseOutDelay = 500;
        private int mouseInDelay = 250;

        /// <summary>Initializes a new instance of the <see cref="DNNMenu"/> class.</summary>
        public DNNMenu()
        {
            this.PreRender += this.DNNMenu_PreRender;
            this.animation = new DNNAnimation();
            this.Initialize();
        }

        /// <summary>Handles the children of a node having to be supplied.</summary>
        /// <param name="source">The menu raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        public delegate void DNNMenuEventHandler(object source, DNNMenuEventArgs e);

        /// <summary>Handles a node being clicked.</summary>
        /// <param name="source">The menu raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        public delegate void DNNMenuNodeClickHandler(object source, DNNMenuNodeClickEventArgs e);

        /// <summary>Occurs when a node is clicked.</summary>
        public event DNNMenuNodeClickHandler NodeClick;

        /// <summary>Occurs when the children of a node have to be supplied.</summary>
        public event DNNMenuEventHandler PopulateOnDemand;

        /// <summary>How the menu renders itself.</summary>
        public enum MenuRenderMode
        {
            /// <summary>The script driven menu.</summary>
            Normal = 0,

            /// <summary>The script driven menu, rendered without tables.</summary>
            NoTables = 1,

            /// <summary>A nested unordered list.</summary>
            UnorderedList = 2,

            /// <summary>Plain, server rendered HTML.</summary>
            DownLevel = 3,
        }

        /// <summary>Gets or sets a value indicating whether down-level rendering is forced.</summary>
        public bool ForceDownLevel { get; set; }

        /// <summary>Gets a value indicating whether the menu renders as plain HTML.</summary>
        [Browsable(false)]
        public bool IsDownLevel
        {
            get
            {
                return this.ForceDownLevel
                    || this.RenderMode == MenuRenderMode.DownLevel
                    || !ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.DHTML);
            }
        }

        /// <summary>Gets or sets a value indicating whether the request comes from a crawler.</summary>
        /// <remarks>
        /// The original fell back to the WebForms <c>Request.Browser.Crawler</c> capability; the
        /// fallback now goes through <see cref="WebControls.IsCrawlerRequest"/>.
        /// </remarks>
        public bool IsCrawler
        {
            get { return this.isCrawler ?? WebControls.IsCrawlerRequest(); }
            set { this.isCrawler = value; }
        }

        /// <summary>Gets or sets the directory the Client API scripts are served from.</summary>
        public string ClientAPIScriptPath
        {
            get { return ClientAPI.ScriptPath; }
            set { ClientAPI.ScriptPath = value; }
        }

        /// <summary>Gets or sets the directory the menu's script is served from.</summary>
        public string MenuScriptPath
        {
            get { return !string.IsNullOrEmpty(this.menuScriptPath) ? this.menuScriptPath : this.ClientAPIScriptPath; }
            set { this.menuScriptPath = value; }
        }

        /// <summary>Gets or sets the directory holding the menu's system images.</summary>
        [ClientPropertyName("sysimgpath")]
        [UrlProperty]
        [DefaultValue("images/")]
        [ClientProperty]
        [Description("Directory to find the images for the menu.  Need to have spacer.gif here!")]
        public string SystemImagesPath
        {
            get { return !string.IsNullOrEmpty(this.systemImagesPath) ? this.systemImagesPath : "images/"; }
            set { this.systemImagesPath = value; }
        }

        /// <summary>Gets the nodes of the menu.</summary>
        [Browsable(true)]
        public MenuNodeCollection MenuNodes
        {
            get
            {
                if (this.nodes is null)
                {
                    this.nodes = new MenuNodeCollection(this.ClientID, this);
                }

                return this.nodes;
            }
        }

        /// <summary>Gets the selected nodes of the menu.</summary>
        [Browsable(false)]
        public ArrayList SelectedMenuNodes
        {
            get { return this.MenuNodes.FindSelectedNodes(); }
        }

        /// <summary>Gets or sets the sub-menu arrow of a root node.</summary>
        [Bindable(true)]
        [ClientProperty]
        [DefaultValue("")]
        [UrlProperty]
        [ClientPropertyName("rarrowimg")]
        public string RootArrowImage { get; set; } = string.Empty;

        /// <summary>Gets or sets the sub-menu arrow of a child node.</summary>
        [DefaultValue("")]
        [UrlProperty]
        [Bindable(true)]
        [ClientPropertyName("carrowimg")]
        [ClientProperty]
        public string ChildArrowImage { get; set; } = string.Empty;

        /// <summary>Gets or sets the image shown while the menu is busy.</summary>
        [ClientPropertyName("workimg")]
        [ClientProperty]
        [DefaultValue("dnnanim.gif")]
        [Bindable(true)]
        public string WorkImage { get; set; } = string.Empty;

        /// <summary>Gets or sets the client side behavior identifier of the menu.</summary>
        [DefaultValue("")]
        [Bindable(true)]
        [ClientPropertyName("bid")]
        [ClientProperty]
        public string BehaviorID { get; set; } = string.Empty;

        /// <summary>Gets or sets the frame navigated when a node navigates.</summary>
        [DefaultValue("")]
        [Bindable(true)]
        [ClientPropertyName("target")]
        [ClientProperty]
        public string Target { get; set; } = string.Empty;

        /// <summary>Gets the images the nodes of the menu pick their icon from.</summary>
        [Browsable(true)]
        public NodeImageCollection ImageList
        {
            get
            {
                if (this.images is null)
                {
                    this.images = new NodeImageCollection();
                }

                return this.images;
            }
        }

        /// <summary>Gets or sets the CSS class of a root node.</summary>
        [ClientPropertyName("css")]
        [DefaultValue("")]
        [Bindable(true)]
        [ClientProperty]
        public string DefaultNodeCssClass
        {
            get { return this.CssClass; }
            set { this.CssClass = value; }
        }

        /// <summary>Gets or sets the CSS class of a child node.</summary>
        [ClientProperty]
        [ClientPropertyName("csschild")]
        [DefaultValue("")]
        [Bindable(true)]
        public string DefaultChildNodeCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied while the pointer is over a node.</summary>
        [ClientPropertyName("csshover")]
        [ClientProperty]
        [DefaultValue("")]
        [Bindable(true)]
        public string DefaultNodeCssClassOver { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied to the selected node.</summary>
        [Bindable(true)]
        [DefaultValue("")]
        [ClientPropertyName("csssel")]
        [ClientProperty]
        public string DefaultNodeCssClassSelected { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied to the icon of a node.</summary>
        [Bindable(true)]
        [DefaultValue("")]
        [ClientPropertyName("cssicon")]
        [ClientProperty]
        public string DefaultIconCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class of the menu bar.</summary>
        [ClientProperty]
        [ClientPropertyName("mbcss")]
        [DefaultValue("")]
        [Bindable(true)]
        public string MenuBarCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class of an open menu.</summary>
        [ClientPropertyName("mcss")]
        [ClientProperty]
        [DefaultValue("")]
        [Bindable(true)]
        public string MenuCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the script run when a node is clicked.</summary>
        [ClientProperty]
        [ClientPropertyName("js")]
        [DefaultValue("")]
        public string JSFunction { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether children are fetched from the client.</summary>
        [DefaultValue(true)]
        [Bindable(false)]
        public bool PopulateNodesFromClient
        {
            get
            {
                switch (this.RenderMode)
                {
                    case MenuRenderMode.UnorderedList:
                    case MenuRenderMode.DownLevel:
                        return false;
                    default:
                        return this.populateNodesFromClient ?? true;
                }
            }

            set
            {
                this.populateNodesFromClient = value;
            }
        }

        /// <summary>Gets or sets the script invoked on each callback status change.</summary>
        [Bindable(true)]
        public string CallbackStatusFunction { get; set; } = string.Empty;

        /// <summary>Gets or sets the orientation of the menu bar.</summary>
        [Bindable(true)]
        [DefaultValue(0)]
        [ClientPropertyName("orient")]
        [ClientProperty]
        public Orientation Orientation
        {
            get { return this.orientation; }
            set { this.orientation = value; }
        }

        /// <summary>Gets or sets the orientation of the sub-menus.</summary>
        [DefaultValue(1)]
        [Bindable(true)]
        [ClientProperty]
        [ClientPropertyName("suborient")]
        public Orientation SubMenuOrientation
        {
            get { return this.subMenuOrientation; }
            set { this.subMenuOrientation = value; }
        }

        /// <summary>Gets or sets how the menu renders itself.</summary>
        [DefaultValue(0)]
        [ClientPropertyName("rmode")]
        [ClientProperty]
        public MenuRenderMode RenderMode
        {
            get { return this.renderMode; }
            set { this.renderMode = value; }
        }

        /// <summary>Gets or sets a value indicating whether the menu renders with tables.</summary>
        [Obsolete("Use RenderMode")]
        [DefaultValue(true)]
        public bool UseTables
        {
            get
            {
                // Preserved verbatim: the original getter reset RenderMode and returned the
                // uninitialized local, i.e. always False.
                this.RenderMode = MenuRenderMode.Normal;
                return false;
            }

            set
            {
                this.RenderMode = value ? MenuRenderMode.Normal : MenuRenderMode.NoTables;
            }
        }

        /// <summary>Gets or sets the delay, in milliseconds, before a sub-menu hides.</summary>
        [Description("Number of milliseconds to wait befor hiding sub-menu on mouse out")]
        [Category("Behavior")]
        [ClientPropertyName("moutdelay")]
        [ClientProperty]
        [DefaultValue(500)]
        public int MouseOutDelay
        {
            get { return this.mouseOutDelay; }
            set { this.mouseOutDelay = value; }
        }

        /// <summary>Gets or sets the delay, in milliseconds, before a sub-menu shows.</summary>
        [ClientPropertyName("mindelay")]
        [Category("Behavior")]
        [ClientProperty]
        [DefaultValue(250)]
        [Description("Number of milliseconds to wait befor displaying sub-menu on mouse over")]
        public int MouseInDelay
        {
            get { return this.mouseInDelay; }
            set { this.mouseInDelay = value; }
        }

        /// <summary>Gets or sets a value indicating whether the open state survives a post back.</summary>
        [ClientProperty]
        [DefaultValue(false)]
        [ClientPropertyName("enablepbstate")]
        public bool EnablePostbackState { get; set; }

        /// <summary>Gets the animation applied to showing and hiding the sub-menus.</summary>
        [NotifyParentProperty(true)]
        [Description("The animation to be applied to showing/hiding the menus")]
        [Category("Appearance")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public DNNAnimation Animation
        {
            get { return this.animation; }
        }

        /// <summary>Gets the writer that renders the menu.</summary>
        private IDNNMenuWriter MenuWriter
        {
            get
            {
                if (this.writer is null)
                {
                    if (this.IsDownLevel)
                    {
                        this.RenderMode = MenuRenderMode.UnorderedList;
                    }
                    else if (this.IsCrawler)
                    {
                        this.RenderMode = MenuRenderMode.UnorderedList;
                    }

                    switch (this.RenderMode)
                    {
                        case MenuRenderMode.Normal:
                        case MenuRenderMode.NoTables:
                            this.writer = new DNNMenuUpLevelWriter();
                            break;
                        case MenuRenderMode.UnorderedList:
                            this.writer = new DNNMenuListWriter();
                            break;
                        case MenuRenderMode.DownLevel:
                            this.writer = new DNNMenuWriter(this.IsCrawler);
                            break;
                        default:
                            break;
                    }
                }

                return this.writer;
            }
        }

        /// <summary>Finds a node of the menu by its identifier.</summary>
        /// <param name="id">The identifier to look for.</param>
        /// <returns>The matching node, or <see langword="null"/>.</returns>
        public MenuNode FindNode(string id)
        {
            return this.MenuNodes.FindNode(id);
        }

        /// <summary>Finds a node of the menu by its key.</summary>
        /// <param name="key">The key to look for.</param>
        /// <returns>The matching node, or <see langword="null"/>.</returns>
        public MenuNode FindNodeByKey(string key)
        {
            return this.MenuNodes.FindNodeByKey(key);
        }

        /// <summary>Replaces the nodes of the menu with the ones of the given JSON payload.</summary>
        /// <param name="dict">The deserialized JSON payload.</param>
        public void LoadJSON(Dictionary<string, object> dict)
        {
            this.nodes = new MenuNodeCollection(dict, this);
        }

        /// <summary>Replaces the nodes of the menu with the ones of the given XML payload.</summary>
        /// <param name="strXml">The XML payload.</param>
        public void LoadXml(string strXml)
        {
            this.nodes = new MenuNodeCollection(strXml, string.Empty, this);
        }

        /// <summary>
        /// Loads the posted value of the menu.
        /// </summary>
        /// <param name="postDataKey">The key the value was posted under.</param>
        /// <param name="postCollection">The posted values.</param>
        /// <returns>Always <see langword="false"/>, as in the original.</returns>
        /// <remarks>Replaces <c>IPostBackDataHandler.LoadPostData</c>.</remarks>
        public bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            return false;
        }

        /// <summary>Signals that the posted value of the menu changed.</summary>
        /// <remarks>Replaces <c>IPostBackDataHandler.RaisePostDataChangedEvent</c>; it was empty.</remarks>
        public void RaisePostDataChangedEvent()
        {
        }

        /// <summary>Raises the <see cref="NodeClick"/> event.</summary>
        /// <param name="e">The arguments of the event.</param>
        public virtual void OnNodeClick(DNNMenuNodeClickEventArgs e)
        {
            this.NodeClick?.Invoke(this, e);
        }

        /// <inheritdoc/>
        public string RaiseClientAPICallbackEvent(string eventArgument)
        {
            var parts = (eventArgument ?? string.Empty).Split(ClientAPI.COLUMN_DELIMITER);
            var collection = new MenuNodeCollection("<root>" + parts[0] + "</root>", string.Empty, this);
            var posted = collection[0];
            if (posted is null)
            {
                return null;
            }

            this.PopulateOnDemand?.Invoke(this, new DNNMenuEventArgs(posted));
            var existing = this.FindNode(posted.ID);
            return existing is null ? posted.MenuNodes.ToJSON() : existing.MenuNodes.ToJSON();
        }

        /// <inheritdoc/>
        public virtual void RaisePostBackEvent(string eventArgument)
        {
            var parts = (eventArgument ?? string.Empty).Split(ClientAPI.COLUMN_DELIMITER);
            var node = this.MenuNodes.FindNode(parts[0]);
            if (node is null)
            {
                return;
            }

            if (parts.Length <= 1)
            {
                node.Click();
                return;
            }

            switch (parts[1])
            {
                case "Click":
                    node.Click();
                    break;
                case "Checked":
                    node.Selected = !node.Selected;
                    break;
                case "OnDemand":
                    this.PopulateOnDemand?.Invoke(this, new DNNMenuEventArgs(node));
                    break;
                default:
                    break;
            }
        }

        /// <summary>Registers the client side scripts the menu needs.</summary>
        public void RegisterClientScript()
        {
            if (this.IsDownLevel)
            {
                return;
            }

            ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_dom);
            ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_dom_positioning);
            if (this.PopulateNodesFromClient)
            {
                ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_xmlhttp);
            }

            if (this.Animation.AnimationType != AnimationType.None)
            {
                ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_motion);
            }

#pragma warning disable CS0618 // the obsolete overloads are kept until the host injects IClientResourceController
            WebControls.RegisterSubmitComponent(this.Page);
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.js", DotNetNuke.Web.Client.FileOrder.Js.DnnControlsLabelEdit);
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.dnnmenu.js");
#pragma warning restore CS0618
        }

        /// <summary>Registers the hidden field the client state is posted back in.</summary>
        [Obsolete("RegisterDNNVariableControl on control level is now obsolete.  Use RegisterDNNVariableControl.WebControls")]
        public void RegisterDNNVariableControl()
        {
            ClientAPI.RegisterDNNVariableControl(this);
        }

        /// <summary>
        /// Restores the nodes of the menu from the state retained for a down-level render.
        /// </summary>
        /// <remarks>
        /// Replaces the <c>OnLoad</c> override, which read the XML back out of view state. View
        /// state does not exist on ASP.NET Core, so the XML the previous render retained in memory
        /// is used instead.
        /// </remarks>
        public void OnLoad()
        {
            if (this.IsDownLevel && !string.IsNullOrEmpty(this.xmlState))
            {
                this.nodes = new MenuNodeCollection(this.xmlState, string.Empty, this);
            }
        }

        /// <inheritdoc/>
        protected override void Render(TextWriter writer)
        {
            this.MenuWriter.RenderMenu(writer, this);

            if (this.HasChildren)
            {
                foreach (var child in this.Children)
                {
                    (child as BaseWebControl)?.RenderControl(writer);
                }
            }

            if (!this.IsDownLevel)
            {
                var properties = this.MarshalledProperties();
                ScriptGenerator.GetMarshalledPropertyJSON(this.animation, properties);
                var json = ScriptGenerator.GetMarshalledPropertyJSON(this, properties);
                RegisterInitialize(this, "initMenu", json);
            }
        }

        /// <summary>Collects the properties the client script needs to build the menu.</summary>
        /// <returns>The marshalled properties.</returns>
        private Hashtable MarshalledProperties()
        {
            var properties = new Hashtable();
            if (this.ImageList.Count > 0)
            {
                var paths = new SortedList();
                var imageList = string.Empty;
                var imagePaths = string.Empty;

                var enumerator = this.ImageList.GetEnumerator();
                try
                {
                    while (enumerator.MoveNext())
                    {
                        var current = (NodeImage)enumerator.Current;
                        if (current.ImageUrl.IndexOf('/') > -1)
                        {
                            var directory = current.ImageUrl.Substring(0, current.ImageUrl.LastIndexOf('/') + 1);
                            var fileName = current.ImageUrl.Substring(current.ImageUrl.LastIndexOf('/') + 1);
                            if (!paths.ContainsValue(directory))
                            {
                                paths.Add(paths.Count, directory);
                            }

                            current.ImageUrl = $"[{paths.IndexOfValue(directory).ToString(CultureInfo.InvariantCulture)}]{fileName}";
                        }

                        imageList += (imageList.Length > 0 ? "," : string.Empty) + current.ImageUrl;
                    }
                }
                finally
                {
                    (enumerator as IDisposable)?.Dispose();
                }

                for (var index = 0; index <= paths.Count - 1; index++)
                {
                    imagePaths += (imagePaths.Length > 0 ? "," : string.Empty)
                        + Convert.ToString(paths.GetByIndex(index), CultureInfo.InvariantCulture);
                }

                properties.Add("imagelist", imageList);
                properties.Add("imagepaths", imagePaths);
            }

            properties.Add(
                "postback",
                ClientAPI.GetPostBackEventReference(this, "[NODEID]" + ClientAPI.COLUMN_DELIMITER + "Click"));

            if (this.PopulateNodesFromClient)
            {
                if (ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.XMLHTTP))
                {
                    properties.Add(
                        "callback",
                        ClientAPI.GetCallbackEventReference(
                            this, "'[NODEXML]'", "this.callBackSuccess", "mNode", "this.callBackFail", "this.callBackStatus"));
                }
                else
                {
                    properties.Add(
                        "callback",
                        ClientAPI.GetPostBackClientHyperlink(this, "[NODEID]" + ClientAPI.COLUMN_DELIMITER + "OnDemand"));
                }

                if (!string.IsNullOrEmpty(this.CallbackStatusFunction))
                {
                    properties.Add("callbacksf", this.CallbackStatusFunction);
                }
            }

            return properties;
        }

        /// <summary>Runs the initialization the original performed in <c>OnInit</c>.</summary>
        /// <remarks>
        /// REVIEW: behavior inferred from usage — verify. The original deferred the registration of
        /// the client state field to <c>Page.Load</c>; the page abstraction only exposes a
        /// <c>PreRender</c> event, which is the closest later lifecycle hook available.
        /// </remarks>
        private void Initialize()
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
            else
            {
                this.LoadPostedJSON();
            }
        }

        /// <summary>Registers the client state field and reads the posted nodes back.</summary>
        /// <param name="sender">The page raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        private void ParentOnLoad(object sender, EventArgs e)
        {
            ClientAPI.RegisterDNNVariableControl(this);
            this.LoadPostedJSON();
        }

        /// <summary>Prepares the menu for rendering.</summary>
        /// <param name="sender">The menu raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        private void DNNMenu_PreRender(object sender, EventArgs e)
        {
            this.RegisterClientScript();
            RegisterRequiresPostBack();
            this.UpdateNodes(this.MenuNodes);
            if (!this.IsDownLevel)
            {
                ClientAPI.RegisterClientVariable(this.Page, this.ClientID + "_json", "{" + this.MenuNodes.ToJSON() + "}", true);
            }
            else
            {
                this.xmlState = this.MenuNodes.ToXml();
            }
        }

        /// <summary>Reads the nodes posted back by the client script.</summary>
        private void LoadPostedJSON()
        {
            var clientVariable = string.Empty;
            if (!this.IsDownLevel)
            {
                clientVariable = ClientAPI.GetClientVariable(this.Page, this.ClientID + "_json");
            }

            if (!string.IsNullOrEmpty(clientVariable))
            {
                var dict = MSAJAX.Deserialize<Dictionary<string, object>>(clientVariable);
                if (dict.ContainsKey("nodes"))
                {
                    this.LoadJSON(dict);
                }
            }
        }

        /// <summary>
        /// Reads the nodes posted back by the client script in their XML form.
        /// </summary>
        /// <remarks>Kept from the original, which no longer called it either.</remarks>
        private void LoadPostedXML()
        {
            var expression = string.Empty;
            if (!this.IsDownLevel)
            {
                expression = ClientAPI.GetClientVariable(this.Page, this.ClientID + "_xml");
            }

            if (!string.IsNullOrEmpty(expression))
            {
                this.LoadXml(expression);
            }
        }

        /// <summary>Moves the images of the nodes into the shared <see cref="ImageList"/>.</summary>
        /// <param name="menuNodes">The nodes to walk.</param>
        private void UpdateNodes(MenuNodeCollection menuNodes)
        {
            var enumerator = menuNodes.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    var current = (MenuNode)enumerator.Current;
                    if (!string.IsNullOrEmpty(current.Image))
                    {
                        current.ImageIndex = this.ImageList.Contains(new NodeImage(current.Image))
                            ? this.ImageList.IndexOf(new NodeImage(current.Image))
                            : this.ImageList.Add(current.Image);
                        current.Image = null;
                    }

                    if (current.DNNNodes.Count > 0)
                    {
                        this.UpdateNodes(current.MenuNodes);
                    }
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }
    }
}
