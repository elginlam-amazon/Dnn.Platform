// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.Collections;
    using System.Globalization;
    using System.Text;

    /// <summary>Gets the hTML Element.</summary>
    public class HTMLElement
    {
        private readonly Hashtable attributes = new Hashtable();
        private string raw;
        private string textField;
        private string tagNameValue;

        /// <summary>Initializes a new instance of the <see cref="HTMLElement"/> class.</summary>
        /// <param name="tagName">The tag Name.</param>
        public HTMLElement(string tagName)
        {
            this.tagNameValue = tagName;
        }

        /// <summary>Gets or sets raw.</summary>
        public string Raw
        {
            get { return this.raw; }
            set { this.raw = value; }
        }

        /// <summary>Gets or sets text.</summary>
        public string Text
        {
            get { return this.textField; }
            set { this.textField = value; }
        }

        /// <summary>Gets or sets tag Name.</summary>
        public string TagName
        {
            get { return this.tagNameValue; }
            set { this.tagNameValue = value; }
        }

        /// <summary>Gets the attributes.</summary>
        public Hashtable Attributes
        {
            get { return this.attributes; }
        }

        /// <summary>Gets the <c>ToJSON</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public string ToJSON()
        {
            var builder = new StringBuilder();
            foreach (var key in this.Attributes.Keys)
            {
                var name = System.Convert.ToString(key, CultureInfo.InvariantCulture);
                if (builder.Length == 0)
                {
                    builder.Append('{');
                }
                else
                {
                    builder.Append(',');
                }

                builder.Append(CultureInfo.InvariantCulture, $"{name}:{SafeJSONString(System.Convert.ToString(this.Attributes[name], CultureInfo.InvariantCulture))}");
            }

            if (!string.IsNullOrEmpty(this.Text))
            {
                if (builder.Length == 0)
                {
                    builder.Append('{');
                }
                else
                {
                    builder.Append(',');
                }

                builder.Append(CultureInfo.InvariantCulture, $"__text:{SafeJSONString(this.Text)}");
            }

            builder.Append('}');
            return builder.ToString();
        }

        private static string SafeJSONString(string strString)
        {
            if (strString == null)
            {
                return "''";
            }

            return "'" + strString.Replace("\r", "\\r").Replace("\n", "\\n").Replace("'", "\\'") + "'";
        }
    }
}
