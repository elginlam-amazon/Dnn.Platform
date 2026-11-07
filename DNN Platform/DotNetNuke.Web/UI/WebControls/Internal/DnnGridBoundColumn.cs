// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls.Internal
{
    using Microsoft.AspNetCore.Components;

    /// <summary>A column of a <see cref="DnnGrid"/> bound to a field of the data item.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.BoundField</c>: the value of <see cref="DataField"/>
    /// is read off the data item and rendered as text, optionally through
    /// <see cref="DataFormatString"/>.
    /// </remarks>
    public class DnnGridBoundColumn : DnnGridColumn
    {
        /// <summary>Gets or sets the name of the data item's field this column shows.</summary>
        public string DataField { get; set; }

        /// <summary>Gets or sets the format applied to the field's value, e.g. <c>"{0:d}"</c>.</summary>
        public string DataFormatString { get; set; }

        /// <inheritdoc />
        public override RenderFragment RenderCell(DnnGridRow row)
        {
            var text = DnnGrid.FormatFieldValue(row?.DataItem, this.DataField, this.DataFormatString);
            return builder => builder.AddContent(0, text);
        }
    }
}
