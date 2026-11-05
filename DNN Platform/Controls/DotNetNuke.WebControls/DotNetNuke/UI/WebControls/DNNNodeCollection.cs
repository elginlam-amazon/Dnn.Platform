// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Xml;
    using System.Xml.Xsl;

    /// <summary>The <see cref="DNNNodeCollection"/> class.</summary>
    public class DNNNodeCollection : XmlCollectionBase
    {
        /// <summary>Initializes a new instance of the <see cref="DNNNodeCollection"/> class.</summary>
        /// <param name="strNamespace">The str Namespace.</param>
        public DNNNodeCollection(string strNamespace)
            : base(strNamespace)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNNodeCollection"/> class.</summary>
        /// <param name="objXmlNode">The obj Xml Node.</param>
        public DNNNodeCollection(XmlNode objXmlNode)
            : base(objXmlNode)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNNodeCollection"/> class.</summary>
        /// <param name="strNamespace">The str Namespace.</param>
        /// <param name="objTreeControl">The obj Tree Control.</param>
        public DNNNodeCollection(string strNamespace, DnnTree objTreeControl)
            : base(strNamespace, objTreeControl)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNNodeCollection"/> class.</summary>
        /// <param name="rootNamespace">The root Namespace.</param>
        /// <param name="jsonDict">The json Dict.</param>
        public DNNNodeCollection(string rootNamespace, Dictionary<string, object> jsonDict)
            : base(rootNamespace)
        {
            this.LoadJSON((IEnumerable)jsonDict["nodes"]);
        }

        /// <summary>Initializes a new instance of the <see cref="DNNNodeCollection"/> class.</summary>
        /// <param name="strXML">The strXML.</param>
        /// <param name="strXSLFile">The strXSL File.</param>
        public DNNNodeCollection(string strXML, string strXSLFile)
            : base(strXML, strXSLFile)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNNodeCollection"/> class.</summary>
        /// <param name="objXmlNode">The obj Xml Node.</param>
        /// <param name="objTreeControl">The obj Tree Control.</param>
        public DNNNodeCollection(XmlNode objXmlNode, DnnTree objTreeControl)
            : base(objXmlNode, objTreeControl)
        {
        }

        /// <summary>Gets the xML Node.</summary>
        public XmlNode XMLNode
        {
            get { return this.InnerXMLNode; }
        }

        /// <summary>Gets the xML Doc.</summary>
        public XmlDocument XMLDoc
        {
            get { return this.InnerXMLDoc; }
        }

        /// <summary>Gets the count.</summary>
        public new int Count
        {
            get { return this.InnerXMLNode.ChildNodes.Count; }
        }

        /// <summary>Gets or sets the item at the given position.</summary>
        /// <param name="index">The index.</param>
        /// <returns>The item at the given position.</returns>
        public DNNNode this[int index]
        {
            get
            {
                return new DNNNode(this.InnerXMLNode.ChildNodes[index]);
            }

            set
            {
                // Original behavior: assignment through the indexer was never supported.
                throw new NotSupportedException("Cannot Assign Node Directly");
            }
        }

        /// <summary>Gets the <c>Add</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public int Add()
        {
            var node = new DNNNode();
            this.XMLNode.AppendChild(this.XMLDoc.ImportNode(node.XmlNode, false));
            node.ID = node.ParentNameSpace + "_" + this.XMLNode.ChildNodes.Count.ToString(CultureInfo.InvariantCulture);
            return this.XMLNode.ChildNodes.Count - 1;
        }

        /// <summary>Gets the <c>Add</c> operation.</summary>
        /// <param name="objNode">The obj Node.</param>
        /// <returns>The result of the operation.</returns>
        public int Add(DNNNode objNode)
        {
            var newChild = this.XMLDoc.ImportNode(objNode.XmlNode, true);
            this.XMLNode.AppendChild(newChild);
            objNode.AssociateXmlNode(newChild);
            return this.XMLNode.ChildNodes.Count - 1;
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
        /// <returns>The result of the operation.</returns>
        public int Add(string strID, string strKey, string strText, string strNavigateURL, string strJSFunction, string strTarget, string strToolTip, bool blnEnabled, string strCSSClass, string strCSSClassSelected, string strCSSClassHover)
        {
            var index = this.Add();
            var node = this[index];
            if (!string.IsNullOrEmpty(strID))
            {
                node.ID = strID;
            }

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
            return index;
        }

        /// <summary>Gets the <c>AddBreak</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public int AddBreak()
        {
            var index = this.Add();
            this[index].IsBreak = true;
            return index;
        }

        /// <summary>Gets the <c>Contains</c> operation.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The result of the operation.</returns>
        public bool Contains(DNNNode value)
        {
            return !ReferenceEquals(this.FindNode(value.ID), null);
        }

        /// <summary>Performs the <c>CopyTo</c> operation.</summary>
        /// <param name="myArr">The my Arr.</param>
        /// <param name="index">The index.</param>
        public void CopyTo(Array myArr, int index)
        {
            foreach (XmlNode current in this.InnerXMLNode.ChildNodes)
            {
                myArr.SetValue(current, index);
                index += 1;
            }
        }

        /// <summary>Gets the <c>FindNode</c> operation.</summary>
        /// <param name="id">The id.</param>
        /// <returns>The result of the operation.</returns>
        public DNNNode FindNode(string id)
        {
            var found = this.FindFast("id", id, this.XMLNode, true);
            return ReferenceEquals(found, null) ? null : new DNNNode(found);
        }

        /// <summary>Gets the <c>FindNodeByKey</c> operation.</summary>
        /// <param name="key">The key.</param>
        /// <returns>The result of the operation.</returns>
        public DNNNode FindNodeByKey(string key)
        {
            var found = this.FindFast("key", key, this.XMLNode, true);
            return ReferenceEquals(found, null) ? null : new DNNNode(found);
        }

        /// <summary>Gets every node flagged as selected.</summary>
        /// <returns>
        /// The selected nodes. The VB original returned <c>Microsoft.VisualBasic.Collection</c>,
        /// which does not exist outside the VB runtime; an <see cref="ArrayList"/> with identical
        /// insertion order and enumeration semantics is used instead.
        /// </returns>
        public virtual ArrayList FindSelectedNodes()
        {
            var collection = new ArrayList();
            if (!ReferenceEquals(this.XMLNode, null))
            {
                var list = this.XMLNode.SelectNodes("//n[@selected='1']");
                foreach (XmlNode current in list)
                {
                    collection.Add(new DNNNode(current));
                }
            }

            return collection;
        }

        /// <summary>Gets the <c>GetEnumerator</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public new IEnumerator GetEnumerator()
        {
            return new DNNNodeEnumerator(this.InnerXMLNode);
        }

        /// <summary>Gets the <c>Import</c> operation.</summary>
        /// <param name="objNode">The obj Node.</param>
        /// <returns>The result of the operation.</returns>
        public int Import(DNNNode objNode)
        {
            return this.Import(objNode, true);
        }

        /// <summary>Gets the <c>Import</c> operation.</summary>
        /// <param name="objNode">The obj Node.</param>
        /// <param name="blnDeep">The bln Deep.</param>
        /// <returns>The result of the operation.</returns>
        public int Import(DNNNode objNode, bool blnDeep)
        {
            var newChild = this.XMLDoc.ImportNode(objNode.XmlNode, blnDeep);
            this.XMLNode.AppendChild(newChild);
            return this.XMLNode.ChildNodes.Count - 1;
        }

        /// <summary>Gets the <c>IndexOf</c> operation.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The result of the operation.</returns>
        public int IndexOf(DNNNode value)
        {
            var last = this.XMLNode.ChildNodes.Count - 1;
            for (var index = 0; index <= last; index++)
            {
                if (new DNNNode(this.XMLNode.ChildNodes[index]).ID == value.ID)
                {
                    return index;
                }
            }

            // Preserves the original VB behavior of returning the uninitialized result (0) when not found.
            return 0;
        }

        /// <summary>Performs the <c>InsertAfter</c> operation.</summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        public void InsertAfter(int index, DNNNode value)
        {
            var newChild = this.XMLDoc.ImportNode(value.XmlNode, true);
            this.XMLNode.InsertAfter(newChild, this.XMLNode.ChildNodes[index]);
        }

        /// <summary>Performs the <c>InsertBefore</c> operation.</summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        public void InsertBefore(int index, DNNNode value)
        {
            var newChild = this.XMLDoc.ImportNode(value.XmlNode, true);
            this.XMLNode.InsertBefore(newChild, this.XMLNode.ChildNodes[index]);
        }

        /// <summary>Performs the <c>LoadJSON</c> operation.</summary>
        /// <param name="jsonNodes">The json Nodes.</param>
        [Obsolete("Use LoadJSON IEnumerable instead")]
        public void LoadJSON(ArrayList jsonNodes)
        {
            this.LoadJSON((IEnumerable)jsonNodes);
        }

        /// <summary>Performs the <c>LoadJSON</c> operation.</summary>
        /// <param name="jsonNodes">The json Nodes.</param>
        public void LoadJSON(IEnumerable jsonNodes)
        {
            foreach (Dictionary<string, object> current in jsonNodes)
            {
                var objNode = new DNNNode();
                foreach (var key in current.Keys)
                {
                    if (key == "nodes")
                    {
                        objNode.DNNNodes.LoadJSON((IEnumerable)current["nodes"]);
                        continue;
                    }

                    objNode.CustomAttribute[key] = current[key].ToString();
                }

                this.Add(objNode);
            }
        }

        /// <summary>Performs the <c>Remove</c> operation.</summary>
        /// <param name="value">The value.</param>
        public void Remove(DNNNode value)
        {
            this.XMLNode.RemoveChild(value.XmlNode);
        }

        /// <summary>Performs the <c>Remove</c> operation.</summary>
        /// <param name="index">The index.</param>
        public void Remove(int index)
        {
            this.XMLNode.RemoveChild(this.XMLNode.ChildNodes[index]);
        }

        /// <summary>Performs the <c>RemoveAt</c> operation.</summary>
        /// <param name="index">The index.</param>
        public new void RemoveAt(int index)
        {
            this.Remove(index);
        }

        /// <summary>Gets the <c>ToJSON</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public string ToJSON()
        {
            var builder = new StringBuilder();
            builder.Append("nodes:[");
            var first = true;
            var enumerator = this.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var current = (DNNNode)enumerator.Current;
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append(current.ToJSON(true));
            }

            var disposable = enumerator as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }

            builder.Append(']');
            return builder.ToString();
        }

        /// <summary>Gets the <c>ToXml</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public string ToXml()
        {
            return this.XMLDoc.OuterXml;
        }

        /// <summary>Gets the <c>FindFast</c> operation.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="parent">The parent.</param>
        /// <param name="optimizedForSmallData">The optimized For Small Data.</param>
        /// <returns>The result of the operation.</returns>
        internal XmlNode FindFast(string key, string value, XmlNode parent, bool optimizedForSmallData)
        {
            if (!optimizedForSmallData)
            {
                return parent.SelectSingleNode($".//n[@{key}='{value}']");
            }

            var child = parent.FirstChild;
            while (!ReferenceEquals(child, null))
            {
                if (child.Attributes[key] != null && child.Attributes[key].Value == value)
                {
                    return child;
                }

                if (parent.HasChildNodes)
                {
                    var found = this.FindFast(key, value, child, true);
                    if (!ReferenceEquals(found, null))
                    {
                        return found;
                    }
                }

                child = child.NextSibling;
            }

            return null;
        }

        private string DoTransform(string xml, string xsl)
        {
            return this.DoTransform(xml, xsl, null);
        }

        private string DoTransform(string xml, string xsl, XsltArgumentList parameters)
        {
            var transform = new XslCompiledTransform();
            transform.Load(xsl);
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            using (var xmlWriter = XmlWriter.Create(writer))
            using (var reader = new StringReader(xml))
            using (var xmlReader = XmlReader.Create(reader, new XmlReaderSettings { XmlResolver = null }))
            {
                transform.Transform(xmlReader, parameters, xmlWriter, new XmlUrlResolver());
                writer.Close();
            }

            return sb.ToString();
        }
    }
}
