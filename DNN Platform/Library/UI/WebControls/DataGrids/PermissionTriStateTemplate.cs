// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls.Internal
{
    using System;
    using System.Data;
    using System.Globalization;
    using System.IO;

    using DotNetNuke.Abstractions.Security.Permissions;

    using Microsoft.AspNetCore.Components;

    /// <summary>Renders a <see cref="PermissionTriState"/> into a permissions grid cell.</summary>
    /// <remarks>
    /// Was an <c>ITemplate</c> that added the control to the cell's control tree and bound it
    /// through its <c>DataBinding</c> event. It now builds and binds the control up front, registers
    /// it in the cell (so that the permissions grid keeps reading it back through
    /// <c>cell.Controls[0]</c>) and emits its markup into the render tree.
    /// </remarks>
    internal class PermissionTriStateTemplate : IGridTemplate
    {
        private readonly IPermissionDefinitionInfo permission;

        /// <summary>Initializes a new instance of the <see cref="PermissionTriStateTemplate"/> class.</summary>
        /// <param name="permission">The permission info.</param>
        public PermissionTriStateTemplate(IPermissionDefinitionInfo permission)
        {
            this.permission = permission;
        }

        /// <summary>Gets or sets a value indicating whether the permission is the full control permission.</summary>
        public bool IsFullControl { get; set; }

        /// <summary>Gets or sets a value indicating whether the permission is the view permission.</summary>
        public bool IsView { get; set; }

        /// <summary>Gets or sets a value indicating whether the permission can be denied.</summary>
        public bool SupportDenyMode { get; set; }

        /// <inheritdoc />
        public RenderFragment Render(DataGridCell cell)
        {
            ArgumentNullException.ThrowIfNull(cell);

            var triState = new PermissionTriState();
            this.BindToTriState(triState, cell);
            cell.Controls.Clear();
            cell.Controls.Add(triState);

            string markup;
            using (var writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                triState.RenderControl(writer);
                markup = writer.ToString();
            }

            return builder => builder.AddMarkupContent(0, markup);
        }

        /// <summary>Binds the given control to the record of the given cell.</summary>
        /// <param name="triState">The control to bind.</param>
        /// <param name="cell">The cell being rendered.</param>
        public void BindToTriState(PermissionTriState triState, DataGridCell cell)
        {
            ArgumentNullException.ThrowIfNull(triState);
            ArgumentNullException.ThrowIfNull(cell);

            var dataRowView = (DataRowView)cell.Item.DataItem;

            triState.Value = dataRowView[this.permission.PermissionName].ToString();
            triState.Locked = !bool.Parse(dataRowView[this.permission.PermissionName + "_Enabled"].ToString());
            triState.SupportsDenyMode = this.SupportDenyMode;
            triState.IsFullControl = this.IsFullControl;
            triState.IsView = this.IsView;
            triState.PermissionKey = this.permission.PermissionKey;
        }
    }
}
