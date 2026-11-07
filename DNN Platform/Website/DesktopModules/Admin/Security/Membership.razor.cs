// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Admin.Users
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Data;
    using DotNetNuke.Entities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.FileSystem;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Mail;
    using DotNetNuke.UI.Skins.Controls;
    using DotNetNuke.Web.UI.WebControls;

    using Microsoft.Extensions.DependencyInjection;

    using NavigationManager = Microsoft.AspNetCore.Components.NavigationManager;

    /// <summary>The Membership UserModuleBase is used to manage the membership aspects of a User.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>Membership.ascx</c> + <c>Membership.ascx.cs</c> +
    /// <c>Membership.ascx.designer.cs</c>; the markup is in <c>Membership.razor</c>.
    /// </para>
    /// <para>
    /// <b>The membership state transitions are unchanged.</b> Every one of them is guarded by the
    /// same two gates, in the same order, before anything is written:
    /// <c>if (!this.IsUserOrAdmin) return;</c> then
    /// <c>if (!this.Request.IsAuthenticated()) return;</c> - the latter was
    /// <c>Request.IsAuthenticated</c>, a WebForms property that is an extension method on ASP.NET
    /// Core's <c>HttpRequest</c>; the test it performs is the same.
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Approve</b> (<c>cmdAuthorize</c>): sets <c>Membership.Approved = true</c> on the membership
    /// taken from the form's data source, saves the user, and <i>then</i> - only when the user is not
    /// a superuser, <i>is</i> in "Unverified Users" and the portal's registration mode is
    /// <c>VerifiedRegistration</c> - also runs <c>UserController.ApproveUser</c> to fix up the roles.
    /// Finally the <c>UserAuthorized</c> mail is sent and <c>MembershipAuthorized</c> is raised.
    /// </description></item>
    /// <item><description>
    /// <b>Unapprove</b> (<c>cmdUnAuthorize</c>): sets <c>Membership.Approved = false</c>, saves the
    /// user and raises <c>MembershipUnAuthorized</c>. It deliberately does <i>not</i> touch roles.
    /// </description></item>
    /// <item><description>
    /// <b>Unlock</b> (<c>cmdUnLock</c>): calls <c>UserController.UnLockUser</c> first and only clears
    /// the in-memory <c>LockedOut</c> flag and raises <c>MembershipUnLocked</c> when that returned
    /// <see langword="true"/>. A failed unlock therefore still leaves the account locked.
    /// </description></item>
    /// <item><description>
    /// <b>Reset-token / force password change</b> (<c>cmdPassword</c>): sends the
    /// <c>PasswordReminder</c> mail <i>first</i> and only sets <c>Membership.UpdatePassword = true</c>
    /// and saves the user when the mail went out (<c>SendMail(...) == string.Empty</c>). If it did
    /// not, nothing is written and the user-enumeration-safe <c>OptionUnavailable</c> warning is
    /// shown - the same wording as before, which says nothing about the account.
    /// </description></item>
    /// <item><description>
    /// <b>Promote/demote superuser</b> (<c>cmdToggleSuperuser</c>): on top of the two common gates it
    /// re-checks <c>UserController.Instance.GetCurrentUserInfo().IsSuperUser</c> on the server, so
    /// only a superuser can change the flag even if the button were forced to render. It flips
    /// <c>IsSuperUser</c>, saves, clears the whole cache, and raises demote or promote according to
    /// the <i>previous</i> state.
    /// </description></item>
    /// </list>
    /// <para>
    /// The button visibility rules in <see cref="DataBind"/> are likewise untouched: a user may never
    /// approve/unapprove/unlock/force-reset <i>themselves</i> (all four hidden when
    /// <c>UserInfo.UserID == User.UserID</c>); unlock appears only when locked out; unapprove only
    /// when approved and not in "Unverified Users"; approve when not approved <i>or</i> in
    /// "Unverified Users"; force-reset only when a password update is not already pending; and the
    /// superuser toggle only for a superuser looking at somebody else who belongs to at least one
    /// portal.
    /// </para>
    /// <para>
    /// Sentinel semantics kept verbatim: <c>LastLockoutDate.Year &gt; 2000</c> is DNN's "has ever
    /// been locked out" test (an unset lockout date is <c>SqlDateTime.MinValue</c>, year 1753), and it
    /// is still spelled that way rather than compared against <c>default</c> or <c>Null.NullDate</c>.
    /// The user-folder path still drops exactly its first 6 characters (the <c>Users/</c> prefix).
    /// </para>
    /// <para>
    /// Mechanical changes: <c>Response.Redirect(url, true)</c> becomes
    /// <c>NavigationManager.NavigateTo(url, forceLoad: true)</c>; the ten literal form items are now
    /// built in <see cref="BuildFormItems"/> because <c>DnnFormEditor.Items</c> is populated
    /// imperatively; and <c>membershipForm.DataSource</c> is still the single object the click
    /// handlers read the membership back out of.
    /// </para>
    /// </remarks>
    public partial class Membership : UserModuleBase
    {
        private readonly INavigationManager navigationManager;
        private readonly DataProvider dataProvider;
        private readonly RoleProvider roleProvider;
        private readonly IRoleController roleController;
        private readonly IEventManager eventManager;
        private readonly IPortalController portalController;
        private readonly IUserController userController;
        private readonly IEventLogger eventLogger;
        private readonly IApplicationStatusInfo appStatus;

        private DnnFormEditor membershipForm;

        /// <summary>The literal items that used to be declared in the .ascx.</summary>
        private DnnFormLiteralItem createdDate;
        private DnnFormLiteralItem lastLoginDate;
        private DnnFormLiteralItem lastActivityDate;
        private DnnFormLiteralItem lastPasswordChangeDate;
        private DnnFormLiteralItem lastLockoutDate;
        private DnnFormLiteralItem lockedOut;
        private DnnFormLiteralItem approved;
        private DnnFormLiteralItem updatePassword;
        private DnnFormLiteralItem isDeleted;
        private DnnFormLiteralItem userFolder;

        /// <summary>The membership the form is bound to; mirrors <c>membershipForm.DataSource</c>.</summary>
        private UserMembership membershipDataSource;

        /// <summary>Set when <see cref="DataBind"/> ran before the editor existed (see <see cref="OnAfterRender"/>).</summary>
        private bool bindPending;

        // Replace the designer's LinkButton.Visible flags.
        private bool authorizeVisible = true;
        private bool unAuthorizeVisible = true;
        private bool unLockVisible = true;
        private bool passwordVisible = true;
        private bool toggleSuperuserVisible;
        private string toggleSuperuserText;

        /// <summary>Initializes a new instance of the <see cref="Membership"/> class.</summary>
        public Membership()
            : this(null, null, null, null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Membership"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="dataProvider">The data provider.</param>
        /// <param name="roleProvider">The role provider.</param>
        /// <param name="roleController">The role controller.</param>
        /// <param name="eventManager">The event manager.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="userController">The user controller.</param>
        /// <param name="eventLogger">The event logger.</param>
        public Membership(INavigationManager navigationManager, DataProvider dataProvider, RoleProvider roleProvider, IRoleController roleController, IEventManager eventManager, IPortalController portalController, IUserController userController, IEventLogger eventLogger)
            : base(null, null)
        {
            this.navigationManager = navigationManager ?? this.DependencyProvider.GetRequiredService<INavigationManager>();
            this.dataProvider = dataProvider ?? this.DependencyProvider.GetRequiredService<DataProvider>();
            this.roleProvider = roleProvider ?? this.DependencyProvider.GetRequiredService<RoleProvider>();
            this.roleController = roleController ?? this.DependencyProvider.GetRequiredService<IRoleController>();
            this.eventManager = eventManager ?? this.DependencyProvider.GetRequiredService<IEventManager>();
            this.portalController = portalController ?? this.DependencyProvider.GetRequiredService<IPortalController>();
            this.userController = userController ?? this.DependencyProvider.GetRequiredService<IUserController>();
            this.eventLogger = eventLogger ?? this.DependencyProvider.GetRequiredService<IEventLogger>();
            this.appStatus = this.DependencyProvider.GetRequiredService<IApplicationStatusInfo>();
        }

        /// <summary>Raises the MembershipAuthorized Event.</summary>
        public event EventHandler MembershipAuthorized;

        /// <summary>Raises the MembershipPasswordUpdateChanged Event.</summary>
        public event EventHandler MembershipPasswordUpdateChanged;

        /// <summary>Raises the MembershipUnAuthorized Event.</summary>
        public event EventHandler MembershipUnAuthorized;

        /// <summary>Raises the MembershipUnLocked Event.</summary>
        public event EventHandler MembershipUnLocked;

        /// <summary>Raises the MembershipPromoteToSuperuser Event.</summary>
        public event EventHandler MembershipPromoteToSuperuser;

        /// <summary>Raises the MembershipDemoteFromSuperuser Event.</summary>
        public event EventHandler MembershipDemoteFromSuperuser;

        /// <summary>Gets the UserMembership associated with this control.</summary>
        public UserMembership UserMembership
        {
            get
            {
                UserMembership membership = null;
                if (this.User != null)
                {
                    membership = this.User.Membership;
                }

                return membership;
            }
        }

        /// <summary>Gets or sets the navigation manager used in place of <c>Response.Redirect</c>.</summary>
        [Microsoft.AspNetCore.Components.Inject]
        protected NavigationManager Navigation { get; set; }

        /// <summary>Raises the MembershipPromoteToSuperuser Event.</summary>
        /// <param name="e">The event arguments.</param>
        public void OnMembershipPromoteToSuperuser(EventArgs e)
        {
            if (!this.IsUserOrAdmin)
            {
                return;
            }

            if (this.MembershipPromoteToSuperuser != null)
            {
                this.MembershipPromoteToSuperuser(this, e);
                this.Navigation.NavigateTo(this.navigationManager.NavigateURL(), forceLoad: true);
            }
        }

        /// <summary>Raises the MembershipDemoteFromSuperuser Event.</summary>
        /// <param name="e">The event arguments.</param>
        public void OnMembershipDemoteFromSuperuser(EventArgs e)
        {
            if (!this.IsUserOrAdmin)
            {
                return;
            }

            if (this.MembershipDemoteFromSuperuser != null)
            {
                this.MembershipDemoteFromSuperuser(this, e);
                this.Navigation.NavigateTo(this.navigationManager.NavigateURL(), forceLoad: true);
            }
        }

        /// <summary>Raises the MembershipAuthorized Event.</summary>
        /// <param name="e">The event arguments.</param>
        public void OnMembershipAuthorized(EventArgs e)
        {
            if (this.IsUserOrAdmin)
            {
                this.MembershipAuthorized?.Invoke(this, e);
            }
        }

        /// <summary>Raises the MembershipPasswordUpdateChanged Event.</summary>
        /// <param name="e">The event arguments.</param>
        public void OnMembershipPasswordUpdateChanged(EventArgs e)
        {
            if (this.IsUserOrAdmin)
            {
                this.MembershipPasswordUpdateChanged?.Invoke(this, e);
            }
        }

        /// <summary>Raises the MembershipUnAuthorized Event.</summary>
        /// <param name="e">The event arguments.</param>
        public void OnMembershipUnAuthorized(EventArgs e)
        {
            if (this.IsUserOrAdmin)
            {
                this.MembershipUnAuthorized?.Invoke(this, e);
            }
        }

        /// <summary>Raises the MembershipUnLocked Event.</summary>
        /// <param name="e">The event arguments.</param>
        public void OnMembershipUnLocked(EventArgs e)
        {
            if (this.IsUserOrAdmin)
            {
                this.MembershipUnLocked?.Invoke(this, e);
            }
        }

        /// <summary>DataBind binds the data to the controls.</summary>
        public void DataBind()
        {
            this.BuildFormItems();

            // disable/enable buttons
            if (this.UserInfo.UserID == this.User.UserID)
            {
                this.authorizeVisible = false;
                this.unAuthorizeVisible = false;
                this.unLockVisible = false;
                this.passwordVisible = false;
            }
            else
            {
                this.unLockVisible = this.UserMembership.LockedOut;
                this.unAuthorizeVisible = this.UserMembership.Approved && !this.User.IsInRole("Unverified Users");
                this.authorizeVisible = !this.UserMembership.Approved || this.User.IsInRole("Unverified Users");
                this.passwordVisible = !this.UserMembership.UpdatePassword;
            }

            if (UserController.Instance.GetCurrentUserInfo().IsSuperUser && UserController.Instance.GetCurrentUserInfo().UserID != this.User.UserID)
            {
                this.toggleSuperuserVisible = true;

                if (this.User.IsSuperUser)
                {
                    this.toggleSuperuserText = Localization.GetString("DemoteFromSuperUser", this.LocalResourceFile);
                }
                else
                {
                    this.toggleSuperuserText = Localization.GetString("PromoteToSuperUser", this.LocalResourceFile);
                }

                if (PortalController.GetPortalsByUser(this.dataProvider, this.User.UserID).Count == 0)
                {
                    this.toggleSuperuserVisible = false;
                }
            }

            // An unset lockout date is SqlDateTime.MinValue (year 1753); "> 2000" is DNN's
            // "has ever been locked out" sentinel test and is kept exactly as it was.
            this.lastLockoutDate.Value = this.UserMembership.LastLockoutDate.Year > 2000
                                        ? this.UserMembership.LastLockoutDate
                                        : this.LocalizeText("Never");

            // ReSharper disable SpecifyACultureInStringConversionExplicitly
            this.lockedOut.Value = this.LocalizeText(this.UserMembership.LockedOut.ToString());
            this.approved.Value = this.LocalizeText(this.UserMembership.Approved.ToString());
            this.updatePassword.Value = this.LocalizeText(this.UserMembership.UpdatePassword.ToString());
            this.isDeleted.Value = this.LocalizeText(this.UserMembership.IsDeleted.ToString());

            // show the user folder path without default parent folder, and only visible to admin.
            // BL0005: userFolder is a DnnFormLiteralItem MODEL this class constructs in
            // BuildFormItems() and renders through the DnnFormEditor's Items collection
            // (SyncFormItems) - the only @ref capture on this page is membershipForm - so these are
            // owner-writes. The administrator-role gate below is unchanged.
#pragma warning disable BL0005
            this.userFolder.Visible = this.UserInfo.IsInRole(this.PortalSettings.AdministratorRoleName);
#pragma warning restore BL0005
            if (this.userFolder.Visible)
            {
                this.userFolder.Value = FolderManager.Instance.GetUserFolder(this.User).FolderPath.Substring(6);
            }

            // ReSharper restore SpecifyACultureInStringConversionExplicitly
            this.membershipDataSource = this.UserMembership;

            if (this.membershipForm is null)
            {
                // The @ref is not assigned until after the first render; finish in OnAfterRender.
                this.bindPending = true;
            }
            else
            {
                this.SyncFormItems();
                this.membershipForm.DataSource = this.UserMembership;
                this.membershipForm.DataBind();
            }

            this.StateHasChanged();
        }

        /// <summary>Page_Load runs when the control is loaded.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // The four Click wire-ups are now @onclick bindings in Membership.razor; OnLoad has
            // nothing else left to do beyond the base class.
        }

        /// <inheritdoc />
        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);

            if (this.bindPending && this.membershipForm is not null)
            {
                this.bindPending = false;
                this.SyncFormItems();
                this.membershipForm.DataSource = this.membershipDataSource;
                this.membershipForm.DataBind();
                this.StateHasChanged();
            }
        }

        /// <summary>Creates the literal items that used to be declared in <c>Membership.ascx</c>.</summary>
        /// <remarks>
        /// <c>DnnFormEditor.Items</c> has a private setter and the editor builds its children from it
        /// imperatively, so the declarative <c>&lt;Items&gt;</c> block has to become code. IDs,
        /// <c>DataField</c>s, <c>ResourceKey</c>s and order are identical to the markup they replace.
        /// </remarks>
        private void BuildFormItems()
        {
            if (this.createdDate is not null)
            {
                return;
            }

            this.createdDate = this.NewLiteralItem("createdDate", dataField: "CreatedDate", resourceKey: null);
            this.lastLoginDate = this.NewLiteralItem("lastLoginDate", dataField: "LastLoginDate", resourceKey: null);
            this.lastActivityDate = this.NewLiteralItem("lastActivityDate", dataField: "LastActivityDate", resourceKey: null);
            this.lastPasswordChangeDate = this.NewLiteralItem("lastPasswordChangeDate", dataField: "LastPasswordChangeDate", resourceKey: null);
            this.lastLockoutDate = this.NewLiteralItem("lastLockoutDate", dataField: null, resourceKey: "LastLockoutDate");
            this.lockedOut = this.NewLiteralItem("lockedOut", dataField: null, resourceKey: "LockedOut");
            this.approved = this.NewLiteralItem("approved", dataField: null, resourceKey: "Approved");
            this.updatePassword = this.NewLiteralItem("updatePassword", dataField: null, resourceKey: "UpdatePassword");
            this.isDeleted = this.NewLiteralItem("isDeleted", dataField: null, resourceKey: "IsDeleted");
            this.userFolder = this.NewLiteralItem("userFolder", dataField: null, resourceKey: "UserFolder");
        }

        // BL0005: this factory CONSTRUCTS the DnnFormLiteralItem models this class owns and renders
        // through the DnnFormEditor's Items collection (SyncFormItems), so the object-initializer
        // writes are owner-writes rather than an outsider mutating a child component's parameters.
#pragma warning disable BL0005
        private DnnFormLiteralItem NewLiteralItem(string id, string dataField, string resourceKey)
            => new DnnFormLiteralItem(this.appStatus, this.eventLogger)
            {
                ID = id,
                DataField = dataField,
                ResourceKey = resourceKey,
                LocalResourceFile = this.LocalResourceFile,
            };
#pragma warning restore BL0005

        /// <summary>Pushes the item list into the editor, in the declared order.</summary>
        private void SyncFormItems()
        {
            var items = new List<DnnFormItemBase>
            {
                this.createdDate,
                this.lastLoginDate,
                this.lastActivityDate,
                this.lastPasswordChangeDate,
                this.lastLockoutDate,
                this.lockedOut,
                this.approved,
                this.updatePassword,
                this.isDeleted,
                this.userFolder,
            };

            if (this.membershipForm.Items.Count == items.Count)
            {
                return;
            }

            this.membershipForm.Items.Clear();
            this.membershipForm.Items.AddRange(items);
        }

        /// <summary>Reads the membership back out of the form, as the original handlers did.</summary>
        private UserMembership FormMembership()
            => (UserMembership)(this.membershipForm?.DataSource ?? this.membershipDataSource);

        /// <summary>cmdAuthorize_Click runs when the Authorize User Button is clicked.</summary>
        private void CmdAuthorizeClick()
        {
            if (!this.IsUserOrAdmin)
            {
                return;
            }

            if (!this.Request.IsAuthenticated())
            {
                return;
            }

            // Get the Membership Information from the property editors
            this.User.Membership = this.FormMembership();

            this.User.Membership.Approved = true;

            // Update User
            UserController.UpdateUser(this.eventLogger, this.PortalId, this.User);

            // Update User Roles if needed
            if (!this.User.IsSuperUser && this.User.IsInRole("Unverified Users") && this.PortalSettings.UserRegistration == (int)Common.Globals.PortalRegistrationType.VerifiedRegistration)
            {
                UserController.ApproveUser(this.roleProvider, this.roleController, this.eventManager, this.portalController, this.userController, this.eventLogger, this.PortalSettings, this.User);
            }

            Mail.SendMail(this.User, MessageType.UserAuthorized, this.PortalSettings);

            this.OnMembershipAuthorized(EventArgs.Empty);
        }

        /// <summary>cmdPassword_Click runs when the ChangePassword Button is clicked.</summary>
        private void CmdPasswordClick()
        {
            if (!this.IsUserOrAdmin)
            {
                return;
            }

            if (!this.Request.IsAuthenticated())
            {
                return;
            }

            bool canSend = Mail.SendMail(this.User, MessageType.PasswordReminder, this.PortalSettings) == string.Empty;
            if (canSend)
            {
                // Get the Membership Information from the property editors
                this.User.Membership = this.FormMembership();

                this.User.Membership.UpdatePassword = true;

                // Update User
                UserController.UpdateUser(this.eventLogger, this.PortalId, this.User);

                this.OnMembershipPasswordUpdateChanged(EventArgs.Empty);
            }
            else
            {
                var message = Localization.GetString("OptionUnavailable", this.LocalResourceFile);
                UI.Skins.Skin.AddModuleMessage(this, message, ModuleMessage.ModuleMessageType.YellowWarning);
            }
        }

        /// <summary>cmdUnAuthorize_Click runs when the UnAuthorize User Button is clicked.</summary>
        private void CmdUnAuthorizeClick()
        {
            if (!this.IsUserOrAdmin)
            {
                return;
            }

            if (!this.Request.IsAuthenticated())
            {
                return;
            }

            // Get the Membership Information from the property editors
            this.User.Membership = this.FormMembership();

            this.User.Membership.Approved = false;

            // Update User
            UserController.UpdateUser(this.eventLogger, this.PortalId, this.User);

            this.OnMembershipUnAuthorized(EventArgs.Empty);
        }

        /// <summary>cmdToggleSuperuser_Click runs when the toggle superuser button is clicked.</summary>
        private void CmdToggleSuperuserClick()
        {
            if (!this.IsUserOrAdmin)
            {
                return;
            }

            if (!this.Request.IsAuthenticated())
            {
                return;
            }

            // ensure only superusers can change user superuser state
            if (!UserController.Instance.GetCurrentUserInfo().IsSuperUser)
            {
                return;
            }

            var currentSuperUserState = this.User.IsSuperUser;
            this.User.IsSuperUser = !currentSuperUserState;

            // Update User
            UserController.UpdateUser(this.eventLogger, this.PortalId, this.User);
            DataCache.ClearCache();

            if (currentSuperUserState)
            {
                this.OnMembershipDemoteFromSuperuser(EventArgs.Empty);
            }
            else
            {
                this.OnMembershipPromoteToSuperuser(EventArgs.Empty);
            }
        }

        /// <summary>cmdUnlock_Click runs when the Unlock Account Button is clicked.</summary>
        private void CmdUnLockClick()
        {
            if (!this.IsUserOrAdmin)
            {
                return;
            }

            if (!this.Request.IsAuthenticated())
            {
                return;
            }

            // update the user record in the database
            bool isUnLocked = UserController.UnLockUser(this.eventLogger, this.User);
            if (isUnLocked)
            {
                this.User.Membership.LockedOut = false;

                this.OnMembershipUnLocked(EventArgs.Empty);
            }
        }
    }
}
