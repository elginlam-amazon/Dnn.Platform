// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.Admin.Security
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;

    using NavigationManager = Microsoft.AspNetCore.Components.NavigationManager;

    /// <summary>The MemberServices UserModuleBase is used to manage a User's services.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>MemberServices.ascx</c> + <c>MemberServices.ascx.cs</c> +
    /// <c>MemberServices.ascx.designer.cs</c>; the markup is in <c>MemberServices.razor</c>.
    /// </para>
    /// <para>
    /// Every one of the formatting and eligibility helpers is byte-for-byte the original, including
    /// the sentinel and boundary tests, which are the ones that decide money and access:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="FormatExpiryDate"/> keeps <c>Null.IsNull(expiryDate)</c> as the "no expiry" test
    /// (an unset expiry is <see cref="Null.NullDate"/>, not <c>default</c>) and the strict
    /// <c>expiryDate &gt; DateTime.Today</c> comparison, so a subscription expiring <i>today</i> is
    /// still reported as "Expired" and not as a live date.
    /// </description></item>
    /// <item><description>
    /// <see cref="ServiceText"/> keeps the same ordering: not subscribed gives "Subscribe";
    /// subscribed gives "Unsubscribe", downgraded to "Renew" only when the expiry is set
    /// (<c>!Null.IsNull</c>) <i>and</i> strictly in the past (<c>expiryDate &lt; DateTime.Today</c>).
    /// A subscription expiring today therefore still reads "Unsubscribe" - the two comparisons are
    /// deliberately asymmetric in the original and are left that way.
    /// </description></item>
    /// <item><description>
    /// <see cref="FormatPrice(float)"/> keeps <c>price != Null.NullSingle</c> as the "no price" test
    /// and the <c>"##0.00"</c> format.
    /// </description></item>
    /// <item><description>
    /// <see cref="ShowSubscribe"/> is unchanged: a role is self-subscribable only when it is public
    /// <i>and</i> free (<c>ServiceFee == 0.0</c>).
    /// </description></item>
    /// <item><description>
    /// <see cref="ShowTrial"/> is unchanged, including its three-way structure: no trial at all when
    /// the trial frequency is empty or "N" or the role is public-and-free; otherwise, for a public
    /// role with a zero trial fee, a trial is offered only when the user has no role record yet
    /// (<c>objUserRole == null</c>) or has not used the trial (<c>!IsTrialUsed</c>); and
    /// <see cref="Null.NullBoolean"/> otherwise.
    /// </description></item>
    /// <item><description>
    /// <see cref="Subscribe"/> and <see cref="UseTrial"/> keep the free/paid split exactly: the role
    /// membership is written directly only for a public role with a zero fee (zero <i>trial</i> fee
    /// for the trial path), and every other case is handed to the PayPal checkout instead. A paid
    /// subscription can therefore still not be granted without going through payment.
    /// </description></item>
    /// <item><description>
    /// The RSVP path still walks every role in the portal and compares <c>RSVPCode</c> for an exact
    /// match, enrols on <i>each</i> match (the loop does not break), and reports <c>RSVPSuccess</c>
    /// or <c>RSVPFailure</c> with the original wording - which says only whether a code matched, not
    /// which role or whether a role exists.
    /// </description></item>
    /// </list>
    /// <para>
    /// The grid's <c>ItemCommand</c> dispatch is preserved as <see cref="GrdServicesItemCommand"/>,
    /// still keyed on the <i>localized</i> command name, because that string comparison is the
    /// dispatch: the localized "Subscribe" or "Renew" subscribes, the localized "Unsubscribe"
    /// unsubscribes, and the literal "UseTrial" starts a trial. The links in the markup pass exactly
    /// the command names the item templates used to emit, and the grid is rebound afterwards just as
    /// before.
    /// </para>
    /// <para>
    /// Mechanical changes: <c>Response.Redirect("~/admin/Sales/PayPalSubscription.aspx?...", true)</c>
    /// becomes <c>NavigationManager.NavigateTo(Globals.ResolveUrl(...), forceLoad: true)</c> against
    /// the preserved legacy route that <c>PayPalSubscription.razor</c> now serves (Blazor route
    /// matching is case insensitive, so the original casing still resolves); and
    /// <c>Request.ApplicationPath</c>, which does not exist on ASP.NET Core, becomes
    /// <see cref="Globals.ApplicationPath"/>, which is <c>Request.PathBase</c>.
    /// </para>
    /// </remarks>
    public partial class MemberServices : UserModuleBase
    {
        /// <summary>The subscription page. Preserved literally - see <c>PayPalSubscription.razor</c>'s @@page.</summary>
        private const string PayPalSubscriptionPath = "~/admin/Sales/paypalsubscription.aspx";

        /// <summary>The grid's data source. Replaces <c>grdServices.DataSource</c>.</summary>
        private IList<UserRoleInfo> services = new List<UserRoleInfo>();

        /// <summary>Replaces <c>ServicesRow.Visible</c>.</summary>
        private bool servicesRowVisible;

        /// <summary>Replaces <c>lblServices.Visible</c>.</summary>
        /// <remarks>
        /// The original <c>MemberServices.ascx</c> declared the label
        /// <c>&lt;asp:label id="lblServices" runat="server" visible="False"/&gt;</c> and
        /// <c>MemberServices.ascx.cs</c> never referenced <c>lblServices</c> at all, so it was
        /// never shown. The <see langword="false"/> initializer is written out explicitly (rather
        /// than suppressing CS0649) to preserve that exactly: setting it <see langword="true"/>
        /// would render a label the original never rendered.
        /// </remarks>
        private bool servicesLabelVisible = false;

        /// <summary>Replaces <c>lblServices.Text</c>.</summary>
        private string servicesLabelText = string.Empty;

        /// <summary>Replaces <c>txtRSVPCode.Text</c>.</summary>
        private string rsvpCode = string.Empty;

        /// <summary>Replaces <c>lblRSVP.Text</c>.</summary>
        private string rsvpMessage = string.Empty;

        /// <summary>Initializes a new instance of the <see cref="MemberServices"/> class.</summary>
        public MemberServices()
            : base(null, null)
        {
        }

        /// <summary>A function which handles a subscription update.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        public delegate void SubscriptionUpdatedEventHandler(object sender, SubscriptionUpdatedEventArgs e);

        /// <summary>An event which is fired when a subscription is added or cancelled.</summary>
        public event SubscriptionUpdatedEventHandler SubscriptionUpdated;

        /// <summary>Gets or sets the navigation manager used in place of <c>Response.Redirect</c>.</summary>
        [Microsoft.AspNetCore.Components.Inject]
        protected NavigationManager Navigation { get; set; }

        /// <summary>DataBind binds the data to the controls.</summary>
        public void DataBind()
        {
            if (this.Request.IsAuthenticated())
            {
                // Localization.LocalizeDataGrid localized the column headers in place; there is no
                // grid object to mutate any more, so the markup calls LocalizeHeader per header,
                // which performs the identical "<HeaderText>.Header" lookup with the same fallback.
                this.services = RoleController.Instance.GetUserRoles(this.UserInfo, false);

                // if no service available then hide options
                this.servicesRowVisible = this.services.Count > 0;
            }

            this.StateHasChanged();
        }

        /// <summary>Raises the SubscriptionUpdated Event.</summary>
        /// <param name="e">The event arguments.</param>
        public void OnSubscriptionUpdated(SubscriptionUpdatedEventArgs e)
        {
            if (this.IsUserOrAdmin == false)
            {
                return;
            }

            if (this.SubscriptionUpdated != null)
            {
                this.SubscriptionUpdated(this, e);
            }
        }

        /// <summary>Localizes a grid column header the way <c>Localization.LocalizeDataGrid</c> did.</summary>
        /// <param name="headerText">The header text declared on the column.</param>
        /// <returns>The localized header, or <paramref name="headerText"/> when there is no resource entry.</returns>
        protected string LocalizeHeader(string headerText)
        {
            if (string.IsNullOrEmpty(headerText))
            {
                return headerText;
            }

            var localizedText = Localization.GetString(headerText + ".Header", this.LocalResourceFile);
            return string.IsNullOrEmpty(localizedText) ? headerText : localizedText;
        }

        /// <summary>FormatExpiryDate formats the expiry date and filters out null-values.</summary>
        /// <param name="expiryDate">The date to format.</param>
        /// <returns>The correctly formatted date.</returns>
        protected string FormatExpiryDate(DateTime expiryDate)
        {
            string formatExpiryDate = Null.NullString;
            try
            {
                if (!Null.IsNull(expiryDate))
                {
                    if (expiryDate > DateTime.Today)
                    {
                        formatExpiryDate = expiryDate.ToShortDateString();
                    }
                    else
                    {
                        formatExpiryDate = Localization.GetString("Expired", this.LocalResourceFile);
                    }
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }

            return formatExpiryDate;
        }

        /// <summary>FormatPrice formats the Fee amount and filters out null-values.</summary>
        /// <param name="price">The price to format.</param>
        /// <param name="period">Period of price.</param>
        /// <param name="frequency">Frenquency of price.</param>
        /// <returns>The correctly formatted price.</returns>
        protected string FormatPrice(float price, int period, string frequency)
        {
            string formatPrice = Null.NullString;
            try
            {
                switch (frequency)
                {
                    case "N":
                    case "":
                        formatPrice = Localization.GetString("NoFee", this.LocalResourceFile);
                        break;
                    case "O":
                        formatPrice = this.FormatPrice(price);
                        break;
                    default:
                        formatPrice = string.Format(CultureInfo.CurrentCulture, Localization.GetString("Fee", this.LocalResourceFile), this.FormatPrice(price), period, Localization.GetString("Frequency_" + frequency, this.LocalResourceFile));
                        break;
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }

            return formatPrice;
        }

        /// <summary>FormatTrial formats the Trial Fee amount and filters out null-values.</summary>
        /// <param name="price">The price to format.</param>
        /// <param name="period">Period of price.</param>
        /// <param name="frequency">Frenquency of price.</param>
        /// <returns>The correctly formatted price.</returns>
        protected string FormatTrial(float price, int period, string frequency)
        {
            string formatTrial = Null.NullString;
            try
            {
                switch (frequency)
                {
                    case "N":
                    case "":
                        formatTrial = Localization.GetString("NoFee", this.LocalResourceFile);
                        break;
                    case "O":
                        formatTrial = this.FormatPrice(price);
                        break;
                    default:
                        formatTrial = string.Format(
                            CultureInfo.CurrentCulture,
                            Localization.GetString("TrialFee", this.LocalResourceFile),
                            this.FormatPrice(price),
                            period,
                            Localization.GetString("Frequency_" + frequency, this.LocalResourceFile));
                        break;
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }

            return formatTrial;
        }

        /// <summary>FormatURL correctly formats a URL.</summary>
        /// <returns>The correctly formatted url.</returns>
        protected string FormatURL()
        {
            string formatURL = Null.NullString;
            try
            {
                // Request.ApplicationPath has no ASP.NET Core equivalent; Globals.ApplicationPath is
                // the migrated seam for it (it reads Request.PathBase).
                string serverPath = Globals.ApplicationPath ?? string.Empty;
                if (!serverPath.EndsWith("/", StringComparison.Ordinal))
                {
                    serverPath += "/";
                }

                formatURL = serverPath + "Register.aspx?tabid=" + this.TabId;
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }

            return formatURL;
        }

        /// <summary>ServiceText gets the Service Text (Cancel or Subscribe).</summary>
        /// <param name="subscribed">The service state.</param>
        /// <param name="expiryDate">The service expiry date.</param>
        /// <returns>The correctly formatted text.</returns>
        protected string ServiceText(bool subscribed, DateTime expiryDate)
        {
            string serviceText = Null.NullString;
            try
            {
                if (!subscribed)
                {
                    serviceText = Localization.GetString("Subscribe", this.LocalResourceFile);
                }
                else
                {
                    serviceText = Localization.GetString("Unsubscribe", this.LocalResourceFile);
                    if (!Null.IsNull(expiryDate))
                    {
                        if (expiryDate < DateTime.Today)
                        {
                            serviceText = Localization.GetString("Renew", this.LocalResourceFile);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }

            return serviceText;
        }

        /// <summary>Gets a value indicating whether the subscribe/unsubscribe link is shown for a role.</summary>
        /// <param name="roleID">The role ID.</param>
        /// <returns><see langword="true"/> when the role is public and free.</returns>
        protected bool ShowSubscribe(int roleID)
        {
            var objRole = RoleController.Instance.GetRole(this.PortalSettings.PortalId, r => r.RoleID == roleID);
            return objRole.IsPublic && objRole.ServiceFee == 0.0;
        }

        /// <summary>Gets a value indicating whether the use-trial link is shown for a role.</summary>
        /// <param name="roleID">The role ID.</param>
        /// <returns><see langword="true"/> when a free trial is still available to this user.</returns>
        protected bool ShowTrial(int roleID)
        {
            bool showTrial = Null.NullBoolean;
            RoleInfo objRole = RoleController.Instance.GetRole(this.PortalSettings.PortalId, r => r.RoleID == roleID);
            if (string.IsNullOrEmpty(objRole.TrialFrequency) || objRole.TrialFrequency == "N" || (objRole.IsPublic && objRole.ServiceFee == 0.0))
            {
                showTrial = Null.NullBoolean;
            }
            else if (objRole.IsPublic && objRole.TrialFee == 0.0)
            {
                // Use Trial?
                UserRoleInfo objUserRole = RoleController.Instance.GetUserRole(this.PortalId, this.UserInfo.UserID, roleID);
                if ((objUserRole == null) || (!objUserRole.IsTrialUsed))
                {
                    showTrial = true;
                }
            }

            return showTrial;
        }

        /// <summary>Page_Load runs when the control is loaded.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // The cmdRSVP.Click and grdServices.ItemCommand wire-ups are @onclick bindings in
            // MemberServices.razor now; clearing the RSVP message is all that is left.
            this.rsvpMessage = string.Empty;
        }

        /// <summary>Dispatches a grid command, keyed on the localized command name as the original did.</summary>
        /// <param name="commandName">The command name emitted by the link.</param>
        /// <param name="roleID">The role the command applies to.</param>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Breaking Change")]
        protected void GrdServicesItemCommand(string commandName, int roleID)
        {
            if (commandName == Localization.GetString("Subscribe", this.LocalResourceFile) || commandName == Localization.GetString("Renew", this.LocalResourceFile))
            {
                // Subscribe
                this.Subscribe(roleID, false);
            }
            else if (commandName == Localization.GetString("Unsubscribe", this.LocalResourceFile))
            {
                // Unsubscribe
                this.Subscribe(roleID, true);
            }
            else if (commandName == "UseTrial")
            {
                // Use Trial
                this.UseTrial(roleID);
            }

            // Rebind Grid
            this.DataBind();
        }

        /// <summary>FormatPrice formats the Fee amount and filters out null-values.</summary>
        /// <param name="price">The price to format.</param>
        /// <returns>The correctly formatted price.</returns>
        private string FormatPrice(float price)
        {
            string formatPrice = Null.NullString;
            try
            {
                if (price != Null.NullSingle)
                {
                    formatPrice = price.ToString("##0.00", CultureInfo.CurrentCulture);
                }
                else
                {
                    formatPrice = string.Empty;
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }

            return formatPrice;
        }

        private void Subscribe(int roleID, bool cancel)
        {
            RoleInfo objRole = RoleController.Instance.GetRole(this.PortalSettings.PortalId, r => r.RoleID == roleID);

            if (objRole.IsPublic && objRole.ServiceFee == 0.0)
            {
                RoleController.Instance.UpdateUserRole(this.PortalId, this.UserInfo.UserID, roleID, RoleStatus.Approved, false, cancel);

                // Raise SubscriptionUpdated Event
                this.OnSubscriptionUpdated(new SubscriptionUpdatedEventArgs(cancel, objRole.RoleName));
            }
            else
            {
                if (!cancel)
                {
                    this.Navigation.NavigateTo(Globals.ResolveUrl(PayPalSubscriptionPath + "?tabid=" + this.TabId + "&RoleID=" + roleID), forceLoad: true);
                }
                else
                {
                    this.Navigation.NavigateTo(Globals.ResolveUrl(PayPalSubscriptionPath + "?tabid=" + this.TabId + "&RoleID=" + roleID + "&cancel=1"), forceLoad: true);
                }
            }
        }

        private void UseTrial(int roleID)
        {
            RoleInfo objRole = RoleController.Instance.GetRole(this.PortalSettings.PortalId, r => r.RoleID == roleID);

            if (objRole.IsPublic && objRole.TrialFee == 0.0)
            {
                RoleController.Instance.UpdateUserRole(this.PortalId, this.UserInfo.UserID, roleID, RoleStatus.Approved, false, false);

                // Raise SubscriptionUpdated Event
                this.OnSubscriptionUpdated(new SubscriptionUpdatedEventArgs(false, objRole.RoleName));
            }
            else
            {
                this.Navigation.NavigateTo(Globals.ResolveUrl(PayPalSubscriptionPath + "?tabid=" + this.TabId + "&RoleID=" + roleID), forceLoad: true);
            }
        }

        /// <summary>cmdRSVP_Click runs when the Subscribe to RSVP Code Roles Button is clicked.</summary>
        private void CmdRSVPClick()
        {
            if (this.IsUserOrAdmin == false)
            {
                return;
            }

            // Get the RSVP code
            string code = this.rsvpCode;
            bool rsvpCodeExists = false;
            if (!string.IsNullOrEmpty(code))
            {
                // Parse the roles
                foreach (RoleInfo objRole in RoleController.Instance.GetRoles(this.PortalSettings.PortalId))
                {
                    if (objRole.RSVPCode == code)
                    {
                        RoleController.Instance.UpdateUserRole(this.PortalId, this.UserInfo.UserID, objRole.RoleID, RoleStatus.Approved, false, false);
                        rsvpCodeExists = true;

                        // Raise SubscriptionUpdated Event
                        this.OnSubscriptionUpdated(new SubscriptionUpdatedEventArgs(false, objRole.RoleName));
                    }
                }

                if (rsvpCodeExists)
                {
                    this.rsvpMessage = Localization.GetString("RSVPSuccess", this.LocalResourceFile);

                    // Reset RSVP Code field
                    this.rsvpCode = string.Empty;
                }
                else
                {
                    this.rsvpMessage = Localization.GetString("RSVPFailure", this.LocalResourceFile);
                }
            }

            this.DataBind();
        }

        /// <summary>
        /// The SubscriptionUpdatedEventArgs class provides a customised EventArgs class for
        /// the SubscriptionUpdated Event.
        /// </summary>
        public class SubscriptionUpdatedEventArgs
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SubscriptionUpdatedEventArgs"/> class.
            /// Constructs a new SubscriptionUpdatedEventArgs.
            /// </summary>
            /// <param name="cancel">Whether this is a subscription cancellation.</param>
            /// <param name="roleName">The role name of subscription role.</param>
            public SubscriptionUpdatedEventArgs(bool cancel, string roleName)
            {
                this.Cancel = cancel;
                this.RoleName = roleName;
            }

            /// <summary>Gets or sets a value indicating whether this was a cancelation.</summary>
            public bool Cancel { get; set; }

            /// <summary>Gets or sets the RoleName that was (un)subscribed to.</summary>
            public string RoleName { get; set; }
        }
    }
}
