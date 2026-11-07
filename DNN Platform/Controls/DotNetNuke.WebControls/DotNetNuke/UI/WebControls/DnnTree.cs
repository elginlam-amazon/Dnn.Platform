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
    using System.IO;

    using DotNetNuke.UI.Utilities;
    using DotNetNuke.Web.Client;

    /// <summary>
    /// A tree of <see cref="TreeNode"/> instances, rendered either by the client script (up-level)
    /// or as plain HTML (down-level).
    /// </summary>
    /// <remarks>
    /// Converted from <c>WebControl</c> to <see cref="BaseWebControl"/>; the state-bag backed
    /// properties became ordinary backing fields keeping the very same defaults the view state
    /// getters returned, <c>IPostBackEventHandler</c> became
    /// <see cref="IClientApiPostBackEventHandler"/> and the <c>IPostBackDataHandler</c> members are
    /// kept as ordinary methods because ASP.NET Core has no post back data phase. The
    /// <c>Designer</c> / <c>ControlBuilder</c> / <c>ToolboxData</c> annotations were
    /// <c>System.Web.UI</c> design-time directives with no .NET 10 equivalent and are dropped.
    /// </remarks>
    [DefaultProperty("Nodes")]
    public class DnnTree : BaseWebControl, IClientApiPostBackEventHandler, IClientAPICallbackEventHandler
    {
        private TreeNodeCollection nodes;
        private NodeImageCollection images;
        private IDNNTreeWriter treeWriter;
        private string xmlState = string.Empty;
        private bool? isCrawler;
        private eCheckBoxMode checkBoxMode = eCheckBoxMode.MultiSelect;
        private bool? populateNodesFromClient;
        private int expandCollapseImageWidth = 12;
        private int animationFrames = 5;
        private string systemImagesPath = "images/";
        private string treeScriptPath;

        /// <summary>Initializes a new instance of the <see cref="DnnTree"/> class.</summary>
        public DnnTree()
        {
            this.PreRender += this.DnnTree_PreRender;
            this.Initialize();
        }

        /// <summary>Handles the expansion state of a node being collapsed.</summary>
        /// <param name="source">The tree raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        public delegate void DNNTreeEventHandler(object source, DNNTreeEventArgs e);

        /// <summary>Handles a node being clicked.</summary>
        /// <param name="source">The tree raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        public delegate void DNNTreeNodeClickHandler(object source, DNNTreeNodeClickEventArgs e);

        /// <summary>Occurs when a node is collapsed.</summary>
        public event DNNTreeEventHandler Collapse;

        /// <summary>Occurs when a node is expanded.</summary>
        public event DNNTreeEventHandler Expand;

        /// <summary>Occurs when a node is clicked.</summary>
        public event DNNTreeNodeClickHandler NodeClick;

        /// <summary>Occurs when the children of a node have to be supplied.</summary>
        public event DNNTreeEventHandler PopulateOnDemand;

        /// <summary>Gets or sets the client side behavior identifier of the tree.</summary>
        [ClientProperty]
        [ClientPropertyName("bid")]
        [DefaultValue("")]
        [Bindable(true)]
        public string BehaviorID { get; set; } = string.Empty;

        /// <summary>Gets or sets how the check boxes of the tree behave.</summary>
        [DefaultValue(0)]
        [ClientProperty]
        [ClientPropertyName("cbm")]
        public eCheckBoxMode CheckBoxMode
        {
            get { return this.checkBoxMode; }
            set { this.checkBoxMode = value; }
        }

        /// <summary>Gets or sets a value indicating whether down-level rendering is forced.</summary>
        public bool ForceDownLevel { get; set; }

        /// <summary>Gets or sets a value indicating whether the request comes from a crawler.</summary>
        /// <remarks>
        /// The original fell back to the WebForms <c>Request.Browser.Crawler</c> capability; the
        /// fallback now goes through <see cref="WebControls.IsCrawlerRequest"/>, which derives the
        /// same answer from the user agent.
        /// </remarks>
        public bool IsCrawler
        {
            get { return this.isCrawler ?? WebControls.IsCrawlerRequest(); }
            set { this.isCrawler = value; }
        }

        /// <summary>Gets a value indicating whether the tree renders as plain HTML.</summary>
        [Browsable(false)]
        public bool IsDownLevel
        {
            get
            {
                return this.ForceDownLevel
                    || this.IsCrawler
                    || !ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.DHTML)
                    || !ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.XML);
            }
        }

        /// <summary>Gets or sets the directory the Client API scripts are served from.</summary>
        public string ClientAPIScriptPath
        {
            get { return ClientAPI.ScriptPath; }
            set { ClientAPI.ScriptPath = value; }
        }

        /// <summary>Gets or sets the directory the tree's script is served from.</summary>
        public string TreeScriptPath
        {
            get { return !string.IsNullOrEmpty(this.treeScriptPath) ? this.treeScriptPath : this.ClientAPIScriptPath; }
            set { this.treeScriptPath = value; }
        }

        /// <summary>Gets or sets the directory holding the tree's system images.</summary>
        [UrlProperty]
        [ClientProperty]
        [Description("Directory to find the images for the menu.  Need to have spacer.gif here!")]
        [DefaultValue("images/")]
        [ClientPropertyName("sysimgpath")]
        public string SystemImagesPath
        {
            get { return !string.IsNullOrEmpty(this.systemImagesPath) ? this.systemImagesPath : "images/"; }
            set { this.systemImagesPath = value; }
        }

        /// <summary>Gets the nodes of the tree.</summary>
        [Browsable(true)]
        public TreeNodeCollection TreeNodes
        {
            get
            {
                if (this.nodes is null)
                {
                    this.nodes = new TreeNodeCollection(this.ClientID, this);
                }

                return this.nodes;
            }
        }

        /// <summary>Gets the selected nodes of the tree.</summary>
        [Browsable(false)]
        public ArrayList SelectedTreeNodes
        {
            get { return this.TreeNodes.FindSelectedNodes(); }
        }

        /// <summary>Gets or sets the width, in pixels, each level is indented by.</summary>
        [Bindable(true)]
        [ClientPropertyName("indentw")]
        [ClientProperty]
        [DefaultValue(10)]
        public int IndentWidth { get; set; }

        /// <summary>Gets or sets the image shown for a collapsed node.</summary>
        [Bindable(true)]
        [DefaultValue("")]
        [ClientPropertyName("colimg")]
        [UrlProperty]
        [ClientProperty]
        public string CollapsedNodeImage { get; set; } = string.Empty;

        /// <summary>Gets or sets the image shown for an expanded node.</summary>
        [ClientPropertyName("expimg")]
        [UrlProperty]
        [ClientProperty]
        [DefaultValue("")]
        [Bindable(true)]
        public string ExpandedNodeImage { get; set; } = string.Empty;

        /// <summary>Gets or sets the image shown while the tree is busy.</summary>
        [DefaultValue("dnnanim.gif")]
        [Bindable(true)]
        [ClientPropertyName("workimg")]
        [ClientProperty]
        public string WorkImage { get; set; } = string.Empty;

        /// <summary>Gets or sets the number of frames of the expand/collapse animation.</summary>
        [ClientProperty]
        [Category("Behavior")]
        [DefaultValue(5)]
        [Bindable(true)]
        [ClientPropertyName("animf")]
        public int AnimationFrames
        {
            get { return this.animationFrames; }
            set { this.animationFrames = value; }
        }

        /// <summary>Gets or sets a value indicating whether the nodes show check boxes.</summary>
        [DefaultValue(false)]
        [Bindable(true)]
        [ClientProperty]
        [ClientPropertyName("checkboxes")]
        public bool CheckBoxes { get; set; }

        /// <summary>Gets or sets the frame navigated when a node navigates.</summary>
        [ClientPropertyName("target")]
        [DefaultValue("")]
        [ClientProperty]
        [Bindable(true)]
        public string Target { get; set; } = string.Empty;

        /// <summary>Gets the images the nodes of the tree pick their icon from.</summary>
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
        [Bindable(true)]
        [DefaultValue("")]
        [ClientPropertyName("csschild")]
        [ClientProperty]
        public string DefaultChildNodeCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied while the pointer is over a node.</summary>
        [DefaultValue("")]
        [Bindable(true)]
        [ClientPropertyName("csshover")]
        [ClientProperty]
        public string DefaultNodeCssClassOver { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied to the selected node.</summary>
        [ClientProperty]
        [ClientPropertyName("csssel")]
        [DefaultValue("")]
        [Bindable(true)]
        public string DefaultNodeCssClassSelected { get; set; } = string.Empty;

        /// <summary>Gets or sets the CSS class applied to the icon of a node.</summary>
        [DefaultValue("")]
        [Bindable(true)]
        [ClientPropertyName("cssicon")]
        [ClientProperty]
        public string DefaultIconCssClass { get; set; } = string.Empty;

        /// <summary>Gets or sets the width, in pixels, of the expand/collapse image.</summary>
        [Bindable(true)]
        [DefaultValue(12)]
        [ClientProperty]
        [ClientPropertyName("expcolimgw")]
        public int ExpandCollapseImageWidth
        {
            get { return this.expandCollapseImageWidth; }
            set { this.expandCollapseImageWidth = value; }
        }

        /// <summary>Gets or sets the script run when a node is clicked.</summary>
        [DefaultValue("")]
        [ClientPropertyName("js")]
        [ClientProperty]
        public string JSFunction { get; set; } = string.Empty;

        /// <summary>Gets or sets a value indicating whether children are fetched from the client.</summary>
        [DefaultValue(true)]
        [Bindable(false)]
        public bool PopulateNodesFromClient
        {
            get { return this.populateNodesFromClient ?? true; }
            set { this.populateNodesFromClient = value; }
        }

        /// <summary>Gets or sets the script invoked on each callback status change.</summary>
        [Bindable(true)]
        public string CallbackStatusFunction { get; set; } = string.Empty;

        /// <summary>Gets the writer that renders the tree.</summary>
        private IDNNTreeWriter TreeWriter
        {
            get
            {
                if (this.treeWriter is null)
                {
                    this.treeWriter = this.IsDownLevel
                        ? new DNNTreeWriter()
                        : (IDNNTreeWriter)new DNNTreeUpLevelWriter();
                }

                return this.treeWriter;
            }
        }

        /// <summary>Clears the selection of every node of the tree.</summary>
        public void ClearSelections()
        {
            var enumerator = this.TreeNodes.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    this.ClearSelections((TreeNode)enumerator.Current);
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }

        /// <summary>Clears the selection of a node and of all of its descendants.</summary>
        /// <param name="parent">The node to start at.</param>
        public void ClearSelections(TreeNode parent)
        {
            ArgumentNullException.ThrowIfNull(parent);

            parent.Selected = false;
            var enumerator = parent.TreeNodes.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    this.ClearSelections((TreeNode)enumerator.Current);
                }
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }

        /// <summary>Finds a node of the tree by its identifier.</summary>
        /// <param name="id">The identifier to look for.</param>
        /// <returns>The matching node, or <see langword="null"/>.</returns>
        public TreeNode FindNode(string id)
        {
            return this.TreeNodes.FindNode(id);
        }

        /// <summary>Finds a node of the tree by its key.</summary>
        /// <param name="key">The key to look for.</param>
        /// <returns>The matching node, or <see langword="null"/>.</returns>
        public TreeNode FindNodeByKey(string key)
        {
            return this.TreeNodes.FindNodeByKey(key);
        }

        /// <summary>Replaces the nodes of the tree with the ones of the given JSON payload.</summary>
        /// <param name="dict">The deserialized JSON payload.</param>
        public void LoadJSON(Dictionary<string, object> dict)
        {
            this.nodes = new TreeNodeCollection(dict, this);
        }

        /// <summary>Replaces the nodes of the tree with the ones of the given XML payload.</summary>
        /// <param name="strXml">The XML payload.</param>
        public void LoadXml(string strXml)
        {
            this.nodes = new TreeNodeCollection(strXml, string.Empty, this);
        }

        /// <summary>
        /// Loads the posted value of the tree.
        /// </summary>
        /// <param name="postDataKey">The key the value was posted under.</param>
        /// <param name="postCollection">The posted values.</param>
        /// <returns>Always <see langword="false"/>, as in the original.</returns>
        /// <remarks>
        /// Replaces <c>IPostBackDataHandler.LoadPostData</c>; the body never read the post
        /// collection and always returned <see langword="false"/>, which is preserved here.
        /// </remarks>
        public bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            return false;
        }

        /// <summary>
        /// Signals that the posted value of the tree changed.
        /// </summary>
        /// <remarks>Replaces <c>IPostBackDataHandler.RaisePostDataChangedEvent</c>; it was empty.</remarks>
        public void RaisePostDataChangedEvent()
        {
        }

        /// <summary>Raises the <see cref="Collapse"/> event.</summary>
        /// <param name="e">The arguments of the event.</param>
        public virtual void OnCollapse(DNNTreeEventArgs e)
        {
            this.Collapse?.Invoke(this, e);
        }

        /// <summary>Raises the <see cref="Expand"/> event.</summary>
        /// <param name="e">The arguments of the event.</param>
        public virtual void OnExpand(DNNTreeEventArgs e)
        {
            this.Expand?.Invoke(this, e);
        }

        /// <summary>Raises the <see cref="NodeClick"/> event.</summary>
        /// <param name="e">The arguments of the event.</param>
        public virtual void OnNodeClick(DNNTreeNodeClickEventArgs e)
        {
            this.NodeClick?.Invoke(this, e);
        }

        /// <inheritdoc/>
        public string RaiseClientAPICallbackEvent(string eventArgument)
        {
            var parts = (eventArgument ?? string.Empty).Split(ClientAPI.COLUMN_DELIMITER);
            var collection = new TreeNodeCollection("<root>" + parts[0] + "</root>", string.Empty, this);
            var posted = collection[0];
            if (posted is null)
            {
                return null;
            }

            this.PopulateOnDemand?.Invoke(this, new DNNTreeEventArgs(posted));
            var existing = this.FindNode(posted.ID);
            return existing is null ? posted.TreeNodes.ToJSON() : existing.TreeNodes.ToJSON();
        }

        /// <inheritdoc/>
        public virtual void RaisePostBackEvent(string eventArgument)
        {
            var parts = (eventArgument ?? string.Empty).Split(ClientAPI.COLUMN_DELIMITER);
            var node = this.TreeNodes.FindNode(parts[0]);
            if (node is null)
            {
                return;
            }

            if (parts.Length <= 1)
            {
                if (node.IsExpanded)
                {
                    node.Collapse();
                }
                else
                {
                    node.Expand();
                }

                if (node.DNNNodes.Count == 0 && this.PopulateNodesFromClient)
                {
                    this.PopulateOnDemand?.Invoke(this, new DNNTreeEventArgs(node));
                }

                return;
            }

            switch (parts[1])
            {
                case "Click":
                    if (!this.CheckBoxes)
                    {
                        var enumerator = this.TreeNodes.FindSelectedNodes().GetEnumerator();
                        try
                        {
                            while (enumerator.MoveNext())
                            {
                                ((TreeNode)enumerator.Current).Selected = false;
                            }
                        }
                        finally
                        {
                            (enumerator as IDisposable)?.Dispose();
                        }
                    }

                    if (node.ClickAction == eClickAction.Expand)
                    {
                        if (node.DNNNodes.Count == 0 && this.PopulateNodesFromClient)
                        {
                            this.PopulateOnDemand?.Invoke(this, new DNNTreeEventArgs(node));
                        }

                        if (node.IsExpanded)
                        {
                            node.Collapse();
                        }
                        else
                        {
                            node.Expand();
                        }
                    }

                    node.Click();
                    break;

                case "Checked":
                    node.Selected = !node.Selected;
                    break;

                case "OnDemand":
                    this.PopulateOnDemand?.Invoke(this, new DNNTreeEventArgs(node));
                    break;

                default:
                    break;
            }
        }

        /// <summary>Registers the client side scripts the tree needs.</summary>
        public void RegisterClientScript()
        {
            if (this.IsDownLevel)
            {
                return;
            }

            ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_dom);
            if (this.PopulateNodesFromClient)
            {
                ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn_xmlhttp);
            }

#pragma warning disable CS0618 // the obsolete overloads are kept until the host injects IClientResourceController
            WebControls.RegisterSubmitComponent(this.Page);
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.js", DotNetNuke.Web.Client.FileOrder.Js.DnnControlsLabelEdit);
            WebControls.RegisterClientScriptBlock(this.Page, "dnn.controls.dnntree.js");
#pragma warning restore CS0618
        }

        /// <summary>Registers the hidden field the client state is posted back in.</summary>
        [Obsolete("RegisterDNNVariableControl on control level is now obsolete.  Use RegisterDNNVariableControl.WebControls")]
        public void RegisterDNNVariableControl()
        {
            ClientAPI.RegisterDNNVariableControl(this);
        }

        /// <summary>Selects a node and expands all of its ancestors.</summary>
        /// <param name="id">The identifier of the node to select.</param>
        /// <returns>The selected node, or <see langword="null"/>.</returns>
        public TreeNode SelectNode(string id)
        {
            TreeNode node = null;
            if (this.TreeNodes.Count > 0)
            {
                node = this.FindNode(id);
                if (node is not null)
                {
                    node.Selected = true;
                    var parent = node.Parent;
                    while (parent is not null)
                    {
                        parent.Expand();
                        parent = parent.Parent;
                    }
                }
            }

            return node;
        }

        /// <summary>Selects a node by key and expands it and all of its ancestors.</summary>
        /// <param name="key">The key of the node to select.</param>
        /// <returns>The selected node, or <see langword="null"/>.</returns>
        public TreeNode SelectNodeByKey(string key)
        {
            TreeNode node = null;
            if (this.TreeNodes.Count > 0)
            {
                node = this.FindNodeByKey(key);
                if (node is not null)
                {
                    node.Selected = true;

                    // The original started the walk at the node itself, not at its parent.
                    var parent = node;
                    while (parent is not null)
                    {
                        parent.Expand();
                        parent = parent.Parent;
                    }
                }
            }

            return node;
        }

        /// <summary>
        /// Restores the nodes of the tree from the state retained for a down-level render.
        /// </summary>
        /// <param name="state">The saved state, which is no longer used.</param>
        /// <remarks>
        /// Replaces the state-bag restore override of the WebForms control. The state bag does
        /// not exist on ASP.NET Core, so the method now restores from the XML the previous render
        /// retained in memory, which is what the original read back out of the state bag.
        /// </remarks>
        protected void RestoreRetainedState(object state)
        {
            if (this.IsDownLevel && !string.IsNullOrEmpty(this.xmlState))
            {
                this.nodes = new TreeNodeCollection(this.xmlState, string.Empty, this);
            }
        }

        /// <inheritdoc/>
        protected override void Render(TextWriter writer)
        {
            this.TreeWriter.RenderTree(writer, this);

            if (this.HasChildren)
            {
                foreach (var child in this.Children)
                {
                    (child as BaseWebControl)?.RenderControl(writer);
                }
            }

            if (!this.IsDownLevel)
            {
                var properties = this.TreeWriter.MarshalledProperties();
                var json = ScriptGenerator.GetMarshalledPropertyJSON(this, properties);
                RegisterInitialize(this, "initTree", json);
            }
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

        /// <summary>Prepares the tree for rendering.</summary>
        /// <param name="sender">The tree raising the event.</param>
        /// <param name="e">The arguments of the event.</param>
        private void DnnTree_PreRender(object sender, EventArgs e)
        {
            this.RegisterClientScript();
            RegisterRequiresPostBack();
            this.UpdateNodes(this.TreeNodes);
            if (!this.IsDownLevel)
            {
                ClientAPI.RegisterClientVariable(this.Page, this.ClientID + "_json", "{" + this.TreeNodes.ToJSON() + "}", true);
            }
            else
            {
                this.xmlState = this.TreeNodes.ToXml();
                if (ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.DHTML))
                {
                    ClientAPI.RegisterClientReference(this.Page, ClientAPI.ClientNamespaceReferences.dnn);
                    var selected = this.SelectedTreeNodes;
                    if (selected.Count > 0)
                    {
                        // The VB original indexed a 1-based collection with 1, i.e. the first node.
                        ClientAPI.RegisterClientVariable(
                            this.Page,
                            this.ClientID + "_selNode",
                            ((TreeNode)selected[0]).ToJSON(false),
                            true);
                    }
                }
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
        private void LoadPostedXml()
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
        /// <param name="treeNodes">The nodes to walk.</param>
        private void UpdateNodes(TreeNodeCollection treeNodes)
        {
            var enumerator = treeNodes.GetEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    var current = (TreeNode)enumerator.Current;
                    if (!string.IsNullOrEmpty(current.Image))
                    {
                        current.ImageIndex = this.ImageList.Contains(current.Image)
                            ? this.ImageList.IndexOf(current.Image)
                            : this.ImageList.Add(current.Image);
                        current.Image = null;
                    }

                    if (current.DNNNodes.Count > 0)
                    {
                        this.UpdateNodes(current.TreeNodes);
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
