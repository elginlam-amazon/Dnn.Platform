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

    /// <summary>Restricts the client to add only controls of specific type into the control collection.</summary>
    /// <typeparam name="T">The type of control.</typeparam>
    /// <remarks>
    /// This derived from <c>System.Web.UI.ControlCollection</c>. <see cref="DnnControlCollection"/>
    /// is sealed, so the type check is now applied by a wrapper around the owner's live child
    /// collection; the behaviour (adding a control of the wrong type throws) is unchanged.
    /// </remarks>
    public class TypedControlCollection<T> : IList<T>
        where T : class, IClientApiControl
    {
        private readonly DnnComponentBase owner;

        /// <summary>Initializes a new instance of the <see cref="TypedControlCollection{T}"/> class.</summary>
        /// <param name="owner">The owner control.</param>
        public TypedControlCollection(DnnComponentBase owner)
        {
            this.owner = owner;
        }

        /// <inheritdoc />
        public int Count => this.owner.Controls.Count;

        /// <inheritdoc />
        public bool IsReadOnly => false;

        /// <inheritdoc />
        public T this[int index]
        {
            get => (T)this.owner.Controls[index];
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public int IndexOf(T item)
            => this.owner.Controls.IndexOf(item);

        /// <inheritdoc />
        public void Insert(int index, T item)
            => throw new NotSupportedException();

        /// <summary>Adds a control, rejecting a control that is not a <typeparamref name="T"/>.</summary>
        /// <param name="child">The control to add.</param>
        public virtual void Add(IClientApiControl child)
        {
            if (child is not T)
            {
                this.ThrowOnInvalidControlType();
            }

            this.owner.Controls.Add(child);
        }

        /// <summary>Inserts a control, rejecting a control that is not a <typeparamref name="T"/>.</summary>
        /// <param name="index">The position to insert at.</param>
        /// <param name="child">The control to insert.</param>
        public virtual void AddAt(int index, IClientApiControl child)
        {
            if (child is not T)
            {
                this.ThrowOnInvalidControlType();
            }

            this.owner.Controls.AddAt(index, child);
        }

        /// <inheritdoc />
        public void Add(T item)
            => this.owner.Controls.Add(item);

        /// <inheritdoc />
        public bool Contains(T item)
            => this.owner.Controls.Contains(item);

        /// <inheritdoc />
        public void CopyTo(T[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);

            var index = arrayIndex;
            foreach (var item in this)
            {
                array[index++] = item;
            }
        }

        /// <inheritdoc />
        public void Clear()
            => this.owner.Controls.Clear();

        /// <inheritdoc />
        public void RemoveAt(int index)
            => this.owner.Controls.RemoveAt(index);

        /// <inheritdoc />
        public bool Remove(T item)
        {
            if (!this.Contains(item))
            {
                return false;
            }

            this.owner.Controls.Remove(item);
            return true;
        }

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            foreach (var child in this.owner.Controls.ToList())
            {
                yield return child as T;
            }
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
            => this.GetEnumerator();

        /// <summary>Throws an <see cref="InvalidOperationException"/>.</summary>
        /// <exception cref="InvalidOperationException">Always.</exception>
        protected virtual void ThrowOnInvalidControlType()
        {
            throw new InvalidOperationException("Not supported");
        }
    }
}
