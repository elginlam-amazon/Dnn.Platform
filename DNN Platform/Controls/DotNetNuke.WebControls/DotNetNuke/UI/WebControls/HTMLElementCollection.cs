// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System.Collections;
    using System.Globalization;
    using System.Text;

    /// <summary>The <see cref="HTMLElementCollection"/> class.</summary>
    public class HTMLElementCollection : CollectionBase
    {
        /// <summary>Gets or sets the item at the given position.</summary>
        /// <param name="index">The index.</param>
        /// <returns>The item at the given position.</returns>
        public HTMLElement this[int index]
        {
            get { return (HTMLElement)this.List[index]; }
            set { this.List[index] = value; }
        }

        /// <summary>Gets the <c>Add</c> operation.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The result of the operation.</returns>
        public int Add(HTMLElement value)
        {
            return this.List.Add(value);
        }

        /// <summary>Performs the <c>AddRange</c> operation.</summary>
        /// <param name="value">The value.</param>
        public void AddRange(HTMLElement[] value)
        {
            for (var index = 0; index <= value.Length - 1; index++)
            {
                this.Add(value[index]);
            }
        }

        /// <summary>Determines whether the collection contains the given element.</summary>
        /// <param name="value">The element to look for.</param>
        /// <returns>Always <see langword="false"/>, preserving the original (never implemented) VB behavior.</returns>
        public bool Contains(HTMLElement value)
        {
            return false;
        }

        /// <summary>Performs the <c>CopyTo</c> operation.</summary>
        /// <param name="array">The array.</param>
        /// <param name="index">The index.</param>
        public void CopyTo(HTMLElement[] array, int index)
        {
            this.List.CopyTo(array, index);
        }

        /// <summary>Gets the position of the given element.</summary>
        /// <param name="value">The element to look for.</param>
        /// <returns>Always <c>0</c>, preserving the original (never implemented) VB behavior.</returns>
        public int IndexOf(HTMLElement value)
        {
            return 0;
        }

        /// <summary>Performs the <c>Insert</c> operation.</summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        public void Insert(int index, HTMLElement value)
        {
            this.List.Insert(index, value);
        }

        /// <summary>Performs the <c>Remove</c> operation.</summary>
        /// <param name="value">The value.</param>
        public void Remove(HTMLElement value)
        {
            this.List.Remove(value);
        }

        /// <summary>Gets the <c>ToJSON</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public string ToJSON()
        {
            return this.ToJSON(string.Empty);
        }

        /// <summary>Gets the <c>ToJSON</c> operation.</summary>
        /// <param name="keyAttribute">The key Attribute.</param>
        /// <returns>The result of the operation.</returns>
        public string ToJSON(string keyAttribute)
        {
            var builder = new StringBuilder();
            builder.Append('{');
            var generatedKeyCount = 0;
            foreach (HTMLElement current in this.List)
            {
                if (builder.Length > 1)
                {
                    builder.Append(',');
                }

                string key;
                if (current.Attributes.Contains(keyAttribute))
                {
                    key = System.Convert.ToString(current.Attributes[keyAttribute], CultureInfo.InvariantCulture);
                }
                else
                {
                    generatedKeyCount += 1;
                    key = "__" + generatedKeyCount.ToString(CultureInfo.InvariantCulture);
                }

                builder.Append(key + ":" + current.ToJSON());
            }

            builder.Append('}');
            return builder.ToString();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var builder = new StringBuilder();
            foreach (HTMLElement current in this.List)
            {
                builder.Append(current.Raw);
            }

            return builder.ToString();
        }
    }
}
