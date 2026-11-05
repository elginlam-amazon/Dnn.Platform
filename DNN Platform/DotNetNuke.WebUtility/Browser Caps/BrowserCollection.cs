// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// A strongly typed collection of <see cref="Browser"/> entries.
    /// </summary>
    public class BrowserCollection : CollectionBase, IList<Browser>
    {
        /// <inheritdoc />
        public bool IsReadOnly => this.InnerList.IsReadOnly;

        /// <inheritdoc />
        int ICollection<Browser>.Count => this.Count;

        /// <summary>Gets or sets the browser at the given position.</summary>
        /// <param name="index">The zero based position of the browser.</param>
        /// <returns>The browser at that position.</returns>
        public virtual Browser this[int index]
        {
            get => (Browser)this.List[index];
            set => this.List[index] = value;
        }

        /// <inheritdoc />
        Browser IList<Browser>.this[int index]
        {
            get => this[index];
            set => this[index] = value;
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1725:ParameterNamesShouldMatchBaseDeclaration", Justification = "The parameter name is part of the public surface preserved from the original VB.NET implementation.")]
        public void Add(Browser b)
        {
            this.InnerList.Add(b);
        }

        /// <inheritdoc />
        public bool Remove(Browser item)
        {
            if (!this.Contains(item))
            {
                return false;
            }

            this.InnerList.Remove(item);
            return true;
        }

        /// <inheritdoc />
        public bool Contains(Browser item)
        {
            return this.InnerList.Contains(item);
        }

        /// <inheritdoc />
        public void CopyTo(Browser[] array, int arrayIndex)
        {
            this.InnerList.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        public int IndexOf(Browser item)
        {
            return this.InnerList.IndexOf(item);
        }

        /// <inheritdoc />
        public void Insert(int index, Browser item)
        {
            this.InnerList.Insert(index, item);
        }

        /// <inheritdoc />
        void ICollection<Browser>.Clear()
        {
            this.Clear();
        }

        /// <inheritdoc />
        void IList<Browser>.RemoveAt(int index)
        {
            this.RemoveAt(index);
        }

        /// <inheritdoc />
        IEnumerator<Browser> IEnumerable<Browser>.GetEnumerator()
        {
            return new BrowserEnumerator(this.GetEnumerator());
        }

        private sealed class BrowserEnumerator : IEnumerator<Browser>
        {
            private readonly IEnumerator enumerator;

            public BrowserEnumerator(IEnumerator enumerator)
            {
                this.enumerator = enumerator;
            }

            /// <inheritdoc />
            public Browser Current => (Browser)this.enumerator.Current;

            /// <inheritdoc />
            object IEnumerator.Current => this.Current;

            /// <inheritdoc />
            public void Dispose()
            {
                if (this.enumerator is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            /// <inheritdoc />
            public bool MoveNext()
            {
                return this.enumerator.MoveNext();
            }

            /// <inheritdoc />
            public void Reset()
            {
                this.enumerator.Reset();
            }
        }
    }
}
