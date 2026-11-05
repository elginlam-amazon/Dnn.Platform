// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// The tabs of a <see cref="DNNTabStrip"/>.
    /// </summary>
    /// <remarks>
    /// The VB original delegated to the WebForms <c>Control.Controls</c> collection. That API does
    /// not exist in .NET 10, so the collection delegates to the owner's child-control list
    /// (<c>Children</c>/<c>HasChildren</c> of the Client API control abstraction) instead. Every
    /// public member keeps its original name, signature and behavior, including the
    /// <c>AddAt(-1, tab)</c> convention that appends.
    /// </remarks>
    public sealed class TabStripTabCollection : IList, ICollection, IEnumerable
    {
        private readonly DNNTabStrip owner;

        /// <summary>Initializes a new instance of the <see cref="TabStripTabCollection"/> class.</summary>
        /// <param name="owner">The owner.</param>
        internal TabStripTabCollection(DNNTabStrip owner)
        {
            this.owner = owner;
        }

        /// <summary>Gets the count.</summary>
        public int Count
        {
            get { return !this.owner.HasChildren ? 0 : this.owner.Children.Count; }
        }

        /// <summary>Gets a value indicating whether <c>IsReadOnly</c>.</summary>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>Gets a value indicating whether <c>IsSynchronized</c>.</summary>
        public bool IsSynchronized
        {
            get { return false; }
        }

        /// <summary>Gets the sync Root.</summary>
        public object SyncRoot
        {
            get { return this; }
        }

        /// <inheritdoc/>
        bool IList.IsFixedSize
        {
            get { return false; }
        }

        /// <summary>Gets or sets the item at the given position.</summary>
        /// <param name="index">The index.</param>
        /// <returns>The item at the given position.</returns>
        public DNNTab this[int index]
        {
            get { return (DNNTab)this.owner.Children[index]; }
        }

        /// <inheritdoc/>
        object IList.this[int index]
        {
            get
            {
                return this.owner.Children[index];
            }

            set
            {
                this.RemoveAt(index);
                this.AddAt(index, (DNNTab)value);
            }
        }

        /// <summary>Gets the <c>Add</c> operation.</summary>
        /// <param name="row">The row.</param>
        /// <returns>The result of the operation.</returns>
        public int Add(DNNTab row)
        {
            this.AddAt(-1, row);
            return this.owner.Children.Count - 1;
        }

        /// <summary>Performs the <c>AddAt</c> operation.</summary>
        /// <param name="index">The index.</param>
        /// <param name="row">The row.</param>
        public void AddAt(int index, DNNTab row)
        {
            if (index < 0)
            {
                this.owner.Children.Add(row);
            }
            else
            {
                this.owner.Children.Insert(index, row);
            }

            if (string.IsNullOrEmpty(row.Id))
            {
                row.Id = this.owner.Children.Count.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Performs the <c>AddRange</c> operation.</summary>
        /// <param name="rows">The rows.</param>
        public void AddRange(DNNTab[] rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            foreach (var tab in rows)
            {
                this.Add(tab);
            }
        }

        /// <summary>Performs the <c>Clear</c> operation.</summary>
        public void Clear()
        {
            if (this.owner.HasChildren)
            {
                this.owner.Children.Clear();
            }
        }

        /// <summary>Performs the <c>CopyTo</c> operation.</summary>
        /// <param name="array">The array.</param>
        /// <param name="index">The index.</param>
        public void CopyTo(Array array, int index)
        {
            var enumerator = this.GetEnumerator();
            while (enumerator.MoveNext())
            {
                index += 1;
                array.SetValue(enumerator.Current, index);
            }
        }

        /// <summary>Gets the <c>FindTab</c> operation.</summary>
        /// <param name="strId">The str Id.</param>
        /// <returns>The result of the operation.</returns>
        public DNNTab FindTab(string strId)
        {
            var enumerator = this.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var current = (DNNTab)enumerator.Current;
                if (current.Id == strId)
                {
                    return current;
                }
            }

            return null;
        }

        /// <summary>Gets the <c>GetEnumerator</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public IEnumerator GetEnumerator()
        {
            return this.owner.Children.GetEnumerator();
        }

        /// <summary>Gets the <c>GetRowIndex</c> operation.</summary>
        /// <param name="row">The row.</param>
        /// <returns>The result of the operation.</returns>
        public int GetRowIndex(DNNTab row)
        {
            return !this.owner.HasChildren ? -1 : this.owner.Children.IndexOf(row);
        }

        /// <summary>Performs the <c>Remove</c> operation.</summary>
        /// <param name="row">The row.</param>
        public void Remove(DNNTab row)
        {
            this.owner.Children.Remove(row);
        }

        /// <summary>Performs the <c>RemoveAt</c> operation.</summary>
        /// <param name="index">The index.</param>
        public void RemoveAt(int index)
        {
            this.owner.Children.RemoveAt(index);
        }

        /// <summary>Gets the <c>ToJSON</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public string ToJSON()
        {
            var builder = new StringBuilder();
            builder.Append("tabs:[");
            var first = true;
            var enumerator = this.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var current = (DNNTab)enumerator.Current;
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append(ScriptGenerator.GetMarshalledPropertyJSON(current, current.MarshalledProperties()));
            }

            builder.Append(']');

            first = true;
            builder.Append(",tablabels:[");
            var labelEnumerator = this.GetEnumerator();
            while (labelEnumerator.MoveNext())
            {
                var current = (DNNTab)labelEnumerator.Current;
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append(ScriptGenerator.GetMarshalledPropertyJSON(current.Label));
            }

            builder.Append(']');
            return builder.ToString();
        }

        /// <inheritdoc/>
        int IList.Add(object o)
        {
            return this.Add((DNNTab)o);
        }

        /// <inheritdoc/>
        bool IList.Contains(object o)
        {
            return this.owner.Children.Contains((DNNTab)o);
        }

        /// <inheritdoc/>
        int IList.IndexOf(object o)
        {
            return this.owner.Children.IndexOf((DNNTab)o);
        }

        /// <inheritdoc/>
        void IList.Insert(int index, object o)
        {
            this.AddAt(index, (DNNTab)o);
        }

        /// <inheritdoc/>
        void IList.Remove(object o)
        {
            this.Remove((DNNTab)o);
        }
    }
}
