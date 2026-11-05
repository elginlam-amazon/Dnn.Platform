// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Globalization;
    using System.Text;
    using System.Xml;

    using DotNetNuke.UI.Utilities;

    /// <summary>Gets the dNN Node.</summary>
    public class DNNNode
    {
        private XmlDocument xmlDoc;
        private XmlNode xmlNodeValue;
        private CustomAttributeAccessor customAttributeAccessor;
        private string parentNS;
        private DNNNodeCollection nodesField;

        /// <summary>Initializes a new instance of the <see cref="DNNNode"/> class.</summary>
        public DNNNode()
            : this(new XmlDocument() { XmlResolver = null }.CreateNode(XmlNodeType.Element, "n", string.Empty))
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DNNNode"/> class.</summary>
        /// <param name="strText">The str Text.</param>
        public DNNNode(string strText)
            : this()
        {
            this.Text = strText;
        }

        /// <summary>Initializes a new instance of the <see cref="DNNNode"/> class.</summary>
        /// <param name="objXmlNode">The obj Xml Node.</param>
        public DNNNode(XmlNode objXmlNode)
        {
            this.xmlNodeValue = objXmlNode;
            this.xmlDoc = objXmlNode.OwnerDocument;
        }

        /// <summary>Initializes a new instance of the <see cref="DNNNode"/> class.</summary>
        /// <param name="nodeText">The node Text.</param>
        /// <param name="navigateUrl">The navigate Url.</param>
        public DNNNode(string nodeText, string navigateUrl)
            : this()
        {
            ArgumentNullException.ThrowIfNull(nodeText);

            ArgumentNullException.ThrowIfNull(navigateUrl);

            this.Text = nodeText;

            // REVIEW: the VB original contained the no-op self assignment `navigateUrl = navigateUrl`
            // (and never created the backing XmlNode, so it always threw). The evident intent is to
            // store the supplied url; verify against any caller relying on the old behavior.
            this.NavigateURL = navigateUrl;
        }

        /// <summary>Gets a value indicating whether <c>IsInHierarchy</c>.</summary>
        public bool IsInHierarchy
        {
            get { return !ReferenceEquals(this.XmlNode.ParentNode, null); }
        }

        /// <summary>Gets the parent Node.</summary>
        public DNNNode ParentNode
        {
            get
            {
                return (this.XmlNode.ParentNode == null || this.XmlNode.ParentNode.NodeType == XmlNodeType.Document)
                    ? null
                    : new DNNNode(this.XmlNode.ParentNode);
            }
        }

        /// <summary>Gets the dNN Nodes.</summary>
        public DNNNodeCollection DNNNodes
        {
            get
            {
                if (ReferenceEquals(this.nodesField, null))
                {
                    this.nodesField = new DNNNodeCollection(this.XmlNode);
                }

                return this.nodesField;
            }
        }

        /// <summary>Gets the xml Nodes.</summary>
        public XmlNodeList XmlNodes
        {
            get { return this.XmlNode.ChildNodes; }
        }

        /// <summary>Gets or sets a value indicating whether <c>HasNodes</c>.</summary>
        public bool HasNodes
        {
            get
            {
                var flag = ToBoolean(this.GetCustomAttribute("hasNodes", "0"));
                return flag ? flag : this.DNNNodes.Count > 0;
            }

            set
            {
                this.CustomAttribute["hasNodes"] = value ? "1" : "0";
            }
        }

        /// <summary>Gets the parent Name Space.</summary>
        public string ParentNameSpace
        {
            get
            {
                if (string.IsNullOrEmpty(this.parentNS))
                {
                    this.parentNS = string.Empty;
                    var parent = this.XmlNode.ParentNode;
                    if (parent != null && parent is XmlElement && parent.Attributes.GetNamedItem("id") != null)
                    {
                        this.parentNS = parent.Attributes.GetNamedItem("id").Value;
                    }
                }

                return this.parentNS;
            }
        }

        /// <summary>Gets the level.</summary>
        public int Level
        {
            get
            {
                if (ReferenceEquals(this.ParentNode, null))
                {
                    return -1;
                }

                var xmlNode = this.XmlNode;
                var level = -1;
                while (true)
                {
                    if (xmlNode != null && xmlNode is XmlElement)
                    {
                        level += 1;
                        xmlNode = xmlNode.ParentNode;
                        if (xmlNode == null || xmlNode.Name != "root")
                        {
                            continue;
                        }
                    }

                    return level;
                }
            }
        }

        /// <summary>Gets or sets iD.</summary>
        public string ID
        {
            get { return this.CustomAttribute["id"]; }
            set { this.CustomAttribute["id"] = value; }
        }

        /// <summary>Gets the clientID.</summary>
        public string ClientID
        {
            get { return this.ID.Replace(":", "_"); }
        }

        /// <summary>Gets or sets key.</summary>
        public string Key
        {
            get { return this.CustomAttribute["key"]; }
            set { this.CustomAttribute["key"] = value; }
        }

        /// <summary>Gets or sets text.</summary>
        public string Text
        {
            get { return this.CustomAttribute["txt"]; }
            set { this.CustomAttribute["txt"] = value; }
        }

        /// <summary>Gets or sets navigateURL.</summary>
        public string NavigateURL
        {
            get
            {
                return this.CustomAttribute["url"];
            }

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    this.CustomAttribute["url"] = null;
                }
                else
                {
                    this.ClickAction = eClickAction.Navigate;
                    this.CustomAttribute["url"] = value;
                }
            }
        }

        /// <summary>Gets or sets jS Function.</summary>
        public string JSFunction
        {
            get { return this.CustomAttribute["js"]; }
            set { this.CustomAttribute["js"] = value; }
        }

        /// <summary>Gets or sets target.</summary>
        public string Target
        {
            get { return this.CustomAttribute["tar"]; }
            set { this.CustomAttribute["tar"] = value; }
        }

        /// <summary>Gets or sets tool Tip.</summary>
        public string ToolTip
        {
            get { return this.CustomAttribute["tTip"]; }
            set { this.CustomAttribute["tTip"] = !string.IsNullOrEmpty(value) ? value : null; }
        }

        /// <summary>Gets or sets a value indicating whether <c>Enabled</c>.</summary>
        public bool Enabled
        {
            get { return ToBoolean(this.GetCustomAttribute("enabled", "1")); }
            set { this.CustomAttribute["enabled"] = value ? "1" : "0"; }
        }

        /// <summary>Gets or sets cSS Class.</summary>
        public string CSSClass
        {
            get { return this.CustomAttribute["css"]; }
            set { this.CustomAttribute["css"] = value; }
        }

        /// <summary>Gets or sets cSS Class Selected.</summary>
        public string CSSClassSelected
        {
            get { return this.CustomAttribute["cssSel"]; }
            set { this.CustomAttribute["cssSel"] = value; }
        }

        /// <summary>Gets or sets cSS Class Hover.</summary>
        public string CSSClassHover
        {
            get { return this.CustomAttribute["cssHover"]; }
            set { this.CustomAttribute["cssHover"] = value; }
        }

        /// <summary>Gets or sets cSS Icon.</summary>
        public string CSSIcon
        {
            get { return this.CustomAttribute["cssIcon"]; }
            set { this.CustomAttribute["cssIcon"] = value; }
        }

        /// <summary>Gets or sets image.</summary>
        public string Image
        {
            get { return this.CustomAttribute["img"]; }
            set { this.CustomAttribute["img"] = value; }
        }

        /// <summary>Gets or sets large Image.</summary>
        public string LargeImage
        {
            get { return this.CustomAttribute["largeImg"]; }
            set { this.CustomAttribute["largeImg"] = value; }
        }

        /// <summary>Gets or sets a value indicating whether <c>Selected</c>.</summary>
        public bool Selected
        {
            get { return ToBoolean(this.GetCustomAttribute("selected", "0")); }
            set { this.CustomAttribute["selected"] = value ? "1" : "0"; }
        }

        /// <summary>Gets or sets a value indicating whether <c>BreadCrumb</c>.</summary>
        public bool BreadCrumb
        {
            get { return ToBoolean(this.GetCustomAttribute("bcrumb", "0")); }
            set { this.CustomAttribute["bcrumb"] = value ? "1" : "0"; }
        }

        /// <summary>Gets or sets click Action.</summary>
        public eClickAction ClickAction
        {
            get
            {
                var raw = this.CustomAttribute["ca"];
                return string.IsNullOrEmpty(raw)
                    ? eClickAction.PostBack
                    : (eClickAction)int.Parse(raw, CultureInfo.InvariantCulture);
            }

            set
            {
                this.CustomAttribute["ca"] = ((int)value).ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Gets or sets a value indicating whether <c>IsBreak</c>.</summary>
        public bool IsBreak
        {
            get { return ToBoolean(this.GetCustomAttribute("isBreak", "0")); }
            set { this.CustomAttribute["isBreak"] = value ? "1" : "0"; }
        }

        /// <summary>
        /// Gets the accessor of the arbitrary attributes of the underlying xml node.
        /// </summary>
        public CustomAttributeAccessor CustomAttribute
        {
            get
            {
                return this.customAttributeAccessor ??= new CustomAttributeAccessor(this);
            }
        }

        /// <summary>Gets the xML Doc.</summary>
        internal XmlDocument XMLDoc
        {
            get { return this.xmlDoc; }
        }

        /// <summary>Gets the xml Node.</summary>
        internal XmlNode XmlNode
        {
            get { return this.xmlNodeValue; }
        }

        private XmlNode RootNode
        {
            get { return this.XMLDoc.ChildNodes[0]; }
        }

        /// <summary>Gets an attribute value falling back to <paramref name="defaultValue"/> when empty.</summary>
        /// <param name="key">The attribute name.</param>
        /// <param name="defaultValue">The fallback value.</param>
        /// <returns>The attribute value or the default.</returns>
        public string GetCustomAttribute(string key, string defaultValue)
        {
            var value = this.GetCustomAttribute(key);
            return !string.IsNullOrEmpty(value) ? value : defaultValue;
        }

        /// <summary>Sets an attribute value, clearing it when it equals <paramref name="defaultValue"/>.</summary>
        /// <param name="key">The attribute name.</param>
        /// <param name="defaultValue">The default value which is stored as an empty string.</param>
        /// <param name="value">The value to store.</param>
        public void SetCustomAttribute(string key, string defaultValue, string value)
        {
            if (value == defaultValue)
            {
                value = string.Empty;
            }

            this.SetCustomAttribute(key, value);
        }

        /// <summary>Gets the <c>Clone</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public DNNNode Clone()
        {
            return this.Clone(true);
        }

        /// <summary>Gets the <c>Clone</c> operation.</summary>
        /// <param name="blnDeep">The bln Deep.</param>
        /// <returns>The result of the operation.</returns>
        public DNNNode Clone(bool blnDeep)
        {
            return new DNNNode(this.XmlNode.CloneNode(blnDeep));
        }

        /// <summary>Gets the <c>ToJSON</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public string ToJSON()
        {
            return this.ToJSON(true);
        }

        /// <summary>Gets the <c>ToJSON</c> operation.</summary>
        /// <param name="blnDeep">The bln Deep.</param>
        /// <returns>The result of the operation.</returns>
        public string ToJSON(bool blnDeep)
        {
            var builder = new StringBuilder();
            foreach (XmlAttribute current in this.XmlNode.Attributes)
            {
                if (builder.Length == 0)
                {
                    builder.Append('{');
                }
                else
                {
                    builder.Append(',');
                }

                if (blnDeep)
                {
                    builder.Append(current.Name);
                }
                else
                {
                    var name = current.Name;
                    if (name == "txt")
                    {
                        builder.Append("text");
                    }
                    else if (name == "tar")
                    {
                        builder.Append("target");
                    }
                    else if (name == "tTip")
                    {
                        builder.Append("toolTip");
                    }
                    else
                    {
                        builder.Append(current.Name);
                    }
                }

                builder.Append(':');
                builder.Append("\"" + ClientAPI.GetSafeJSString(current.Value) + "\"");
            }

            if (blnDeep)
            {
                builder.Append(',');
                builder.Append(this.DNNNodes.ToJSON());
            }

            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>Gets the <c>ToXML</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public string ToXML()
        {
            return this.XmlNode.OuterXml;
        }

        /// <summary>Performs the <c>AssociateXmlNode</c> operation.</summary>
        /// <param name="objXmlNode">The obj Xml Node.</param>
        internal void AssociateXmlNode(XmlNode objXmlNode)
        {
            this.xmlNodeValue = objXmlNode;
            this.xmlDoc = objXmlNode.OwnerDocument;
        }

        /// <summary>
        /// Replacement for <c>Microsoft.VisualBasic.CompilerServices.Conversions.ToBoolean(String)</c>:
        /// numeric strings are non-zero tests, otherwise a standard boolean parse, empty/null is false.
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

        private string GetCustomAttribute(string key)
        {
            var attribute = this.XmlNode.Attributes.GetNamedItem(key);
            return ReferenceEquals(attribute, null) ? null : attribute.Value;
        }

        private void SetCustomAttribute(string key, string value)
        {
            var existing = this.XmlNode.Attributes.GetNamedItem(key);
            if (ReferenceEquals(existing, null))
            {
                if (!ReferenceEquals(value, null))
                {
                    var node = this.XMLDoc.CreateAttribute(key);
                    node.Value = value;
                    this.XmlNode.Attributes.Append(node);
                }
            }
            else if (ReferenceEquals(value, null))
            {
                this.XmlNode.Attributes.Remove((XmlAttribute)existing);
            }
            else
            {
                existing.Value = value;
            }
        }

        /// <summary>
        /// Indexer shim preserving the VB <c>CustomAttribute(Key)</c> parameterized property so that
        /// existing call sites (<c>node.CustomAttribute("key")</c>) keep working in C#.
        /// </summary>
        public sealed class CustomAttributeAccessor
        {
            private readonly DNNNode owner;

            /// <summary>Initializes a new instance of the <see cref="CustomAttributeAccessor"/> class.</summary>
            /// <param name="owner">The owner.</param>
            internal CustomAttributeAccessor(DNNNode owner)
            {
                this.owner = owner;
            }

            /// <summary>Gets or sets the item at the given position.</summary>
            /// <param name="key">The key.</param>
            /// <returns>The item at the given position.</returns>
            public string this[string key]
            {
                get { return this.owner.GetCustomAttribute(key); }
                set { this.owner.SetCustomAttribute(key, value); }
            }

            /// <summary>Gets or sets the item at the given position.</summary>
            /// <param name="key">The key.</param>
            /// <param name="defaultValue">The default Value.</param>
            /// <returns>The item at the given position.</returns>
            public string this[string key, string defaultValue]
            {
                get { return this.owner.GetCustomAttribute(key, defaultValue); }
                set { this.owner.SetCustomAttribute(key, defaultValue, value); }
            }
        }
    }
}
