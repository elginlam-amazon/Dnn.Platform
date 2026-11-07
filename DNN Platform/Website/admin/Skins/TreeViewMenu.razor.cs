// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Skins.Controls
{
    using System;

    using DotNetNuke.Common;
    using DotNetNuke.Modules.NavigationProvider;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.WebControls;

    using Microsoft.AspNetCore.Components;

    /// <summary>
    /// TreeViewMenu is a Skin Object that creates a Menu using the DNN Treeview Control
    /// to provide a Windows Explore like Menu.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Was <c>treeviewmenu.ascx</c> + <c>TreeViewMenu.ascx.cs</c> +
    /// <c>TreeViewMenu.ascx.designer.cs</c>; the markup now lives in <c>TreeViewMenu.razor</c>,
    /// which documents how each designer control maps onto the state here.
    /// </para>
    /// <para>
    /// This control is NOT superseded by the Aperture theme. Aperture ships its own
    /// <c>Menus/*.razor</c> components over <c>ApertureMenuNode</c>/<c>IApertureMenuProvider</c>
    /// (its own data source and its own markup) and its README names a recursive component as the
    /// "modern mechanism" for <c>dnn:TreeViewMenu</c> without providing one. The platform skin
    /// object - the DNN tree provider, populate-on-demand, the go-up node and the whole image/CSS
    /// property surface third party skins set - has no Aperture equivalent, so it is converted here.
    /// </para>
    /// <para>
    /// Forced platform changes: <c>Page.IsPostBack</c> -&gt; the <see cref="loaded"/> guard (the
    /// tree is built once per rendered instance, which is what <c>!IsPostBack</c> meant for a GET);
    /// <c>Control.ResolveUrl</c> -&gt; <see cref="Globals.ResolveUrl"/>;
    /// <c>Response.Redirect</c> -&gt; <c>NavigationManager.NavigateTo(..., forceLoad: true)</c>.
    /// </para>
    /// </remarks>
    public partial class TreeViewMenu
    {
        private const string MyFileName = "TreeViewMenu.ascx";

        /// <summary>The caption rendered by <c>lblHeader</c>.</summary>
        private string headerLabelText = string.Empty;

        /// <summary>Whether the request bound initialization already ran (was <c>!Page.IsPostBack</c>).</summary>
        private bool loaded;

        /// <summary>Initializes a new instance of the <see cref="TreeViewMenu"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.2. Please use overload with IServiceProvider. Scheduled removal in v12.0.0.")]
        public TreeViewMenu()
            : base()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="TreeViewMenu"/> class.</summary>
        /// <param name="serviceProvider">The service provider.</param>
        public TreeViewMenu(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        private enum EImageType
        {
            FolderClosed = 0,
            FolderOpen = 1,
            Page = 2,
            GotoParent = 3,
        }

        [Parameter]
        public string BodyCssClass { get; set; } = string.Empty;

        [Parameter]
        public string HeaderCssClass { get; set; } = string.Empty;

        [Parameter]
        public string HeaderTextCssClass { get; set; } = "Head";

        [Parameter]
        public string HeaderText { get; set; } = string.Empty;

        [Parameter]
        public bool IncludeHeader { get; set; } = true;

        [Parameter]
        public string NodeChildCssClass { get; set; } = "Normal";

        [Parameter]
        public string NodeClosedImage { get; set; } = "~/images/folderclosed.gif";

        [Parameter]
        public string NodeCollapseImage { get; set; } = "~/images/min.gif";

        [Parameter]
        public string NodeCssClass { get; set; } = "Normal";

        [Parameter]
        public string NodeExpandImage { get; set; } = "~/images/max.gif";

        [Parameter]
        public string NodeLeafImage { get; set; } = "~/images/file.gif";

        [Parameter]
        public string NodeOpenImage { get; set; } = "~/images/folderopen.gif";

        [Parameter]
        public string NodeOverCssClass { get; set; } = "Normal";

        [Parameter]
        public string NodeSelectedCssClass { get; set; } = "Normal";

        [Parameter]
        public bool NoWrap { get; set; }

        [Parameter]
        public string ResourceKey { get; set; } = string.Empty;

        [Parameter]
        public string TreeCssClass { get; set; } = string.Empty;

        [Parameter]
        public string TreeGoUpImage { get; set; } = "~/images/folderup.gif";

        [Parameter]
        public int TreeIndentWidth { get; set; } = 10;

        [Parameter]
        public string Width { get; set; } = "100%";

        /// <summary>Gets the caption of the header (replaces <c>lblHeader.Text</c>).</summary>
        protected string HeaderLabelText => this.headerLabelText;

        /// <summary>Gets the inline style carrying <see cref="Width"/> (was <c>tblMain.Width</c>).</summary>
        protected string TableWidthStyle =>
            string.IsNullOrEmpty(this.Width) ? null : $"width:{this.Width};";

        /// <summary>Gets the inline style carrying <see cref="NoWrap"/> (was <c>cellBody.NoWrap</c>).</summary>
        protected string BodyWrapStyle => this.NoWrap ? "white-space:nowrap;" : null;

        /// <summary>
        /// Gets the content of the body cell: the navigation provider control
        /// <see cref="DotNetNuke.UI.Skins.NavObjectBase.InitializeNavControl"/> added to this
        /// component's <c>Controls</c> collection.
        /// </summary>
        protected RenderFragment NavigationContent => builder =>
        {
            var sequence = 0;
            this.RenderChildControls(builder, ref sequence);
        };

        /// <summary>
        /// Gets or sets the Blazor navigation manager used by the node click redirect (named BlazorNavigation because DotNetNuke.UI.Navigation is the static helper this file also calls).
        /// </summary>
        /// <remarks>
        /// Fully qualified on purpose: <c>NavigationManager</c> alone is ambiguous between
        /// <c>DotNetNuke.Common.NavigationManager</c> and the Blazor one.
        /// </remarks>
        [Inject]
        private Microsoft.AspNetCore.Components.NavigationManager BlazorNavigation { get; set; }

        /// <summary>
        /// The Page_Load server event handler on this user control is used
        /// to populate the tree with the Pages.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                if (!this.loaded)
                {
                    this.loaded = true;

                    this.BuildTree(null, false);

                    // Header Text (if set)
                    if (!string.IsNullOrEmpty(this.HeaderText))
                    {
                        this.headerLabelText = this.HeaderText;
                    }

                    // ResourceKey overrides if found
                    if (!string.IsNullOrEmpty(this.ResourceKey))
                    {
                        string strHeader = Localization.GetString(this.ResourceKey, Localization.GetResourceFile(this, MyFileName));
                        if (!string.IsNullOrEmpty(strHeader))
                        {
                            this.headerLabelText = strHeader;
                        }
                    }

                    // If still not set get default key
                    if (string.IsNullOrEmpty(this.headerLabelText))
                    {
                        string strHeader = Localization.GetString("Title", Localization.GetResourceFile(this, MyFileName));
                        if (!string.IsNullOrEmpty(strHeader))
                        {
                            this.headerLabelText = strHeader;
                        }
                        else
                        {
                            this.headerLabelText = "Site Navigation";
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            this.InitializeTree();
            this.InitializeNavControl(this, "DNNTreeNavigationProvider");
            this.Control.NodeClick += this.DNNTree_NodeClick;
            this.Control.PopulateOnDemand += this.DNNTree_PopulateOnDemand;
            base.OnInit(e);
        }

        /// <summary>The BuildTree helper method is used to build the tree.</summary>
        private void BuildTree(DNNNode objNode, bool blnPODRequest)
        {
            bool blnAddUpNode = false;
            DNNNodeCollection objNodes;

            // The node collection is permission filtered by Navigation.GetNavigationNodes; nothing
            // the visitor may not view can appear here.
            objNodes = this.GetNavigationNodes(objNode);

            if (blnPODRequest == false)
            {
                if (!string.IsNullOrEmpty(this.Level))
                {
                    switch (this.Level.ToLowerInvariant())
                    {
                        case "root":
                            break;
                        case "child":
                            blnAddUpNode = true;
                            break;
                        default:
                            if (this.Level.ToLowerInvariant() != "root" && this.PortalSettings.ActiveTab.BreadCrumbs.Count > 1)
                            {
                                blnAddUpNode = true;
                            }

                            break;
                    }
                }
            }

            // add goto Parent node
            if (blnAddUpNode)
            {
                var objParentNode = new DNNNode();
                objParentNode.ID = this.PortalSettings.ActiveTab.ParentId.ToString();
                objParentNode.Key = objParentNode.ID;
                objParentNode.Text = Localization.GetString("Parent", Localization.GetResourceFile(this, MyFileName));
                objParentNode.ToolTip = Localization.GetString("GoUp", Localization.GetResourceFile(this, MyFileName));
                objParentNode.CSSClass = this.NodeCssClass;
                objParentNode.Image = Globals.ResolveUrl(this.TreeGoUpImage);
                objParentNode.ClickAction = eClickAction.PostBack;
                objNodes.InsertBefore(0, objParentNode);
            }

            foreach (DNNNode objPNode in objNodes)
            {
                // clean up to do in processnodes???
                this.ProcessNodes(objPNode);
            }

            this.Bind(objNodes);

            // technically this should always be a dnntree.  If using dynamic controls Nav.ascx should be used.  just being safe.
            if (this.Control.NavigationControl is DnnTree)
            {
                var objTree = (DnnTree)this.Control.NavigationControl;
                if (objTree.SelectedTreeNodes.Count > 0)
                {
                    // SelectedTreeNodes was a 1-based Microsoft.VisualBasic.Collection before the
                    // VB -> C# migration and is now a 0-based ArrayList; index 0 is the first node.
                    var objTNode = (TreeNode)objTree.SelectedTreeNodes[0];
                    if (objTNode.DNNNodes.Count > 0)
                    {
                        // only expand it if nodes are not pending
                        objTNode.Expand();
                    }
                }
            }
        }

        private void ProcessNodes(DNNNode objParent)
        {
            if (!string.IsNullOrEmpty(objParent.Image))
            {
            }
            else if (objParent.HasNodes)
            {
                // imagepath applied in provider...
                objParent.Image = Globals.ResolveUrl(this.NodeClosedImage);
            }
            else
            {
                objParent.Image = Globals.ResolveUrl(this.NodeLeafImage);
            }

            foreach (DNNNode objNode in objParent.DNNNodes)
            {
                this.ProcessNodes(objNode);
            }
        }

        /// <summary>Sets common properties on DNNTree control.</summary>
        private void InitializeTree()
        {
            if (string.IsNullOrEmpty(this.PathImage))
            {
                this.PathImage = this.PortalSettings.HomeDirectory;
            }

            if (string.IsNullOrEmpty(this.PathSystemImage))
            {
                this.PathSystemImage = Globals.ResolveUrl("~/images/");
            }

            if (string.IsNullOrEmpty(this.IndicateChildImageRoot))
            {
                this.IndicateChildImageRoot = Globals.ResolveUrl(this.NodeExpandImage);
            }

            if (string.IsNullOrEmpty(this.IndicateChildImageSub))
            {
                this.IndicateChildImageSub = Globals.ResolveUrl(this.NodeExpandImage);
            }

            if (string.IsNullOrEmpty(this.IndicateChildImageExpandedRoot))
            {
                this.IndicateChildImageExpandedRoot = Globals.ResolveUrl(this.NodeCollapseImage);
            }

            if (string.IsNullOrEmpty(this.IndicateChildImageExpandedSub))
            {
                this.IndicateChildImageExpandedSub = Globals.ResolveUrl(this.NodeCollapseImage);
            }

            if (string.IsNullOrEmpty(this.CSSNode))
            {
                this.CSSNode = this.NodeChildCssClass;
            }

            if (string.IsNullOrEmpty(this.CSSNodeRoot))
            {
                this.CSSNodeRoot = this.NodeCssClass;
            }

            if (string.IsNullOrEmpty(this.CSSNodeHover))
            {
                this.CSSNodeHover = this.NodeOverCssClass;
            }

            if (string.IsNullOrEmpty(this.CSSNodeSelectedRoot))
            {
                this.CSSNodeSelectedRoot = this.NodeSelectedCssClass;
            }

            if (string.IsNullOrEmpty(this.CSSNodeSelectedSub))
            {
                this.CSSNodeSelectedSub = this.NodeSelectedCssClass;
            }

            if (string.IsNullOrEmpty(this.CSSControl))
            {
                this.CSSControl = this.TreeCssClass;
            }
        }

        /// <summary>
        /// The DNNTree_NodeClick server event handler on this user control runs when a
        /// Node (Page) in the TreeView is clicked.
        /// </summary>
        /// <remarks>The event only fires when the Node contains child nodes, as leaf nodes
        /// have their NavigateUrl Property set.
        /// </remarks>
        private void DNNTree_NodeClick(NavigationEventArgs args)
        {
            if (args.Node == null)
            {
                // Was `this.Control.ID`, the navigation provider control's own id. WebForms
                // Control.ID is gone; the migrated NavigationProvider exposes the same identity
                // as ControlID, which NavObjectBase.InitializeNavControl assigns ("ctl" + this.ID)
                // and which the provider gives to the control it renders, so the node namespace
                // string is unchanged and the client-side element id the menu script selects on
                // still matches.
                args.Node = Navigation.GetNavigationNode(args.ID, this.Control.ControlID);
            }

            this.BlazorNavigation.NavigateTo(Globals.ApplicationURL(int.Parse(args.Node.Key)), forceLoad: true);
        }

        private void DNNTree_PopulateOnDemand(NavigationEventArgs args)
        {
            if (args.Node == null)
            {
                // Was `this.Control.ID`, the navigation provider control's own id. WebForms
                // Control.ID is gone; the migrated NavigationProvider exposes the same identity
                // as ControlID, which NavObjectBase.InitializeNavControl assigns ("ctl" + this.ID)
                // and which the provider gives to the control it renders, so the node namespace
                // string is unchanged and the client-side element id the menu script selects on
                // still matches.
                args.Node = Navigation.GetNavigationNode(args.ID, this.Control.ControlID);
            }

            this.BuildTree(args.Node, true);
        }
    }
}
