// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Admin.Users
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Lists;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Data;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Entities.Users.Membership;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Security.Membership;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Log.EventLog;
    using DotNetNuke.Services.Mail;
    using DotNetNuke.UI.Skins.Controls;
    using DotNetNuke.UI.WebControls;
    using DotNetNuke.Web.Client;
    using DotNetNuke.Web.UI.WebControls;
    using DotNetNuke.Website;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.JSInterop;

    /// <summary>The Password UserModuleBase is used to manage Users Passwords.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>Password.ascx</c> + <c>Password.ascx.cs</c> + <c>Password.ascx.designer.cs</c>; the
    /// markup is in <c>Password.razor</c>.
    /// </para>
    /// <para>
    /// <b>EVERY VALIDATION BRANCH IS A SECURITY CONTROL AND IS PORTED IN THE SAME ORDER WITH THE
    /// SAME SHORT-CIRCUIT BEHAVIOUR.</b> <see cref="CmdUpdateClick"/> applies, in this exact
    /// sequence, each rule returning immediately so that no later rule can mask an earlier failure
    /// and nothing is written until all of them have passed:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <b>Captcha gate.</b> <c>(UseCaptcha &amp;&amp; ctlCaptcha.IsValid) || !UseCaptcha</c> - when
    /// the portal setting <c>Security_CaptchaChangePassword</c> is on, the entire handler body is
    /// skipped unless the captcha validated. The captcha is validated server side by
    /// <see cref="CaptchaControl"/> against its cache entry, so this cannot be satisfied from the
    /// client.
    /// </description></item>
    /// <item><description>
    /// <b>Authorization.</b> <c>if (!this.IsUserOrAdmin) return;</c> - only the account owner or an
    /// administrator/superuser may proceed. Nothing weakens or widens this.
    /// </description></item>
    /// <item><description>
    /// <b>Confirmation match.</b> new != confirm → <c>PasswordMismatch</c>, return.
    /// </description></item>
    /// <item><description>
    /// <b>Strength/complexity.</b> <c>!UserController.ValidatePassword(new)</c> →
    /// <c>PasswordInvalid</c>, return. This is the single seam that enforces minimum length,
    /// minimum non-alphanumeric characters and the password regular expression; it reads them from
    /// <see cref="MembershipProviderConfig"/> (<c>MinPasswordLength</c>,
    /// <c>PasswordStrengthRegularExpression</c>, <c>MinNonAlphanumericCharacters</c>) - the same
    /// replacement for the removed <c>System.Web.Security.Membership</c> static that
    /// <c>InstallWizard.ValidatePassword</c> uses - so the rule set is identical on both surfaces.
    /// No length, complexity or regular-expression rule is relaxed here.
    /// </description></item>
    /// <item><description>
    /// <b>Old password present.</b> <c>!IsAdmin &amp;&amp; old is empty</c> →
    /// <c>PasswordMissing</c>, return. A non-administrator must supply the current password; an
    /// administrator resetting somebody else's password is not asked for it (and the
    /// <c>oldPasswordRow</c> is not even rendered for them), which is the original rule.
    /// </description></item>
    /// <item><description>
    /// <b>Must differ.</b> <c>!IsAdmin &amp;&amp; new == old</c> → <c>PasswordNotDifferent</c>,
    /// return.
    /// </description></item>
    /// <item><description>
    /// <b>Banned list / username reuse.</b> When <c>MembershipPasswordSettings.EnableBannedList</c>
    /// is on: <c>FoundBannedPassword(new) || User.Username == new</c> →
    /// <c>BannedPasswordUsed</c>, return. Both halves are kept, including the username comparison,
    /// and both are kept <i>inside</i> the <c>EnableBannedList</c> gate exactly as before.
    /// </description></item>
    /// <item><description>
    /// <b>History reuse.</b> <c>IsPasswordInHistory(UserID, PortalID, new, false)</c> →
    /// <c>PasswordResetFailed</c>, return. The final <c>false</c> argument (do not record the
    /// password as used) is preserved - flipping it would silently poison the history.
    /// </description></item>
    /// <item><description>
    /// <b>Must differ (repeated).</b> The original repeats <c>!IsAdmin &amp;&amp; new == old</c> →
    /// <c>PasswordNotDifferent</c> after the history check. It is kept: a rule that can no longer
    /// fail is a vulnerability, and removing a duplicated guard is exactly the kind of "tidying"
    /// that changes behaviour when an earlier branch is ever modified.
    /// </description></item>
    /// <item><description>
    /// <b>Apply.</b> Only now is the password written, and through two different paths that are not
    /// interchangeable: a non-administrator goes through
    /// <c>UserController.ChangePassword(eventLogger, User, old, new)</c>, which re-verifies the old
    /// password inside the membership provider; an administrator goes through
    /// <c>UserController.ResetAndChangePassword(User, new)</c>. <c>MembershipPasswordException</c>
    /// still maps to <c>InvalidPasswordAnswer</c> and any other exception to
    /// <c>PasswordResetFailed</c>, separately for each path, so the two are still logged
    /// distinctly.
    /// </description></item>
    /// </list>
    /// <para>
    /// <see cref="CmdResetClick"/> (administrator / Q&amp;A reset) keeps its own order:
    /// <c>!IsUserOrAdmin</c> → return; then, when the provider requires a question and answer and
    /// the caller is <i>not</i> an administrator, an empty answer → <c>InvalidPasswordAnswer</c>,
    /// return; then a reset token is created with the host's
    /// <c>AdminMembershipResetLinkValidity</c> expressed in whole minutes (the token expiry window
    /// is unchanged); then the <c>PasswordReminder</c> mail is sent and the outcome is reported with
    /// <c>PasswordSent</c> / <c>OptionUnavailable</c> and logged as
    /// <c>PASSWORD_SENT_SUCCESS</c>/<c>PASSWORD_SENT_FAILURE</c>. <c>ArgumentException</c> still maps
    /// to <c>InvalidPasswordAnswer</c> and anything else to <c>PasswordResetFailed</c>.
    /// </para>
    /// <para>
    /// <see cref="CmdUserResetClick"/> (self-service reset) is unchanged too, including
    /// <c>UserController.ResetPasswordToken(User, true)</c> - the overload that issues a <i>fresh</i>
    /// token - and the identical <c>PasswordSent</c>/<c>OptionUnavailable</c> messaging. That wording
    /// is deliberately not made more specific: it reveals nothing about whether the account exists or
    /// what went wrong.
    /// </para>
    /// <para>
    /// <see cref="CmdUpdateQAClick"/> keeps all four of its guards in order:
    /// <c>!IsUserOrAdmin</c> → return; empty password → <c>PasswordInvalid</c>; empty question →
    /// <c>InvalidPasswordQuestion</c>; empty answer → <c>InvalidPasswordAnswer</c>. Only then is
    /// <c>ChangePasswordQuestionAndAnswer</c> called, which re-verifies the supplied password before
    /// replacing the question and answer.
    /// </para>
    /// <para>
    /// The <see cref="DataBind"/> visibility matrix is also a set of rules rather than styling and is
    /// reproduced branch for branch: whether the change panel, the reset panel, the old-password row,
    /// the question/answer rows and the Q&amp;A panel appear depends on
    /// <c>MembershipProviderConfig.PasswordRetrievalEnabled</c>,
    /// <c>PasswordResetEnabled</c> and <c>RequiresQuestionAndAnswer</c> combined with
    /// <c>IsAdmin</c>/<c>IsUser</c> and <c>Request.IsAuthenticated()</c>, exactly as before. The
    /// expiry label keeps the <c>PasswordConfig.PasswordExpiry &gt; 0</c> test (0 means "never
    /// expires", so it must stay a <c>&gt; 0</c> test and not a null check) and the <c>ForcedExpiry</c>
    /// override when <c>Membership.UpdatePassword</c> is set. <see cref="OnLoad"/> keeps the rule that
    /// a user whose provider requires a question and answer cannot have their password changed by
    /// somebody else at all - the change panel and its button are removed and
    /// <c>CannotChangePassword</c> is shown.
    /// </para>
    /// <para>
    /// Mechanical changes only: <c>Request.IsAuthenticated</c> → the <c>IsAuthenticated()</c>
    /// extension on ASP.NET Core's <c>HttpRequest</c>; the <c>ScriptManager</c> /
    /// <c>Page.ClientScript.RegisterStartupScript</c> pair that bootstrapped the strength meter and
    /// the password comparer → <see cref="IJSRuntime"/> calls from
    /// <see cref="OnAfterRenderAsync(bool)"/> (no <c>&lt;script&gt;</c> may appear in a .razor file,
    /// and the options objects are still serialized by <c>Json.Serialize</c> so that their
    /// <c>[DataContract]</c> names and the <c>[OnSerializing]</c> hook that fills in the portal's
    /// password rules still apply); the <c>ThreadAbortException</c> catch blocks are dropped because
    /// that exception is never thrown on ASP.NET Core (it only existed to swallow
    /// <c>Response.Redirect</c> aborts) - every other catch is kept; and
    /// <c>Localization.GetString</c> is kept throughout rather than <c>IStringLocalizer</c>, because
    /// the password rule and help strings are routinely customised per portal in the DNN Language
    /// Editor.
    /// </para>
    /// </remarks>
    public partial class Password : UserModuleBase
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<Password>();

        private readonly IEventLogger eventLogger;
        private readonly IJavaScriptLibraryHelper javaScript;
        private readonly IClientResourceController clientResourceController;
        private readonly DataProvider dataProvider;

        /// <summary>A stable DOM id for the container the comparer script selects the first box in.</summary>
        private readonly string passwordContainerClientId = "passwordContainer_" + Guid.NewGuid().ToString("N");

        private CaptchaControl ctlCaptcha;

        // Replace the designer's TextBox.Text values.
        private string oldPassword = string.Empty;
        private string newPassword = string.Empty;
        private string newConfirm = string.Empty;
        private string answer = string.Empty;
        private string qaPassword = string.Empty;
        private string editQuestion = string.Empty;
        private string editAnswer = string.Empty;

        // Replace the designer's Label.Text values.
        private string changeHelpText = string.Empty;
        private string resetHelpText = string.Empty;
        private string questionText = string.Empty;
        private string lastChangedText = string.Empty;
        private string expiresText = string.Empty;
        private string captchaErrorMessage;
        private string captchaText;

        // Replace the designer's Visible flags. The initial values are the ones declared in the
        // .ascx: the two panels and the three action buttons were visible, CannotChangePasswordMessage
        // / captchaRow / cmdUserReset were not.
        private bool cannotChangePasswordMessageVisible;
        private bool pnlChangeVisible = true;
        private bool oldPasswordRowVisible = true;
        private bool captchaRowVisible;
        private bool cmdUpdateVisible = true;
        private bool pnlResetVisible = true;
        private bool questionRowVisible = true;
        private bool answerRowVisible = true;
        private bool pnlQAVisible = true;
        private bool cmdResetVisible = true;
        private bool cmdUserResetVisible;
        private bool cmdUpdateQAVisible = true;

        // Replace the CssClass assignments the strength meter made in OnPreRender.
        private string passwordContainerCssClass = string.Empty;
        private string newPasswordCssClass = string.Empty;

        private bool strengthMeterEnabled;

        /// <summary>Initializes a new instance of the <see cref="Password"/> class.</summary>
        public Password()
            : this(null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Password"/> class.</summary>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="javaScript">The JavaScript library helper.</param>
        /// <param name="clientResourceController">The client resources controller.</param>
        /// <param name="dataProvider">The data provider.</param>
        public Password(IEventLogger eventLogger, IJavaScriptLibraryHelper javaScript, IClientResourceController clientResourceController, DataProvider dataProvider)
            : base(null, null)
        {
            this.eventLogger = eventLogger ?? Common.Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            this.javaScript = javaScript ?? Common.Globals.GetCurrentServiceProvider().GetRequiredService<IJavaScriptLibraryHelper>();
            this.clientResourceController = clientResourceController ?? Common.Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
            this.dataProvider = dataProvider ?? Common.Globals.GetCurrentServiceProvider().GetRequiredService<DataProvider>();
        }

        /// <summary>A function which handles an event with <see cref="PasswordUpdatedEventArgs"/>.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        public delegate void PasswordUpdatedEventHandler(object sender, PasswordUpdatedEventArgs e);

        /// <summary>An event which is fired when something related to password updating occurs.</summary>
        public event PasswordUpdatedEventHandler PasswordUpdated;

        /// <summary>An event which is fired when something related to a password Q&amp;A occurs.</summary>
        public event PasswordUpdatedEventHandler PasswordQuestionAnswerUpdated;

        /// <summary>Gets the UserMembership associated with this control.</summary>
        public UserMembership Membership
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

        /// <summary>Gets the DOM id of the new-password container.</summary>
        /// <remarks>Replaces <c>passwordContainer.ClientID</c>, which the comparer options used.</remarks>
        protected string PasswordContainerClientId => this.passwordContainerClientId;

        /// <summary>Gets a value indicating whether the captcha is required to change the password.</summary>
        protected bool UseCaptcha => Convert.ToBoolean(GetSetting(this.PortalId, "Security_CaptchaChangePassword"));

        /// <summary>Gets or sets the JavaScript runtime used to bootstrap the strength meter and comparer.</summary>
        [Microsoft.AspNetCore.Components.Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>Raises the <see cref="PasswordUpdated"/> Event.</summary>
        /// <param name="e">The event arguments.</param>
        public void OnPasswordUpdated(PasswordUpdatedEventArgs e)
        {
            if (this.IsUserOrAdmin)
            {
                this.PasswordUpdated?.Invoke(this, e);
            }
        }

        /// <summary>Raises the PasswordQuestionAnswerUpdated Event.</summary>
        /// <param name="e">The event arguments.</param>
        public void OnPasswordQuestionAnswerUpdated(PasswordUpdatedEventArgs e)
        {
            if (this.IsUserOrAdmin)
            {
                this.PasswordQuestionAnswerUpdated?.Invoke(this, e);
            }
        }

        /// <summary>DataBind binds the data to the controls.</summary>
        public void DataBind()
        {
            this.lastChangedText = this.User.Membership.LastPasswordChangeDate.ToLongDateString();

            // Set Password Expiry Label. PasswordExpiry == 0 means "never expires", so this stays a
            // "> 0" test.
            if (this.User.Membership.UpdatePassword)
            {
                this.expiresText = Localization.GetString("ForcedExpiry", this.LocalResourceFile);
            }
            else
            {
                this.expiresText = PasswordConfig.PasswordExpiry > 0 ? this.User.Membership.LastPasswordChangeDate.AddDays(PasswordConfig.PasswordExpiry).ToLongDateString() : Localization.GetString("NoExpiry", this.LocalResourceFile);
            }

            if ((!MembershipProviderConfig.PasswordRetrievalEnabled) && this.IsAdmin && (!this.IsUser))
            {
                this.pnlChangeVisible = true;
                this.cmdUpdateVisible = true;
                this.oldPasswordRowVisible = false;
                this.changeHelpText = Localization.GetString("AdminChangeHelp", this.LocalResourceFile);
            }
            else
            {
                this.pnlChangeVisible = true;
                this.cmdUpdateVisible = true;

                // Set up Change Password
                if (this.IsAdmin && !this.IsUser)
                {
                    this.changeHelpText = Localization.GetString("AdminChangeHelp", this.LocalResourceFile);
                    this.oldPasswordRowVisible = false;
                }
                else
                {
                    this.changeHelpText = Localization.GetString("UserChangeHelp", this.LocalResourceFile);
                    if (this.Request.IsAuthenticated())
                    {
                        this.pnlChangeVisible = true;
                        this.cmdUserResetVisible = false;
                        this.cmdUpdateVisible = true;
                    }
                    else
                    {
                        this.pnlChangeVisible = false;
                        this.cmdUserResetVisible = true;
                        this.cmdUpdateVisible = false;
                    }
                }
            }

            // If Password Reset is not enabled then only the Admin can reset the
            // Password, a User must Update
            if (!MembershipProviderConfig.PasswordResetEnabled)
            {
                this.pnlResetVisible = false;
                this.cmdResetVisible = false;
            }
            else
            {
                this.pnlResetVisible = true;
                this.cmdResetVisible = true;

                // Set up Reset Password
                if (this.IsAdmin && !this.IsUser)
                {
                    if (MembershipProviderConfig.RequiresQuestionAndAnswer)
                    {
                        this.pnlResetVisible = false;
                        this.cmdResetVisible = false;
                    }
                    else
                    {
                        this.resetHelpText = Localization.GetString("AdminResetHelp", this.LocalResourceFile);
                    }

                    this.questionRowVisible = false;
                    this.answerRowVisible = false;
                }
                else
                {
                    if (MembershipProviderConfig.RequiresQuestionAndAnswer && this.IsUser)
                    {
                        this.resetHelpText = Localization.GetString("UserResetHelp", this.LocalResourceFile);
                        this.questionText = this.User.Membership.PasswordQuestion;
                        this.questionRowVisible = true;
                        this.answerRowVisible = true;
                    }
                    else
                    {
                        this.pnlResetVisible = false;
                        this.cmdResetVisible = false;
                    }
                }
            }

            // Set up Edit Question and Answer area
            if (MembershipProviderConfig.RequiresQuestionAndAnswer && this.IsUser)
            {
                this.pnlQAVisible = true;
                this.cmdUpdateQAVisible = true;
            }
            else
            {
                this.pnlQAVisible = false;
                this.cmdUpdateQAVisible = false;
            }

            this.StateHasChanged();
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // The five Click wire-ups are @onclick bindings in Password.razor now.
            if (MembershipProviderConfig.RequiresQuestionAndAnswer && this.User.UserID != UserController.Instance.GetCurrentUserInfo().UserID)
            {
                this.pnlChangeVisible = false;
                this.cmdUpdateVisible = false;
                this.cannotChangePasswordMessageVisible = true;
            }

            if (this.UseCaptcha)
            {
                this.captchaRowVisible = true;
                this.captchaErrorMessage = Localization.GetString("InvalidCaptcha", this.LocalResourceFile);
                this.captchaText = Localization.GetString("CaptchaText", this.LocalResourceFile);
            }
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            this.clientResourceController.RegisterScript("~/resources/Shared/scripts/dnn.jquery.extensions.js");
            this.clientResourceController.RegisterScript("~/resources/Shared/scripts/dnn.jquery.tooltip.js");
            this.clientResourceController.RegisterScript("~/resources/Shared/scripts/dnn.PasswordStrength.js");
            this.clientResourceController.RegisterScript("~/desktopmodules/Admin/Security/Scripts/dnn.PasswordComparer.js");

            // FileOrder is declared in two namespaces; RegisterStylesheet's priority parameter is the
            // DotNetNuke.Abstractions.ClientResources one, so it must be named explicitly or overload
            // resolution silently falls through to the (path, bool checkIfExists) overload and the
            // priority is lost. ResourceCss (12) is the same ordering value as before.
            this.clientResourceController.RegisterStylesheet("~/resources/Shared/stylesheets/dnn.PasswordStrength.css", DotNetNuke.Abstractions.ClientResources.FileOrder.Css.ResourceCss);

            this.javaScript.RequestRegistration(CommonJs.DnnPlugins);

            base.OnPreRender(e);

            this.strengthMeterEnabled = this.HostSettings.EnableStrengthMeter;
            if (this.strengthMeterEnabled)
            {
                this.passwordContainerCssClass = "password-strength-container";
                this.newPasswordCssClass = "password-strength";
            }
        }

        /// <summary>
        /// Runs the two start-up scripts that used to go through <c>ScriptManager</c> /
        /// <c>Page.ClientScript.RegisterStartupScript</c>.
        /// </summary>
        /// <param name="firstRender">Whether this is the first render.</param>
        /// <returns>A task.</returns>
        /// <remarks>
        /// The option objects are still serialized with <c>Json.Serialize</c>: they are
        /// <c>[DataContract]</c> types whose members are renamed to the camel-cased names the scripts
        /// read, and <c>DnnPaswordStrengthOptions</c> fills in the portal's minimum length, minimum
        /// special-character count and validation expression from an <c>[OnSerializing]</c> hook.
        /// Letting Blazor's own serializer handle the objects would skip both and silently publish a
        /// different (weaker-looking) rule set to the meter.
        /// </remarks>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (!firstRender)
            {
                return;
            }

            if (this.strengthMeterEnabled)
            {
                var options = new DnnPaswordStrengthOptions();
                var optionsAsJsonString = Json.Serialize(options);
                using var optionsJson = JsonDocument.Parse(optionsAsJsonString);
                await this.JsRuntime.InvokeVoidAsync("dnn.initializePasswordStrength", ".password-strength", optionsJson.RootElement);
            }

            var confirmPasswordOptions = new DnnConfirmPasswordOptions()
            {
                FirstElementSelector = "#" + this.passwordContainerClientId + " input[type=password]",
                SecondElementSelector = ".password-confirm",
                ContainerSelector = ".dnnPassword",
                UnmatchedCssClass = "unmatched",
                MatchedCssClass = "matched",
            };

            var confirmOptionsAsJsonString = Json.Serialize(confirmPasswordOptions);
            using var confirmJson = JsonDocument.Parse(confirmOptionsAsJsonString);
            await this.JsRuntime.InvokeVoidAsync("dnn.initializePasswordComparer", confirmJson.RootElement);
        }

        /// <inheritdoc />
        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);

            if (firstRender && this.ctlCaptcha is not null)
            {
                // ErrorStyle-CssClass="dnnFormMessage dnnFormError dnnCaptcha" from the .ascx;
                // CaptchaControl.ErrorStyle is get-only, so it is configured once the @ref exists.
                this.ctlCaptcha.ErrorStyle.CssClass = "dnnFormMessage dnnFormError dnnCaptcha";
            }
        }

        private void CmdResetClick()
        {
            if (!this.IsUserOrAdmin)
            {
                return;
            }

            if (MembershipProviderConfig.RequiresQuestionAndAnswer && !this.IsAdmin)
            {
                if (string.IsNullOrEmpty(this.answer))
                {
                    this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.InvalidPasswordAnswer));
                    return;
                }
            }

            try
            {
                // create reset token
                UserController.ResetPasswordToken(this.User, (int)this.HostSettings.AdminMembershipResetLinkValidity.TotalMinutes);

                bool canSend = Mail.SendMail(this.User, MessageType.PasswordReminder, this.PortalSettings) == string.Empty;
                string message;
                var moduleMessageType = ModuleMessage.ModuleMessageType.GreenSuccess;
                if (canSend)
                {
                    message = Localization.GetString("PasswordSent", this.LocalResourceFile);
                    this.LogSuccess();
                }
                else
                {
                    message = Localization.GetString("OptionUnavailable", this.LocalResourceFile);
                    moduleMessageType = ModuleMessage.ModuleMessageType.RedError;
                    this.LogFailure(message);
                }

                UI.Skins.Skin.AddModuleMessage(this, message, moduleMessageType);
            }
            catch (ArgumentException exc)
            {
                Logger.PasswordResetArgumentException(exc);
                this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.InvalidPasswordAnswer));
            }
            catch (Exception exc)
            {
                Logger.PasswordResetGeneralException(exc);
                this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordResetFailed));
            }
        }

        private void CmdUserResetClick()
        {
            try
            {
                // send fresh reset token copy
                bool canSend = UserController.ResetPasswordToken(this.User, true);

                string message;
                var moduleMessageType = ModuleMessage.ModuleMessageType.GreenSuccess;
                if (canSend)
                {
                    message = Localization.GetString("PasswordSent", this.LocalResourceFile);
                    this.LogSuccess();
                }
                else
                {
                    message = Localization.GetString("OptionUnavailable", this.LocalResourceFile);
                    moduleMessageType = ModuleMessage.ModuleMessageType.RedError;
                    this.LogFailure(message);
                }

                UI.Skins.Skin.AddModuleMessage(this, message, moduleMessageType);
            }
            catch (ArgumentException exc)
            {
                Logger.PasswordUserResetArgumentException(exc);
                this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.InvalidPasswordAnswer));
            }
            catch (Exception exc)
            {
                Logger.PasswordUserResetGeneralException(exc);
                this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordResetFailed));
            }
        }

        private void LogSuccess()
        {
            this.LogResult(string.Empty);
        }

        private void LogFailure(string reason)
        {
            this.LogResult(reason);
        }

        private void LogResult(string message)
        {
            ILogInfo log = new LogInfo
            {
                LogPortalName = this.PortalSettings.PortalName,
                LogUserName = this.User.Username,
            };
            log.LogUserId = this.UserId;
            log.LogPortalId = this.PortalSettings.PortalId;

            if (string.IsNullOrEmpty(message))
            {
                log.LogTypeKey = "PASSWORD_SENT_SUCCESS";
            }
            else
            {
                log.LogTypeKey = "PASSWORD_SENT_FAILURE";
                log.LogProperties.Add(new LogDetailInfo("Cause", message));
            }

            this.eventLogger.AddLog(log);
        }

        private void CmdUpdateClick()
        {
            if ((this.UseCaptcha && this.ctlCaptcha is not null && this.ctlCaptcha.IsValid) || !this.UseCaptcha)
            {
                if (!this.IsUserOrAdmin)
                {
                    return;
                }

                // 1. Check New Password and Confirm are the same
                if (this.newPassword != this.newConfirm)
                {
                    this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordMismatch));
                    return;
                }

                // 2. Check New Password is Valid
                if (!UserController.ValidatePassword(this.newPassword))
                {
                    this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordInvalid));
                    return;
                }

                // 3. Check old Password is Provided
                if (!this.IsAdmin && string.IsNullOrEmpty(this.oldPassword))
                {
                    this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordMissing));
                    return;
                }

                // 4. Check New Password is different
                if (!this.IsAdmin && this.newPassword == this.oldPassword)
                {
                    this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordNotDifferent));
                    return;
                }

                // 5. Check New Password is not same as username or banned
                var membershipPasswordController = new MembershipPasswordController(this.ListController, this.dataProvider);
                var settings = new MembershipPasswordSettings(this.User.PortalID);

                if (settings.EnableBannedList)
                {
                    if (membershipPasswordController.FoundBannedPassword(this.newPassword) || this.User.Username == this.newPassword)
                    {
                        this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.BannedPasswordUsed));
                        return;
                    }
                }

                // check new password is not in history
                if (membershipPasswordController.IsPasswordInHistory(this.User.UserID, this.User.PortalID, this.newPassword, false))
                {
                    this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordResetFailed));
                    return;
                }

                if (!this.IsAdmin && this.newPassword == this.oldPassword)
                {
                    this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordNotDifferent));
                    return;
                }

                if (!this.IsAdmin)
                {
                    try
                    {
                        this.OnPasswordUpdated(UserController.ChangePassword(this.eventLogger, this.User, this.oldPassword, this.newPassword)
                                              ? new PasswordUpdatedEventArgs(PasswordUpdateStatus.Success)
                                              : new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordResetFailed));
                    }
                    catch (MembershipPasswordException exc)
                    {
                        // Password Answer missing
                        Logger.PasswordUserUpdateMembershipPasswordException(exc);

                        this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.InvalidPasswordAnswer));
                    }
                    catch (Exception exc)
                    {
                        // Fail
                        Logger.PasswordUserUpdateGeneralException(exc);

                        this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordResetFailed));
                    }
                }
                else
                {
                    try
                    {
                        this.OnPasswordUpdated(UserController.ResetAndChangePassword(this.User, this.newPassword)
                                              ? new PasswordUpdatedEventArgs(PasswordUpdateStatus.Success)
                                              : new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordResetFailed));
                    }
                    catch (MembershipPasswordException exc)
                    {
                        // Password Answer missing
                        Logger.PasswordUserAdminUpdateMembershipPasswordException(exc);

                        this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.InvalidPasswordAnswer));
                    }
                    catch (Exception exc)
                    {
                        // Fail
                        Logger.PasswordUserAdminUpdateGeneralException(exc);

                        this.OnPasswordUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordResetFailed));
                    }
                }
            }
        }

        /// <summary>cmdUpdate_Click runs when the Update Question and Answer  Button is clicked.</summary>
        private void CmdUpdateQAClick()
        {
            if (!this.IsUserOrAdmin)
            {
                return;
            }

            if (string.IsNullOrEmpty(this.qaPassword))
            {
                this.OnPasswordQuestionAnswerUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordInvalid));
                return;
            }

            if (string.IsNullOrEmpty(this.editQuestion))
            {
                this.OnPasswordQuestionAnswerUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.InvalidPasswordQuestion));
                return;
            }

            if (string.IsNullOrEmpty(this.editAnswer))
            {
                this.OnPasswordQuestionAnswerUpdated(new PasswordUpdatedEventArgs(PasswordUpdateStatus.InvalidPasswordAnswer));
                return;
            }

            // Try and set password Q and A
            UserInfo objUser = UserController.GetUserById(this.HostSettings, this.PortalId, this.UserId);
            this.OnPasswordQuestionAnswerUpdated(UserController.ChangePasswordQuestionAndAnswer(this.eventLogger, objUser, this.qaPassword, this.editQuestion, this.editAnswer)
                                                ? new PasswordUpdatedEventArgs(PasswordUpdateStatus.Success)
                                                : new PasswordUpdatedEventArgs(PasswordUpdateStatus.PasswordResetFailed));
        }

        /// <summary>Provides a customised EventArgs class for the <see cref="Password.PasswordUpdated"/> event.</summary>
        public class PasswordUpdatedEventArgs
        {
            /// <summary>Initializes a new instance of the <see cref="PasswordUpdatedEventArgs"/> class.</summary>
            /// <param name="status">The Password Update Status.</param>
            public PasswordUpdatedEventArgs(PasswordUpdateStatus status)
            {
                this.UpdateStatus = status;
            }

            /// <summary>Gets or sets the Update Status.</summary>
            public PasswordUpdateStatus UpdateStatus { get; set; }
        }
    }
}
