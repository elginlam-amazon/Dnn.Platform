// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Security.Permissions.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Abstractions.Security.Permissions;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Utilities;
    using DotNetNuke.UI.WebControls;
    using DotNetNuke.UI.WebControls.Internal;
    using DotNetNuke.Web.Client;
    using DotNetNuke.Web.Client.ClientResourceManagement;
    using Globals = DotNetNuke.Common.Globals;
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A base class for permissions grid controls.</summary>
    /// <remarks>
    /// <para>
    /// This was a <c>System.Web.UI.Control</c> (an <c>INamingContainer</c>) that built its whole UI
    /// in <c>CreateChildControls</c>. On ASP.NET Core the platform renders with Blazor, so it is now
    /// a component deriving from <see cref="UserControlBase"/>: <c>CreateChildControls</c> survives as
    /// the (still overridable) place where the grids and their columns are configured, and
    /// <see cref="BuildRenderTree"/> emits the markup the WebForms child controls used to emit.
    /// </para>
    /// <para>
    /// Every permission decision -- <c>GetPermission</c>, <c>GetEnabled</c>, <c>SupportsDenyPermissions</c>,
    /// <c>IsFullControl</c>, <c>IsViewPermission</c>, the <c>True</c>/<c>False</c>/<c>Null</c> sentinels
    /// and the key format of <see cref="BuildKey(bool,int,int,int,string)"/> -- is unchanged.
    /// </para>
    /// </remarks>
    public abstract partial class PermissionsGrid : UserControlBase
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected const string PermissionTypeGrant = "True";
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected const string PermissionTypeDeny = "False";
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected const string PermissionTypeNull = "Null";

        /// <summary>The name of the bound column that carries whether a row's delete action renders.</summary>
        private const string ActionsVisibleColumnName = "ActionsVisible";

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DNNDataGrid rolePermissionsGrid;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        protected DNNDataGrid userPermissionsGrid;

        private readonly IServicesFramework servicesFramework;
        private readonly int unAuthUsersRoleId = int.Parse(Globals.glbRoleUnauthUser, CultureInfo.InvariantCulture);
        private readonly int allUsersRoleId = int.Parse(Globals.glbRoleAllUsers, CultureInfo.InvariantCulture);

        private IList<IPermissionDefinitionInfo> permissions;
        private ArrayList users;

        // These were the child controls the WebForms grid created in CreateChildControls. A Blazor
        // component renders its markup instead of owning a control tree, so what is left is the
        // state those controls carried: the two drop down lists' items and selections, the values of
        // the text box and the two hidden fields the permission grid's client script writes into, and
        // the error message.
        private readonly List<GridOption> roleGroupOptions = new List<GridOption>();
        private readonly List<GridOption> selectRoleOptions = new List<GridOption>();
        private string selectedRoleGroupValue = "-1";
        private string selectedRoleValue = string.Empty;
        private string userNameValue = string.Empty;
        private string hiddenUserIdsValue = string.Empty;
        private string roleFieldValue = string.Empty;
        private string errorMessage = string.Empty;
        private bool userPermissionsGridVisible;
        private bool childControlsCreated;
        private object columnAddedState;

        /// <summary>Initializes a new instance of the <see cref="PermissionsGrid"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public PermissionsGrid()
            : this(null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="PermissionsGrid"/> class.</summary>
        /// <param name="servicesFramework">The web API service framework.</param>
        public PermissionsGrid(IServicesFramework servicesFramework)
        {
            this.servicesFramework = servicesFramework ?? Globals.GetCurrentServiceProvider().GetRequiredService<IServicesFramework>();
            this.dtUserPermissions = new DataTable();
            this.dtRolePermissions = new DataTable();
        }

        public TableItemStyle AlternatingItemStyle => this.rolePermissionsGrid.AlternatingItemStyle;

        public DataGridColumnCollection Columns => this.rolePermissionsGrid.Columns;

        public TableItemStyle FooterStyle => this.rolePermissionsGrid.FooterStyle;

        public TableItemStyle HeaderStyle => this.rolePermissionsGrid.HeaderStyle;

        public TableItemStyle ItemStyle => this.rolePermissionsGrid.ItemStyle;

        public DataGridItemCollection Items => this.rolePermissionsGrid.Items;

        public TableItemStyle SelectedItemStyle => this.rolePermissionsGrid.SelectedItemStyle;

        /// <summary>Gets the ID of the Administrator Role.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public int AdministratorRoleId => PortalController.Instance.GetCurrentSettings().AdministratorRoleId;

        /// <summary>Gets the ID of the Registered Users Role.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public int RegisteredUsersRoleId => PortalController.Instance.GetCurrentSettings().RegisteredRoleId;

        /// <summary>Gets the ID of the Portal.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public int PortalId
        {
            get
            {
                // Obtain PortalSettings from Current Context
                var portalSettings = PortalController.Instance.GetCurrentSettings();
                int portalId;
                if (Globals.IsHostTab(TabController.CurrentPage.TabID))
                {
                    // if we are in host filemanager then we need to pass a null portal id
                    portalId = Null.NullInteger;
                }
                else
                {
                    portalId = portalSettings.PortalId;
                }

                return portalId;
            }
        }

        public bool AutoGenerateColumns
        {
            get
            {
                return this.rolePermissionsGrid.AutoGenerateColumns;
            }

            set
            {
                this.rolePermissionsGrid.AutoGenerateColumns = value;
                this.userPermissionsGrid.AutoGenerateColumns = value;
            }
        }

        public int CellSpacing
        {
            get
            {
                return this.rolePermissionsGrid.CellSpacing;
            }

            set
            {
                this.rolePermissionsGrid.CellSpacing = value;
                this.userPermissionsGrid.CellSpacing = value;
            }
        }

        public GridLines GridLines
        {
            get
            {
                return this.rolePermissionsGrid.GridLines;
            }

            set
            {
                this.rolePermissionsGrid.GridLines = value;
                this.userPermissionsGrid.GridLines = value;
            }
        }

        /// <summary>Gets or sets a value indicating whether a Dynamic Column has been added.</summary>
        /// <remarks>
        /// Was backed by <c>ViewState["ColumnAdded"]</c>. The (long standing) semantics are
        /// preserved exactly: the getter reports whether the setter has ever been called, whatever
        /// value it was called with.
        /// </remarks>
        public bool DynamicColumnAdded
        {
            get => this.columnAddedState != null;
            set => this.columnAddedState = value;
        }

        /// <summary>Gets the underlying Permissions Data Table.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]

        // ReSharper disable once InconsistentNaming
        public DataTable dtRolePermissions { get; private set; }

        /// <summary>Gets the underlying Permissions Data Table.</summary>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]

        // ReSharper disable once InconsistentNaming
        public DataTable dtUserPermissions { get; private set; }

        /// <summary>Gets or sets the collection of Roles to display.</summary>
        public ArrayList Roles { get; set; }

        /// <summary>Gets or sets the ResourceFile to localize permissions.</summary>
        public string ResourceFile { get; set; }

        /// <summary>Gets the DOM id of this grid, used by its client script.</summary>
        /// <remarks>
        /// Replaces <c>Control.ClientID</c>. There is no naming container to derive an id from any
        /// more, so a unique one is generated per instance; it is still rendered onto the wrapper
        /// element and still handed to <c>dnn.permissionGridManager</c>.
        /// </remarks>
        public string ClientID { get; } = "PermissionsGrid_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>Gets a value indicating whether the implementation supports the APIs using <see cref="IPermissionDefinitionInfo"/>, rather than <see cref="PermissionInfoBase"/>.</summary>
        protected virtual bool SupportsPermissionsAbstractions => false;

        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use PermissionCollection property instead. Scheduled removal in v12.0.0.")]
        protected virtual List<PermissionInfoBase> PermissionsList => null;

        protected virtual IList<IPermissionInfo> PermissionCollection => this.PermissionsList?.Cast<IPermissionInfo>().ToList();

        protected virtual bool RefreshGrid => false;

        private int UnAuthUsersRoleId => this.unAuthUsersRoleId;

        private int AllUsersRoleId => this.allUsersRoleId;

        /// <summary>Registers the scripts necessary to make the tri-state controls work inside a RadAjaxPanel.</summary>
        /// <remarks>
        /// No need to call this unless using the PermissionGrid inside an ajax control that omits scripts on postback
        /// See DesktopModules/Admin/Tabs.ascx.cs for an example of usage.
        /// </remarks>
        public void RegisterScriptsForAjaxPanel()
        {
            // The control arguments became the Client API abstractions (see Specialist 8's notes):
            // the page is resolved from the current request, and PermissionTriState only uses the
            // control argument to look up localized tool tips, which it does without it.
            var page = ClientApiPageAccessor.GetPage(HttpContextSource.Current, nameof(this.RegisterScriptsForAjaxPanel));
            if (page != null)
            {
                PermissionTriState.RegisterScripts(page, page);
            }
        }

        /// <summary>Generate the Data Grid.</summary>
        public abstract void GenerateDataGrid();

        /// <summary>Add the permission to the permission grid's collection.</summary>
        /// <param name="permission">The permission definition.</param>
        /// <param name="roleId">The ID of the role or <see cref="Globals.glbRoleNothing"/>.</param>
        /// <param name="roleName">The role name or <see cref="Null.NullString"/>.</param>
        /// <param name="userId">The user ID or <see cref="Null.NullInteger"/>.</param>
        /// <param name="displayName">The user's display name or <see cref="Null.NullString"/>.</param>
        /// <param name="allowAccess">Whether the permission allows or denies access.</param>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial void AddPermission(PermissionInfo permission, int roleId, string roleName, int userId, string displayName, bool allowAccess)
            => this.AddPermission((IPermissionDefinitionInfo)permission, roleId, roleName, userId, displayName, allowAccess);

        /// <summary>Add the permission to the permission grid's collection.</summary>
        /// <param name="permissionDefinition">The permission definition.</param>
        /// <param name="roleId">The ID of the role or <see cref="Globals.glbRoleNothing"/>.</param>
        /// <param name="roleName">The role name or <see cref="Null.NullString"/>.</param>
        /// <param name="userId">The user ID or <see cref="Null.NullInteger"/>.</param>
        /// <param name="displayName">The user's display name or <see cref="Null.NullString"/>.</param>
        /// <param name="allowAccess">Whether the permission allows or denies access.</param>
        protected virtual void AddPermission(IPermissionDefinitionInfo permissionDefinition, int roleId, string roleName, int userId, string displayName, bool allowAccess)
        {
        }

        /// <summary>Updates a Permission.</summary>
        /// <param name="permissions">The collection of permissions.</param>
        /// <param name="user">The user to add.</param>
        [DnnDeprecated(10, 2, 2, "Use overload taking IList<IPermissionDefinitionInfo>")]
        protected virtual partial void AddPermission(ArrayList permissions, UserInfo user)
        {
        }

        /// <summary>Updates a Permission.</summary>
        /// <param name="permissionsList">The collection of permissions.</param>
        /// <param name="user">The user to add.</param>
        protected virtual void AddPermission(IList<IPermissionDefinitionInfo> permissionsList, UserInfo user)
        {
        }

        /// <summary>Updates a Permission.</summary>
        /// <param name="permissions">The collection of permissions.</param>
        /// <param name="role">The role to add.</param>
        [DnnDeprecated(10, 2, 2, "Use overload taking IList<IPermissionDefinitionInfo>")]
        protected virtual partial void AddPermission(ArrayList permissions, RoleInfo role)
        {
        }

        /// <summary>Updates a Permission.</summary>
        /// <param name="permissionsList">The collection of permissions.</param>
        /// <param name="role">The role to add.</param>
        protected virtual void AddPermission(IList<IPermissionDefinitionInfo> permissionsList, RoleInfo role)
        {
        }

        /// <summary>Builds the key used to store the "permission" information in the ViewState.</summary>
        /// <param name="allowAccess">The type of permission ( grant / deny ).</param>
        /// <param name="permissionId">The Id of the permission.</param>
        /// <param name="objectPermissionId">The Id of the object permission.</param>
        /// <param name="roleId">The role id.</param>
        /// <param name="roleName">The role name.</param>
        /// <returns>The permission state key.</returns>
        protected string BuildKey(bool allowAccess, int permissionId, int objectPermissionId, int roleId, string roleName)
        {
            return this.BuildKey(allowAccess, permissionId, objectPermissionId, roleId, roleName, Null.NullInteger, Null.NullString);
        }

        /// <summary>Builds the key used to store the "permission" information in the ViewState.</summary>
        /// <param name="allowAccess">The type of permission ( grant / deny ).</param>
        /// <param name="permissionId">The ID of the permission.</param>
        /// <param name="objectPermissionId">The ID of the object permission.</param>
        /// <param name="roleId">The role id.</param>
        /// <param name="roleName">The role name.</param>
        /// <param name="userID">The user id.</param>
        /// <param name="displayName">The user display name.</param>
        /// <returns>The permission state key.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        protected string BuildKey(bool allowAccess, int permissionId, int objectPermissionId, int roleId, string roleName, int userID, string displayName)
        {
            string key;
            if (allowAccess)
            {
                key = "True";
            }
            else
            {
                key = "False";
            }

            key += "|" + Convert.ToString(permissionId, CultureInfo.InvariantCulture);
            key += "|";
            if (objectPermissionId > -1)
            {
                key += Convert.ToString(objectPermissionId, CultureInfo.InvariantCulture);
            }

            key += "|" + roleName;
            key += "|" + roleId;
            key += "|" + userID;
            key += "|" + displayName;

            return key;
        }

        /// <summary>Creates and configures the two permission grids and the add role/user controls.</summary>
        /// <remarks>
        /// Was <c>Control.CreateChildControls</c>. It no longer builds a control tree -- the markup is
        /// emitted by <see cref="BuildRenderTree"/> -- but it is still the single place where the
        /// grids, their columns and the role group filter are set up, and it is still the method the
        /// derived grids override to attach their own behaviour.
        /// </remarks>
        protected virtual void CreateChildControls()
        {
            this.permissions = this.SupportsPermissionsAbstractions
                ? this.GetPermissionDefinitions()
#pragma warning disable CS0618 // Type or member is obsolete
                : this.GetPermissions().Cast<IPermissionDefinitionInfo>().ToList();
#pragma warning restore CS0618 // Type or member is obsolete

            // Optionally Add Role Group Filter
            this.CreateAddRoleControls();

            this.rolePermissionsGrid = new DNNDataGrid
            {
                AutoGenerateColumns = false,
                CellSpacing = 0,
                CellPadding = 2,
                GridLines = GridLines.None,
                CssClass = "dnnPermissionsGrid",
            };
            this.rolePermissionsGrid.FooterStyle.CssClass = "dnnGridFooter";
            this.rolePermissionsGrid.HeaderStyle.CssClass = "dnnGridHeader";
            this.rolePermissionsGrid.ItemStyle.CssClass = "dnnGridItem";
            this.rolePermissionsGrid.AlternatingItemStyle.CssClass = "dnnGridAltItem";
            this.SetUpRolesGrid();

            this.users = this.GetUsers();
            if (this.users != null)
            {
                this.userPermissionsGrid = new DNNDataGrid
                {
                    AutoGenerateColumns = false,
                    CellSpacing = 0,
                    GridLines = GridLines.None,
                    CssClass = "dnnPermissionsGrid",
                };
                this.userPermissionsGrid.FooterStyle.CssClass = "dnnGridFooter";
                this.userPermissionsGrid.HeaderStyle.CssClass = "dnnGridHeader";
                this.userPermissionsGrid.ItemStyle.CssClass = "dnnGridItem";
                this.userPermissionsGrid.AlternatingItemStyle.CssClass = "dnnGridAltItem";

                this.SetUpUsersGrid();
            }
        }

        /// <summary>Gets the Enabled status of the permission.</summary>
        /// <param name="objPerm">The permission being loaded.</param>
        /// <param name="role">The role.</param>
        /// <param name="column">The column of the Grid.</param>
        /// <returns><see langword="true"/> if the permission is enabled, otherwise <see langword="false"/>.</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial bool GetEnabled(PermissionInfo objPerm, RoleInfo role, int column)
            => this.GetEnabled((IPermissionDefinitionInfo)objPerm, role, column);

        /// <summary>Gets the Enabled status of the permission.</summary>
        /// <param name="permissionDefinition">The permission being loaded.</param>
        /// <param name="role">The role.</param>
        /// <param name="column">The column of the Grid.</param>
        /// <returns><see langword="true"/> if the permission is enabled, otherwise <see langword="false"/>.</returns>
        protected virtual bool GetEnabled(IPermissionDefinitionInfo permissionDefinition, RoleInfo role, int column)
            => true;

        /// <summary>Gets the Enabled status of the permission.</summary>
        /// <param name="objPerm">The permission being loaded.</param>
        /// <param name="user">The user.</param>
        /// <param name="column">The column of the Grid.</param>
        /// <returns><see langword="true"/> if the permission is enabled, otherwise <see langword="false"/>.</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial bool GetEnabled(PermissionInfo objPerm, UserInfo user, int column)
            => this.GetEnabled((IPermissionDefinitionInfo)objPerm, user, column);

        /// <summary>Gets the Enabled status of the permission.</summary>
        /// <param name="permissionDefinition">The permission being loaded.</param>
        /// <param name="user">The user.</param>
        /// <param name="column">The column of the Grid.</param>
        /// <returns><see langword="true"/> if the permission is enabled, otherwise <see langword="false"/>.</returns>
        protected virtual bool GetEnabled(IPermissionDefinitionInfo permissionDefinition, UserInfo user, int column)
            => true;

        /// <summary>Gets the Value of the permission.</summary>
        /// <param name="objPerm">The permission being loaded.</param>
        /// <param name="role">The role.</param>
        /// <param name="column">The column of the Grid.</param>
        /// <returns><see langword="true"/> if the permission is granted, otherwise <see langword="false"/>.</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial bool GetPermission(PermissionInfo objPerm, RoleInfo role, int column)
            => this.GetPermission((IPermissionDefinitionInfo)objPerm, role, column);

        /// <summary>Gets the Value of the permission.</summary>
        /// <param name="permissionDefinition">The permission being loaded.</param>
        /// <param name="role">The role.</param>
        /// <param name="column">The column of the Grid.</param>
        /// <returns><see langword="true"/> if the permission is granted, otherwise <see langword="false"/>.</returns>
        protected virtual bool GetPermission(IPermissionDefinitionInfo permissionDefinition, RoleInfo role, int column)
        {
            return Convert.ToBoolean(this.GetPermission(permissionDefinition, role, column, PermissionTypeDeny));
        }

        /// <summary>Gets the Value of the permission.</summary>
        /// <param name="objPerm">The permission being loaded.</param>
        /// <param name="role">The role.</param>
        /// <param name="column">The column of the Grid.</param>
        /// <param name="defaultState">Default State.</param>
        /// <returns>The permission state (one of <see cref="PermissionTypeGrant"/>, <see cref="PermissionTypeDeny"/> or <paramref name="defaultState"/>).</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial string GetPermission(PermissionInfo objPerm, RoleInfo role, int column, string defaultState)
            => this.GetPermission((IPermissionDefinitionInfo)objPerm, role, column, defaultState);

        /// <summary>Gets the Value of the permission.</summary>
        /// <param name="permissionDefinition">The permission being loaded.</param>
        /// <param name="role">The role.</param>
        /// <param name="column">The column of the Grid.</param>
        /// <param name="defaultState">Default State.</param>
        /// <returns>The permission state (one of <see cref="PermissionTypeGrant"/>, <see cref="PermissionTypeDeny"/> or <paramref name="defaultState"/>).</returns>
        protected virtual string GetPermission(IPermissionDefinitionInfo permissionDefinition, RoleInfo role, int column, string defaultState)
        {
            if (this.PermissionCollection == null)
            {
                return defaultState;
            }

            foreach (var permission in this.PermissionCollection)
            {
                if (permission.PermissionId == permissionDefinition.PermissionId && permission.RoleId == role.RoleID)
                {
                    return permission.AllowAccess ? PermissionTypeGrant : PermissionTypeDeny;
                }
            }

            return defaultState;
        }

        /// <summary>Gets the Value of the permission.</summary>
        /// <param name="objPerm">The permission being loaded.</param>
        /// <param name="user">The user.</param>
        /// <param name="column">The column of the Grid.</param>
        /// <returns><see langword="true"/> if the permission is granted, otherwise <see langword="false"/>.</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial bool GetPermission(PermissionInfo objPerm, UserInfo user, int column)
            => this.GetPermission((IPermissionDefinitionInfo)objPerm, user, column);

        /// <summary>Gets the Value of the permission.</summary>
        /// <param name="permissionDefinition">The permission being loaded.</param>
        /// <param name="user">The user.</param>
        /// <param name="column">The column of the Grid.</param>
        /// <returns><see langword="true"/> if the permission is granted, otherwise <see langword="false"/>.</returns>
        protected virtual bool GetPermission(IPermissionDefinitionInfo permissionDefinition, UserInfo user, int column)
        {
            return Convert.ToBoolean(this.GetPermission(permissionDefinition, user, column, PermissionTypeDeny));
        }

        /// <summary>Gets the Value of the permission.</summary>
        /// <param name="objPerm">The permission being loaded.</param>
        /// <param name="user">The user.</param>
        /// <param name="column">The column of the Grid.</param>
        /// <param name="defaultState">Default State.</param>
        /// <returns>The permission state (one of <see cref="PermissionTypeGrant"/>, <see cref="PermissionTypeDeny"/> or <paramref name="defaultState"/>).</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial string GetPermission(PermissionInfo objPerm, UserInfo user, int column, string defaultState)
            => this.GetPermission((IPermissionDefinitionInfo)objPerm, user, column, defaultState);

        /// <summary>Gets the Value of the permission.</summary>
        /// <param name="permissionDefinition">The permission being loaded.</param>
        /// <param name="user">The user.</param>
        /// <param name="column">The column of the Grid.</param>
        /// <param name="defaultState">Default State.</param>
        /// <returns>The permission state (one of <see cref="PermissionTypeGrant"/>, <see cref="PermissionTypeDeny"/> or <paramref name="defaultState"/>).</returns>
        protected virtual string GetPermission(IPermissionDefinitionInfo permissionDefinition, UserInfo user, int column, string defaultState)
        {
            IEnumerable<IPermissionInfo> permissionCollection = this.SupportsPermissionsAbstractions ? this.PermissionCollection : this.PermissionsList;
            if (permissionCollection == null)
            {
                return defaultState;
            }

            foreach (IPermissionInfo permission in permissionCollection)
            {
                if (permission.PermissionId == permissionDefinition.PermissionId && permission.UserId == user.UserID)
                {
                    return permission.AllowAccess ? PermissionTypeGrant : PermissionTypeDeny;
                }
            }

            return defaultState;
        }

        /// <summary>Gets the permissions from the Database.</summary>
        /// <returns>An <see cref="ArrayList"/> of <see cref="PermissionInfo"/> instances.</returns>
        [DnnDeprecated(10, 2, 2, "Use GetPermissionDefinitions")]
        protected virtual partial ArrayList GetPermissions() => null;

        /// <summary>Gets the permissions from the Database.</summary>
        /// <remarks>If method does not return <see langword="null"/>, this value is used instead of the value from <see cref="GetPermissions"/>.</remarks>
        /// <returns>An <see cref="IList{T}"/> of <see cref="IPermissionDefinitionInfo"/> instances.</returns>
        protected virtual IList<IPermissionDefinitionInfo> GetPermissionDefinitions() => null;

        /// <summary>Gets the users from the Database.</summary>
        /// <returns>An <see cref="ArrayList"/> of <see cref="UserInfo"/> instances.</returns>
        protected virtual ArrayList GetUsers()
        {
            IEnumerable<IPermissionInfo> permissionCollection = this.SupportsPermissionsAbstractions ? this.PermissionCollection : this.PermissionsList;
            if (permissionCollection == null)
            {
                return [];
            }

            var usersMap = new Dictionary<int, UserInfo>();
            foreach (var permission in permissionCollection.Where(permission => !Null.IsNull(permission.UserId)))
            {
                if (!usersMap.ContainsKey(permission.UserId))
                {
                    usersMap.Add(
                        permission.UserId,
                        new UserInfo
                        {
                            UserID = permission.UserId,
                            Username = permission.Username,
                            DisplayName = permission.DisplayName,
                        });
                }
            }

            return new ArrayList(usersMap.Values);
        }

        /// <summary>Gets a value indicating whether the given permission definition represents full control.</summary>
        /// <param name="permissionInfo">The permission definition.</param>
        /// <returns><see langword="true"/> if it represents full control, otherwise <see langword="false"/>.</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial bool IsFullControl(PermissionInfo permissionInfo)
            => this.IsFullControl((IPermissionDefinitionInfo)permissionInfo);

        /// <summary>Gets a value indicating whether the given permission definition represents full control.</summary>
        /// <param name="permissionDefinition">The permission definition.</param>
        /// <returns><see langword="true"/> if it represents full control, otherwise <see langword="false"/>.</returns>
        protected virtual bool IsFullControl(IPermissionDefinitionInfo permissionDefinition) => false;

        /// <summary>Gets a value indicating whether the given permission definition represents the view permission.</summary>
        /// <param name="permissionInfo">The permission definition.</param>
        /// <returns><see langword="true"/> if it represents the view permission, otherwise <see langword="false"/>.</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial bool IsViewPermisison(PermissionInfo permissionInfo)
            => this.IsViewPermission((IPermissionDefinitionInfo)permissionInfo);

        /// <summary>Gets a value indicating whether the given permission definition represents the view permission.</summary>
        /// <param name="permissionDefinition">The permission definition.</param>
        /// <returns><see langword="true"/> if it represents the view permission, otherwise <see langword="false"/>.</returns>
        protected virtual bool IsViewPermission(IPermissionDefinitionInfo permissionDefinition) => false;

        /// <inheritdoc />
        /// <remarks>
        /// The client resources are registered against the current request rather than against
        /// <c>this.Page</c> (see Specialist 8's notes). The grid's start-up script also moved here
        /// from <c>OnPreRender</c>, because <c>OnPreRender</c> now runs after the first render, by
        /// which time there is no request to register page state against.
        /// </remarks>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.servicesFramework.RequestAjaxAntiForgerySupport();

            var context = HttpContextSource.Current;

            ClientResourceManager.RegisterScript(context, "~/Resources/Shared/Components/Tokeninput/jquery.tokeninput.js");
            ClientResourceManager.RegisterScript(context, "~/js/dnn.permissiongrid.js");

            ClientResourceManager.RegisterStyleSheet(context, "~/Resources/Shared/Components/Tokeninput/Themes/token-input-facebook.css", FileOrder.Css.ResourceCss);

            var script = "var pgm = new dnn.permissionGridManager('" + this.ClientID + "');";
            var page = ClientApiPageAccessor.GetPage(context, nameof(PermissionsGrid));
            if (page != null)
            {
                // Both WebForms branches (ScriptManager.RegisterStartupScript and
                // Page.ClientScript.RegisterStartupScript) registered the very same keyed start-up
                // script; on ASP.NET Core there is a single registry for it.
                ClientAPI.RegisterStartUpScript(
                    page,
                    this.ClientID + "PermissionGridManager",
                    "<script type=\"text/javascript\">" + script + "</script>");
            }
        }

        /// <summary>Overrides the base OnPreRender method to Bind the Grid to the Permissions.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            this.BindData();
        }

        /// <summary>Update the <paramref name="permission"/> with the <paramref name="settings"/>.</summary>
        /// <param name="permission">The permission.</param>
        /// <param name="settings">The settings.</param>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionInfo")]
        protected virtual partial void ParsePermissionKeys(PermissionInfoBase permission, string[] settings)
            => this.ParsePermissionKeys((IPermissionInfo)permission, settings);

        /// <summary>Update the <paramref name="permission"/> with the <paramref name="settings"/>.</summary>
        /// <param name="permission">The permission.</param>
        /// <param name="settings">The settings.</param>
        protected virtual void ParsePermissionKeys(IPermissionInfo permission, string[] settings)
        {
            permission.PermissionId = Convert.ToInt32(settings[1], CultureInfo.InvariantCulture);
            permission.RoleId = Convert.ToInt32(settings[4], CultureInfo.InvariantCulture);
            permission.RoleName = settings[3];
            permission.AllowAccess = Convert.ToBoolean(settings[0], CultureInfo.InvariantCulture);
            permission.UserId = Convert.ToInt32(settings[5], CultureInfo.InvariantCulture);
            permission.DisplayName = settings[6];
        }

        protected virtual void RemovePermission(int permissionID, int roleID, int userID)
        {
        }

        /// <summary>Gets a value indicating whether the permission supports deny permissions.</summary>
        /// <param name="permissionInfo">The permission definition.</param>
        /// <returns><see langword="true"/> if deny permissions are supported, otherwise <see langword="false"/>.</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial bool SupportsDenyPermissions(PermissionInfo permissionInfo) => this.SupportsDenyPermissions((IPermissionDefinitionInfo)permissionInfo);

        /// <summary>Gets a value indicating whether the permission supports deny permissions.</summary>
        /// <param name="permissionDefinition">The permission definition.</param>
        /// <returns><see langword="true"/> if deny permissions are supported, otherwise <see langword="false"/>.</returns>
        protected virtual bool SupportsDenyPermissions(IPermissionDefinitionInfo permissionDefinition) => false;

        /// <summary>Updates a Permission.</summary>
        /// <param name="permission">The permission being updated.</param>
        /// <param name="roleId">Role Id.</param>
        /// <param name="roleName">The name of the role.</param>
        /// <param name="allowAccess">The value of the permission.</param>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial void UpdatePermission(PermissionInfo permission, int roleId, string roleName, bool allowAccess)
            => this.UpdatePermission((IPermissionDefinitionInfo)permission, roleId, roleName, allowAccess);

        /// <summary>Updates a Permission.</summary>
        /// <param name="permission">The permission being updated.</param>
        /// <param name="roleId">Role Id.</param>
        /// <param name="roleName">The name of the role.</param>
        /// <param name="allowAccess">The value of the permission.</param>
        protected virtual void UpdatePermission(IPermissionDefinitionInfo permission, int roleId, string roleName, bool allowAccess)
            => this.UpdatePermission(permission, roleId, roleName, allowAccess ? PermissionTypeGrant : PermissionTypeNull);

        /// <summary>Updates a Permission.</summary>
        /// <param name="permission">The permission being updated.</param>
        /// <param name="roleId">Role Id.</param>
        /// <param name="roleName">The name of the role.</param>
        /// <param name="stateKey">The permission state.</param>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial void UpdatePermission(PermissionInfo permission, int roleId, string roleName, string stateKey)
            => this.UpdatePermission((IPermissionDefinitionInfo)permission, roleId, roleName, stateKey);

        /// <summary>Updates a Permission.</summary>
        /// <param name="permission">The permission being updated.</param>
        /// <param name="roleId">Role Id.</param>
        /// <param name="roleName">The name of the role.</param>
        /// <param name="stateKey">The permission state.</param>
        protected virtual void UpdatePermission(IPermissionDefinitionInfo permission, int roleId, string roleName, string stateKey)
        {
            this.RemovePermission(permission.PermissionId, roleId, Null.NullInteger);
            switch (stateKey)
            {
                case PermissionTypeGrant:
                    this.AddPermission(permission, roleId, roleName, Null.NullInteger, Null.NullString, true);
                    break;
                case PermissionTypeDeny:
                    this.AddPermission(permission, roleId, roleName, Null.NullInteger, Null.NullString, false);
                    break;
            }
        }

        /// <summary>Updates a Permission.</summary>
        /// <param name="permission">The permission being updated.</param>
        /// <param name="displayName">The user's display name.</param>
        /// <param name="userId">The user's id.</param>
        /// <param name="allowAccess">The value of the permission.</param>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial void UpdatePermission(PermissionInfo permission, string displayName, int userId, bool allowAccess)
            => this.UpdatePermission((IPermissionDefinitionInfo)permission, displayName, userId, allowAccess);

        /// <summary>Updates a Permission.</summary>
        /// <param name="permission">The permission being updated.</param>
        /// <param name="displayName">The user's display name.</param>
        /// <param name="userId">The user's id.</param>
        /// <param name="allowAccess">The value of the permission.</param>
        protected virtual void UpdatePermission(IPermissionDefinitionInfo permission, string displayName, int userId, bool allowAccess)
            => this.UpdatePermission(permission, displayName, userId, allowAccess ? PermissionTypeGrant : PermissionTypeNull);

        /// <summary>Updates a Permission.</summary>
        /// <param name="permission">The permission being updated.</param>
        /// <param name="displayName">The user's display name.</param>
        /// <param name="userId">The user's id.</param>
        /// <param name="stateKey">The permission state.</param>
        [DnnDeprecated(10, 2, 2, "Use overload taking IPermissionDefinitionInfo")]
        protected virtual partial void UpdatePermission(PermissionInfo permission, string displayName, int userId, string stateKey)
            => this.UpdatePermission((IPermissionDefinitionInfo)permission, displayName, userId, stateKey);

        /// <summary>Updates a Permission.</summary>
        /// <param name="permission">The permission being updated.</param>
        /// <param name="displayName">The user's display name.</param>
        /// <param name="userId">The user's id.</param>
        /// <param name="stateKey">The permission state.</param>
        protected virtual void UpdatePermission(IPermissionDefinitionInfo permission, string displayName, int userId, string stateKey)
        {
            this.RemovePermission(permission.PermissionId, int.Parse(Globals.glbRoleNothing, CultureInfo.InvariantCulture), userId);
            switch (stateKey)
            {
                case PermissionTypeGrant:
                    this.AddPermission(permission, int.Parse(Globals.glbRoleNothing, CultureInfo.InvariantCulture), Null.NullString, userId, displayName, true);
                    break;
                case PermissionTypeDeny:
                    this.AddPermission(permission, int.Parse(Globals.glbRoleNothing, CultureInfo.InvariantCulture), Null.NullString, userId, displayName, false);
                    break;
            }
        }

        /// <summary>Updates the permissions.</summary>
        protected void UpdatePermissions()
        {
            this.EnsureChildControls();
            this.GetRoles();
            this.UpdateRolePermissions();
            this.users = this.GetUsers();
            this.UpdateUserPermissions();
        }

        /// <summary>Updates the permissions.</summary>
        protected void UpdateRolePermissions()
        {
            if (this.rolePermissionsGrid != null && !this.RefreshGrid)
            {
                var rolesList = this.Roles.Cast<RoleInfo>().ToList();
                foreach (DataGridItem dgi in this.rolePermissionsGrid.Items)
                {
                    var roleId = int.Parse(dgi.Cells[1].Text, CultureInfo.InvariantCulture);
                    if (rolesList.All(r => r.RoleID != roleId))
                    {
                        continue;
                    }

                    for (int i = 2; i <= dgi.Cells.Count - 2; i++)
                    {
                        // all except first two cells which is role names and role ids and last column is Actions
                        if (dgi.Cells[i].Controls.Count > 0)
                        {
                            var permissionInfo = this.permissions[i - 2];
                            var triState = (PermissionTriState)dgi.Cells[i].Controls[0];
                            if (SupportsDenyPermissions(this, permissionInfo))
                            {
                                UpdatePermission(this, permissionInfo, roleId, dgi.Cells[0].Text, triState.Value);
                            }
                            else
                            {
                                UpdatePermission(this, permissionInfo, roleId, dgi.Cells[0].Text, triState.Value == PermissionTypeGrant);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Updates the permissions.</summary>
        protected void UpdateUserPermissions()
        {
            if (this.userPermissionsGrid != null && !this.RefreshGrid)
            {
                var usersList = this.users.Cast<UserInfo>().ToList();
                foreach (DataGridItem dgi in this.userPermissionsGrid.Items)
                {
                    var userId = int.Parse(dgi.Cells[1].Text, CultureInfo.InvariantCulture);
                    if (usersList.All(u => u.UserID != userId))
                    {
                        continue;
                    }

                    for (int i = 2; i <= dgi.Cells.Count - 2; i++)
                    {
                        // all except first two cells which is display name and user ID and Last column is Actions
                        if (dgi.Cells[i].Controls.Count > 0)
                        {
                            var permissionInfo = this.permissions[i - 2];
                            var triState = (PermissionTriState)dgi.Cells[i].Controls[0];
                            if (SupportsDenyPermissions(this, permissionInfo))
                            {
                                UpdatePermission(this, permissionInfo, dgi.Cells[0].Text, userId, triState.Value);
                            }
                            else
                            {
                                UpdatePermission(this, permissionInfo, dgi.Cells[0].Text, userId, triState.Value == PermissionTypeGrant);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>RoleGroupsSelectedIndexChanged runs when the Role Group is changed.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        protected virtual void RoleGroupsSelectedIndexChanged(object sender, EventArgs e)
        {
            this.FillSelectRoleComboBox(int.Parse(this.selectedRoleGroupValue, CultureInfo.InvariantCulture));
        }

        /// <summary>AddUser runs when the Add user linkbutton is clicked.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        protected virtual void AddUser(object sender, EventArgs e)
        {
            this.UpdatePermissions();
            if (!string.IsNullOrEmpty(this.hiddenUserIdsValue))
            {
                foreach (var id in this.hiddenUserIdsValue.Split(','))
                {
                    var userId = Convert.ToInt32(id, CultureInfo.InvariantCulture);
                    var user = UserController.GetUserById(this.PortalId, userId);
                    if (user != null)
                    {
                        AddPermission(this, this.permissions, user);
                        this.BindData();
                    }
                }

                this.userNameValue = this.hiddenUserIdsValue = string.Empty;
            }
        }

        private static bool SupportsDenyPermissions(PermissionsGrid @this, IPermissionDefinitionInfo permissionDefinitionInfo)
        {
            return @this.SupportsPermissionsAbstractions
                ? @this.SupportsDenyPermissions(permissionDefinitionInfo)
#pragma warning disable CS0618 // Type or member is obsolete
                : @this.SupportsDenyPermissions((PermissionInfo)permissionDefinitionInfo);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private static bool GetEnabled(PermissionsGrid @this, IPermissionDefinitionInfo permissionDefinition, RoleInfo role, int column)
        {
            return @this.SupportsPermissionsAbstractions
                ? @this.GetEnabled(permissionDefinition, role, column)
#pragma warning disable CS0618 // Type or member is obsolete
                : @this.GetEnabled((PermissionInfo)permissionDefinition, role, column);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private static bool GetEnabled(PermissionsGrid @this, IPermissionDefinitionInfo permissionDefinition, UserInfo user, int column)
        {
            return @this.SupportsPermissionsAbstractions
                ? @this.GetEnabled(permissionDefinition, user, column)
#pragma warning disable CS0618 // Type or member is obsolete
                : @this.GetEnabled((PermissionInfo)permissionDefinition, user, column);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private static bool GetPermission(PermissionsGrid @this, IPermissionDefinitionInfo permissionDefinition, RoleInfo role, int column)
        {
            return @this.SupportsPermissionsAbstractions
                ? @this.GetPermission(permissionDefinition, role, column)
#pragma warning disable CS0618 // Type or member is obsolete
                : @this.GetPermission((PermissionInfo)permissionDefinition, role, column);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private static bool GetPermission(PermissionsGrid @this, IPermissionDefinitionInfo permissionDefinition, UserInfo user, int column)
        {
            return @this.SupportsPermissionsAbstractions
                ? @this.GetPermission(permissionDefinition, user, column)
#pragma warning disable CS0618 // Type or member is obsolete
                : @this.GetPermission((PermissionInfo)permissionDefinition, user, column);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private static string GetPermission(PermissionsGrid @this, IPermissionDefinitionInfo permissionDefinition, RoleInfo role, int column, string defaultState)
        {
            return @this.SupportsPermissionsAbstractions
                ? @this.GetPermission(permissionDefinition, role, column, defaultState)
#pragma warning disable CS0618 // Type or member is obsolete
                : @this.GetPermission((PermissionInfo)permissionDefinition, role, column, defaultState);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private static string GetPermission(PermissionsGrid @this, IPermissionDefinitionInfo permissionDefinition, UserInfo user, int column, string defaultState)
        {
            return @this.SupportsPermissionsAbstractions
                ? @this.GetPermission(permissionDefinition, user, column, defaultState)
#pragma warning disable CS0618 // Type or member is obsolete
                : @this.GetPermission((PermissionInfo)permissionDefinition, user, column, defaultState);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private static bool IsViewPermission(PermissionsGrid @this, IPermissionDefinitionInfo permissionDefinition)
        {
            return @this.SupportsPermissionsAbstractions
                ? @this.IsViewPermission(permissionDefinition)
#pragma warning disable CS0618 // Type or member is obsolete
                : @this.IsViewPermisison((PermissionInfo)permissionDefinition);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private static bool IsFullControl(PermissionsGrid @this, IPermissionDefinitionInfo permissionDefinition)
        {
            return @this.SupportsPermissionsAbstractions
                ? @this.IsFullControl(permissionDefinition)
#pragma warning disable CS0618 // Type or member is obsolete
                : @this.IsFullControl((PermissionInfo)permissionDefinition);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private static void AddPermission(PermissionsGrid @this, IList<IPermissionDefinitionInfo> permissionsList, UserInfo user)
        {
            if (@this.SupportsPermissionsAbstractions)
            {
                @this.AddPermission(permissionsList, user);
            }
            else
            {
#pragma warning disable CS0618 // Type or member is obsolete
                var arrayList = new ArrayList(permissionsList.ToList());
                @this.AddPermission(arrayList, user);
                permissionsList.Clear();
                foreach (IPermissionDefinitionInfo item in arrayList)
                {
                    permissionsList.Add(item);
                }
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        private static void UpdatePermission(PermissionsGrid @this, IPermissionDefinitionInfo permissionDefinitionInfo, int roleId, string roleName, string stateKey)
        {
            if (@this.SupportsPermissionsAbstractions)
            {
                @this.UpdatePermission(permissionDefinitionInfo, roleId, roleName, stateKey);
            }
            else
            {
#pragma warning disable CS0618 // Type or member is obsolete
                @this.UpdatePermission((PermissionInfo)permissionDefinitionInfo, roleId, roleName, stateKey);
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        private static void UpdatePermission(PermissionsGrid @this, IPermissionDefinitionInfo permissionDefinitionInfo, int roleId, string roleName, bool allowAccess)
        {
            if (@this.SupportsPermissionsAbstractions)
            {
                @this.UpdatePermission(permissionDefinitionInfo, roleId, roleName, allowAccess);
            }
            else
            {
#pragma warning disable CS0618 // Type or member is obsolete
                @this.UpdatePermission((PermissionInfo)permissionDefinitionInfo, roleId, roleName, allowAccess);
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        private static void UpdatePermission(PermissionsGrid @this, IPermissionDefinitionInfo permissionDefinitionInfo, string displayName, int userId, string stateKey)
        {
            if (@this.SupportsPermissionsAbstractions)
            {
                @this.UpdatePermission(permissionDefinitionInfo, displayName, userId, stateKey);
            }
            else
            {
#pragma warning disable CS0618 // Type or member is obsolete
                @this.UpdatePermission((PermissionInfo)permissionDefinitionInfo, displayName, userId, stateKey);
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        private static void UpdatePermission(PermissionsGrid @this, IPermissionDefinitionInfo permissionDefinitionInfo, string displayName, int userId, bool allowAccess)
        {
            if (@this.SupportsPermissionsAbstractions)
            {
                @this.UpdatePermission(permissionDefinitionInfo, displayName, userId, allowAccess);
            }
            else
            {
#pragma warning disable CS0618 // Type or member is obsolete
                @this.UpdatePermission((PermissionInfo)permissionDefinitionInfo, displayName, userId, allowAccess);
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        private static string GetCommandType(string commandName)
        {
            var command = commandName.ToLower(CultureInfo.InvariantCulture);
            if (command.Contains("rolename"))
            {
                return "ROLE";
            }

            if (command.Contains("displayname"))
            {
                return "USER";
            }

            return Null.NullString;
        }

        private static string GetGridCommand(string commandName)
        {
            var commandParts = commandName.Split('/');
            return commandParts[0].ToUpper(CultureInfo.InvariantCulture);
        }

        private void BindData()
        {
            this.EnsureChildControls();
            this.BindRolesGrid();
            this.BindUsersGrid();
        }

        private void BindRolesGrid()
        {
            this.dtRolePermissions.Columns.Clear();
            this.dtRolePermissions.Rows.Clear();

            // Add Roles Column
            this.dtRolePermissions.Columns.Add(new DataColumn("RoleId"));

            // Add Roles Column
            this.dtRolePermissions.Columns.Add(new DataColumn("RoleName"));

            // Carries the decision the WebForms ItemDataBound handlers used to take after binding.
            this.dtRolePermissions.Columns.Add(new DataColumn(ActionsVisibleColumnName, typeof(bool)));

            foreach (var permissionInfo in this.permissions)
            {
                // Add Enabled Column
                this.dtRolePermissions.Columns.Add(new DataColumn(permissionInfo.PermissionName + "_Enabled"));

                // Add Permission Column
                this.dtRolePermissions.Columns.Add(new DataColumn(permissionInfo.PermissionName));
            }

            this.GetRoles();

            this.UpdateRolePermissions();
            for (int i = 0; i <= this.Roles.Count - 1; i++)
            {
                var role = (RoleInfo)this.Roles[i];
                var row = this.dtRolePermissions.NewRow();
                row["RoleId"] = role.RoleID;
                row["RoleName"] = Localization.LocalizeRole(role.RoleName);
                row[ActionsVisibleColumnName] = this.GetRoleActionsVisible(role.RoleID);
                int j;
                for (j = 0; j <= this.permissions.Count - 1; j++)
                {
                    var objPerm = this.permissions[j];
                    row[objPerm.PermissionName + "_Enabled"] = GetEnabled(this, objPerm, role, j + 1);
                    if (this.SupportsDenyPermissions(objPerm))
                    {
                        row[objPerm.PermissionName] = GetPermission(this, objPerm, role, j + 1, PermissionTypeNull);
                    }
                    else
                    {
                        if (GetPermission(this, objPerm, role, j + 1))
                        {
                            row[objPerm.PermissionName] = PermissionTypeGrant;
                        }
                        else
                        {
                            row[objPerm.PermissionName] = PermissionTypeNull;
                        }
                    }
                }

                this.dtRolePermissions.Rows.Add(row);
            }

            this.rolePermissionsGrid.DataSource = this.dtRolePermissions.DefaultView;
            this.rolePermissionsGrid.DataBind();
        }

        private void BindUsersGrid()
        {
            this.dtUserPermissions.Columns.Clear();
            this.dtUserPermissions.Rows.Clear();

            // Add Roles Column
            var col = new DataColumn("UserId");
            this.dtUserPermissions.Columns.Add(col);

            // Add Roles Column
            col = new DataColumn("DisplayName");
            this.dtUserPermissions.Columns.Add(col);

            // Carries the decision the WebForms ItemDataBound handlers used to take after binding.
            this.dtUserPermissions.Columns.Add(new DataColumn(ActionsVisibleColumnName, typeof(bool)));
            int i;
            for (i = 0; i <= this.permissions.Count - 1; i++)
            {
                PermissionInfo objPerm;
                objPerm = (PermissionInfo)this.permissions[i];

                // Add Enabled Column
                col = new DataColumn(objPerm.PermissionName + "_Enabled");
                this.dtUserPermissions.Columns.Add(col);

                // Add Permission Column
                col = new DataColumn(objPerm.PermissionName);
                this.dtUserPermissions.Columns.Add(col);
            }

            if (this.userPermissionsGrid != null)
            {
                this.users = this.GetUsers();

                if (this.users.Count != 0)
                {
                    this.userPermissionsGridVisible = true;
                    DataRow row;
                    for (i = 0; i <= this.users.Count - 1; i++)
                    {
                        var user = (UserInfo)this.users[i];
                        row = this.dtUserPermissions.NewRow();
                        row["UserId"] = user.UserID;
                        row["DisplayName"] = user.DisplayName;
                        row[ActionsVisibleColumnName] = this.GetUserActionsVisible(user.UserID);
                        int j;
                        for (j = 0; j <= this.permissions.Count - 1; j++)
                        {
                            var objPerm = this.permissions[j];
                            row[objPerm.PermissionName + "_Enabled"] = GetEnabled(this, objPerm, user, j + 1);
                            if (this.SupportsDenyPermissions(objPerm))
                            {
                                row[objPerm.PermissionName] = GetPermission(this, objPerm, user, j + 1, PermissionTypeNull);
                            }
                            else
                            {
                                if (GetPermission(this, objPerm, user, j + 1))
                                {
                                    row[objPerm.PermissionName] = PermissionTypeGrant;
                                }
                                else
                                {
                                    row[objPerm.PermissionName] = PermissionTypeNull;
                                }
                            }
                        }

                        this.dtUserPermissions.Rows.Add(row);
                    }

                    this.userPermissionsGrid.DataSource = this.dtUserPermissions.DefaultView;
                    this.userPermissionsGrid.DataBind();
                }
                else
                {
                    this.dtUserPermissions.Rows.Clear();
                    this.userPermissionsGrid.DataSource = this.dtUserPermissions.DefaultView;
                    this.userPermissionsGrid.DataBind();
                    this.userPermissionsGridVisible = false;
                }
            }
        }

        private void EnsureRole(RoleInfo role)
        {
            if (this.Roles.Cast<RoleInfo>().All(r => r.RoleID != role.RoleID))
            {
                this.Roles.Add(role);
            }
        }

        private void GetRoles()
        {
            var checkedRoles = this.GetCheckedRoles();
            var portalSettings = PortalController.Instance.GetCurrentSettings();
            this.Roles = new ArrayList(RoleController.Instance.GetRoles(portalSettings.PortalId, r => r.SecurityMode != SecurityMode.SocialGroup && r.Status == RoleStatus.Approved && checkedRoles.Contains(r.RoleID)).ToArray());

            if (checkedRoles.Contains(this.UnAuthUsersRoleId))
            {
                this.Roles.Add(new RoleInfo { RoleID = this.UnAuthUsersRoleId, RoleName = Globals.glbRoleUnauthUserName });
            }

            if (checkedRoles.Contains(this.AllUsersRoleId))
            {
                this.Roles.Add(new RoleInfo { RoleID = this.AllUsersRoleId, PortalID = portalSettings.PortalId, RoleName = Globals.glbRoleAllUsersName });
            }

            // Administrators Role always has implicit permissions, then it should be always in
            this.EnsureRole(RoleController.Instance.GetRoleById(portalSettings.PortalId, portalSettings.AdministratorRoleId));

            // Show also default roles
            this.EnsureRole(RoleController.Instance.GetRoleById(portalSettings.PortalId, portalSettings.RegisteredRoleId));
            this.EnsureRole(new RoleInfo { RoleID = this.AllUsersRoleId, PortalID = portalSettings.PortalId, RoleName = Globals.glbRoleAllUsersName });

            this.Roles.Reverse();
            this.Roles.Sort(new RoleComparer());
        }

        private IEnumerable<int> GetCheckedRoles()
        {
            IEnumerable<IPermissionInfo> permissionCollection = this.SupportsPermissionsAbstractions ? this.PermissionCollection : this.PermissionsList;
            return permissionCollection?.Select(p => p.RoleId).Distinct() ?? new List<int>();
        }

        private void SetUpGrid(DNNDataGrid grid, string nameColumnDataField, string idColumnDataField, string permissionHeaderText)
        {
            grid.Columns.Clear();
            grid.Columns.Add(new BoundColumn
            {
                HeaderText = permissionHeaderText,
                DataField = nameColumnDataField,
                ItemStyle = { CssClass = "permissionHeader", },
                HeaderStyle = { CssClass = "permissionHeader", },
            });
            grid.Columns.Add(new BoundColumn
            {
                HeaderText = string.Empty,
                DataField = idColumnDataField,
                Visible = false,
            });

            foreach (var permission in this.permissions)
            {
                var locName = permission.ModuleDefId <= 0
                    ? Localization.GetString(permission.PermissionName + ".Permission", PermissionProvider.Instance().LocalResourceFile) // system permission
                    : !string.IsNullOrEmpty(this.ResourceFile)
                        ? Localization.GetString(permission.PermissionName + ".Permission", this.ResourceFile) // custom permission
                        : string.Empty;
                var templateCol = new TemplateColumn
                {
                    ItemTemplate = new PermissionTriStateTemplate(permission)
                    {
                        IsFullControl = IsFullControl(this, permission),
                        IsView = IsViewPermission(this, permission),
                        SupportDenyMode = SupportsDenyPermissions(this, permission),
                    },
                    HeaderText = !string.IsNullOrEmpty(locName) ? locName : permission.PermissionName,
                    HeaderStyle = { Wrap = true, },
                };
                grid.Columns.Add(templateCol);
            }

            var actionsColumn = new ImageCommandColumn
            {
                CommandName = "Delete/" + nameColumnDataField,
                KeyField = idColumnDataField,
                IconKey = "Delete",
                IconSize = "16x16",
                IconStyle = "PermissionGrid",
                HeaderText = Localization.GetString("PermissionActionsHeader.Text", PermissionProvider.Instance().LocalResourceFile),

                // The WebForms grids hid the delete button after binding, by walking the bound row's
                // control tree and setting the ImageButton's Visible to false (see the
                // RolePermissionsGrid_ItemDataBound handlers of this class and of the derived grids).
                // There is no control tree to walk now, so the very same decision is taken while the
                // row is built, through GetRoleActionsVisible/GetUserActionsVisible, and carried in
                // the bound record's ActionsVisible column.
                VisibleField = ActionsVisibleColumnName,
            };
            grid.Columns.Add(actionsColumn);
            grid.ItemCommand += this.Grid_ItemCommand;
        }

        private void Grid_ItemCommand(object source, DataGridCommandEventArgs e)
        {
            var entityId = int.Parse(e.CommandArgument.ToString(), CultureInfo.InvariantCulture);
            var command = GetGridCommand(e.CommandName);
            var entityType = GetCommandType(e.CommandName);
            switch (command)
            {
                case "DELETE":
                    if (entityType == "ROLE")
                    {
                        this.DeleteRolePermissions(entityId);
                    }
                    else if (entityType == "USER")
                    {
                        this.DeleteUserPermissions(entityId);
                    }

                    this.BindData();
                    break;
            }
        }

        private void DeleteRolePermissions(int entityId)
        {
            ////PermissionsList.RemoveAll(p => p.RoleID == entityID);
            IEnumerable<IPermissionInfo> permissionCollection = this.SupportsPermissionsAbstractions ? this.PermissionCollection : this.PermissionsList;
            var permissionToDelete = permissionCollection.Where(p => p.RoleId == entityId);
            foreach (var permission in permissionToDelete)
            {
                this.RemovePermission(permission.PermissionId, entityId, permission.UserId);
            }
        }

        private void DeleteUserPermissions(int entityId)
        {
            IEnumerable<IPermissionInfo> permissionCollection = this.SupportsPermissionsAbstractions ? this.PermissionCollection : this.PermissionsList;
            var permissionToDelete = permissionCollection.Where(p => p.UserId == entityId);
            foreach (var permission in permissionToDelete)
            {
                this.RemovePermission(permission.PermissionId, permission.RoleId, entityId);
            }
        }

        private void SetUpRolesGrid()
        {
            this.SetUpGrid(this.rolePermissionsGrid, "RoleName", "roleid", Localization.GetString("PermissionRoleHeader.Text", PermissionProvider.Instance().LocalResourceFile));
        }

        private void SetUpUsersGrid()
        {
            if (this.userPermissionsGrid != null)
            {
                this.SetUpGrid(this.userPermissionsGrid, "DisplayName", "userid", Localization.GetString("PermissionUserHeader.Text", PermissionProvider.Instance().LocalResourceFile));
            }
        }

        private void FillSelectRoleComboBox(int selectedRoleGroupId)
        {
            this.selectRoleOptions.Clear();
            var portalSettings = PortalController.Instance.GetCurrentSettings();
            var groupRoles = (selectedRoleGroupId > -2) ? RoleController.Instance.GetRoles(portalSettings.PortalId, r => r.RoleGroupID == selectedRoleGroupId && r.SecurityMode != SecurityMode.SocialGroup && r.Status == RoleStatus.Approved)
                : RoleController.Instance.GetRoles(portalSettings.PortalId, r => r.SecurityMode != SecurityMode.SocialGroup && r.Status == RoleStatus.Approved);

            if (selectedRoleGroupId < 0)
            {
                groupRoles.Add(new RoleInfo { RoleID = this.UnAuthUsersRoleId, RoleName = Globals.glbRoleUnauthUserName });
                groupRoles.Add(new RoleInfo { RoleID = this.AllUsersRoleId, RoleName = Globals.glbRoleAllUsersName });
            }

            var roleList = new SortedList<string, int>();

            foreach (var role in groupRoles)
            {
                roleList[Localization.LocalizeRole(role.RoleName)] = role.RoleID;
            }

            foreach (var role in roleList)
            {
                this.selectRoleOptions.Add(new GridOption(role.Key, role.Value.ToString(CultureInfo.InvariantCulture)));
            }

            int[] defaultRoleIds = [this.AllUsersRoleId, portalSettings.RegisteredRoleId, portalSettings.AdministratorRoleId,];
            var itemToSelect = this.selectRoleOptions.FirstOrDefault(i => !defaultRoleIds.Contains(int.Parse(i.Value, CultureInfo.InvariantCulture)));
            if (itemToSelect != null)
            {
                this.selectedRoleValue = itemToSelect.Value;
            }
        }

        private void SetErrorMessage(string errorKey)
        {
            // TODO Remove DEBUG test
            this.errorMessage = "<br />" + (errorKey.StartsWith("DEBUG", StringComparison.Ordinal) ? errorKey : Localization.GetString(errorKey));
        }

        private void CreateAddRoleControls()
        {
            var portalSettings = PortalController.Instance.GetCurrentSettings();
            var arrGroups = RoleController.GetRoleGroups(portalSettings.PortalId);

            // The role group drop down list was an AutoPostBack DropDownList; it is now a select
            // whose change handler calls the very same RoleGroupsSelectedIndexChanged.
            this.roleGroupOptions.Clear();
            this.roleGroupOptions.Add(new GridOption(Localization.GetString("AllRoles"), "-2"));
            this.roleGroupOptions.Add(new GridOption(Localization.GetString("GlobalRoles"), "-1"));
            foreach (RoleGroupInfo roleGroup in arrGroups)
            {
                this.roleGroupOptions.Add(new GridOption(roleGroup.RoleGroupName, roleGroup.RoleGroupID.ToString(CultureInfo.InvariantCulture)));
            }

            this.selectedRoleGroupValue = "-1";

            this.FillSelectRoleComboBox(-1); // Default Role Group is Global Roles
        }

        /// <summary>AddRole runs when the Add Role linkbutton is clicked.</summary>
        private void AddRole(object sender, EventArgs e)
        {
            this.UpdatePermissions();

            // The hidden field is written by dnn.permissiongrid.js, which replaces the select with a
            // token input. When the script did not write it (or could not), the select's own value is
            // used, which is what the WebForms DropDownList would have posted.
            var postedRole = !string.IsNullOrEmpty(this.roleFieldValue) ? this.roleFieldValue : this.selectedRoleValue;
            if (!int.TryParse(postedRole, out var selectedRoleId))
            {
                // Role not selected
                this.SetErrorMessage("InvalidRoleId");
                return;
            }

            // verify role
            var role = this.GetSelectedRole(selectedRoleId);
            if (role != null)
            {
                this.AddPermission(this.permissions, role);
                this.BindData();
            }
            else
            {
                // role does not exist
                this.SetErrorMessage("RoleNotFound");
            }
        }

        /// <summary>Gets a value indicating whether the delete action renders for the given role's row.</summary>
        /// <param name="roleId">The ID of the role of the row.</param>
        /// <returns><see langword="true"/> when the row may be deleted, otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// Replaces the WebForms <c>RolePermissionsGrid_ItemDataBound</c> handler, which hid the
        /// delete <c>ImageButton</c> of the administrator, all-users and registered-users rows by
        /// walking the bound row's control tree. The condition is unchanged.
        /// </remarks>
        protected virtual bool GetRoleActionsVisible(int roleId)
        {
            return roleId != PortalSettings.Current.AdministratorRoleId
                && roleId != this.AllUsersRoleId
                && roleId != PortalSettings.Current.RegisteredRoleId;
        }

        /// <summary>Gets a value indicating whether the delete action renders for the given user's row.</summary>
        /// <param name="userId">The ID of the user of the row.</param>
        /// <returns><see langword="true"/> when the row may be deleted, otherwise <see langword="false"/>.</returns>
        /// <remarks>The WebForms user grid never hid its delete button, so this is always <see langword="true"/> by default.</remarks>
        protected virtual bool GetUserActionsVisible(int userId) => true;

        /// <summary>Binds this grid to its permissions.</summary>
        /// <remarks>
        /// Replaces <c>Control.DataBind()</c>, which bound the child controls. The derived grids
        /// override it to refresh their permission collection first, so it stays virtual.
        /// </remarks>
        public virtual void DataBind()
        {
            this.BindData();
        }

        /// <summary>Saves the state of this grid.</summary>
        /// <returns>The state, or <see langword="null"/>.</returns>
        /// <remarks>
        /// Was <c>Control.SaveViewState()</c>. There is no view state on ASP.NET Core, but the
        /// derived grids persist their permission collections through this protocol, so it survives
        /// as an explicit, overridable pair of methods: a host that needs to round-trip a grid over
        /// something other than a Blazor circuit (see <c>DotNetNuke.Framework.PageStatePersister</c>)
        /// can call them. The state produced by the derived grids is the same string of permission
        /// keys as before, so nothing about the format changed.
        /// </remarks>
        protected virtual object SaveViewState() => null;

        /// <summary>Restores the state of this grid.</summary>
        /// <param name="savedState">The state previously returned by <see cref="SaveViewState"/>.</param>
        /// <remarks>See <see cref="SaveViewState"/>.</remarks>
        protected virtual void LoadViewState(object savedState)
        {
        }

        /// <summary>Creates the child controls once, if they have not been created yet.</summary>
        /// <remarks>Replaces <c>Control.EnsureChildControls()</c>, with the same "exactly once" semantics.</remarks>
        protected void EnsureChildControls()
        {
            if (this.childControlsCreated)
            {
                return;
            }

            this.childControlsCreated = true;
            this.CreateChildControls();
        }

        /// <inheritdoc />
        /// <remarks>
        /// Emits what the WebForms child control tree emitted: the permissions panel, the role group
        /// filter and role picker, the two grid tables (through
        /// <see cref="DNNDataGrid.RenderTable"/>) and the add-user controls.
        /// </remarks>
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.EnsureChildControls();

            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "id", this.ClientID);
            builder.AddAttribute(2, "class", "dnnGrid dnnPermissionsGrid");

            // Add role controls (was CreateAddRoleControls' panel).
            builder.OpenElement(3, "div");
            builder.AddAttribute(4, "class", "dnnFormItem");

            builder.OpenElement(5, "div");
            builder.AddAttribute(6, "class", "leftGroup");
            this.RenderLabel(builder, 7, "cboRoleGroups", Localization.GetString("RoleGroupFilter"));
            this.RenderSelect(builder, 10, "cboRoleGroups", this.selectedRoleGroupValue, this.roleGroupOptions, this.RoleGroupChanged);
            builder.CloseElement();

            // The permission grid's client script writes the selected role into this field.
            builder.OpenElement(20, "input");
            builder.AddAttribute(21, "type", "hidden");
            builder.AddAttribute(22, "id", this.ClientID + "_roleField");
            builder.AddAttribute(23, "name", "roleField");
            builder.AddAttribute(24, "value", this.roleFieldValue);
            builder.AddAttribute(25, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, args => this.roleFieldValue = args?.Value?.ToString() ?? string.Empty));
            builder.CloseElement();

            builder.OpenElement(30, "div");
            builder.AddAttribute(31, "class", "rightGroup");
            this.RenderLabel(builder, 32, "cboSelectRole", Localization.GetString("RoleSelect"));
            this.RenderSelect(builder, 35, "cboSelectRole", this.selectedRoleValue, this.selectRoleOptions, this.SelectRoleChanged);
            this.RenderActionLink(builder, 45, Localization.GetString("Add"), EventCallback.Factory.Create<MouseEventArgs>(this, () => this.AddRole(this, EventArgs.Empty)));
            builder.CloseElement();

            builder.CloseElement();

            if (this.rolePermissionsGrid != null)
            {
                builder.AddContent(100, this.rolePermissionsGrid.RenderTable());
            }

            if (this.users != null && this.userPermissionsGrid != null)
            {
                if (this.userPermissionsGridVisible)
                {
                    builder.AddContent(200, this.userPermissionsGrid.RenderTable());
                }

                builder.OpenElement(300, "div");
                builder.AddAttribute(301, "class", "dnnFormItem");
                this.RenderLabel(builder, 302, "txtUser", Localization.GetString("DisplayName"));

                builder.OpenElement(305, "input");
                builder.AddAttribute(306, "type", "text");
                builder.AddAttribute(307, "id", this.ClientID + "_txtUser");
                builder.AddAttribute(308, "name", "txtUser");
                builder.AddAttribute(309, "value", this.userNameValue);
                builder.AddAttribute(310, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, args => this.userNameValue = args?.Value?.ToString() ?? string.Empty));
                builder.CloseElement();

                // The token input writes the picked user IDs into this field.
                builder.OpenElement(315, "input");
                builder.AddAttribute(316, "type", "hidden");
                builder.AddAttribute(317, "id", this.ClientID + "_hiddenUserIds");
                builder.AddAttribute(318, "name", "hiddenUserIds");
                builder.AddAttribute(319, "value", this.hiddenUserIdsValue);
                builder.AddAttribute(320, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, args => this.hiddenUserIdsValue = args?.Value?.ToString() ?? string.Empty));
                builder.CloseElement();

                this.RenderActionLink(builder, 325, Localization.GetString("Add"), EventCallback.Factory.Create<MouseEventArgs>(this, () => this.AddUser(this, EventArgs.Empty)));

                builder.CloseElement();
            }

            if (!string.IsNullOrEmpty(this.errorMessage))
            {
                builder.OpenElement(400, "span");
                builder.AddAttribute(401, "class", "NormalRed");
                builder.AddContent(402, new MarkupString(this.errorMessage));
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        private void RenderLabel(RenderTreeBuilder builder, int sequence, string forControl, string text)
        {
            builder.OpenElement(sequence, "label");
            builder.AddAttribute(sequence + 1, "for", this.ClientID + "_" + forControl);
            builder.AddContent(sequence + 2, text);
            builder.CloseElement();
        }

        private void RenderSelect(RenderTreeBuilder builder, int sequence, string id, string selectedValue, List<GridOption> options, Action<ChangeEventArgs> onChange)
        {
            builder.OpenElement(sequence, "select");
            builder.AddAttribute(sequence + 1, "id", this.ClientID + "_" + id);
            builder.AddAttribute(sequence + 2, "name", id);
            builder.AddAttribute(sequence + 3, "value", selectedValue);
            builder.AddAttribute(sequence + 4, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, onChange));

            var optionSequence = sequence + 5;
            foreach (var option in options)
            {
                builder.OpenElement(optionSequence, "option");
                builder.AddAttribute(optionSequence + 1, "value", option.Value);
                if (option.Value == selectedValue)
                {
                    builder.AddAttribute(optionSequence + 2, "selected", true);
                }

                builder.AddContent(optionSequence + 3, option.Text);
                builder.CloseElement();
                optionSequence += 4;
            }

            builder.CloseElement();
        }

        private void RenderActionLink(RenderTreeBuilder builder, int sequence, string text, EventCallback<MouseEventArgs> onClick)
        {
            builder.OpenElement(sequence, "button");
            builder.AddAttribute(sequence + 1, "type", "button");
            builder.AddAttribute(sequence + 2, "class", "dnnSecondaryAction");
            builder.AddAttribute(sequence + 3, "onclick", onClick);
            builder.AddContent(sequence + 4, text);
            builder.CloseElement();
        }

        private void RoleGroupChanged(ChangeEventArgs args)
        {
            this.selectedRoleGroupValue = args?.Value?.ToString() ?? "-1";
            this.RoleGroupsSelectedIndexChanged(this, EventArgs.Empty);
        }

        private void SelectRoleChanged(ChangeEventArgs args)
        {
            this.selectedRoleValue = args?.Value?.ToString() ?? string.Empty;
        }

        private RoleInfo GetSelectedRole(int selectedRoleId)
        {
            RoleInfo role = null;
            if (selectedRoleId == this.AllUsersRoleId)
            {
                role = new RoleInfo
                {
                    RoleID = this.AllUsersRoleId,
                    RoleName = Globals.glbRoleAllUsersName,
                };
            }
            else if (selectedRoleId == this.UnAuthUsersRoleId)
            {
                role = new RoleInfo
                {
                    RoleID = this.UnAuthUsersRoleId,
                    RoleName = Globals.glbRoleUnauthUserName,
                };
            }
            else
            {
                role = RoleController.Instance.GetRoleById(this.PortalId, selectedRoleId);
            }

            return role;
        }

        /// <summary>An option of one of the grid's drop down lists.</summary>
        /// <remarks>Replaces <c>System.Web.UI.WebControls.ListItem</c>.</remarks>
        private sealed class GridOption
        {
            /// <summary>Initializes a new instance of the <see cref="GridOption"/> class.</summary>
            /// <param name="text">The display text.</param>
            /// <param name="value">The value.</param>
            public GridOption(string text, string value)
            {
                this.Text = text;
                this.Value = value;
            }

            /// <summary>Gets the display text of this option.</summary>
            public string Text { get; }

            /// <summary>Gets the value of this option.</summary>
            public string Value { get; }
        }
    }
}
