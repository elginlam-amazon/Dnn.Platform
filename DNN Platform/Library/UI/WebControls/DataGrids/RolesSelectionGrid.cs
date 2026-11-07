// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Common;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>A grid that lets the user pick a set of roles.</summary>
    /// <remarks>
    /// Was a <c>System.Web.UI.Control</c> that composed a <c>Panel</c>, a <c>DropDownList</c> and a
    /// <c>DataGrid</c> in <c>CreateChildControls</c>. It is now a <see cref="DnnComponentBase"/>
    /// (DNN's replacement for <c>System.Web.UI.Control</c>) that renders that same chrome, and the
    /// inner grid is a <see cref="DNNDataGrid"/> whose table is
    /// embedded through <see cref="DNNDataGrid.RenderTable"/>. The two view state backed properties
    /// became ordinary fields, which survive a render over a Blazor circuit by themselves.
    /// </remarks>
    public class RolesSelectionGrid : DnnComponentBase
    {
        private readonly DataTable dtRoleSelections = new DataTable();
        private readonly DNNDataGrid dgRoleSelection = new DNNDataGrid
        {
            AutoGenerateColumns = false,
            CellSpacing = 0,
            GridLines = GridLines.None,
        };

        private IList<RoleInfo> roles;
        private IList<string> selectedRoles;
        private ArrayList roleGroups;
        private string selectedRoleGroup = "-1";

        /// <summary>Gets the style of the alternating data rows.</summary>
        public TableItemStyle AlternatingItemStyle => this.dgRoleSelection.AlternatingItemStyle;

        /// <summary>Gets the columns of the grid.</summary>
        public DataGridColumnCollection Columns => this.dgRoleSelection.Columns;

        /// <summary>Gets the style of the footer row.</summary>
        public TableItemStyle FooterStyle => this.dgRoleSelection.FooterStyle;

        /// <summary>Gets the style of the header row.</summary>
        public TableItemStyle HeaderStyle => this.dgRoleSelection.HeaderStyle;

        /// <summary>Gets the style of the data rows.</summary>
        public TableItemStyle ItemStyle => this.dgRoleSelection.ItemStyle;

        /// <summary>Gets the rows of the grid.</summary>
        public DataGridItemCollection Items => this.dgRoleSelection.Items;

        /// <summary>Gets the style of the selected data row.</summary>
        public TableItemStyle SelectedItemStyle => this.dgRoleSelection.SelectedItemStyle;

        /// <summary>Gets or sets a value indicating whether a column is generated for every field of the bound records.</summary>
        public bool AutoGenerateColumns
        {
            get
            {
                return this.dgRoleSelection.AutoGenerateColumns;
            }

            set
            {
                this.dgRoleSelection.AutoGenerateColumns = value;
            }
        }

        /// <summary>Gets or sets the spacing between the cells of the grid.</summary>
        public int CellSpacing
        {
            get
            {
                return this.dgRoleSelection.CellSpacing;
            }

            set
            {
                this.dgRoleSelection.CellSpacing = value;
            }
        }

        /// <summary>Gets or sets the grid lines the grid renders.</summary>
        public GridLines GridLines
        {
            get
            {
                return this.dgRoleSelection.GridLines;
            }

            set
            {
                this.dgRoleSelection.GridLines = value;
            }
        }

        /// <summary>Gets or sets list of the Names of the selected Roles.</summary>
        public ArrayList SelectedRoleNames
        {
            get
            {
                this.UpdateRoleSelections();
                return new ArrayList(this.CurrentRoleSelection.ToArray());
            }

            set
            {
                this.CurrentRoleSelection = value.Cast<string>().ToList();
            }
        }

        /// <summary>Gets or sets the ResourceFile to localize permissions.</summary>
        [Parameter]
        public string ResourceFile { get; set; }

        /// <summary>Gets or sets a value indicating whether enable ShowAllUsers to display the virtual "Unauthenticated Users" role.</summary>
        [Parameter]
        public bool ShowUnauthenticatedUsers { get; set; }

        /// <summary>Gets or sets a value indicating whether enable ShowAllUsers to display the virtual "All Users" role.</summary>
        [Parameter]
        public bool ShowAllUsers { get; set; }

        private DataTable DtRolesSelection
        {
            get
            {
                return this.dtRoleSelections;
            }
        }

        private IList<string> CurrentRoleSelection
        {
            get
            {
                return this.selectedRoles ?? (this.selectedRoles = new List<string>());
            }

            set
            {
                this.selectedRoles = value;
            }
        }

        /// <inheritdoc />
        protected override string TagName => null;

        /// <inheritdoc />
        /// <remarks>Replaces the <c>OnPreRender</c> override, which bound the grid to the roles.</remarks>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.BindData();
        }

        /// <summary>Creates the Child Controls.</summary>
        /// <remarks>
        /// Replaces <c>CreateChildControls</c>: what is created here is the state the render tree
        /// needs (the role groups to offer and the grid's columns), the markup itself is built by
        /// <see cref="RenderContents"/>.
        /// </remarks>
        protected override void CreateChildControls()
        {
            var portalSettings = PortalController.Instance.GetCurrentSettings();
            this.roleGroups = RoleController.GetRoleGroups(portalSettings.PortalId);

            this.dgRoleSelection.FooterStyle.CssClass = "dnnGridFooter";
            this.dgRoleSelection.HeaderStyle.CssClass = "dnnGridHeader";
            this.dgRoleSelection.ItemStyle.CssClass = "dnnGridItem";
            this.dgRoleSelection.AlternatingItemStyle.CssClass = "dnnGridAltItem";
            this.SetUpRolesGrid();
        }

        /// <summary>Updates a Selection.</summary>
        /// <param name="roleName">The name of the role.</param>
        /// <param name="selected">Whether it's selected.</param>
        protected virtual void UpdateSelection(string roleName, bool selected)
        {
            var isMatch = false;
            foreach (string currentRoleName in this.CurrentRoleSelection)
            {
                if (currentRoleName == roleName)
                {
                    // role is in collection
                    if (!selected)
                    {
                        // Remove from collection as we only keep selected roles
                        this.CurrentRoleSelection.Remove(currentRoleName);
                    }

                    isMatch = true;
                    break;
                }
            }

            // Rolename not found so add new
            if (!isMatch && selected)
            {
                this.CurrentRoleSelection.Add(roleName);
            }
        }

        /// <summary>Updates the Selections.</summary>
        protected void UpdateSelections()
        {
            this.EnsureChildControls();

            this.UpdateRoleSelections();
        }

        /// <summary>Updates the permissions.</summary>
        protected void UpdateRoleSelections()
        {
            foreach (var dgi in this.dgRoleSelection.Items)
            {
                const int I = 2;
                if (dgi.Cells.Count > I && dgi.Cells[I].Controls.Count > 0)
                {
                    var cb = (GridCheckBox)dgi.Cells[I].Controls[0];
                    this.UpdateSelection(dgi.Cells[0].Text, cb.Checked);
                }
            }
        }

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "dnnRolesGrid");

            // Optionally Add Role Group Filter
            if (this.roleGroups != null && this.roleGroups.Count > 0)
            {
                builder.OpenElement(2, "label");
                builder.AddContent(3, Localization.GetString("RoleGroupFilter"));
                builder.CloseElement();
                builder.AddMarkupContent(4, "&nbsp;&nbsp;");

                builder.OpenElement(5, "select");
                builder.AddAttribute(6, "value", this.selectedRoleGroup);
                builder.AddAttribute(7, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.RoleGroupChanged));
                this.RenderOption(builder, 8, "-2", Localization.GetString("AllRoles"));
                this.RenderOption(builder, 11, "-1", Localization.GetString("GlobalRoles"));
                var groupSequence = 14;
                foreach (RoleGroupInfo roleGroup in this.roleGroups)
                {
                    this.RenderOption(builder, groupSequence, roleGroup.RoleGroupID.ToString(CultureInfo.InvariantCulture), roleGroup.RoleGroupName);
                    groupSequence += 3;
                }

                builder.CloseElement();
                builder.AddMarkupContent(groupSequence, "<br/><br/>");
            }

            builder.AddContent(1000, this.dgRoleSelection.RenderTable());
            builder.CloseElement();

            sequence += 1001;
            base.RenderContents(builder, ref sequence);
        }

        private void RenderOption(RenderTreeBuilder builder, int sequence, string value, string text)
        {
            builder.OpenElement(sequence, "option");
            builder.AddAttribute(sequence + 1, "value", value);
            builder.AddContent(sequence + 2, text);
            builder.CloseElement();
        }

        private void RoleGroupChanged(ChangeEventArgs args)
        {
            this.selectedRoleGroup = args?.Value?.ToString() ?? "-1";
            this.BindData();
        }

        /// <summary>Bind the data to the controls.</summary>
        private void BindData()
        {
            this.EnsureChildControls();

            this.BindRolesGrid();
        }

        /// <summary>Bind the Roles data to the Grid.</summary>
        private void BindRolesGrid()
        {
            this.DtRolesSelection.Columns.Clear();
            this.DtRolesSelection.Rows.Clear();

            // Add Roles Column
            var col = new DataColumn("RoleId", typeof(string));
            this.DtRolesSelection.Columns.Add(col);

            // Add Roles Column
            col = new DataColumn("RoleName", typeof(string));
            this.DtRolesSelection.Columns.Add(col);

            // Add Selected Column
            col = new DataColumn("Selected", typeof(bool));
            this.DtRolesSelection.Columns.Add(col);

            this.GetRoles();

            this.UpdateRoleSelections();
            for (int i = 0; i <= this.roles.Count - 1; i++)
            {
                var role = this.roles[i];
                DataRow row = this.DtRolesSelection.NewRow();
                row["RoleId"] = role.RoleID;
                row["RoleName"] = Localization.LocalizeRole(role.RoleName);
                row["Selected"] = this.GetSelection(role.RoleName);

                this.DtRolesSelection.Rows.Add(row);
            }

            this.dgRoleSelection.DataSource = this.DtRolesSelection.DefaultView;
            this.dgRoleSelection.DataBind();
        }

        private bool GetSelection(string roleName)
        {
            return this.CurrentRoleSelection.Any(r => r == roleName);
        }

        /// <summary>Gets the roles from the Database and loads them into the Roles property.</summary>
        private void GetRoles()
        {
            int roleGroupId = -2;
            if (this.roleGroups != null && this.roleGroups.Count > 0 && !string.IsNullOrEmpty(this.selectedRoleGroup))
            {
                roleGroupId = int.Parse(this.selectedRoleGroup, CultureInfo.InvariantCulture);
            }

            this.roles = roleGroupId > -2
                    ? RoleController.Instance.GetRoles(PortalController.Instance.GetCurrentSettings().PortalId, r => r.RoleGroupID == roleGroupId && r.SecurityMode != SecurityMode.SocialGroup && r.Status == RoleStatus.Approved)
                    : RoleController.Instance.GetRoles(PortalController.Instance.GetCurrentSettings().PortalId, r => r.SecurityMode != SecurityMode.SocialGroup && r.Status == RoleStatus.Approved);

            if (roleGroupId < 0)
            {
                if (this.ShowUnauthenticatedUsers)
                {
                    this.roles.Add(new RoleInfo { RoleID = int.Parse(Globals.glbRoleUnauthUser, CultureInfo.InvariantCulture), RoleName = Globals.glbRoleUnauthUserName });
                }

                if (this.ShowAllUsers)
                {
                    this.roles.Add(new RoleInfo { RoleID = int.Parse(Globals.glbRoleAllUsers, CultureInfo.InvariantCulture), RoleName = Globals.glbRoleAllUsersName });
                }
            }

            this.roles = this.roles.OrderBy(r => r.RoleName).ToList();
        }

        /// <summary>Sets up the columns for the Grid.</summary>
        private void SetUpRolesGrid()
        {
            this.dgRoleSelection.Columns.Clear();
            var textCol = new BoundColumn { HeaderText = "&nbsp;", DataField = "RoleName" };
            textCol.ItemStyle.Width = Unit.Parse("150px", CultureInfo.InvariantCulture);
            this.dgRoleSelection.Columns.Add(textCol);
            var idCol = new BoundColumn { HeaderText = string.Empty, DataField = "roleid", Visible = false };
            this.dgRoleSelection.Columns.Add(idCol);
            var checkCol = new TemplateColumn();
            var columnTemplate = new CheckBoxColumnTemplate { DataField = "Selected" };
            checkCol.ItemTemplate = columnTemplate;
            checkCol.HeaderText = Localization.GetString("SelectedRole");
            checkCol.ItemStyle.HorizontalAlign = HorizontalAlign.Center;
            checkCol.HeaderStyle.Wrap = true;
            this.dgRoleSelection.Columns.Add(checkCol);
        }
    }
}
