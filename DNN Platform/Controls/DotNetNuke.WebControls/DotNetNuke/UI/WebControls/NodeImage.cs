// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// An image used by a tree node.
    /// </summary>
    /// <remarks>
    /// The VB original implemented <c>System.Web.UI.IStateManager</c> over a
    /// <c>System.Web.UI.StateBag</c>. Neither type exists in .NET 10, so the backing store is a
    /// plain dictionary while the state members keep their original names, signatures and
    /// observable behavior (save returns the stored state, load restores it).
    /// </remarks>
    public class NodeImage
    {
        private bool marked;
        private Dictionary<string, object> state;
        private bool dirty;

        /// <summary>Initializes a new instance of the <see cref="NodeImage"/> class.</summary>
        public NodeImage()
        {
            this.marked = false;
        }

        /// <summary>Initializes a new instance of the <see cref="NodeImage"/> class.</summary>
        /// <param name="newImageUrl">The new Image Url.</param>
        public NodeImage(string newImageUrl)
        {
            this.marked = false;
            ArgumentNullException.ThrowIfNull(newImageUrl);

            this.TrackState();
            this.ImageUrl = newImageUrl;
        }

        /// <summary>Gets a value indicating whether the stored values are being tracked.</summary>
        public bool IsTrackingState
        {
            get { return this.marked; }
        }

        /// <summary>Gets or sets image Url.</summary>
        public string ImageUrl
        {
            get
            {
                object value;
                if (this.State.TryGetValue("ImageUrl", out value) && value != null)
                {
                    return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                }

                return string.Empty;
            }

            set
            {
                this.State["ImageUrl"] = value;
            }
        }

        /// <summary>Gets a value indicating whether the stored state was explicitly marked dirty.</summary>
        internal bool IsDirty
        {
            get { return this.dirty; }
        }

        /// <summary>Gets the state bag replacement holding the persisted values of this image.</summary>
        protected IDictionary<string, object> State
        {
            get
            {
                if (ReferenceEquals(this.state, null))
                {
                    this.state = new Dictionary<string, object>();
                }

                return this.state;
            }
        }

        /// <summary>Restores the stored values of this image.</summary>
        /// <param name="savedState">The saved State.</param>
        public void LoadState(object savedState)
        {
            if (savedState != null)
            {
                var restored = savedState as Dictionary<string, object>;
                if (restored != null)
                {
                    foreach (var pair in restored)
                    {
                        this.State[pair.Key] = pair.Value;
                    }
                }
            }
        }

        /// <summary>Saves the stored values of this image.</summary>
        /// <returns>The saved values.</returns>
        public object SaveState()
        {
            if (ReferenceEquals(this.state, null))
            {
                return null;
            }

            return new Dictionary<string, object>(this.state);
        }

        /// <summary>Starts tracking the stored values of this image.</summary>
        public void TrackState()
        {
            this.marked = true;
        }

        /// <summary>Marks every stored value as changed so that it is persisted again.</summary>
        internal void SetDirty()
        {
            if (!ReferenceEquals(this.state, null))
            {
                this.dirty = true;
            }
        }
    }
}
