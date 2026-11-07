// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using Microsoft.AspNetCore.Components;

    /// <summary>A template whose content is a literal piece of markup, or a render fragment.</summary>
    /// <remarks>
    /// Was a <c>System.Web.UI.ITemplate</c> that added a <c>LiteralControl</c> (or a given control)
    /// to the container it was instantiated in. It now implements <see cref="IGridTemplate"/> and
    /// returns that content as a <see cref="RenderFragment"/>; the constructor that took a
    /// <c>System.Web.UI.Control</c> takes a <see cref="RenderFragment"/> instead, which is the
    /// Blazor equivalent of "a piece of UI someone else built".
    /// </remarks>
    public class LiteralTemplate : IGridTemplate
    {
        private readonly RenderFragment fragment;
        private readonly string strHTML = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="LiteralTemplate"/> class.</summary>
        /// <param name="html">The literal HTML that is the contents of this template.</param>
        public LiteralTemplate(string html)
        {
            this.strHTML = html;
        }

        /// <summary>Initializes a new instance of the <see cref="LiteralTemplate"/> class.</summary>
        /// <param name="ctl">The content of this template.</param>
        public LiteralTemplate(RenderFragment ctl)
        {
            this.fragment = ctl;
        }

        /// <inheritdoc />
        public RenderFragment Render(DataGridCell cell) => this.InstantiateIn();

        /// <summary>Gets the content of this template.</summary>
        /// <returns>The content.</returns>
        /// <remarks>Replaces <c>ITemplate.InstantiateIn(Control container)</c>: there is no container
        /// control tree to add to any more, the caller renders the returned fragment where the
        /// container used to be.</remarks>
        public RenderFragment InstantiateIn()
        {
            if (this.fragment == null)
            {
                var html = this.strHTML;
                return builder => builder.AddMarkupContent(0, html);
            }

            return this.fragment;
        }
    }
}
