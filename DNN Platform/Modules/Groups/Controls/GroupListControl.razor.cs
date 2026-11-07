// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.Groups.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Modules.Groups.Components;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.WebUtilities;

    /// <summary>Renders the list of social groups a module instance is configured to show.</summary>
    /// <remarks>
    /// This was <c>GroupListControl</c>, a <c>System.Web.UI.WebControls.WebControl</c>. The
    /// declarative properties (which the <c>.ascx</c> set as attributes or inner properties) are now
    /// component <see cref="ParameterAttribute"/>s with the same names and the same default values
    /// the <c>[DefaultValue]</c> attributes declared, and the <c>Render(HtmlTextWriter)</c> override
    /// is the <see cref="OnParametersSet"/> computation below plus the markup of
    /// GroupListControl.razor. Role filtering, sorting, paging, <see cref="ItemsPerRow"/> row
    /// chunking, the seven <c>{resx:...}</c> substitutions, the <c>[GroupViewTabId]</c> substitution,
    /// the "no groups found" message and the pager are unchanged.
    /// </remarks>
    public partial class GroupListControl : ComponentBase
    {
        private readonly List<List<string>> rows = new List<List<string>>();
        private string headerMarkup = string.Empty;
        private string footerMarkup = string.Empty;
        private string rowHeaderMarkup = string.Empty;
        private string rowFooterMarkup = string.Empty;
        private string pagerMarkup = string.Empty;
        private string noGroupsFoundText = string.Empty;
        private bool showNoGroupsFound;

        /// <summary>Gets or sets the current user the group list is rendered for.</summary>
        /// <remarks>
        /// Replaces the public <c>currentUser</c> field. When no value is supplied it is resolved the
        /// way the control's <c>OnInit</c> resolved it, with
        /// <c>UserController.Instance.GetCurrentUserInfo()</c>.
        /// </remarks>
        [Parameter]
        public UserInfo CurrentUser { get; set; }

        /// <summary>Gets or sets the portal settings the group list is rendered for.</summary>
        /// <remarks>Replaces the read-only <c>PortalSettings</c> property, which returned <c>PortalSettings.Current</c>; that is still the fallback.</remarks>
        [Parameter]
        public PortalSettings PortalSettings { get; set; }

        /// <summary>Gets or sets the template a single group is rendered with.</summary>
        [Parameter]
        public string ItemTemplate { get; set; } = string.Empty;

        /// <summary>Gets or sets the markup written before the list.</summary>
        [Parameter]
        public string HeaderTemplate { get; set; } = string.Empty;

        /// <summary>Gets or sets the markup written after the list.</summary>
        [Parameter]
        public string FooterTemplate { get; set; } = string.Empty;

        /// <summary>Gets or sets the markup written before each row.</summary>
        [Parameter]
        public string RowHeaderTemplate { get; set; } = string.Empty;

        /// <summary>Gets or sets the markup written after each row.</summary>
        [Parameter]
        public string RowFooterTemplate { get; set; } = string.Empty;

        /// <summary>Gets or sets the number of groups rendered per row.</summary>
        [Parameter]
        public int ItemsPerRow { get; set; } = 1;

        /// <summary>Gets or sets the role group the listed groups belong to.</summary>
        [Parameter]
        public int RoleGroupId { get; set; }

        /// <summary>Gets or sets the number of groups per page.</summary>
        [Parameter]
        public int PageSize { get; set; } = 20;

        /// <summary>Gets or sets the zero based index of the page being rendered.</summary>
        [Parameter]
        public int CurrentIndex { get; set; }

        /// <summary>Gets or sets the tab the pager links back to.</summary>
        [Parameter]
        public int TabId { get; set; } = -1;

        /// <summary>Gets or sets the search filter applied to the group name and description.</summary>
        [Parameter]
        public string SearchFilter { get; set; } = string.Empty;

        /// <summary>Gets or sets the property the list is sorted by.</summary>
        [Parameter]
        public string SortField { get; set; } = "rolename";

        /// <summary>Gets or sets the sort direction (<c>asc</c> or <c>desc</c>).</summary>
        [Parameter]
        public string SortDirection { get; set; } = "asc";

        /// <summary>Gets or sets a value indicating whether only the current user's groups are listed.</summary>
        [Parameter]
        public bool DisplayCurrentUserGroups { get; set; }

        /// <summary>Gets or sets the tab ID of the group view page.</summary>
        [Parameter]
        public int GroupViewTabId { get; set; }

        /// <summary>Gets or sets the navigation manager the page number is read from.</summary>
        /// <remarks>
        /// Replaces <c>HttpContext.Current.Request.QueryString["page"]</c>. The query string is read
        /// off the component's URL, because <c>HttpContext</c> is <see langword="null"/> once the
        /// Blazor circuit is interactive.
        /// </remarks>
        [Inject]
        protected NavigationManager Navigation { get; set; }

        /// <inheritdoc />
        /// <remarks>Replaces <c>OnInit</c>, which resolved the current user.</remarks>
        protected override void OnInitialized()
        {
            base.OnInitialized();
            this.CurrentUser ??= UserController.Instance.GetCurrentUserInfo();
        }

        /// <inheritdoc />
        /// <remarks>This is the body of the original <c>Render(HtmlTextWriter)</c>; the writes it performed are the fields GroupListControl.razor emits.</remarks>
        protected override void OnParametersSet()
        {
            base.OnParametersSet();

            this.CurrentUser ??= UserController.Instance.GetCurrentUserInfo();
            var portalSettings = this.PortalSettings ?? Entities.Portals.PortalSettings.Current;

            var whereCls = new List<Func<RoleInfo, bool>>
            {
                grp => grp.SecurityMode != SecurityMode.SecurityRole && grp.Status == RoleStatus.Approved,
            };

            if (this.RoleGroupId >= -1)
            {
                whereCls.Add(grp => grp.RoleGroupID == this.RoleGroupId);
            }

            if (this.DisplayCurrentUserGroups)
            {
                whereCls.Add(grp => this.CurrentUser.IsInRole(grp.RoleName));
            }
            else
            {
                whereCls.Add(grp => grp.IsPublic || this.CurrentUser.IsInRole(grp.RoleName) || this.CurrentUser.IsInRole(portalSettings.AdministratorRoleName));
            }

            if (!string.IsNullOrEmpty(this.SearchFilter))
            {
                whereCls.Add(grp => grp.RoleName.ToLowerInvariant().Contains(this.SearchFilter.ToLowerInvariant()) || grp.Description.ToLowerInvariant().Contains(this.SearchFilter.ToLowerInvariant()));
            }

            var roles = RoleController.Instance.GetRoles(portalSettings.PortalId, grp => TestPredicateGroup(whereCls, grp));

            if ((this.SortDirection ?? string.Empty).ToLowerInvariant() == "asc")
            {
                roles = roles.OrderBy(info => GetOrderByProperty(info, this.SortField)).ToList();
            }
            else
            {
                roles = roles.OrderByDescending(info => GetOrderByProperty(info, this.SortField)).ToList();
            }

            decimal pages = (decimal)roles.Count / (decimal)this.PageSize;

            this.headerMarkup = this.HeaderTemplate ?? string.Empty;

            var itemTemplate = this.ItemTemplate ?? string.Empty;
            itemTemplate = itemTemplate.Replace("{resx:posts}", Localization.GetString("posts", Constants.SharedResourcesPath));
            itemTemplate = itemTemplate.Replace("{resx:members}", Localization.GetString("members", Constants.SharedResourcesPath));
            itemTemplate = itemTemplate.Replace("{resx:photos}", Localization.GetString("photos", Constants.SharedResourcesPath));
            itemTemplate = itemTemplate.Replace("{resx:documents}", Localization.GetString("documents", Constants.SharedResourcesPath));

            itemTemplate = itemTemplate.Replace("{resx:Join}", Localization.GetString("Join", Constants.SharedResourcesPath));
            itemTemplate = itemTemplate.Replace("{resx:Pending}", Localization.GetString("Pending", Constants.SharedResourcesPath));
            itemTemplate = itemTemplate.Replace("{resx:LeaveGroup}", Localization.GetString("LeaveGroup", Constants.SharedResourcesPath));
            itemTemplate = itemTemplate.Replace("[GroupViewTabId]", this.GroupViewTabId.ToString(CultureInfo.InvariantCulture));

            this.showNoGroupsFound = roles.Count == 0;
            this.noGroupsFoundText = this.showNoGroupsFound
                ? Localization.GetString("NoGroupsFound", Constants.SharedResourcesPath)
                : string.Empty;

            var pageValue = this.GetQueryValue("page");
            if (!string.IsNullOrEmpty(pageValue))
            {
                this.CurrentIndex = Convert.ToInt32(pageValue, CultureInfo.InvariantCulture);
                this.CurrentIndex = this.CurrentIndex - 1;
            }

            int rowItem = 0;
            int recordStart = this.CurrentIndex * this.PageSize;

            if (this.CurrentIndex == 0)
            {
                recordStart = 0;
            }

            // The writer wrote RowHeaderTemplate when rowItem reached 0 and RowFooterTemplate when
            // it reached ItemsPerRow (plus once more for a partial trailing row); chunking the
            // parsed items into rows of ItemsPerRow reproduces that sequence exactly.
            this.rows.Clear();
            this.rowHeaderMarkup = this.RowHeaderTemplate ?? string.Empty;
            this.rowFooterMarkup = this.RowFooterTemplate ?? string.Empty;
            List<string> currentRow = null;

            for (int x = recordStart; x < recordStart + this.PageSize; x++)
            {
                if (x > roles.Count - 1)
                {
                    break;
                }

                var role = roles[x];

                string rowTemplate = itemTemplate;

                if (rowItem == 0)
                {
                    currentRow = new List<string>();
                    this.rows.Add(currentRow);
                }

                var groupParser = new GroupViewParser(portalSettings, role, this.CurrentUser, rowTemplate, this.GroupViewTabId);
                currentRow.Add(groupParser.ParseView());

                rowItem += 1;

                if (rowItem == this.ItemsPerRow)
                {
                    rowItem = 0;
                }
            }

            this.footerMarkup = this.FooterTemplate ?? string.Empty;

            int totalPages = Convert.ToInt32(Math.Ceiling(pages));

            if (totalPages == 0)
            {
                totalPages = 1;
            }

            string sUrlFormat = "<a href=\"{0}\" class=\"{1}\">{2}</a>";
            string[] currParams = new string[] { };

            StringBuilder sb = new StringBuilder();

            if (totalPages > 1)
            {
                for (int x = 1; x <= totalPages; x++)
                {
                    string[] @params = new string[] { };

                    if (currParams.Length > 0 & x > 1)
                    {
                        @params = Utilities.AddParams("page=" + x.ToString(CultureInfo.InvariantCulture), currParams);
                    }
                    else if (currParams.Length > 0 & x == 1)
                    {
                        @params = currParams;
                    }
                    else if (x > 1)
                    {
                        @params = new string[] { "page=" + x.ToString(CultureInfo.InvariantCulture) };
                    }

                    string sUrl = Utilities.NavigateUrl(this.TabId, @params);

                    string cssClass = "pagerItem";

                    if (x - 1 == this.CurrentIndex)
                    {
                        cssClass = "pagerItemSelected";
                    }

                    sb.AppendFormat(CultureInfo.InvariantCulture, sUrlFormat, sUrl, cssClass, x.ToString(CultureInfo.InvariantCulture));
                }
            }

            this.pagerMarkup = sb.ToString();
        }

        private static bool TestPredicateGroup(IEnumerable<Func<RoleInfo, bool>> predicates, RoleInfo ri)
        {
            return predicates.All(p => p(ri));
        }

        private static object GetOrderByProperty(object obj, string property)
        {
            var propertyInfo = obj.GetType().GetProperty(property, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            return propertyInfo == null ? null : propertyInfo.GetValue(obj, null);
        }

        /// <summary>Reads a value off the query string of the page's URL.</summary>
        /// <param name="name">The name of the query string parameter.</param>
        /// <returns>The value, or <see langword="null"/> when it was not supplied.</returns>
        private string GetQueryValue(string name)
        {
            var uri = this.Navigation?.Uri;
            if (string.IsNullOrEmpty(uri))
            {
                return null;
            }

            var query = QueryHelpers.ParseQuery(new Uri(uri).Query);
            foreach (var parameter in query)
            {
                if (string.Equals(parameter.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return parameter.Value.ToString();
                }
            }

            return null;
        }
    }
}
