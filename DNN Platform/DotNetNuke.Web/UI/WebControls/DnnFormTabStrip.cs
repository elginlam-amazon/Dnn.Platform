// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A tab strip control.</summary>
    /// <remarks>
    /// This was a <c>ListControl</c> rendering through <c>HtmlTextWriter</c>. The markup
    /// (<c>ul &gt; li &gt; a</c>, one per item, the item value being the anchor target) is
    /// unchanged; <c>ListItem</c> became <see cref="DnnListItem"/>.
    /// </remarks>
    public class DnnFormTabStrip : DnnComponentBase
    {
        private readonly List<DnnListItem> items = new List<DnnListItem>();

        /// <summary>Gets the items of the tab strip.</summary>
        public IList<DnnListItem> Items => this.items;

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (!this.Visible)
            {
                return;
            }

            builder.OpenElement(0, "ul");
            builder.AddAttribute(1, "class", this.CssClass);

            foreach (var item in this.items)
            {
                builder.OpenElement(2, "li");
                builder.OpenElement(3, "a");
                builder.AddAttribute(4, "href", item.Value);
                builder.AddContent(5, item.Text);
                builder.CloseElement();
                builder.CloseElement();
            }

            builder.CloseElement();
        }
    }
}
