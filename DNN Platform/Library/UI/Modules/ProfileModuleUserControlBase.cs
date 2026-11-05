// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Modules
{
    using System;
    using System.Globalization;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Internal;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using Microsoft.Extensions.DependencyInjection;

    public abstract class ProfileModuleUserControlBase : ModuleUserControlBase, IProfileModule
    {
        /// <summary>Initializes a new instance of the <see cref="ProfileModuleUserControlBase"/> class.</summary>
        public ProfileModuleUserControlBase()
        {
            this.NavigationManager = Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
        }

        /// <inheritdoc />
        public abstract bool DisplayModule { get; }

        /// <inheritdoc />
        public int ProfileUserId
        {
            get
            {
                if (!string.IsNullOrEmpty(this.Request.GetValue("UserId")))
                {
                    return int.Parse(this.Request.GetValue("UserId"), CultureInfo.InvariantCulture);
                }

                return UserController.Instance.GetCurrentUserInfo().UserID;
            }
        }

        protected INavigationManager NavigationManager { get; }

        protected bool IsUser
        {
            get { return this.ProfileUserId == this.ModuleContext.PortalSettings.UserId; }
        }

        protected UserInfo ProfileUser
        {
            get { return UserController.GetUserById(this.ModuleContext.PortalId, this.ProfileUserId); }
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            if (string.IsNullOrEmpty(this.Request.GetValue("UserId")) &&
                            (this.ModuleContext.PortalSettings.ActiveTab.TabID == this.ModuleContext.PortalSettings.UserTabId
                                || this.ModuleContext.PortalSettings.ActiveTab.ParentId == this.ModuleContext.PortalSettings.UserTabId))
            {
                // Clicked on breadcrumb - don't know which user.
                // The WebForms version swallowed the ThreadAbortException Response.Redirect raised;
                // on ASP.NET Core Redirect only writes the 302 and returns, so the method returns.
                var redirectUrl = this.Request.IsAuthenticated()
                    ? this.NavigationManager.NavigateURL(this.ModuleContext.PortalSettings.ActiveTab.TabID, string.Empty, "UserId=" + this.ModuleContext.PortalSettings.UserId.ToString(CultureInfo.InvariantCulture))
                    : this.GetRedirectUrl();
                this.Response.Redirect(redirectUrl, true);
                return;
            }

            base.OnInit(e);
        }

        private string GetRedirectUrl()
        {
            // redirect user to default page if not specific the home tab, do this action to prevent loop redirect.
            var homeTabId = this.ModuleContext.PortalSettings.HomeTabId;
            string redirectUrl;

            if (homeTabId > Null.NullInteger)
            {
                redirectUrl = TestableGlobals.Instance.NavigateURL(homeTabId);
            }
            else
            {
                redirectUrl = TestableGlobals.Instance.GetPortalDomainName(PortalSettings.Current.PortalAlias.HTTPAlias, this.Request, true) +
                              "/" + Globals.glbDefaultPage;
            }

            return redirectUrl;
        }
    }
}
