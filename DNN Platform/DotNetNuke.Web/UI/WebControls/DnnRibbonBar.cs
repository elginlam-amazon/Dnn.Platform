// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Linq;

    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A ribbon bar control.</summary>
    /// <remarks>
    /// This was a <c>WebControl</c> with <c>[ParseChildren]</c> rendering through
    /// <c>HtmlTextWriter</c>. The markup it emits — the outer <c>div.dnnRibbon</c>, the
    /// <c>div.barContent</c>, the single-row table with one cell per visible group and the two
    /// <c>barBottom*</c> divs — is unchanged, and the group collection still rejects anything that
    /// is not a <see cref="DnnRibbonBarGroup"/>.
    /// </remarks>
    public class DnnRibbonBar : DnnComponentBase
    {
        private readonly IClientResourceController clientResourceController;
        private DnnRibbonBarGroupCollection groups;

        /// <summary>Initializes a new instance of the <see cref="DnnRibbonBar"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public DnnRibbonBar()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnRibbonBar"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        public DnnRibbonBar(IClientResourceController clientResourceController)
        {
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();

            this.CssClass = "dnnRibbon";
            Utilities.ApplyControlSkin(this.clientResourceController, this, "RibbonBar", "RibbonBar");
        }

        /// <summary>Gets the groups.</summary>
        public DnnRibbonBarGroupCollection Groups =>
            this.groups ??= new DnnRibbonBarGroupCollection(this);

        /// <inheritdoc />
        protected override string TagName => "div";

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            if (this.Visible)
            {
                Utilities.ApplyControlSkin(this.clientResourceController, this, "RibbonBar", "RibbonBar");
            }
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

            var groupList = this.Groups.ToList();
            if (groupList.Count > 0)
            {
                var firstGroup = groupList[0];
                firstGroup.CssClass = $"{firstGroup.CssClass} {firstGroup.CssClass.Trim()}First";
                var lastGroup = groupList[groupList.Count - 1];
                lastGroup.CssClass = $"{lastGroup.CssClass} {lastGroup.CssClass.Trim()}Last";
            }

            var sequence = 0;

            builder.OpenElement(sequence++, "div");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
            }

            foreach (var attribute in this.Attributes)
            {
                builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
            }

            var inlineStyle = this.BuildStyleAttribute();
            if (!string.IsNullOrEmpty(inlineStyle))
            {
                builder.AddAttribute(sequence++, "style", inlineStyle);
            }

            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "barContent");

            builder.OpenElement(sequence++, "table");
            builder.AddAttribute(sequence++, "cellpadding", "0");
            builder.AddAttribute(sequence++, "cellspacing", "0");
            builder.AddAttribute(sequence++, "border", "0");
            builder.OpenElement(sequence++, "tr");

            foreach (var group in groupList)
            {
                if (group.Visible)
                {
                    builder.OpenElement(sequence, "td");
                    RenderControl(builder, sequence + 1, group);
                    builder.CloseElement();
                    sequence += 2;
                }
            }

            builder.CloseElement(); // tr
            builder.CloseElement(); // table
            builder.CloseElement(); // div.barContent

            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "barBottomLeft");
            builder.CloseElement();

            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "barBottomRight");
            builder.CloseElement();

            builder.CloseElement(); // outer div
        }
        #pragma warning restore ASP0006
    }
}
