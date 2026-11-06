// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.UI;
    using DotNetNuke.UI.Utilities;

    /// <summary>A list of children controls.</summary>
    /// <typeparam name="TOwner">The type of the owner/parent control.</typeparam>
    /// <typeparam name="TChildren">The type of the children controls.</typeparam>
    /// <remarks>
    /// This was a strongly typed view over <c>Control.Controls</c>; it is now a strongly typed view
    /// over <see cref="DnnComponentBase.Controls"/>, which is the ASP.NET Core replacement of the
    /// Web Forms control collection. Every operation still goes straight to the owner's live child
    /// collection, so the control tree remains the single source of truth.
    /// </remarks>
    public sealed class UniformControlCollection<TOwner, TChildren> : IList<TChildren>
        where TOwner : DnnComponentBase
        where TChildren : class, IClientApiControl
    {
        private readonly TOwner owner;

        /// <summary>Initializes a new instance of the <see cref="UniformControlCollection{TOwner, TChildren}"/> class.</summary>
        /// <param name="owner">The owner control.</param>
        internal UniformControlCollection(TOwner owner)
        {
            this.owner = owner;
        }

        /// <inheritdoc />
        public int Count => this.owner.HasControls() ? this.owner.Controls.Count : 0;

        /// <inheritdoc />
        public bool IsReadOnly => false;

        /// <inheritdoc />
        public TChildren this[int index]
        {
            get => this.owner.Controls[index] as TChildren;

            set
            {
                this.RemoveAt(index);
                this.AddAt(index, value);
            }
        }

        /// <summary>Adds the specified <typeparamref name="TChildren"/> object to the collection at the specified index location.</summary>
        /// <param name="index">The location in the array at which to add the child control.</param>
        /// <param name="childControl">The <typeparamref name="TChildren"/> to add to the collection.</param>
        public void AddAt(int index, TChildren childControl)
        {
            this.owner.Controls.AddAt(index, childControl);
        }

        /// <inheritdoc />
        public int IndexOf(TChildren item)
        {
            return this.owner.Controls.IndexOf(item);
        }

        /// <inheritdoc />
        public void Insert(int index, TChildren item)
        {
            this.owner.Controls.AddAt(index, item);
        }

        /// <inheritdoc />
        public void RemoveAt(int index)
        {
            this.owner.Controls.RemoveAt(index);
        }

        /// <inheritdoc />
        public bool Remove(TChildren item)
        {
            this.owner.Controls.Remove(item);
            return true;
        }

        /// <inheritdoc />
        public IEnumerator<TChildren> GetEnumerator()
        {
            foreach (var child in this.owner.Controls.ToList())
            {
                yield return child as TChildren;
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            if (this.owner.HasControls())
            {
                this.owner.Controls.Clear();
            }
        }

        /// <inheritdoc />
        public void Add(TChildren item)
        {
            this.owner.Controls.Add(item);
        }

        /// <inheritdoc />
        public void CopyTo(TChildren[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);

            var index = arrayIndex;
            foreach (var child in this)
            {
                array[index++] = child;
            }
        }

        /// <inheritdoc />
        public bool Contains(TChildren item)
        {
            return this.owner.Controls.Contains(item);
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
