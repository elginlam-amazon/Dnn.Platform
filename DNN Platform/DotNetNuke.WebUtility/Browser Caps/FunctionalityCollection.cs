// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// A strongly typed collection of <see cref="FunctionalityInfo"/> entries.
    /// </summary>
    public class FunctionalityCollection : CollectionBase, IList<FunctionalityInfo>
    {
        /// <inheritdoc />
        public bool IsReadOnly => this.InnerList.IsReadOnly;

        /// <inheritdoc />
        int ICollection<FunctionalityInfo>.Count => this.Count;

        /// <summary>Gets or sets the functionality at the given position.</summary>
        /// <param name="index">The zero based position of the functionality.</param>
        /// <returns>The functionality at that position.</returns>
        public virtual FunctionalityInfo this[int index]
        {
            get => (FunctionalityInfo)this.List[index];
            set => this.List[index] = value;
        }

        /// <summary>Gets or sets the entry describing the given client functionality.</summary>
        /// <param name="functionality">The client functionality to look for.</param>
        /// <returns>The entry describing that functionality, or <see langword="null"/> when it is not in the collection.</returns>
        /// <exception cref="KeyNotFoundException">Thrown by the setter when the functionality is not in the collection.</exception>
        public virtual FunctionalityInfo this[ClientAPI.ClientFunctionality functionality]
        {
            get
            {
                FunctionalityInfo found = null;
                foreach (FunctionalityInfo fInfo in this.List)
                {
                    if (fInfo.Functionality == functionality)
                    {
                        found = fInfo;
                        break;
                    }
                }

                return found;
            }

            set
            {
                // REVIEW: the VB setter assigned to the For Each loop variable, which silently did
                // nothing; the entry is now really replaced. The throw-when-absent behavior is kept.
                var bFound = false;
                for (var i = 0; i < this.List.Count; i++)
                {
                    if (((FunctionalityInfo)this.List[i]).Functionality == functionality)
                    {
                        this.List[i] = value;
                        bFound = true;
                        break;
                    }
                }

                if (!bFound)
                {
                    throw new KeyNotFoundException("Item Not Found");
                }
            }
        }

        /// <inheritdoc />
        FunctionalityInfo IList<FunctionalityInfo>.this[int index]
        {
            get => this[index];
            set => this[index] = value;
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1725:ParameterNamesShouldMatchBaseDeclaration", Justification = "The parameter name is part of the public surface preserved from the original VB.NET implementation.")]
        public void Add(FunctionalityInfo f)
        {
            this.InnerList.Add(f);
        }

        /// <inheritdoc />
        public bool Remove(FunctionalityInfo item)
        {
            if (!this.Contains(item))
            {
                return false;
            }

            this.InnerList.Remove(item);
            return true;
        }

        /// <inheritdoc />
        public bool Contains(FunctionalityInfo item)
        {
            return this.InnerList.Contains(item);
        }

        /// <inheritdoc />
        public void CopyTo(FunctionalityInfo[] array, int arrayIndex)
        {
            this.InnerList.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc />
        public int IndexOf(FunctionalityInfo item)
        {
            return this.InnerList.IndexOf(item);
        }

        /// <inheritdoc />
        public void Insert(int index, FunctionalityInfo item)
        {
            this.InnerList.Insert(index, item);
        }

        /// <inheritdoc />
        void ICollection<FunctionalityInfo>.Clear()
        {
            this.Clear();
        }

        /// <inheritdoc />
        void IList<FunctionalityInfo>.RemoveAt(int index)
        {
            this.RemoveAt(index);
        }

        /// <inheritdoc />
        IEnumerator<FunctionalityInfo> IEnumerable<FunctionalityInfo>.GetEnumerator()
        {
            return new FunctionalityInfoEnumerator(this.GetEnumerator());
        }

        private sealed class FunctionalityInfoEnumerator : IEnumerator<FunctionalityInfo>
        {
            private readonly IEnumerator enumerator;

            public FunctionalityInfoEnumerator(IEnumerator enumerator)
            {
                this.enumerator = enumerator;
            }

            /// <inheritdoc />
            public FunctionalityInfo Current => (FunctionalityInfo)this.enumerator.Current;

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
