// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;

    /// <summary>
    /// A collection of <see cref="NodeImage"/> instances.
    /// </summary>
    /// <remarks>
    /// The VB original implemented <c>System.Web.UI.IStateManager</c>, which has no .NET 10
    /// equivalent; the interface is dropped while every member keeps its original name, signature
    /// and behavior.
    /// </remarks>
    public class NodeImageCollection : CollectionBase
    {
        /// <summary>Initializes a new instance of the <see cref="NodeImageCollection"/> class.</summary>
        public NodeImageCollection()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="NodeImageCollection"/> class.</summary>
        /// <param name="value">The value.</param>
        public NodeImageCollection(NodeImageCollection value)
        {
            this.AddRange(value);
        }

        /// <summary>Initializes a new instance of the <see cref="NodeImageCollection"/> class.</summary>
        /// <param name="value">The value.</param>
        public NodeImageCollection(NodeImage[] value)
        {
            this.AddRange(value);
        }

        /// <summary>Gets or sets a value indicating whether the stored values are being tracked.</summary>
        public bool IsTrackingState { get; protected set; }

        /// <summary>Gets or sets the item at the given position.</summary>
        /// <param name="index">The index.</param>
        /// <returns>The item at the given position.</returns>
        public NodeImage this[int index]
        {
            get { return (NodeImage)this.List[index]; }
            set { this.List[index] = value; }
        }

        /// <summary>Gets the <c>Add</c> operation.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The result of the operation.</returns>
        public int Add(NodeImage value)
        {
            var index = this.List.Add(value);
            if (this.IsTrackingState)
            {
                value.TrackState();
                value.SetDirty();
            }

            return index;
        }

        /// <summary>Gets the <c>Add</c> operation.</summary>
        /// <param name="imageUrl">The image Url.</param>
        /// <returns>The result of the operation.</returns>
        public int Add(string imageUrl)
        {
            return this.Add(new NodeImage(imageUrl));
        }

        /// <summary>Performs the <c>AddRange</c> operation.</summary>
        /// <param name="value">The value.</param>
        public void AddRange(NodeImage[] value)
        {
            for (var index = 0; index <= value.Length - 1; index++)
            {
                this.Add(value[index]);
            }
        }

        /// <summary>Performs the <c>AddRange</c> operation.</summary>
        /// <param name="value">The value.</param>
        public void AddRange(NodeImageCollection value)
        {
            for (var index = 0; index <= value.Count - 1; index++)
            {
                this.Add((NodeImage)value.List[index]);
            }
        }

        /// <summary>Gets the <c>Contains</c> operation.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The result of the operation.</returns>
        public bool Contains(NodeImage value)
        {
            return !ReferenceEquals(this.GetImage(value.ImageUrl), null);
        }

        /// <summary>Gets the <c>Contains</c> operation.</summary>
        /// <param name="strImageUrl">The str Image Url.</param>
        /// <returns>The result of the operation.</returns>
        public bool Contains(string strImageUrl)
        {
            return !ReferenceEquals(this.GetImage(strImageUrl), null);
        }

        /// <summary>Performs the <c>CopyTo</c> operation.</summary>
        /// <param name="array">The array.</param>
        /// <param name="index">The index.</param>
        public void CopyTo(NodeImage[] array, int index)
        {
            this.List.CopyTo(array, index);
        }

        /// <summary>Gets the <c>IndexOf</c> operation.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The result of the operation.</returns>
        public int IndexOf(NodeImage value)
        {
            return this.GetImageIndex(value.ImageUrl);
        }

        /// <summary>Gets the <c>IndexOf</c> operation.</summary>
        /// <param name="strImageUrl">The str Image Url.</param>
        /// <returns>The result of the operation.</returns>
        public int IndexOf(string strImageUrl)
        {
            return this.GetImageIndex(strImageUrl);
        }

        /// <summary>Performs the <c>Insert</c> operation.</summary>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        public void Insert(int index, NodeImage value)
        {
            this.List.Insert(index, value);
        }

        /// <summary>Restores the stored values of the images of this collection.</summary>
        /// <param name="state">The state.</param>
        public void LoadState(object state)
        {
            if (state != null)
            {
                var savedStates = (object[])state;
                for (var index = 0; index <= savedStates.Length - 1; index++)
                {
                    var image = new NodeImage();
                    this.Add(image);
                    image.TrackState();
                    image.LoadState(savedStates[index]);
                }
            }
        }

        /// <summary>Performs the <c>Remove</c> operation.</summary>
        /// <param name="value">The value.</param>
        public void Remove(NodeImage value)
        {
            this.List.Remove(value);
        }

        /// <summary>Saves the stored values of the images of this collection.</summary>
        /// <returns>The saved values.</returns>
        public object SaveState()
        {
            if (this.Count == 0)
            {
                return null;
            }

            // Preserves the original over-allocation of one extra (unused) slot.
            var savedStates = new object[this.Count + 1];
            for (var index = 0; index <= this.Count - 1; index++)
            {
                savedStates[index] = this[index].SaveState();
            }

            return savedStates;
        }

        /// <summary>Gets the <c>ToArray</c> operation.</summary>
        /// <returns>The result of the operation.</returns>
        public NodeImage[] ToArray()
        {
            var imageArray = new NodeImage[this.Count];
            this.CopyTo(imageArray, 0);
            return imageArray;
        }

        /// <summary>Starts tracking the stored values of this collection.</summary>
        public void TrackState()
        {
            this.IsTrackingState = true;
        }

        private NodeImage GetImage(string strUrl)
        {
            foreach (NodeImage current in this.List)
            {
                if (strUrl == current.ImageUrl)
                {
                    return current;
                }
            }

            return null;
        }

        private int GetImageIndex(string strUrl)
        {
            for (var index = 0; index <= this.List.Count - 1; index++)
            {
                if (strUrl == ((NodeImage)this.List[index]).ImageUrl)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
