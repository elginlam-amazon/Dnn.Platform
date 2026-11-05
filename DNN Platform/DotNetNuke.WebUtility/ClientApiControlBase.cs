// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Base implementation of <see cref="IClientApiControl"/> used by the controls this library
    /// creates on behalf of its callers.
    /// </summary>
    /// <remarks>
    /// Replaces the <c>System.Web.UI.Control</c> base class the original WebForms implementation
    /// derived <c>ClientAPIPostBackControl</c> and <c>NonNamingHiddenInput</c> from. It provides the
    /// identifier composition, the attribute bag and the parent/child bookkeeping those controls
    /// inherited from <c>Control</c>.
    /// </remarks>
    public abstract class ClientApiControlBase : IClientApiControl
    {
        private readonly ChildControlCollection children;
        private readonly Dictionary<string, string> attributes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Initializes a new instance of the <see cref="ClientApiControlBase"/> class.</summary>
        protected ClientApiControlBase()
        {
            this.children = new ChildControlCollection(this);
        }

        /// <inheritdoc/>
        public event EventHandler PreRender;

        /// <inheritdoc/>
        public string Id { get; set; }

        /// <inheritdoc/>
        /// <remarks>Mirrors the WebForms composition of <c>ClientID</c> from the naming container chain.</remarks>
        public virtual string ClientId =>
            this.NamingContainer is null ? this.Id : this.NamingContainer.ClientId + "_" + this.Id;

        /// <inheritdoc/>
        /// <remarks>Mirrors the WebForms composition of <c>UniqueID</c> from the naming container chain.</remarks>
        public virtual string UniqueId =>
            this.NamingContainer is null ? this.Id : this.NamingContainer.UniqueId + "$" + this.Id;

        /// <inheritdoc/>
        public IDictionary<string, string> Attributes => this.attributes;

        /// <inheritdoc/>
        public IClientApiControl Parent { get; internal set; }

        /// <inheritdoc/>
        public virtual IClientApiControl NamingContainer => this.Parent;

        /// <inheritdoc/>
        public IList<IClientApiControl> Children => this.children;

        /// <inheritdoc/>
        public bool HasChildren => this.children.Count > 0;

        /// <inheritdoc/>
        public virtual IClientApiPage Page => this.AssignedPage ?? this.Parent?.Page;

        /// <summary>
        /// Gets or sets the page this control was explicitly created for, which is used while the
        /// control is not part of a control tree yet.
        /// </summary>
        protected IClientApiPage AssignedPage { get; set; }

        /// <inheritdoc/>
        public virtual IClientApiControl FindControl(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (var child in this.children)
            {
                if (string.Equals(child.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            // like WebForms, the search continues into children that do not open a naming scope.
            foreach (var child in this.children)
            {
                if (child is IClientApiPage)
                {
                    continue;
                }

                var found = child.FindControl(id);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>Raises the <see cref="PreRender"/> event.</summary>
        /// <param name="e">The arguments of the event.</param>
        protected virtual void OnPreRender(EventArgs e)
        {
            this.PreRender?.Invoke(this, e);
        }

        /// <summary>
        /// The live child collection of a control, which keeps the <see cref="Parent"/> of the
        /// controls added to it up to date.
        /// </summary>
        private sealed class ChildControlCollection : Collection<IClientApiControl>
        {
            private readonly ClientApiControlBase owner;

            /// <summary>Initializes a new instance of the <see cref="ChildControlCollection"/> class.</summary>
            /// <param name="owner">The control owning this collection.</param>
            internal ChildControlCollection(ClientApiControlBase owner)
            {
                this.owner = owner;
            }

            /// <inheritdoc/>
            protected override void InsertItem(int index, IClientApiControl item)
            {
                base.InsertItem(index, item);
                Adopt(this.owner, item);
            }

            /// <inheritdoc/>
            protected override void SetItem(int index, IClientApiControl item)
            {
                base.SetItem(index, item);
                Adopt(this.owner, item);
            }

            /// <summary>Makes the given control a child of the given parent.</summary>
            /// <param name="parent">The parent of the control.</param>
            /// <param name="item">The control being added.</param>
            private static void Adopt(ClientApiControlBase parent, IClientApiControl item)
            {
                if (item is ClientApiControlBase child)
                {
                    child.Parent = parent;
                }
            }
        }
    }
}
