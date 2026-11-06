// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls.Internal
{
    using Microsoft.AspNetCore.Components;

    /// <summary>A column of a <see cref="DnnGrid"/> whose cells are rendered by a template.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.TemplateField</c>. Its <c>ITemplate ItemTemplate</c> —
    /// which Web Forms instantiated into every data cell — is a
    /// <see cref="RenderFragment{TValue}"/> taking the row, which is the ASP.NET Core equivalent and
    /// gives the template the same access to the row's data item.
    /// </remarks>
    public class DnnGridTemplateColumn : DnnGridColumn
    {
        /// <summary>Gets or sets the template rendering the content of a data cell.</summary>
        public RenderFragment<DnnGridRow> ItemTemplate { get; set; }

        /// <summary>Gets or sets the template rendering the content of the header cell.</summary>
        public RenderFragment HeaderTemplate { get; set; }

        /// <inheritdoc />
        public override RenderFragment RenderCell(DnnGridRow row)
        {
            if (this.ItemTemplate == null)
            {
                return builder => { };
            }

            return builder => builder.AddContent(0, this.ItemTemplate(row));
        }
    }
}
