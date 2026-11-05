// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    /// <summary>The horizontal alignment of the content of a grid cell.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.HorizontalAlign</c>.</remarks>
    public enum HorizontalAlign
    {
        /// <summary>No alignment is rendered.</summary>
        NotSet = 0,

        /// <summary>Align to the left.</summary>
        Left = 1,

        /// <summary>Align centered.</summary>
        Center = 2,

        /// <summary>Align to the right.</summary>
        Right = 3,

        /// <summary>Justify the content.</summary>
        Justify = 4,
    }

    /// <summary>The vertical alignment of the content of a grid cell.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.VerticalAlign</c>.</remarks>
    public enum VerticalAlign
    {
        /// <summary>No alignment is rendered.</summary>
        NotSet = 0,

        /// <summary>Align to the top.</summary>
        Top = 1,

        /// <summary>Align to the middle.</summary>
        Middle = 2,

        /// <summary>Align to the bottom.</summary>
        Bottom = 3,
    }

    /// <summary>The grid lines a data grid renders.</summary>
    /// <remarks>Replaces <c>System.Web.UI.WebControls.GridLines</c>.</remarks>
    public enum GridLines
    {
        /// <summary>No grid lines.</summary>
        None = 0,

        /// <summary>Horizontal grid lines only.</summary>
        Horizontal = 1,

        /// <summary>Vertical grid lines only.</summary>
        Vertical = 2,

        /// <summary>Both horizontal and vertical grid lines.</summary>
        Both = 3,
    }

    /// <summary>The kind of row a data grid renders.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.UI.WebControls.ListItemType</c>. The member names and values are kept
    /// so that the DNN column templates, which switch over this enum, stay source compatible.
    /// </remarks>
    public enum ListItemType
    {
        /// <summary>The header row.</summary>
        Header = 0,

        /// <summary>The footer row.</summary>
        Footer = 1,

        /// <summary>A data row.</summary>
        Item = 2,

        /// <summary>An alternating data row.</summary>
        AlternatingItem = 3,

        /// <summary>The selected data row.</summary>
        SelectedItem = 4,

        /// <summary>The data row currently being edited.</summary>
        EditItem = 5,

        /// <summary>A separator between data rows.</summary>
        Separator = 6,

        /// <summary>The pager row.</summary>
        Pager = 7,
    }
}
