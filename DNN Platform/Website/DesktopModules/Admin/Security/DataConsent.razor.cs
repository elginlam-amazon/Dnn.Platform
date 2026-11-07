// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Admin.Users
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Security;
    using DotNetNuke.Services.Authentication;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    using NavigationManager = Microsoft.AspNetCore.Components.NavigationManager;

    /// <summary>A control which handles a user's consent to the site's usage of their data.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>DataConsent.ascx</c> + <c>DataConsent.ascx.cs</c> +
    /// <c>DataConsent.ascx.designer.cs</c>. The markup is in <c>DataConsent.razor</c>.
    /// </para>
    /// <para>
    /// The GDPR flow is preserved exactly:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// The consent statement is rendered from the <c>DataConsent</c> resource string, formatted with
    /// the Terms URL and the Privacy URL. Each URL keeps its <see cref="Null.NullInteger"/> sentinel
    /// test: when <c>TermsTabId</c>/<c>PrivacyTabId</c> is -1 ("not set") the URL is built from the
    /// current tab plus the <c>Terms</c>/<c>Privacy</c> control key, otherwise from the configured
    /// tab. A tab ID of 0 is a real tab and must not take the "not set" branch, so the comparison is
    /// still <c>== Null.NullInteger</c> and not <c>&lt;= 0</c>.
    /// </description></item>
    /// <item><description>
    /// The agreement checkbox starts unticked and the submit button starts disabled (the original did
    /// both on the non-postback pass).
    /// </description></item>
    /// <item><description>
    /// Ticking the checkbox enables submit; unticking it disables submit again. That was an injected
    /// <c>onclick</c> that flipped <c>disabled</c> in the browser; it is now computed on the server
    /// from <see cref="agreed"/>.
    /// </description></item>
    /// <item><description>
    /// Submit records consent only when the box is ticked - <see cref="CmdSubmitClick"/> re-tests
    /// <see cref="agreed"/> just as the original re-tested <c>chkAgree.Checked</c>, so a client-side
    /// tamper of the disabled attribute still cannot record a consent that was not given. Consent is
    /// recorded by <see cref="UserController.UserAgreedToTerms(UserInfo)"/>, which is what writes the
    /// consent timestamp, followed by setting <c>User.HasAgreedToTerms</c> on the in-memory user, in
    /// that order. Then <c>Consented</c> is raised.
    /// </description></item>
    /// <item><description>
    /// Cancel records nothing and raises <c>Cancelled</c>.
    /// </description></item>
    /// <item><description>
    /// The withdrawal ("delete me") button is shown only when
    /// <c>PortalSettings.DataConsentUserDeleteAction != Off</c>, and is guarded by the same
    /// per-action confirmation prompt (<see cref="DeleteMeConfirmString"/>). Declining the prompt
    /// aborts, as it did when the prompt was an injected <c>onclick</c>.
    /// </description></item>
    /// <item><description>
    /// Withdrawal then runs the identical three-way switch: <c>Manual</c> unapproves the membership,
    /// saves the user and flags the removal request; <c>DelayedHardDelete</c> soft-deletes the user
    /// (<c>notify: true</c>, <c>deleteAdmin: false</c>) and flags the removal request;
    /// <c>HardDelete</c> removes the user outright. The success flag is taken from the same place in
    /// each branch (always <see langword="true"/> for <c>Manual</c>, the return value of
    /// <c>DeleteUser</c>/<c>RemoveUser</c> otherwise).
    /// </description></item>
    /// <item><description>
    /// On success the user is signed out and <c>RemovedAccount</c> is raised; on failure nothing is
    /// signed out and <c>FailedToRemoveAccount</c> is raised.
    /// </description></item>
    /// </list>
    /// <para>
    /// The one forced change is how the sign-out reaches the response.
    /// <c>PortalSecurity.Instance.SignOut()</c> expires the authentication cookie through
    /// <c>HttpContextSource.CurrentOrThrow</c>, and there is no live <c>HttpContext</c> on an
    /// interactive circuit. When the control is rendered statically the call is made directly, as
    /// before. When it is rendered interactively the sign-out is completed by forcing a navigation to
    /// <see cref="LogOffEndpoint.LogOffPath"/> - the endpoint that owns the cookie sign-out - after
    /// the <c>RemovedAccount</c> event has been raised, so the event ordering the consumers rely on
    /// is unchanged and the session is still invalidated. The cookie itself is never written from
    /// here.
    /// </para>
    /// </remarks>
    public partial class DataConsent : UserModuleBase
    {
        private readonly INavigationManager navigationManager;
        private readonly IEventLogger eventLogger;

        /// <summary>Whether the agreement checkbox is ticked. Replaces <c>chkAgree.Checked</c>.</summary>
        private bool agreed;

        /// <summary>Whether the submit button is enabled. Replaces <c>cmdSubmit.Enabled</c>.</summary>
        private bool submitEnabled;

        /// <summary>Whether the (empty) no-agreement panel is rendered. Replaces <c>pnlNoAgreement.Visible</c>.</summary>
        private bool noAgreementVisible;

        /// <summary>Whether the withdrawal button is rendered. Replaces <c>cmdDeleteMe.Visible</c>.</summary>
        private bool deleteMeVisible;

        /// <summary>Initializes a new instance of the <see cref="DataConsent"/> class.</summary>
        public DataConsent()
            : this(null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DataConsent"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="eventLogger">The event logger.</param>
        public DataConsent(INavigationManager navigationManager, IEventLogger eventLogger)
            : base(null, null)
        {
            this.navigationManager = navigationManager ?? Common.Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
            this.eventLogger = eventLogger ?? Common.Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
        }

        /// <summary>A function which handles the <see cref="DataConsent.DataConsentCompleted"/> event.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        public delegate void DataConsentEventHandler(object sender, DataConsentEventArgs e);

        /// <summary>An event which is triggered when the user takes a data consent action.</summary>
        public event DataConsentEventHandler DataConsentCompleted;

        /// <summary>The options a user can take from the data consent screen.</summary>
        public enum DataConsentStatus
        {
            /// <summary>Consented.</summary>
            Consented = 0,

            /// <summary>Cancelled.</summary>
            Cancelled = 1,

            /// <summary>Removed account.</summary>
            RemovedAccount = 2,

            /// <summary>Failed to remove account.</summary>
            FailedToRemoveAccount = 3,
        }

        /// <summary>Gets the confirmation text to display when a user chooses for their account to be deleted.</summary>
        public string DeleteMeConfirmString
        {
            get
            {
                switch (this.PortalSettings.DataConsentUserDeleteAction)
                {
                    case PortalSettings.UserDeleteAction.Manual:
                        return this.LocalizeText("ManualDelete.Confirm");
                    case PortalSettings.UserDeleteAction.DelayedHardDelete:
                        return this.LocalizeText("DelayedHardDelete.Confirm");
                    case PortalSettings.UserDeleteAction.HardDelete:
                        return this.LocalizeText("HardDelete.Confirm");
                }

                return string.Empty;
            }
        }

        /// <summary>Gets or sets the JavaScript runtime used for the withdrawal confirmation prompt.</summary>
        [Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>Gets the HTML to display with the statement agreeing to the terms and privacy policies.</summary>
        /// <remarks>
        /// Was an <c>IHtmlString</c> written with <c>&lt;%: %&gt;</c>, which writes an
        /// <c>IHtmlString</c> without encoding it. <see cref="MarkupString"/> is the Blazor
        /// expression with that same (unencoded) behaviour.
        /// </remarks>
        protected MarkupString DataConsentHtml
        {
            get
            {
                var termsUrl = this.PortalSettings.TermsTabId == Null.NullInteger
                    ? this.navigationManager.NavigateURL(this.TabId, "Terms")
                    : this.navigationManager.NavigateURL(this.PortalSettings.TermsTabId);
                var privacyUrl = this.PortalSettings.PrivacyTabId == Null.NullInteger
                    ? this.navigationManager.NavigateURL(this.TabId, "Privacy")
                    : this.navigationManager.NavigateURL(this.PortalSettings.PrivacyTabId);
                var dataConsentHtml = string.Format(
                    CultureInfo.CurrentCulture,
                    this.LocalizeText("DataConsent"),
                    termsUrl,
                    privacyUrl);
                return new MarkupString(dataConsentHtml);
            }
        }

        /// <summary>Gets or sets the navigation manager used to leave the page once the account is gone.</summary>
        [Inject]
        private NavigationManager Navigation { get; set; }

        /// <summary>Called when any data consent action is completed.</summary>
        /// <param name="e">The details of the action.</param>
        public void OnDataConsentComplete(DataConsentEventArgs e)
        {
            this.DataConsentCompleted?.Invoke(this, e);
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.deleteMeVisible = this.PortalSettings.DataConsentUserDeleteAction != PortalSettings.UserDeleteAction.Off;

            // The original did this inside `if (!this.Page.IsPostBack)`. A Blazor component instance
            // lives for the life of the circuit, so OnLoad runs once per instance and the reachable
            // (non-postback) path is the only one left.
            this.agreed = false;
            this.submitEnabled = false;
            this.noAgreementVisible = false;
        }

        /// <summary>Builds a stable DOM id for a control that used to get one from the WebForms naming container.</summary>
        /// <param name="controlName">The original control ID.</param>
        /// <returns>The element id.</returns>
        private string ClientIdFor(string controlName)
            => string.Format(CultureInfo.InvariantCulture, "dnn_ctr{0}_DataConsent_{1}", this.ModuleId, controlName);

        /// <summary>Replaces the injected <c>onclick</c> that enabled/disabled the submit button.</summary>
        /// <param name="args">The change event arguments.</param>
        private void ChkAgreeChanged(ChangeEventArgs args)
        {
            this.agreed = args?.Value is bool boolValue ? boolValue : false;
            this.submitEnabled = this.agreed;
        }

        private void CmdCancelClick()
        {
            this.OnDataConsentComplete(new DataConsentEventArgs(DataConsentStatus.Cancelled));
        }

        private void CmdSubmitClick()
        {
            if (this.agreed)
            {
                UserController.UserAgreedToTerms(this.User);
                this.User.HasAgreedToTerms = true;
                this.OnDataConsentComplete(new DataConsentEventArgs(DataConsentStatus.Consented));
            }
        }

        private async Task CmdDeleteMeClickAsync()
        {
            // Was an injected `onclick` that called confirm() and cancelled the postback when the
            // user declined. Declining still aborts before anything is deleted.
            var confirmed = await this.JsRuntime.InvokeAsync<bool>("confirm", this.DeleteMeConfirmString);
            if (!confirmed)
            {
                return;
            }

            var success = false;
            switch (this.PortalSettings.DataConsentUserDeleteAction)
            {
                case PortalSettings.UserDeleteAction.Manual:
                    this.User.Membership.Approved = false;
                    UserController.UpdateUser(this.eventLogger, this.PortalSettings.PortalId, this.User);
                    UserController.UserRequestsRemoval(this.User, true);
                    success = true;
                    break;
                case PortalSettings.UserDeleteAction.DelayedHardDelete:
                    var user = this.User;
                    success = UserController.DeleteUser(this.eventLogger, ref user, true, false);
                    UserController.UserRequestsRemoval(this.User, true);
                    break;
                case PortalSettings.UserDeleteAction.HardDelete:
                    success = UserController.RemoveUser(this.User);
                    break;
            }

            if (success)
            {
                // PortalSecurity.SignOut() expires the authentication cookie on the live request. On
                // a static render there is one, so sign out exactly as before. On an interactive
                // circuit there is not, so the sign-out is delegated to the logoff endpoint below -
                // after the event, to keep the original ordering.
                var signOutDeferred = false;
                if (HttpContextSource.Current is not null)
                {
                    await PortalSecurity.Instance.SignOutAsync();
                }
                else
                {
                    signOutDeferred = true;
                }

                this.OnDataConsentComplete(new DataConsentEventArgs(DataConsentStatus.RemovedAccount));

                if (signOutDeferred)
                {
                    this.Navigation.NavigateTo(LogOffEndpoint.LogOffPath, forceLoad: true);
                }
            }
            else
            {
                this.OnDataConsentComplete(new DataConsentEventArgs(DataConsentStatus.FailedToRemoveAccount));
            }
        }

        /// <summary>The DataConsentEventArgs class provides a customised EventArgs class for the <see cref="DataConsent.DataConsentCompleted"/> Event.</summary>
        public class DataConsentEventArgs
        {
            /// <summary>Initializes a new instance of the <see cref="DataConsentEventArgs"/> class.</summary>
            /// <param name="status">The Data Consent Status.</param>
            public DataConsentEventArgs(DataConsentStatus status)
            {
                this.Status = status;
            }

            /// <summary>Gets or sets the Update Status.</summary>
            public DataConsentStatus Status { get; set; }
        }
    }
}
