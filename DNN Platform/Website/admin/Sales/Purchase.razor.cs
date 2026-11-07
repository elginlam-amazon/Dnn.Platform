// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Admin.Sales
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Globalization;
    using System.Net.Http;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Lists;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.Exceptions;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Forms;
    using Microsoft.Extensions.DependencyInjection;

    using Host = DotNetNuke.Entities.Host.Host;

    /// <summary>A control which allows a user to purchase access to a role.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>purchase.ascx</c> + <c>Purchase.ascx.cs</c> + <c>Purchase.ascx.designer.cs</c>; the
    /// markup now lives in <c>Purchase.razor</c>. The designer fields map as follows:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>lblServiceName</c>/<c>lblDescription</c>/<c>lblFee</c>/<c>lblFeeCurrency</c>/<c>lblFrequency</c>/<c>lblTotal</c>/<c>lblTotalCurrency</c> become <see cref="ServiceName"/>, <see cref="Description"/>, <see cref="Fee"/>, <see cref="FeeCurrency"/>, <see cref="Frequency"/>, <see cref="Total"/> and <see cref="TotalCurrency"/>.</description></item>
    /// <item><description><c>txtUnits.Text</c>/<c>txtUnits.Enabled</c> become <c>Model.Units</c> and <see cref="UnitsEnabled"/>.</description></item>
    /// <item><description><c>valUnits1</c> (required), <c>valUnits2</c> (<c>DataTypeCheck</c>/<c>Integer</c>) and <c>valUnits3</c> (<c>GreaterThan 0</c>) become the data annotations of <see cref="PurchaseModel"/>, which keep the original error text.</description></item>
    /// <item><description><c>cmdUpdate</c>, <c>cmdPurchase</c> and <c>cmdCancel</c> become the three named submit buttons handled by <see cref="HandleSubmitAsync"/>; the two that declared <c>CausesValidation="False"</c> still bypass validation.</description></item>
    /// </list>
    /// <para>
    /// <c>ViewState</c> has no replacement, so the two things it carried are re-established per
    /// request: the role (and therefore the fee, description and frequency) is re-read from
    /// <see cref="RoleController"/> on every render using the <c>RoleID</c> query string, and the
    /// stored URL referrer travels in a hidden form field. <c>Page.IsPostBack</c> becomes
    /// <see cref="IsPostBack"/>, which is true exactly when the form was posted back (the mapped form
    /// model is present).
    /// </para>
    /// <para>
    /// The PayPal <c>notify_url</c> is built from <see cref="PayPalIpnEndpoint.IpnPath"/> - the literal
    /// legacy path the migrated IPN endpoint is mapped on - rather than from a hand written string, so
    /// the checkout round trip cannot drift out of step with the endpoint.
    /// </para>
    /// </remarks>
    [DnnDeprecated(10, 0, 2, "No replacement")]
    public partial class Purchase : PortalModuleBase
    {
        /// <summary>The name of the submit field which identifies the clicked command button.</summary>
        internal const string ActionField = "purchaseAction";

        /// <summary>The <c>cmdUpdate</c> command.</summary>
        internal const string UpdateAction = "update";

        /// <summary>The <c>cmdPurchase</c> command.</summary>
        internal const string PurchaseAction = "purchase";

        /// <summary>The <c>cmdCancel</c> command.</summary>
        internal const string CancelAction = "cancel";

        private readonly INavigationManager dnnNavigationManager;
        private int roleID = -1;

        /// <summary>Initializes a new instance of the <see cref="Purchase"/> class.</summary>
        public Purchase()
        {
            this.dnnNavigationManager = this.DependencyProvider.GetRequiredService<INavigationManager>();
        }

        /// <summary>Gets or sets the posted form state (was <c>txtUnits</c> plus the <c>ViewState["UrlReferrer"]</c> slot).</summary>
        [SupplyParameterFromForm]
        public PurchaseModel Model { get; set; }

        /// <summary>Gets the role name (was <c>lblServiceName.Text</c>).</summary>
        protected string ServiceName { get; private set; } = string.Empty;

        /// <summary>Gets the role description (was <c>lblDescription.Text</c>).</summary>
        protected string Description { get; private set; } = string.Empty;

        /// <summary>Gets the formatted fee (was <c>lblFee.Text</c>).</summary>
        protected string Fee { get; private set; } = string.Empty;

        /// <summary>Gets the fee currency (was <c>lblFeeCurrency.Text</c>).</summary>
        protected string FeeCurrency { get; private set; } = string.Empty;

        /// <summary>Gets the billing frequency text (was <c>lblFrequency.Text</c>).</summary>
        protected string Frequency { get; private set; } = string.Empty;

        /// <summary>Gets the formatted total (was <c>lblTotal.Text</c>).</summary>
        protected string Total { get; private set; } = string.Empty;

        /// <summary>Gets the total currency (was <c>lblTotalCurrency.Text</c>).</summary>
        protected string TotalCurrency { get; private set; } = string.Empty;

        /// <summary>Gets a value indicating whether the units box accepts input (was <c>txtUnits.Enabled</c>; false for a one-time fee).</summary>
        protected bool UnitsEnabled { get; private set; } = true;

        /// <summary>Gets a value indicating whether this render is the result of a form post (was <c>Page.IsPostBack</c>).</summary>
        protected bool IsPostBack { get; private set; }

        /// <summary>Gets or sets the navigation manager used to issue the redirects <c>Response.Redirect</c> used to issue.</summary>
        [Inject]
        protected Microsoft.AspNetCore.Components.NavigationManager Navigation { get; set; }

        /// <summary>Gets or sets the factory for the outbound currency conversion call.</summary>
        [Inject]
        protected IHttpClientFactory HttpClientFactory { get; set; }

        /// <inheritdoc />
        /// <remarks>Was <c>OnInit</c>, whose only statement beyond the base call was the empty <c>InitializeComponent</c>.</remarks>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.InitializeComponent();
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                // The form model is only supplied when this render is a post back; on the first GET it
                // is created here with the defaults the WebForms page applied on !IsPostBack.
                this.IsPostBack = this.Model is not null;
                this.Model ??= new PurchaseModel();

                var query = this.Request?.Query;
                if (query is not null && query.ContainsKey("RoleID"))
                {
                    this.roleID = int.Parse(query["RoleID"].ToString());
                }

                if (this.roleID != -1)
                {
                    RoleInfo objRole = RoleController.Instance.GetRole(this.PortalSettings.PortalId, r => r.RoleID == this.roleID);

                    if (objRole.RoleID != -1)
                    {
                        this.ServiceName = objRole.RoleName;
                        if (!Null.IsNull(objRole.Description))
                        {
                            this.Description = objRole.Description;
                        }

                        if (this.roleID == this.PortalSettings.AdministratorRoleId)
                        {
                            if (!Null.IsNull(this.PortalSettings.HostFee))
                            {
                                this.Fee = this.PortalSettings.HostFee.ToString("#,##0.00");
                            }
                        }
                        else
                        {
                            if (!Null.IsNull(objRole.ServiceFee))
                            {
                                this.Fee = objRole.ServiceFee.ToString("#,##0.00");
                            }
                        }

                        if (!Null.IsNull(objRole.BillingFrequency))
                        {
#pragma warning disable CS0618 // the parameterless ListController is deprecated; its replacement is not reachable from here
                            var ctlEntry = new ListController();
#pragma warning restore CS0618
                            ListEntryInfo entry = ctlEntry.GetListEntryInfo("Frequency", objRole.BillingFrequency);
                            this.Frequency = entry.Text;
                        }

                        if (!this.IsPostBack)
                        {
                            this.Model.Units = "1";
                        }

                        if (objRole.BillingFrequency == "O")
                        {
                            // one-time fee
                            this.UnitsEnabled = false;
                        }
                    }
                    else
                    {
                        // security violation attempt to access item not related to this Module
                        this.Navigation.NavigateTo(this.dnnNavigationManager.NavigateURL(), forceLoad: true);
                        return;
                    }
                }

                if (!this.IsPostBack)
                {
                    // Store URL Referrer to return to portal
                    this.Model.UrlReferrer = this.Request?.Headers.Referer.ToString() ?? string.Empty;
                }

                this.TotalCurrency = this.FeeCurrency = this.roleID == this.PortalSettings.AdministratorRoleId
                    ? Host.HostCurrency
                    : this.PortalSettings.Currency;

                this.RecalculateTotal();
            }
            catch (NavigationException)
            {
                // The redirect above; during a static server render NavigateTo signals the response by
                // throwing, which must not be reported as a module load error.
                throw;
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>
        /// Converts an amount between two currencies. Dead code at the baseline (it was a private
        /// member of the control that nothing called) and preserved as such, now modernized: the
        /// <c>HttpWebRequest</c>/<c>StreamWriter</c>/<c>HttpWebResponse</c> trio is replaced by an
        /// awaited <see cref="IHttpClientFactory"/> call.
        /// </summary>
        /// <param name="amount">The amount to convert.</param>
        /// <param name="fromCurrency">The source currency code.</param>
        /// <param name="toCurrency">The target currency code.</param>
        /// <returns>The converted amount, or <c>0</c> when the conversion failed.</returns>
        protected async Task<double> ConvertCurrencyAsync(string amount, string fromCurrency, string toCurrency)
        {
            var strPost = "Amount=" + amount + "&From=" + fromCurrency + "&To=" + toCurrency;
            double retValue = 0;
            try
            {
                var client = this.HttpClientFactory.CreateClient(Globals.ExternalRequestHttpClientName);
                using var content = new StringContent(strPost, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
                using var response = await client.PostAsync("http://www.xe.com/ucc/convert.cgi", content);
                var strResponse = await response.Content.ReadAsStringAsync();

                var intPos1 = strResponse.IndexOf(toCurrency + "</B>", StringComparison.Ordinal);
                var intPos2 = strResponse.LastIndexOf("<B>", intPos1, StringComparison.Ordinal);

                retValue = Convert.ToDouble(strResponse.Substring(intPos2 + 3, (intPos1 - intPos2) - 4));
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }

            return retValue;
        }

        /// <summary>
        /// Dispatches the posted command, replacing the <c>cmdUpdate</c>/<c>cmdPurchase</c>/
        /// <c>cmdCancel</c> click handlers and the WebForms <c>CausesValidation</c> semantics.
        /// </summary>
        /// <param name="editContext">The form's edit context.</param>
        /// <returns>A task.</returns>
        private Task HandleSubmitAsync(EditContext editContext)
        {
            var action = this.Request?.Form[ActionField].ToString() ?? UpdateAction;

            switch (action)
            {
                case CancelAction:
                    // cmdCancel: CausesValidation="False".
                    this.CmdCancelClick();
                    break;

                case PurchaseAction:
                    // cmdPurchase was the only button that validated; Page.IsValid becomes the
                    // EditContext validation result.
                    if (editContext.Validate())
                    {
                        this.RecalculateTotal();
                        this.CmdPurchaseClick();
                    }

                    break;

                default:
                    // cmdUpdate: CausesValidation="False". The WebForms Update did its work in
                    // OnLoad (which recomputed the total from the posted units) and had an empty
                    // handler, so recomputing here is the whole of its behavior.
                    this.RecalculateTotal();
                    break;
            }

            return Task.CompletedTask;
        }

        /// <summary>Was the tail of <c>OnLoad</c>: total = fee * units, formatted as before.</summary>
        private void RecalculateTotal()
        {
            if (string.IsNullOrEmpty(this.Fee) ||
                !double.TryParse(this.Fee, NumberStyles.Any, CultureInfo.CurrentCulture, out var fee) ||
                !double.TryParse(this.Model.Units, NumberStyles.Any, CultureInfo.CurrentCulture, out var units))
            {
                // Convert.ToDouble threw here in the original and the exception was reported by the
                // OnLoad catch; an unparseable units value is already reported by the validators, so
                // the total is simply left blank instead of replacing the form with an error.
                this.Total = string.Empty;
                return;
            }

            this.Total = (fee * units).ToString("#.##");
        }

        private void InitializeComponent()
        {
        }

        private void CmdPurchaseClick()
        {
            try
            {
                var strPaymentProcessor = string.Empty;
                var strProcessorUserId = string.Empty;

                PortalInfo objPortalInfo = PortalController.Instance.GetPortal(this.PortalSettings.PortalId);
                if (objPortalInfo != null)
                {
                    // PortalInfo.PaymentProcessor/ProcessorUserId are [Obsolete] with no replacement
                    // and are the payment-processor configuration this checkout needs, so the
                    // deprecation is suppressed rather than the values being dropped.
#pragma warning disable CS0618
                    strPaymentProcessor = objPortalInfo.PaymentProcessor;
                    strProcessorUserId = objPortalInfo.ProcessorUserId;
#pragma warning restore CS0618
                }

                if (strPaymentProcessor == "PayPal")
                {
                    // build secure PayPal URL
                    var strPayPalURL = "https://www.paypal.com/xclick/business=" + Globals.HTTPPOSTEncode(strProcessorUserId);
                    strPayPalURL = strPayPalURL + "&item_name=" +
                                   Globals.HTTPPOSTEncode(this.PortalSettings.PortalName + " - " + this.Description + " ( " + this.Model.Units + " units @ " + this.Fee + " " + this.FeeCurrency +
                                                          " per " + this.Frequency + " )");
                    strPayPalURL = strPayPalURL + "&item_number=" + Globals.HTTPPOSTEncode(Convert.ToString(this.roleID));
                    strPayPalURL = strPayPalURL + "&quantity=1";
                    strPayPalURL = strPayPalURL + "&custom=" + Globals.HTTPPOSTEncode(this.UserInfo.UserID.ToString());
                    strPayPalURL = strPayPalURL + "&amount=" + Globals.HTTPPOSTEncode(this.Total);
                    strPayPalURL = strPayPalURL + "&currency_code=" + Globals.HTTPPOSTEncode(this.TotalCurrency);
                    strPayPalURL = strPayPalURL + "&return=" + Globals.HTTPPOSTEncode("http://" + Globals.GetDomainName(this.Request));
                    strPayPalURL = strPayPalURL + "&cancel_return=" + Globals.HTTPPOSTEncode("http://" + Globals.GetDomainName(this.Request));
                    strPayPalURL = strPayPalURL + "&notify_url=" + Globals.HTTPPOSTEncode("http://" + Globals.GetDomainName(this.Request) + PayPalIpnEndpoint.IpnPath);
                    strPayPalURL = strPayPalURL + "&undefined_quantity=&no_note=1&no_shipping=1";

                    // redirect to PayPal
                    this.Navigation.NavigateTo(strPayPalURL, forceLoad: true);
                }
            }
            catch (NavigationException)
            {
                throw;
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        private void CmdCancelClick()
        {
            try
            {
                this.Navigation.NavigateTo(Convert.ToString(this.Model.UrlReferrer), forceLoad: true);
            }
            catch (NavigationException)
            {
                throw;
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>
        /// The posted state of <c>purchase.ascx</c>'s form: the units text box and the referrer slot
        /// <c>ViewState["UrlReferrer"]</c> used to hold. The annotations reproduce <c>valUnits1</c>,
        /// <c>valUnits2</c> and <c>valUnits3</c>, keeping their <c>ErrorMessage</c> text (without the
        /// leading <c>&lt;br&gt;</c>, which was layout rather than message).
        /// </summary>
        public sealed class PurchaseModel : IValidatableObject
        {
            private const string UnitsError = "You Must Enter The Number Of Units You Wish To Purchase";

            /// <summary>Gets or sets the number of units to purchase (was <c>txtUnits.Text</c>).</summary>
            [Required(ErrorMessage = UnitsError)]
            [RegularExpression(@"^[+-]?\d+$", ErrorMessage = UnitsError)]
            public string Units { get; set; } = string.Empty;

            /// <summary>Gets or sets the stored URL referrer (was <c>ViewState["UrlReferrer"]</c>).</summary>
            public string UrlReferrer { get; set; } = string.Empty;

            /// <inheritdoc />
            /// <remarks>Reproduces <c>valUnits3</c>: <c>Operator="GreaterThan" ValueToCompare="0"</c>.</remarks>
            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (int.TryParse(this.Units, out var units) && units <= 0)
                {
                    yield return new ValidationResult(UnitsError, new[] { nameof(this.Units) });
                }
            }
        }
    }
}
