// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;

    using DotNetNuke.UI;
    using DotNetNuke.UI.Utilities;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A tab control.</summary>
    /// <remarks>
    /// This was a <c>WebControl</c> rendering a <c>div</c> around the content of its
    /// <c>ITemplate Content</c>, with a separate <c>ITemplate Header</c> rendered by the hosting
    /// tab strip. Both templates are <see cref="RenderFragment"/> now, which is the ASP.NET Core
    /// equivalent of an <c>ITemplate</c>; the imperative <see cref="DnnComponentBase.Controls"/>
    /// tree is still honoured so that code that added children directly keeps working.
    /// </remarks>
    public class DnnTab : DnnComponentBase
    {
        /// <summary>Gets or sets the header template.</summary>
        [Parameter]
        public virtual RenderFragment Header { get; set; }

        /// <summary>Gets or sets the content template.</summary>
        [Parameter]
        public virtual RenderFragment Content { get; set; }

        /// <inheritdoc />
        protected override string TagName => "div";

        /// <inheritdoc />
        public override IClientApiControl FindControl(string id)
        {
            this.EnsureChildControls();
            return base.FindControl(id);
        }

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (this.Content != null)
            {
                builder.AddContent(sequence++, this.Content);
            }

            base.RenderContents(builder, ref sequence);
        }
        #pragma warning restore ASP0006
    }
}
