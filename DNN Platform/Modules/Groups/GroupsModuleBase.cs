// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Groups
{
    using System;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Modules.Groups.Components;
    using DotNetNuke.Security.Permissions;

    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The base component of every Social Groups module control.</summary>
    /// <remarks>
    /// This was a WebForms control base. <see cref="PortalModuleBase"/> is now a Blazor component
    /// base (through <c>DotNetNuke.Framework.UserControlBase</c>, which derives from
    /// <c>ComponentBase</c>), which is the same base the already migrated
    /// <c>DotNetNuke.Web.DDRMenu.ModuleBase</c> uses, so this class keeps deriving from it and
    /// every member of the original is preserved. Two WebForms-only concepts changed shape:
    /// <list type="bullet">
    /// <item><description><c>Request.QueryString[...]</c> is read through
    /// <see cref="GetQueryValue"/>, which prefers the component's URL (available over a Blazor
    /// circuit, where <c>HttpContext</c> is <see langword="null"/>) and falls back to the request
    /// captured during the initial render.</description></item>
    /// <item><description><c>System.Web.IHtmlString</c>/<c>HtmlString.ToHtmlString()</c> are gone;
    /// the two template properties return the same raw strings they always did.</description></item>
    /// </list>
    /// </remarks>
    public class GroupsModuleBase : PortalModuleBase
    {
        /// <summary>Initializes a new instance of the <see cref="GroupsModuleBase"/> class.</summary>
        public GroupsModuleBase()
        {
            this.NavigationManager = this.DependencyProvider.GetRequiredService<INavigationManager>();
        }

        /// <summary>The view the module control renders.</summary>
        public enum GroupMode
        {
            /// <summary>Setup group.</summary>
            Setup = 0,

            /// <summary>List groups.</summary>
            List = 1,

            /// <summary>View group.</summary>
            View = 2,
        }

        /// <summary>Gets the view this module instance is configured to render.</summary>
        public GroupMode LoadView
        {
            get
            {
                var mode = GroupMode.Setup;
                if (this.Settings.ContainsKey(Constants.GroupLoadView))
                {
                    switch (this.Settings[Constants.GroupLoadView].ToString())
                    {
                        case "List":
                            mode = GroupMode.List;
                            break;
                        case "View":
                            mode = GroupMode.View;
                            break;
                    }
                }

                return mode;
            }
        }

        /// <summary>Gets the ID of the group (role) named by the URL, or <c>-1</c>.</summary>
        public int GroupId
        {
            get
            {
                int groupId = -1;
                var value = this.GetQueryValue("GroupId");
                if (string.IsNullOrEmpty(value))
                {
                    return groupId;
                }

                if (int.TryParse(value, out groupId))
                {
                    return groupId;
                }

                return -1;
            }
        }

        /// <summary>Gets the configured default role group ID (<c>-2</c> is "&lt; All Roles &gt;").</summary>
        public int DefaultRoleGroupId
        {
            get
            {
                var roleGroupId = Null.NullInteger;
                if (this.Settings.ContainsKey(Constants.DefaultRoleGroupSetting))
                {
                    int id;
                    if (int.TryParse(this.Settings[Constants.DefaultRoleGroupSetting].ToString(), out id))
                    {
                        roleGroupId = id;
                    }
                }

                return roleGroupId; // -2 is for "< All Roles >"
            }
        }

        /// <summary>Gets the tab ID of the group listing page.</summary>
        public int GroupListTabId
        {
            get
            {
                if (this.Settings.ContainsKey(Constants.GroupListPage))
                {
                    return Convert.ToInt32(this.Settings[Constants.GroupListPage].ToString());
                }

                return this.TabId;
            }
        }

        /// <summary>Gets the tab ID of the group view page.</summary>
        public int GroupViewTabId
        {
            get
            {
                if (this.Settings.ContainsKey(Constants.GroupViewPage))
                {
                    return Convert.ToInt32(this.Settings[Constants.GroupViewPage].ToString());
                }

                return this.TabId;
            }
        }

        /// <summary>Gets the template a single group is rendered with.</summary>
        public string GroupViewTemplate
        {
            get
            {
                var template = this.LocalizeText("GroupViewTemplate.Text");
                if (this.Settings.ContainsKey(Constants.GroupViewTemplate))
                {
                    if (!string.IsNullOrEmpty(this.Settings[Constants.GroupViewTemplate].ToString()))
                    {
                        template = this.Settings[Constants.GroupViewTemplate].ToString();
                    }
                }

                return template;
            }
        }

        /// <summary>Gets the template a group list item is rendered with.</summary>
        public string GroupListTemplate
        {
            get
            {
                var template = this.LocalizeText("GroupListTemplate.Text");
                if (this.Settings.ContainsKey(Constants.GroupListTemplate))
                {
                    if (!string.IsNullOrEmpty(this.Settings[Constants.GroupListTemplate].ToString()))
                    {
                        template = this.Settings[Constants.GroupListTemplate].ToString();
                    }
                }

                return template;
            }
        }

        /// <summary>Gets the configured default group view mode.</summary>
        public string DefaultGroupMode
        {
            get
            {
                if (this.Settings.ContainsKey(Constants.DefautlGroupViewMode))
                {
                    return this.Settings[Constants.DefautlGroupViewMode].ToString();
                }

                return string.Empty;
            }
        }

        /// <summary>Gets a value indicating whether group moderation is enabled.</summary>
        public bool GroupModerationEnabled
        {
            get
            {
                if (this.Settings.ContainsKey(Constants.GroupModerationEnabled))
                {
                    return Convert.ToBoolean(this.Settings[Constants.GroupModerationEnabled].ToString());
                }

                return false;
            }
        }

        /// <summary>Gets a value indicating whether the current user may create a group.</summary>
        public bool CanCreate
        {
            get
            {
                if (this.IsAuthenticated)
                {
                    if (this.UserInfo.IsSuperUser)
                    {
                        return true;
                    }

                    return ModulePermissionController.HasModulePermission(this.ModuleConfiguration.ModulePermissions, "CREATEGROUP");
                }

                return false;
            }
        }

        /// <summary>Gets the search filter named by the URL.</summary>
        public string GroupListFilter
        {
            get
            {
                return this.GetQueryValue("Filter");
            }
        }

        /// <summary>Gets the page size of the group list.</summary>
        public int GroupListPageSize
        {
            get
            {
                if (this.Settings.ContainsKey(Constants.GroupListPageSize))
                {
                    if (!string.IsNullOrEmpty(this.Settings[Constants.GroupListPageSize].ToString()))
                    {
                        return Convert.ToInt32(this.Settings[Constants.GroupListPageSize].ToString());
                    }
                }

                return 20;
            }
        }

        /// <summary>Gets a value indicating whether the group list search panel is shown.</summary>
        public bool GroupListSearchEnabled
        {
            get
            {
                var enableSearch = false;

                if (this.Settings.ContainsKey(Constants.GroupListSearchEnabled))
                {
                    if (!string.IsNullOrEmpty(this.Settings[Constants.GroupListSearchEnabled].ToString()))
                    {
                        _ = bool.TryParse(this.Settings[Constants.GroupListSearchEnabled].ToString(), out enableSearch);
                    }
                }

                return enableSearch;
            }
        }

        /// <summary>Gets the field the group list is sorted by.</summary>
        public string GroupListSortField
        {
            get
            {
                return this.Settings.ContainsKey(Constants.GroupListSortField) ? this.Settings[Constants.GroupListSortField].ToString() : string.Empty;
            }
        }

        /// <summary>Gets the direction the group list is sorted in.</summary>
        public string GroupListSortDirection
        {
            get
            {
                return this.Settings.ContainsKey(Constants.GroupListSortDirection) ? this.Settings[Constants.GroupListSortDirection].ToString() : string.Empty;
            }
        }

        /// <summary>Gets a value indicating whether only the current user's groups are listed.</summary>
        public bool GroupListUserGroupsOnly
        {
            get
            {
                var userGroupsOnly = false;

                if (this.Settings.ContainsKey(Constants.GroupListUserGroupsOnly))
                {
                    if (!string.IsNullOrEmpty(this.Settings[Constants.GroupListUserGroupsOnly].ToString()))
                    {
                        _ = bool.TryParse(this.Settings[Constants.GroupListUserGroupsOnly].ToString(), out userGroupsOnly);
                    }
                }

                return userGroupsOnly;
            }
        }

        /// <summary>Gets or sets the module configuration the dispatching control hands down to this control.</summary>
        /// <remarks>
        /// Replaces the <c>ctl.ModuleConfiguration = this.ModuleConfiguration</c> assignment the
        /// WebForms dispatchers (<c>Loader.ascx.cs</c>, <c>View.ascx.cs</c>) performed right after
        /// <c>LoadControl</c>. A Blazor parent can only hand values to a child through a
        /// <see cref="Microsoft.AspNetCore.Components.ParameterAttribute"/>, and
        /// <see cref="PortalModuleBase.ModuleConfiguration"/> is not one (it is backed by
        /// <c>ModuleInstanceContext</c>), so the value arrives here and is copied onto the module
        /// context before the control's load phase runs — the same order as the original.
        /// </remarks>
        [Microsoft.AspNetCore.Components.Parameter]
        public ModuleInfo ParentModuleConfiguration { get; set; }

        /// <summary>Gets the navigation manager DNN URLs are composed with.</summary>
        protected INavigationManager NavigationManager { get; }

        /// <summary>Gets or sets the navigation manager the component's URL is read from.</summary>
        /// <remarks>
        /// This is the Blazor navigation manager (not DNN's <see cref="INavigationManager"/>); it is
        /// the only query string source that keeps working once the circuit is interactive.
        /// </remarks>
        [Microsoft.AspNetCore.Components.Inject]
        protected Microsoft.AspNetCore.Components.NavigationManager Navigation { get; set; }

        /// <summary>Gets a value indicating whether the current user is authenticated.</summary>
        /// <remarks>
        /// Replaces <c>Request.IsAuthenticated</c>. In a request context the answer comes from the
        /// ASP.NET Core request; over a Blazor circuit (no <c>HttpContext</c>) the request
        /// independent portal user lookup gives the same answer, exactly as the migrated
        /// <see cref="Components.GroupViewParser"/> does.
        /// </remarks>
        protected bool IsAuthenticated =>
            HttpContextSource.Current?.Request.IsAuthenticated()
            ?? (this.UserInfo != null && this.UserInfo.UserID > Null.NullInteger);

        /// <summary>Gets the URL of the group creation control.</summary>
        /// <returns>A URL.</returns>
        public string GetCreateUrl()
        {
            return this.ModuleContext.EditUrl("Create"); ////.NavigateUrl(GroupCreateTabId,"",true,null);
        }

        /// <summary>Gets the URL of this page without the search filter.</summary>
        /// <returns>A URL.</returns>
        public string GetClearFilterUrl()
        {
            return this.NavigationManager.NavigateURL(this.TabId, string.Empty);
        }

        /// <summary>Gets the URL of the group edit control for the current group.</summary>
        /// <returns>A URL.</returns>
        public string GetEditUrl()
        {
            return this.ModuleContext.EditUrl("GroupId", this.GroupId.ToString("D"), "Edit");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Applies <see cref="ParentModuleConfiguration"/> before the base class raises the load
        /// phase, which is the point the WebForms dispatchers had already assigned
        /// <c>ModuleConfiguration</c> on the control they had just loaded.
        /// </remarks>
        protected override void OnParametersSet()
        {
            if (this.ParentModuleConfiguration != null)
            {
                this.ModuleConfiguration = this.ParentModuleConfiguration;
            }

            base.OnParametersSet();
        }

        /// <summary>Reads a value off the query string of the page's URL.</summary>
        /// <param name="name">The name of the query string parameter (matched case insensitively, as <c>Request.QueryString</c> was).</param>
        /// <returns>The value, or <see langword="null"/> when it was not supplied.</returns>
        protected string GetQueryValue(string name)
        {
            var uri = this.Navigation?.Uri;
            if (!string.IsNullOrEmpty(uri))
            {
                var query = QueryHelpers.ParseQuery(new Uri(uri).Query);
                foreach (var parameter in query)
                {
                    if (string.Equals(parameter.Key, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return parameter.Value.ToString();
                    }
                }
            }

            var request = this.Request;
            if (request == null)
            {
                return null;
            }

            return request.Query.TryGetValue(name, out var values) ? values.ToString() : null;
        }
    }
}
