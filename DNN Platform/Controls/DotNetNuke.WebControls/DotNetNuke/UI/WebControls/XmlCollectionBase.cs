// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.IO;
    using System.Xml;

    /// <summary>The <see cref="XmlCollectionBase"/> class.</summary>
    public abstract class XmlCollectionBase : ICollection, IList, IEnumerable
    {
        private XmlNode xmlNode;
        private XmlDocument xmlDoc;
        private DnnTree tree;

        /// <summary>Initializes a new instance of the <see cref="XmlCollectionBase"/> class.</summary>
        protected XmlCollectionBase()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="XmlCollectionBase"/> class.</summary>
        /// <param name="objTreeControl">The obj Tree Control.</param>
        protected XmlCollectionBase(DnnTree objTreeControl)
        {
            this.tree = objTreeControl;
        }

        /// <summary>Initializes a new instance of the <see cref="XmlCollectionBase"/> class.</summary>
        /// <param name="strNamespace">The str Namespace.</param>
        protected XmlCollectionBase(string strNamespace)
        {
            this.InnerXMLDoc = new XmlDocument { XmlResolver = null };
            this.InnerXMLNode = this.InnerXMLDoc.CreateNode(XmlNodeType.Element, "root", string.Empty);
            var node = this.InnerXMLDoc.CreateAttribute("id");
            node.Value = strNamespace;
            this.InnerXMLNode.Attributes.Append(node);
            this.InnerXMLDoc.AppendChild(this.InnerXMLNode);
        }

        /// <summary>Initializes a new instance of the <see cref="XmlCollectionBase"/> class.</summary>
        /// <param name="objXmlNode">The obj Xml Node.</param>
        protected XmlCollectionBase(XmlNode objXmlNode)
        {
            this.InnerXMLNode = objXmlNode;
            this.InnerXMLDoc = this.InnerXMLNode.OwnerDocument;
        }

        /// <summary>Initializes a new instance of the <see cref="XmlCollectionBase"/> class.</summary>
        /// <param name="strNamespace">The str Namespace.</param>
        /// <param name="objTreeControl">The obj Tree Control.</param>
        protected XmlCollectionBase(string strNamespace, DnnTree objTreeControl)
        {
            this.tree = objTreeControl;
            this.InnerXMLDoc = new XmlDocument { XmlResolver = null };
            this.InnerXMLNode = this.InnerXMLDoc.CreateNode(XmlNodeType.Element, "root", string.Empty);
            var node = this.InnerXMLDoc.CreateAttribute("id");
            node.Value = strNamespace;
            this.InnerXMLNode.Attributes.Append(node);
            this.InnerXMLDoc.AppendChild(this.InnerXMLNode);
        }

        /// <summary>Initializes a new instance of the <see cref="XmlCollectionBase"/> class.</summary>
        /// <param name="strXML">The strXML.</param>
        /// <param name="strXSLFile">The strXSL File.</param>
        protected XmlCollectionBase(string strXML, string strXSLFile)
        {
            this.InnerXMLDoc = new XmlDocument { XmlResolver = null };
            if (string.IsNullOrEmpty(strXSLFile))
            {
                using (var stringReader = new StringReader(strXML))
                using (var xmlReader = XmlReader.Create(stringReader, new XmlReaderSettings { XmlResolver = null }))
                {
                    this.InnerXMLDoc.Load(xmlReader);
                }
            }

            this.InnerXMLNode = this.InnerXMLDoc.SelectSingleNode("//root");
        }

        /// <summary>Initializes a new instance of the <see cref="XmlCollectionBase"/> class.</summary>
        /// <param name="objXmlNode">The obj Xml Node.</param>
        /// <param name="objTreeControl">The obj Tree Control.</param>
        protected XmlCollectionBase(XmlNode objXmlNode, DnnTree objTreeControl)
        {
            this.tree = objTreeControl;
            this.InnerXMLNode = objXmlNode;
            this.InnerXMLDoc = this.InnerXMLNode.OwnerDocument;
        }

        /// <summary>Gets the count.</summary>
        public int Count
        {
            get { return this.InnerXMLNode.ChildNodes.Count; }
        }

        /// <inheritdoc/>
        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        /// <inheritdoc/>
        object ICollection.SyncRoot
        {
            get { return null; }
        }

        /// <inheritdoc/>
        bool IList.IsFixedSize
        {
            get { return false; }
        }

        /// <inheritdoc/>
        bool IList.IsReadOnly
        {
            get { return false; }
        }

        /// <summary>Gets the inner List.</summary>
        protected ArrayList InnerList
        {
            get { return null; }
        }

        /// <summary>Gets the list.</summary>
        protected IList List
        {
            get { return null; }
        }

        /// <summary>Gets or sets innerXML Node.</summary>
        protected XmlNode InnerXMLNode
        {
            get { return this.xmlNode; }
            set { this.xmlNode = value; }
        }

        /// <summary>Gets or sets innerXML Doc.</summary>
        protected XmlDocument InnerXMLDoc
        {
            get { return this.xmlDoc; }
            set { this.xmlDoc = value; }
        }

        /// <inheritdoc/>
        object IList.this[int index]
        {
            get { return null; }
            set { }
        }

        /// <summary>Performs the <c>Clear</c> operation.</summary>
        public void Clear()
        {
            for (var index = this.InnerXMLNode.ChildNodes.Count - 1; index >= 0; index--)
            {
                this.InnerXMLNode.RemoveChild(this.InnerXMLNode.ChildNodes[index]);
            }
        }

        /// <summary>Gets the <c>GetEnumerator</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public IEnumerator GetEnumerator()
        {
            return !ReferenceEquals(this.tree, null)
                ? new TreeNodeEnumerator(this.xmlNode, this.tree)
                : this.xmlNode.ChildNodes.GetEnumerator();
        }

        /// <summary>Performs the <c>RemoveAt</c> operation.</summary>
        /// <param name="index">The index.</param>
        public void RemoveAt(int index)
        {
            this.InnerXMLNode.RemoveChild(this.InnerXMLNode.ChildNodes[index]);
        }

        /// <inheritdoc/>
        int IList.Add(object value)
        {
            return 0;
        }

        /// <inheritdoc/>
        bool IList.Contains(object value)
        {
            return false;
        }

        /// <inheritdoc/>
        void ICollection.CopyTo(Array array, int index)
        {
        }

        /// <inheritdoc/>
        int IList.IndexOf(object value)
        {
            return 0;
        }

        /// <inheritdoc/>
        void IList.Insert(int index, object value)
        {
        }

        /// <inheritdoc/>
        void IList.Remove(object value)
        {
        }
    }
}
