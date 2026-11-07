// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Admin.Sales
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Log.EventLog;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Maps PayPal's Instant Payment Notification (IPN) callback, which was served by the
    /// <c>paypalipn.aspx</c> WebForms page (<c>PayPalIPN : PageBase</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a server-to-server callback with no user interface whatsoever - the page never emitted
    /// any markup - so it becomes a minimal API endpoint rather than a component. The URL is preserved
    /// <b>literally and case sensitively</b> as <see cref="IpnPath"/>: PayPal is configured with that
    /// exact notification URL in the merchant account (and by
    /// <c>PayPalSubscription</c>/<c>Purchase</c>, which send it as <c>notify_url</c>), so it cannot be
    /// renamed.
    /// </para>
    /// <para>
    /// The <c>OnInit</c>/<c>InitializeComponent</c> pair of the page did nothing beyond the
    /// <see cref="DotNetNuke.Framework.PageBase"/> plumbing (<c>InitializeComponent</c> had an empty
    /// body), and the DI constructor only resolved <c>IPortalController</c>,
    /// <c>IApplicationStatusInfo</c>, <c>IHostSettings</c> and <c>IHostSettingsService</c>; of those,
    /// the handler genuinely uses <c>IPortalController</c> (portal, portal settings) and
    /// <c>IHostSettingsService</c> (the host <c>ProcessorUserId</c>), which it now takes from the
    /// request service scope. <c>IApplicationStatusInfo</c>/<c>IHostSettings</c> were consumed only by
    /// the page base class, which no longer exists on this path.
    /// </para>
    /// <para>
    /// SECURITY: the <c>cmd=_notify-validate</c> postback to PayPal is the only thing that proves the
    /// notification really came from PayPal, because anybody can POST to this public URL. It is
    /// therefore performed <b>before</b> any order or role fulfilment, exactly as in the original, and
    /// fulfilment only runs when PayPal answers <c>VERIFIED</c>. The outbound call goes through
    /// <see cref="IHttpClientFactory"/> (the <see cref="Globals.ExternalRequestHttpClientName"/>
    /// registration, which carries the DNN timeout/user-agent/proxy host settings that
    /// <c>Globals.GetExternalRequest</c> used to apply) and is awaited - never
    /// sync-over-async - because this runs on the request pipeline.
    /// </para>
    /// </remarks>
    public static class PayPalIpnEndpoint
    {
        /// <summary>The preserved, case-sensitive IPN callback URL that PayPal is configured with.</summary>
        public const string IpnPath = "/admin/Sales/paypalipn.aspx";

        /// <summary>Maps the PayPal IPN callback onto its original URL.</summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <returns>The same <paramref name="endpoints"/>, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
        public static IEndpointRouteBuilder MapPayPalIpnEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            // MapPost: PayPal always delivers an IPN as a form POST. The page accepted any verb, but
            // its whole body walked Request.Form, so a GET was already a no-op.
            endpoints.MapPost(IpnPath, HandleRequestAsync);

            return endpoints;
        }

        private static async Task HandleRequestAsync(HttpContext context)
        {
            var services = context.RequestServices;
            var portalController = services.GetRequiredService<IPortalController>();
            var hostSettingsService = services.GetRequiredService<IHostSettingsService>();
            var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
            var hostSettings = services.GetRequiredService<IHostSettings>();
            var appStatus = services.GetRequiredService<IApplicationStatusInfo>();

            try
            {
                var portalSettings = portalController.GetCurrentSettings();

                var blnValid = true;

                // string strTransactionID;
                var intRoleID = 0;
                var intPortalID = portalSettings.PortalId;
                var intUserID = 0;

                // string strDescription;
                double dblAmount = 0;

                // string strEmail;
                var blnCancel = false;
                var strPayPalID = Null.NullString;
                var strPost = new StringBuilder("cmd=_notify-validate");

                // Request.Form -> the awaited form collection; the page read it synchronously off
                // System.Web's already-parsed form.
                var form = await context.Request.ReadFormAsync();
                foreach (var strName in form.Keys)
                {
                    var strValue = form[strName].ToString();
                    switch (strName)
                    {
                        case "txn_type": // get the transaction type
                            var strTransactionType = strValue;
                            switch (strTransactionType)
                            {
                                case "subscr_signup":
                                case "subscr_payment":
                                case "web_accept":
                                    break;
                                case "subscr_cancel":
                                    blnCancel = true;
                                    break;
                                default:
                                    blnValid = false;
                                    break;
                            }

                            break;
                        case "payment_status": // verify the status
                            if (strValue != "Completed")
                            {
                                blnValid = false;
                            }

                            break;
                        case "txn_id": // verify the transaction id for duplicates
                                       //                            strTransactionID = strValue;
                            break;
                        case "receiver_email": // verify the PayPalId
                            strPayPalID = strValue;
                            break;
                        case "mc_gross": // verify the price
                            dblAmount = double.Parse(strValue);
                            break;
                        case "item_number": // get the RoleID
                            intRoleID = int.Parse(strValue);

                            // RoleInfo objRole = objRoles.GetRole(intRoleID, intPortalID);
                            break;
                        case "item_name": // get the product description
                                          //                            strDescription = strValue;
                            break;
                        case "custom": // get the UserID
                            intUserID = int.Parse(strValue);
                            break;
                        case "email": // get the email
                                      //                            strEmail = strValue;
                            break;
                    }

                    // reconstruct post for postback validation
                    strPost.Append(string.Format("&{0}={1}", Globals.HTTPPOSTEncode(strName), Globals.HTTPPOSTEncode(strValue)));
                }

                // postback to verify the source. SECURITY CONTROL: nothing below this block may run
                // until PayPal itself has confirmed the notification, so this stays ahead of every
                // fulfilment path and has no bypass.
                if (blnValid)
                {
                    Dictionary<string, string> settings = portalController.GetPortalSettings(portalSettings.PortalId);
                    string strPayPalURL;

                    // Sandbox mode
                    if (settings.ContainsKey("paypalsandbox") && !string.IsNullOrEmpty(settings["paypalsandbox"]) && settings["paypalsandbox"].Equals("true", StringComparison.InvariantCultureIgnoreCase))
                    {
                        strPayPalURL = "https://www.sandbox.paypal.com/cgi-bin/webscr?";
                    }
                    else
                    {
                        strPayPalURL = "https://www.paypal.com/cgi-bin/webscr?";
                    }

                    // Replaces Globals.GetExternalRequest + the StreamWriter/HttpWebResponse pair: a
                    // pooled, host-configured client from IHttpClientFactory, fully awaited.
                    var client = httpClientFactory.CreateClient(Globals.ExternalRequestHttpClientName);
                    using var content = new StringContent(strPost.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded");
                    using var response = await client.PostAsync(strPayPalURL, content, context.RequestAborted);
                    var strResponse = await response.Content.ReadAsStringAsync(context.RequestAborted);

                    switch (strResponse)
                    {
                        case "VERIFIED":
                            break;
                        default:
                            // possible fraud
                            blnValid = false;
                            break;
                    }
                }

                if (blnValid)
                {
                    var intAdministratorRoleId = 0;
                    var strProcessorID = Null.NullString;
                    PortalInfo objPortalInfo = portalController.GetPortal(intPortalID);
                    if (objPortalInfo != null)
                    {
                        intAdministratorRoleId = objPortalInfo.AdministratorRoleId;

                        // PortalInfo.ProcessorUserId/HostFee are [Obsolete] with no replacement, and
                        // they are the payment-processor identity this flow verifies against: there is
                        // nothing else to read them from, so the deprecation is suppressed rather than
                        // the check being dropped.
#pragma warning disable CS0618
                        strProcessorID = objPortalInfo.ProcessorUserId.ToLowerInvariant();
#pragma warning restore CS0618
                    }

                    if (intRoleID == intAdministratorRoleId)
                    {
                        // admin portal renewal
                        strProcessorID = hostSettingsService.GetString("ProcessorUserId").ToLowerInvariant();
#pragma warning disable CS0618
                        var portalPrice = objPortalInfo.HostFee;
#pragma warning restore CS0618
                        if ((portalPrice.ToString() == dblAmount.ToString()) && (WebUtility.UrlDecode(strPayPalID.ToLowerInvariant()) == strProcessorID))
                        {
                            // The obsolete GetActivePortalLanguage(int) overload only resolved IHostSettings/
                            // IApplicationStatusInfo from the service locator and called the overload
                            // below, so the language passed to UpdatePortalExpiry is unchanged.
                            portalController.UpdatePortalExpiry(intPortalID, DotNetNuke.Entities.Portals.PortalController.GetActivePortalLanguage(hostSettings, appStatus, intPortalID));
                        }
                        else
                        {
                            // LogPortalID/LogUserID are now explicit ILogInfo members (LogPortalId/
                            // LogUserId); the logged values are unchanged.
                            var log = new LogInfo
                            {
                                LogPortalName = portalSettings.PortalName,
                                LogTypeKey = nameof(EventLogType.POTENTIAL_PAYPAL_PAYMENT_FRAUD),
                            };
                            ((ILogInfo)log).LogPortalId = intPortalID;
                            ((ILogInfo)log).LogUserId = intUserID;
                            LogController.Instance.AddLog(log);
                        }
                    }
                    else
                    {
                        // user subscription
                        RoleInfo objRoleInfo = RoleController.Instance.GetRole(intPortalID, r => r.RoleID == intRoleID);
                        var rolePrice = objRoleInfo.ServiceFee;
                        var trialPrice = objRoleInfo.TrialFee;
                        if ((rolePrice.ToString() == dblAmount.ToString() || trialPrice.ToString() == dblAmount.ToString()) && (WebUtility.UrlDecode(strPayPalID.ToLowerInvariant()) == strProcessorID))
                        {
                            RoleController.Instance.UpdateUserRole(intPortalID, intUserID, intRoleID, RoleStatus.Approved, false, blnCancel);
                        }
                        else
                        {
                            // LogPortalID/LogUserID are now explicit ILogInfo members (LogPortalId/
                            // LogUserId); the logged values are unchanged.
                            var log = new LogInfo
                            {
                                LogPortalName = portalSettings.PortalName,
                                LogTypeKey = nameof(EventLogType.POTENTIAL_PAYPAL_PAYMENT_FRAUD),
                            };
                            ((ILogInfo)log).LogPortalId = intPortalID;
                            ((ILogInfo)log).LogUserId = intUserID;
                            LogController.Instance.AddLog(log);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Exceptions.ProcessPageLoadException(exc);
            }
        }
    }
}
