// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Xml;

    /// <summary>The <see cref="MenuNodeCollection"/> class.</summary>
    public class MenuNodeCollection : DNNNodeCollection
    {
        private readonly DNNMenu dnnMenu;

        /// <summary>Initializes a new instance of the <see cref="MenuNodeCollection"/> class.</summary>
        /// <param name="jsonDict">The json Dict.</param>
        /// <param name="menu">The menu.</param>
        public MenuNodeCollection(Dictionary<string, object> jsonDict, DNNMenu menu)
            : base(menu.ClientID, jsonDict)
        {
            this.dnnMenu = menu;
        }

        /// <summary>Initializes a new instance of the <see cref="MenuNodeCollection"/> class.</summary>
        /// <param name="strNamespace">The str Namespace.</param>
        /// <param name="objControl">The obj Control.</param>
        public MenuNodeCollection(string strNamespace, DNNMenu objControl)
            : base(strNamespace)
        {
            this.dnnMenu = objControl;
        }

        /// <summary>Initializes a new instance of the <see cref="MenuNodeCollection"/> class.</summary>
        /// <param name="objXmlNode">The obj Xml Node.</param>
        /// <param name="objControl">The obj Control.</param>
        public MenuNodeCollection(XmlNode objXmlNode, DNNMenu objControl)
            : base(objXmlNode)
        {
            this.dnnMenu = objControl;
        }

        /// <summary>Initializes a new instance of the <see cref="MenuNodeCollection"/> class.</summary>
        /// <param name="strXML">The strXML.</param>
        /// <param name="strXSLFile">The strXSL File.</param>
        /// <param name="objControl">The obj Control.</param>
        public MenuNodeCollection(string strXML, string strXSLFile, DNNMenu objControl)
            : base(strXML, strXSLFile)
        {
            this.dnnMenu = objControl;
        }

        /// <summary>Gets or sets the item at the given position.</summary>
        /// <param name="index">The index.</param>
        /// <returns>The item at the given position.</returns>
        public new MenuNode this[int index]
        {
            get
            {
                return new MenuNode(this.XMLNode.ChildNodes[index], this.dnnMenu);
            }

            set
            {
                // Original behavior: assignment through the indexer was never supported.
                throw new NotSupportedException("Cannot Assign Node Directly");
            }
        }

        /// <summary>Gets the <c>Add</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public new int Add()
        {
            return this.Add(string.Empty);
        }

        /// <summary>Gets the <c>Add</c> operation.</summary>
        /// <param name="objNode">The obj Node.</param>
        /// <returns>The result of the operation.</returns>
        public int Add(MenuNode objNode)
        {
            var index = base.Add(objNode);
            objNode.SetDNNMenu(this.dnnMenu);
            if (string.IsNullOrEmpty(objNode.ID))
            {
                objNode.ID = objNode.ParentNameSpace + "_" + objNode.GetHashCode().ToString(CultureInfo.InvariantCulture);
            }

            return index;
        }

        /// <summary>Gets the <c>Add</c> operation.</summary>
        /// <param name="strText">The str Text.</param>
        /// <returns>The result of the operation.</returns>
        public int Add(string strText)
        {
            var objNode = new MenuNode();
            var index = this.Add(objNode);
            objNode.Text = strText;
            return index;
        }

        /// <summary>Gets the <c>Add</c> operation.</summary>
        /// <param name="strID">The strID.</param>
        /// <param name="strKey">The str Key.</param>
        /// <param name="strText">The str Text.</param>
        /// <param name="strNavigateURL">The str NavigateURL.</param>
        /// <returns>The result of the operation.</returns>
        public int Add(string strID, string strKey, string strText, string strNavigateURL)
        {
            return this.Add(strID, strKey, strText, strNavigateURL, string.Empty, string.Empty, string.Empty, true, string.Empty, string.Empty, string.Empty, false, eClickAction.Navigate, string.Empty, -1);
        }

        /// <summary>Gets the <c>Add</c> operation.</summary>
        /// <param name="strID">The strID.</param>
        /// <param name="strKey">The str Key.</param>
        /// <param name="strText">The str Text.</param>
        /// <param name="strNavigateURL">The str NavigateURL.</param>
        /// <param name="strJSFunction">The strJS Function.</param>
        /// <param name="strTarget">The str Target.</param>
        /// <param name="strToolTip">The str Tool Tip.</param>
        /// <param name="blnEnabled">The bln Enabled.</param>
        /// <param name="strCSSClass">The strCSS Class.</param>
        /// <param name="strCSSClassSelected">The strCSS Class Selected.</param>
        /// <param name="strCSSClassHover">The strCSS Class Hover.</param>
        /// <param name="blnSelected">The bln Selected.</param>
        /// <param name="enumClickAction">The enum Click Action.</param>
        /// <param name="strCssClassOver">The str Css Class Over.</param>
        /// <param name="intImageIndex">The int Image Index.</param>
        /// <returns>The result of the operation.</returns>
        public int Add(string strID, string strKey, string strText, string strNavigateURL, string strJSFunction, string strTarget, string strToolTip, bool blnEnabled, string strCSSClass, string strCSSClassSelected, string strCSSClassHover, bool blnSelected, eClickAction enumClickAction, string strCssClassOver, int intImageIndex)
        {
            var index = this.Add();
            var node = this[index];
            node.ID = string.IsNullOrEmpty(strID)
                ? node.ParentNameSpace + "_" + this.XMLNode.ChildNodes.Count.ToString(CultureInfo.InvariantCulture)
                : strID;
            node.Key = strKey;
            node.Text = strText;
            node.NavigateURL = strNavigateURL;
            node.JSFunction = strJSFunction;
            node.Target = strTarget;
            node.ToolTip = strToolTip;
            node.Enabled = blnEnabled;
            node.CSSClass = strCSSClass;
            node.CSSClassSelected = strCSSClassSelected;
            node.CSSClassHover = strCSSClassHover;
            node.Selected = blnSelected;
            node.ClickAction = enumClickAction;
            node.CssClassOver = strCssClassOver;
            node.ImageIndex = intImageIndex;
            return index;
        }

        /// <summary>Gets the <c>Contains</c> operation.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The result of the operation.</returns>
        public bool Contains(MenuNode value)
        {
            return !ReferenceEquals(this.FindNode(value.ID), null);
        }

        /// <summary>Gets the <c>FindNode</c> operation.</summary>
        /// <param name="id">The id.</param>
        /// <returns>The result of the operation.</returns>
        public new MenuNode FindNode(string id)
        {
            var found = this.FindFast("id", id, this.XMLNode, true);
            return ReferenceEquals(found, null) ? null : new MenuNode(found, this.dnnMenu);
        }

        /// <summary>Gets the <c>FindNodeByKey</c> operation.</summary>
        /// <param name="key">The key.</param>
        /// <returns>The result of the operation.</returns>
        public new MenuNode FindNodeByKey(string key)
        {
            var found = this.FindFast("key", key, this.XMLNode, true);
            return ReferenceEquals(found, null) ? null : new MenuNode(found, this.dnnMenu);
        }

        /// <inheritdoc/>
        public override ArrayList FindSelectedNodes()
        {
            var collection = new ArrayList();
            if (!ReferenceEquals(this.XMLNode, null))
            {
                var list = this.XMLNode.SelectNodes("//n[@selected='1']");
                foreach (XmlNode current in list)
                {
                    collection.Add(new MenuNode(current, this.dnnMenu));
                }
            }

            return collection;
        }

        /// <summary>Gets the <c>GetEnumerator</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public new IEnumerator GetEnumerator()
        {
            return new MenuNodeEnumerator(this.XMLNode, this.dnnMenu);
        }

        /// <summary>Gets the <c>IndexOf</c> operation.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The result of the operation.</returns>
        public int IndexOf(MenuNode value)
        {
            var count = this.XMLNode.ChildNodes.Count;
            for (var index = 0; index <= count; index++)
            {
                if (new MenuNode(this.XMLNode.ChildNodes[index], this.dnnMenu).ID == value.ID)
                {
                    return index;
                }
            }

            // Preserves the original VB behavior of returning the uninitialized result (0) when not found.
            return 0;
        }

        /// <summary>Performs the <c>Insert</c> operation.</summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        public void Insert(int index, MenuNode value)
        {
            this.XMLNode.InsertAfter(this.XMLNode.ChildNodes[index], value.XmlNode);
        }

        /// <summary>Performs the <c>Remove</c> operation.</summary>
        /// <param name="value">The value.</param>
        public void Remove(MenuNode value)
        {
            this.XMLNode.RemoveChild(value.XmlNode);
        }
    }
}
