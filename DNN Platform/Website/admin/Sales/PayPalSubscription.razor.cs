// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Admin.Sales
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Lists;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Website;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>A page which receives subscription messages from PayPal.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>paypalsubscription.aspx</c> + <c>PayPalSubscription.aspx.cs</c> +
    /// <c>PayPalSubscription.aspx.designer.cs</c>; the (empty) markup and the preserved route now live
    /// in <c>PayPalSubscription.razor</c>. The designer file declared no controls, and
    /// <c>InitializeComponent</c> - the only thing <c>OnInit</c> did beyond the page base plumbing -
    /// had an empty body, so the component's initialization is just the ported <c>OnLoad</c>.
    /// </para>
    /// <para>
    /// The DI constructor is preserved (<see cref="IPortalController"/>,
    /// <see cref="IApplicationStatusInfo"/>, <see cref="IHostSettings"/>), with the same
    /// <c>Globals.GetCurrentServiceProvider()</c> fallback <see cref="DotNetNuke.Framework.PageBase"/>
    /// applied, so a container that activates this component by type behaves as before. The base class
    /// is <see cref="ComponentBase"/> rather than <c>PageBase</c>: <c>PageBase</c> is now a
    /// <c>LayoutComponentBase</c> (a DNN skin), and this page used none of its members except
    /// <c>PortalSettings</c>/<c>Request</c>/<c>UserController</c>, which are reproduced below.
    /// </para>
    /// <para>
    /// <c>Response.Redirect(url, true)</c> becomes
    /// <c>NavigationManager.NavigateTo(url, forceLoad: true)</c>. During the static server render that
    /// serves this route that is a 302 to the absolute PayPal URL, which is exactly what the page did;
    /// the <c>endResponse: true</c> argument has no equivalent and needs none, because the navigation
    /// aborts the render.
    /// </para>
    /// </remarks>
    [DnnDeprecated(10, 0, 2, "No replacement")]
    public partial class PayPalSubscription : ComponentBase
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<PayPalSubscription>();

        private readonly IPortalController portalController;

        private readonly IUserController userController;

        private HttpRequest request;

        /// <summary>Initializes a new instance of the <see cref="PayPalSubscription"/> class.</summary>
        public PayPalSubscription()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="PayPalSubscription"/> class.</summary>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="hostSettings">The host settings.</param>
        public PayPalSubscription(IPortalController portalController, IApplicationStatusInfo appStatus, IHostSettings hostSettings)
        {
            var services = Globals.GetCurrentServiceProvider();
            this.portalController = portalController ?? services.GetRequiredService<IPortalController>();
            this.AppStatus = appStatus ?? services.GetRequiredService<IApplicationStatusInfo>();
            this.HostSettings = hostSettings ?? services.GetRequiredService<IHostSettings>();
            this.userController = services.GetRequiredService<IUserController>();
        }

        /// <summary>Gets the application status (was <c>PageBase.AppStatus</c>, from the preserved DI constructor).</summary>
        protected IApplicationStatusInfo AppStatus { get; }

        /// <summary>Gets the host settings (was <c>PageBase.HostSettings</c>, from the preserved DI constructor).</summary>
        protected IHostSettings HostSettings { get; }

        /// <summary>
        /// Gets the message rendered when the redirect to PayPal could not be built or issued. Empty in
        /// the normal flow, in which the component always redirects and renders nothing.
        /// </summary>
        protected string RedirectFailureMessage { get; private set; } = string.Empty;

        /// <summary>Gets or sets the navigation manager used to issue the redirect to PayPal.</summary>
        [Inject]
        protected Microsoft.AspNetCore.Components.NavigationManager NavigationManager { get; set; }

        /// <summary>Gets the current portal settings (was <c>PageBase.PortalSettings</c>).</summary>
        protected IPortalSettings PortalSettings => this.portalController.GetCurrentSettings();

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            base.OnInitialized();

            // Was PageBase.Request. Captured once, on the initial (static) render: HttpContext is null
            // once a circuit is interactive.
            this.request = HttpContextSource.Current?.Request;

            this.Load();
        }

        /// <summary>This methods return the text description of the Frequency value.</summary>
        /// <param name="value">value of the Frequency.</param>
        /// <returns>text of the Frequency.</returns>
        private static string GetBillingFrequencyText(string value)
        {
#pragma warning disable CS0618 // the parameterless ListController is deprecated; its replacement is not reachable from here
            var ctlEntry = new ListController();
#pragma warning restore CS0618
            ListEntryInfo entry = ctlEntry.GetListEntryInfo("Frequency", value);
            return entry.Text;
        }

        /// <summary>The ported <c>OnLoad</c>: builds the PayPal subscription/payment URL and redirects to it.</summary>
        private void Load()
        {
            try
            {
                UserInfo objUserInfo = null;
                var intUserID = -1;
                if (this.request?.HttpContext.User?.Identity?.IsAuthenticated == true)
                {
                    objUserInfo = this.userController.GetCurrentUserInfo();
                    if (objUserInfo != null)
                    {
                        intUserID = objUserInfo.UserID;
                    }
                }

                var intRoleId = -1;
                var query = this.request?.Query;
                if (query != null && query.ContainsKey("roleid"))
                {
                    intRoleId = int.Parse(query["roleid"].ToString());
                }

                var strProcessorUserId = string.Empty;
                PortalInfo objPortalInfo = this.portalController.GetPortal(this.PortalSettings.PortalId);
                if (objPortalInfo != null)
                {
                    // PortalInfo.ProcessorUserId is [Obsolete] with no replacement and is the PayPal
                    // merchant identity this page builds its URL from, so the deprecation is
                    // suppressed rather than the value being dropped.
#pragma warning disable CS0618
                    strProcessorUserId = objPortalInfo.ProcessorUserId;
#pragma warning restore CS0618
                }

                Dictionary<string, string> settings = this.portalController.GetPortalSettings(this.PortalSettings.PortalId);
                string strPayPalURL;
                if (intUserID != -1 && intRoleId != -1 && !string.IsNullOrEmpty(strProcessorUserId))
                {
                    // Sandbox mode
                    if (settings.ContainsKey("paypalsandbox") && !string.IsNullOrEmpty(settings["paypalsandbox"]) && settings["paypalsandbox"].ToLowerInvariant() == "true")
                    {
                        strPayPalURL = "https://www.sandbox.paypal.com/cgi-bin/webscr?";
                    }
                    else
                    {
                        strPayPalURL = "https://www.paypal.com/cgi-bin/webscr?";
                    }

                    if (query != null && query.ContainsKey("cancel"))
                    {
                        // build the cancellation PayPal URL
                        strPayPalURL += "cmd=_subscr-find&alias=" + Globals.HTTPPOSTEncode(strProcessorUserId);
                    }
                    else
                    {
                        strPayPalURL += "cmd=_ext-enter";
                        RoleInfo objRole = RoleController.Instance.GetRole(this.PortalSettings.PortalId, r => r.RoleID == intRoleId);
                        if (objRole.RoleID != -1)
                        {
                            var intTrialPeriod = 1;
                            if (objRole.TrialPeriod != 0)
                            {
                                intTrialPeriod = objRole.TrialPeriod;
                            }

                            var intBillingPeriod = 1;
                            if (objRole.BillingPeriod != 0)
                            {
                                intBillingPeriod = objRole.BillingPeriod;
                            }

                            // explicitely format numbers using en-US so numbers are correctly built
                            var enFormat = new CultureInfo("en-US");
                            var strService = string.Format(enFormat.NumberFormat, "{0:#####0.00}", objRole.ServiceFee);
                            var strTrial = string.Format(enFormat.NumberFormat, "{0:#####0.00}", objRole.TrialFee);
                            if (objRole.BillingFrequency == "O" || objRole.TrialFrequency == "O")
                            {
                                // build the payment PayPal URL
                                strPayPalURL += "&redirect_cmd=_xclick&business=" + Globals.HTTPPOSTEncode(strProcessorUserId);
                                strPayPalURL += "&item_name=" +
                                                Globals.HTTPPOSTEncode(this.PortalSettings.PortalName + " - " + objRole.RoleName + " ( " + objRole.ServiceFee.ToString("#.##") + " " +
                                                                       this.PortalSettings.Currency + " )");
                                strPayPalURL += "&item_number=" + Globals.HTTPPOSTEncode(intRoleId.ToString());
                                strPayPalURL += "&no_shipping=1&no_note=1";
                                strPayPalURL += "&quantity=1";
                                strPayPalURL += "&amount=" + Globals.HTTPPOSTEncode(strService);
                                strPayPalURL += "&currency_code=" + Globals.HTTPPOSTEncode(this.PortalSettings.Currency);
                            }
                            else
                            {
                                // recurring payments
                                // build the subscription PayPal URL
                                strPayPalURL += "&redirect_cmd=_xclick-subscriptions&business=" + Globals.HTTPPOSTEncode(strProcessorUserId);
                                strPayPalURL += "&item_name=" +
                                                Globals.HTTPPOSTEncode(this.PortalSettings.PortalName + " - " + objRole.RoleName + " ( " + objRole.ServiceFee.ToString("#.##") + " " +
                                                                       this.PortalSettings.Currency + " every " + intBillingPeriod + " " + GetBillingFrequencyText(objRole.BillingFrequency) + " )");
                                strPayPalURL += "&item_number=" + Globals.HTTPPOSTEncode(intRoleId.ToString());
                                strPayPalURL += "&no_shipping=1&no_note=1";
                                if (objRole.TrialFrequency != "N")
                                {
                                    strPayPalURL += "&a1=" + Globals.HTTPPOSTEncode(strTrial);
                                    strPayPalURL += "&p1=" + Globals.HTTPPOSTEncode(intTrialPeriod.ToString());
                                    strPayPalURL += "&t1=" + Globals.HTTPPOSTEncode(objRole.TrialFrequency);
                                }

                                strPayPalURL += "&a3=" + Globals.HTTPPOSTEncode(strService);
                                strPayPalURL += "&p3=" + Globals.HTTPPOSTEncode(intBillingPeriod.ToString());
                                strPayPalURL += "&t3=" + Globals.HTTPPOSTEncode(objRole.BillingFrequency);
                                strPayPalURL += "&src=1";
                                strPayPalURL += "&currency_code=" + Globals.HTTPPOSTEncode(this.PortalSettings.Currency);
                            }
                        }

#pragma warning disable CS0618 // the parameterless ListController is deprecated; its replacement is not reachable from here
                        var ctlList = new ListController();
#pragma warning restore CS0618

                        strPayPalURL += "&custom=" + Globals.HTTPPOSTEncode(intUserID.ToString());
                        strPayPalURL += "&first_name=" + Globals.HTTPPOSTEncode(objUserInfo.Profile.FirstName);
                        strPayPalURL += "&last_name=" + Globals.HTTPPOSTEncode(objUserInfo.Profile.LastName);
                        try
                        {
                            if (objUserInfo.Profile.Country == "United States")
                            {
                                ListEntryInfo colList = ctlList.GetListEntryInfo("Region", objUserInfo.Profile.Region);
                                strPayPalURL += "&address1=" +
                                                Globals.HTTPPOSTEncode(Convert.ToString(!string.IsNullOrEmpty(objUserInfo.Profile.Unit) ? objUserInfo.Profile.Unit + " " : string.Empty) +
                                                                       objUserInfo.Profile.Street);
                                strPayPalURL += "&city=" + Globals.HTTPPOSTEncode(objUserInfo.Profile.City);
                                strPayPalURL += "&state=" + Globals.HTTPPOSTEncode(colList.Value);
                                strPayPalURL += "&zip=" + Globals.HTTPPOSTEncode(objUserInfo.Profile.PostalCode);
                            }
                        }
                        catch (Exception ex)
                        {
                            // issue getting user address
                            Logger.PayPalSubscriptionUserAddressException(ex);
                        }

                        // Return URL
                        if (settings.ContainsKey("paypalsubscriptionreturn") && !string.IsNullOrEmpty(settings["paypalsubscriptionreturn"]))
                        {
                            strPayPalURL += "&return=" + Globals.HTTPPOSTEncode(settings["paypalsubscriptionreturn"]);
                        }
                        else
                        {
                            strPayPalURL += "&return=" + Globals.HTTPPOSTEncode(Globals.AddHTTP(Globals.GetDomainName(this.request)));
                        }

                        // Cancellation URL
                        if (settings.ContainsKey("paypalsubscriptioncancelreturn") && !string.IsNullOrEmpty(settings["paypalsubscriptioncancelreturn"]))
                        {
                            strPayPalURL += "&cancel_return=" + Globals.HTTPPOSTEncode(settings["paypalsubscriptioncancelreturn"]);
                        }
                        else
                        {
                            strPayPalURL += "&cancel_return=" + Globals.HTTPPOSTEncode(Globals.AddHTTP(Globals.GetDomainName(this.request)));
                        }

                        // Instant Payment Notification URL. The path is PayPalIpnEndpoint.IpnPath, the
                        // literal legacy URL the IPN endpoint is mapped on, so the notification lands
                        // on the migrated handler.
                        if (settings.ContainsKey("paypalsubscriptionnotifyurl") && !string.IsNullOrEmpty(settings["paypalsubscriptionnotifyurl"]))
                        {
                            strPayPalURL += "&notify_url=" + Globals.HTTPPOSTEncode(settings["paypalsubscriptionnotifyurl"]);
                        }
                        else
                        {
                            strPayPalURL += "&notify_url=" + Globals.HTTPPOSTEncode(Globals.AddHTTP(Globals.GetDomainName(this.request)) + PayPalIpnEndpoint.IpnPath);
                        }

                        strPayPalURL += "&sra=1"; // reattempt on failure
                    }

                    // redirect to PayPal
                    this.NavigationManager.NavigateTo(strPayPalURL, forceLoad: true);
                }
                else
                {
                    if (settings.ContainsKey("paypalsubscriptioncancelreturn") && !string.IsNullOrEmpty(settings["paypalsubscriptioncancelreturn"]))
                    {
                        strPayPalURL = settings["paypalsubscriptioncancelreturn"];
                    }
                    else
                    {
                        strPayPalURL = Globals.AddHTTP(Globals.GetDomainName(this.request));
                    }

                    // redirect to PayPal
                    this.NavigationManager.NavigateTo(strPayPalURL, forceLoad: true);
                }
            }
            catch (NavigationException)
            {
                // The redirect above: during a static server render NavigateTo signals the 302 by
                // throwing, so it must not be swallowed by the handler below (which would turn every
                // successful redirect into an error page).
                throw;
            }
            catch (Exception exc)
            {
                Exceptions.ProcessPageLoadException(exc);
                this.RedirectFailureMessage = exc.Message;
            }
        }
    }
}
