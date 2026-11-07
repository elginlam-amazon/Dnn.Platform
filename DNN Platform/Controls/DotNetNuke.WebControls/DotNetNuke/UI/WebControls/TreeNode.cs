// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Xml;

    using DotNetNuke.UI.Utilities;

    /// <summary>
    /// A node of the <see cref="DnnTree"/> control.
    /// </summary>
    /// <remarks>
    /// The VB original implemented <c>System.Web.UI.IStateManager</c> with empty view state members.
    /// System.Web.UI has no .NET 10 equivalent, so the interface is dropped while the members are
    /// kept (with their original no-op bodies) to preserve the public surface for existing callers.
    /// </remarks>
    public class TreeNode : DNNNode
    {
        private TreeNodeCollection nodesField;
        private DnnTree dnnTree;

        /// <summary>Initializes a new instance of the <see cref="TreeNode"/> class.</summary>
        public TreeNode()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="TreeNode"/> class.</summary>
        /// <param name="strText">The str Text.</param>
        public TreeNode(string strText)
            : base(strText)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="TreeNode"/> class.</summary>
        /// <param name="ctlOwner">The ctl Owner.</param>
        internal TreeNode(DnnTree ctlOwner)
        {
            this.dnnTree = ctlOwner;
        }

        /// <summary>Initializes a new instance of the <see cref="TreeNode"/> class.</summary>
        /// <param name="objXmlNode">The obj Xml Node.</param>
        /// <param name="ctlOwner">The ctl Owner.</param>
        internal TreeNode(XmlNode objXmlNode, DnnTree ctlOwner)
            : base(objXmlNode)
        {
            this.dnnTree = ctlOwner;
        }

        /// <summary>Gets or sets the CSS class of the node.</summary>
        /// <remarks>
        /// VB is case insensitive, so the original <c>Shadows CssClass</c> was the very same member
        /// as <see cref="DNNNode.CSSClass"/>. C# treats the two spellings as distinct members, which
        /// is not CLS compliant, hence the explicit opt-out: both spellings are kept so that every
        /// existing call site keeps compiling, and this one simply forwards to the base property.
        /// </remarks>
        [CLSCompliant(false)]
        public string CssClass
        {
            get { return this.CSSClass; }
            set { this.CSSClass = value; }
        }

        /// <summary>Gets or sets the URL the node navigates to.</summary>
        /// <remarks>
        /// VB is case insensitive, so the original <c>Shadows NavigateUrl</c> was the very same
        /// member as <see cref="DNNNode.NavigateURL"/>. See <see cref="CssClass"/> for details.
        /// </remarks>
        [CLSCompliant(false)]
        public string NavigateUrl
        {
            get { return this.NavigateURL; }
            set { this.NavigateURL = value; }
        }

        /// <summary>Gets the tree Nodes.</summary>
        [Browsable(true)]
        public TreeNodeCollection TreeNodes
        {
            get
            {
                if (ReferenceEquals(this.nodesField, null))
                {
                    this.nodesField = new TreeNodeCollection(this.XmlNode, this.DNNTree);
                }

                return this.nodesField;
            }
        }

        /// <summary>Gets the parent.</summary>
        [Browsable(false)]
        public TreeNode Parent
        {
            get { return this.ParentNode; }
        }

        /// <summary>Gets the dNN Tree.</summary>
        [Browsable(false)]
        public DnnTree DNNTree
        {
            get { return this.dnnTree; }
        }

        /// <summary>Gets a value indicating whether <c>IsExpanded</c>.</summary>
        [Browsable(false)]
        [DefaultValue(false)]
        public bool IsExpanded
        {
            get
            {
                if (this.DNNTree.IsDownLevel)
                {
                    return ToBoolean(this.GetCustomAttribute("expanded", "0"));
                }

                var clientVariable = ClientAPI.GetClientVariable(
                    this.dnnTree.Page,
                    this.dnnTree.ClientID + "_" + this.ClientID + ":expanded");
                return string.IsNullOrEmpty(clientVariable)
                    ? ToBoolean(this.GetCustomAttribute("expanded", "0"))
                    : ToBoolean(clientVariable);
            }
        }

        /// <summary>Gets or sets css Class Over.</summary>
        [Bindable(true)]
        [DefaultValue("")]
        public string CssClassOver
        {
            get { return this.CSSClassHover; }
            set { this.CSSClassHover = value; }
        }

        /// <summary>Gets or sets image Index.</summary>
        [Bindable(true)]
        [DefaultValue(-1)]
        public int ImageIndex
        {
            get
            {
                var raw = this.CustomAttribute["imgIdx"];
                if (string.IsNullOrEmpty(raw))
                {
                    return this.DNNTree.ImageList.Count <= 0 ? -1 : 0;
                }

                return int.Parse(raw, CultureInfo.InvariantCulture);
            }

            set
            {
                this.CustomAttribute["imgIdx"] = value.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Gets or sets leftHTML.</summary>
        public string LeftHTML
        {
            get { return this.GetCustomAttribute("lhtml", string.Empty); }
            set { this.CustomAttribute["lhtml"] = value; }
        }

        /// <summary>Gets or sets rightHTML.</summary>
        public string RightHTML
        {
            get { return this.GetCustomAttribute("rhtml", string.Empty); }
            set { this.CustomAttribute["rhtml"] = value; }
        }

        /// <summary>Gets the parent Node.</summary>
        public new TreeNode ParentNode
        {
            get
            {
                return (this.XmlNode.ParentNode == null || this.XmlNode.ParentNode.NodeType == XmlNodeType.Document)
                    ? null
                    : new TreeNode(this.XmlNode.ParentNode, this.dnnTree);
            }
        }

        /// <summary>Gets the separator used between the segments of a node identifier.</summary>
        internal static string Separator
        {
            get { return ":"; }
        }

        /// <summary>Gets the suffix appended to the identifier of a node's check box.</summary>
        internal static string CheckboxIdSuffix
        {
            get { return "checkbox"; }
        }

        private TreeNodeWriter NodeWriter
        {
            get { return !this.dnnTree.IsDownLevel ? null : new TreeNodeWriter(); }
        }

        /// <summary>Performs the <c>Click</c> operation.</summary>
        public void Click()
        {
            this.Selected = true;
            this.DNNTree.OnNodeClick(new DNNTreeNodeClickEventArgs(this));
        }

        /// <summary>Performs the <c>Collapse</c> operation.</summary>
        public void Collapse()
        {
            if (this.HasNodes)
            {
                this.CustomAttribute["expanded"] = "0";
                this.DNNTree.OnCollapse(new DNNTreeEventArgs(this));
            }
        }

        /// <summary>Performs the <c>Expand</c> operation.</summary>
        public void Expand()
        {
            if (this.HasNodes)
            {
                this.CustomAttribute["expanded"] = "1";
                this.DNNTree.OnExpand(new DNNTreeEventArgs(this));
            }
        }

        /// <summary>Performs the <c>MakeNodeVisible</c> operation.</summary>
        public void MakeNodeVisible()
        {
            if (!ReferenceEquals(this.Parent, null))
            {
                this.Parent.Expand();
                this.Parent.MakeNodeVisible();
            }
        }

        /// <summary>Performs the <c>Render</c> operation.</summary>
        /// <param name="writer">The writer.</param>
        public virtual void Render(TextWriter writer)
        {
            this.NodeWriter.RenderNode(writer, this);
        }

        /// <summary>Performs the <c>SetDNNTree</c> operation.</summary>
        /// <param name="objTree">The obj Tree.</param>
        internal void SetDNNTree(DnnTree objTree)
        {
            this.dnnTree = objTree;
        }

        /// <summary>
        /// Replacement for <c>Microsoft.VisualBasic.CompilerServices.Conversions.ToBoolean(String)</c>.
        /// </summary>
        /// <param name="value">The raw attribute value.</param>
        /// <returns>The boolean interpretation of <paramref name="value"/>.</returns>
        private static bool ToBoolean(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            double number;
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out number))
            {
                return number != 0d;
            }

            bool flag;
            return bool.TryParse(value, out flag) && flag;
        }
    }
}
