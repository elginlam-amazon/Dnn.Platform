// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;

    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A literal control.</summary>
    /// <remarks>
    /// This was a <c>System.Web.UI.WebControls.Literal</c>: it emits <see cref="Text"/> with no
    /// wrapper element at all, after resolving it through DNN's localization engine.
    /// </remarks>
    public class DnnLiteral : DnnComponentBase, ILocalizable
    {
        private bool localize = true;

        /// <summary>Gets or sets the text (markup) rendered by this control.</summary>
        [Parameter]
        public string Text { get; set; } = string.Empty;

        // BL0007: the parameter keeps the property accessors of the original Web Forms control, which forward to the child control or to view state.
        #pragma warning disable BL0007
        /// <inheritdoc />
        [Parameter]
        public bool Localize
        {
            get
            {
                return this.localize;
            }

            set
            {
                this.localize = value;
            }
        }
        #pragma warning restore BL0007

        /// <inheritdoc />
        [Parameter]
        public string LocalResourceFile { get; set; }

        /// <inheritdoc />
        public virtual void LocalizeStrings()
        {
            if (this.Localize)
            {
                if (!string.IsNullOrEmpty(this.Text))
                {
                    this.Text = Localization.GetString(this.Text, this.LocalResourceFile);
                }
            }
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            this.LocalResourceFile = Utilities.GetLocalResourceFile(this);
        }

        // ASP0006: the render tree of this ported Web Forms control is built imperatively through helper methods that share a running sequence number, so literal sequence numbers cannot express its structure.
        #pragma warning disable ASP0006
        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (!this.Visible)
            {
                return;
            }

            this.LocalizeStrings();

            var sequence = 0;
            if (!string.IsNullOrEmpty(this.Text))
            {
                builder.AddMarkupContent(sequence++, this.Text);
            }

            if (this.ChildContent != null)
            {
                builder.AddContent(sequence++, this.ChildContent);
            }

            this.RenderChildControls(builder, ref sequence);
        }
        #pragma warning restore ASP0006
    }
}
