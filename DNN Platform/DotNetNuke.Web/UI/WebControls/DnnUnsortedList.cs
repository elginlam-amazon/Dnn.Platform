// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.UI.WebControls
{
    using System;

    using DotNetNuke.UI;
    using DotNetNuke.UI.Utilities;

    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>Creates a control that renders its children as a bulleted list.</summary>
    /// <remarks>
    /// Control renders an unordered list HTML control.
    /// Each child control in <see cref="DnnUnsortedList"/> is rendered as a separate list item.
    /// To obtain a control over list item style, add a <see cref="DnnUnsortedListItem" /> to a controls list,
    /// and tune this object appropriately.
    /// <para>
    /// This was a <c>WebControl, INamingContainer</c> whose <c>TagKey</c> was <c>ul</c> and which
    /// restricted its control collection to <see cref="DnnUnsortedListItem"/> by overriding
    /// <c>CreateControlCollection</c>. The restriction is enforced by <see cref="AddListItem"/> and
    /// the typed <see cref="ListItems"/> view now, because the ASP.NET Core control collection
    /// (<see cref="DnnControlCollection"/>) is sealed.
    /// </para>
    /// </remarks>
    public class DnnUnsortedList : DnnComponentBase
    {
        private UniformControlCollection<DnnUnsortedList, DnnUnsortedListItem> listItems;

        /// <summary>Gets the list items.</summary>
        public virtual UniformControlCollection<DnnUnsortedList, DnnUnsortedListItem> ListItems
        {
            get
            {
                return this.listItems ??= new UniformControlCollection<DnnUnsortedList, DnnUnsortedListItem>(this);
            }
        }

        /// <inheritdoc />
        protected override string TagName => "ul";

        /// <summary>A "macro" that adds a set of controls or control as a single list item (li). Use <c>ListItems.Add(DnnUnsortedListItem)</c> method.</summary>
        /// <remarks>All controls from the list will be rendered as children of a single list item.</remarks>
        /// <param name="listItemControls">The controls to add to a new list item.</param>
        public void AddListItem(params IClientApiControl[] listItemControls)
        {
            var listItem = new DnnUnsortedListItem();
            listItem.AddControls(listItemControls);
            this.ListItems.Add(listItem);
        }

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.EnsureChildControls();

            if (!this.Visible)
            {
                return;
            }

            var sequence = 0;
            builder.OpenElement(sequence++, "ul");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
            }

            if (!string.IsNullOrEmpty(this.CssClass))
            {
                builder.AddAttribute(sequence++, "class", this.CssClass);
            }

            var inlineStyle = this.BuildStyleAttribute();
            if (!string.IsNullOrEmpty(inlineStyle))
            {
                builder.AddAttribute(sequence++, "style", inlineStyle);
            }

            this.RenderContents(builder, ref sequence);

            builder.CloseElement();
        }
        #pragma warning restore ASP0006
    }
}
