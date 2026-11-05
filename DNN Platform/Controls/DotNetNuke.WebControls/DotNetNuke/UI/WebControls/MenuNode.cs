// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.Collections;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Xml;

    /// <summary>The <see cref="MenuNode"/> class.</summary>
    public class MenuNode : DNNNode
    {
        private MenuNodeCollection nodesField;
        private DNNMenu dnnMenu;

        /// <summary>Initializes a new instance of the <see cref="MenuNode"/> class.</summary>
        public MenuNode()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MenuNode"/> class.</summary>
        /// <param name="strText">The str Text.</param>
        public MenuNode(string strText)
            : base(strText)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MenuNode"/> class.</summary>
        /// <param name="ctlOwner">The ctl Owner.</param>
        internal MenuNode(DNNMenu ctlOwner)
        {
            this.dnnMenu = ctlOwner;
        }

        /// <summary>Initializes a new instance of the <see cref="MenuNode"/> class.</summary>
        /// <param name="objXmlNode">The obj Xml Node.</param>
        /// <param name="ctlOwner">The ctl Owner.</param>
        internal MenuNode(XmlNode objXmlNode, DNNMenu ctlOwner)
            : base(objXmlNode)
        {
            this.dnnMenu = ctlOwner;
        }

        /// <summary>Gets the menu Nodes.</summary>
        [Browsable(true)]
        public MenuNodeCollection MenuNodes
        {
            get
            {
                if (ReferenceEquals(this.nodesField, null))
                {
                    this.nodesField = new MenuNodeCollection(this.XmlNode, this.DNNMenu);
                }

                return this.nodesField;
            }
        }

        /// <summary>Gets the parent.</summary>
        [Browsable(false)]
        public MenuNode Parent
        {
            get { return this.ParentNode; }
        }

        /// <summary>Gets the dNN Menu.</summary>
        [Browsable(false)]
        public DNNMenu DNNMenu
        {
            get { return this.dnnMenu; }
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
                var raw = this.CustomAttribute["iIdx"];
                return string.IsNullOrEmpty(raw) ? -1 : int.Parse(raw, CultureInfo.InvariantCulture);
            }

            set
            {
                this.CustomAttribute["iIdx"] = value.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Gets or sets url Index.</summary>
        [Bindable(true)]
        [DefaultValue(-1)]
        public int UrlIndex
        {
            get
            {
                var raw = this.CustomAttribute["uIdx"];
                return string.IsNullOrEmpty(raw) ? -1 : int.Parse(raw, CultureInfo.InvariantCulture);
            }

            set
            {
                this.CustomAttribute["uIdx"] = value.ToString(CultureInfo.InvariantCulture);
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
        public new MenuNode ParentNode
        {
            get
            {
                return (this.XmlNode.ParentNode == null || this.XmlNode.ParentNode.NodeType == XmlNodeType.Document)
                    ? null
                    : new MenuNode(this.XmlNode.ParentNode, this.dnnMenu);
            }
        }

        private IMenuNodeWriter NodeWriter
        {
            get
            {
                switch (this.dnnMenu.RenderMode)
                {
                    case DNNMenu.MenuRenderMode.UnorderedList:
                        return new MenuNodeListWriter(this.dnnMenu.Orientation);
                    case DNNMenu.MenuRenderMode.DownLevel:
                        return new MenuNodeWriter(this.dnnMenu.IsCrawler, this.dnnMenu.Orientation);
                    default:
                        return new MenuNodeListWriter(this.dnnMenu.Orientation);
                }
            }
        }

        /// <summary>Performs the <c>Click</c> operation.</summary>
        public void Click()
        {
            this.Selected = !this.Selected;
            if (this.DNNMenu.IsDownLevel)
            {
                var enumerator = this.DNNMenu.SelectedMenuNodes.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var current = (MenuNode)enumerator.Current;
                    if (current.Level >= this.Level && current.ID != this.ID)
                    {
                        current.Selected = false;
                    }
                }

                var disposable = enumerator as System.IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
            }

            this.DNNMenu.OnNodeClick(new DNNMenuNodeClickEventArgs(this));
        }

        /// <summary>Performs the <c>Render</c> operation.</summary>
        /// <param name="writer">The writer.</param>
        public virtual void Render(TextWriter writer)
        {
            this.NodeWriter.RenderNode(writer, this);
        }

        /// <summary>Performs the <c>SetDNNMenu</c> operation.</summary>
        /// <param name="objMenu">The obj Menu.</param>
        internal void SetDNNMenu(DNNMenu objMenu)
        {
            this.dnnMenu = objMenu;
        }
    }
}
