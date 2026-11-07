// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Antiforgery;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Options;
    using Microsoft.Extensions.Primitives;

    /// <summary>The default <see cref="IAntiForgery"/> implementation, backed by the ASP.NET Core <see cref="IAntiforgery"/> service.</summary>
    /// <remarks>
    /// Replaces the <c>System.Web.Helpers.AntiForgery</c> wrapper. The cookie, header and form field names
    /// are taken from the configured <see cref="AntiforgeryOptions"/>, which the host pins to the legacy
    /// <c>__RequestVerificationToken</c> / <c>RequestVerificationToken</c> names so that existing DNN
    /// client script keeps working unchanged.
    /// </remarks>
    internal class AntiForgeryImpl : IAntiForgery
    {
        /// <summary>The legacy <c>System.Web.Helpers.AntiForgeryConfig.CookieName</c> default.</summary>
        internal const string DefaultCookieName = "__RequestVerificationToken";

        /// <summary>The header name the DNN services framework client script sends the request token in.</summary>
        internal const string DefaultHeaderName = "RequestVerificationToken";

        /// <summary>The form field name the DNN iframe upload flow posts the request token in.</summary>
        internal const string DefaultFormFieldName = "__RequestVerificationToken";

        private readonly IAntiforgery antiforgery;
        private readonly AntiforgeryOptions options;
        private readonly IServiceProvider serviceProvider;

        /// <summary>Initializes a new instance of the <see cref="AntiForgeryImpl"/> class.</summary>
        /// <param name="antiforgery">The ASP.NET Core anti-forgery service.</param>
        /// <param name="options">The configured anti-forgery options.</param>
        /// <param name="serviceProvider">The service provider used for the validation context.</param>
        public AntiForgeryImpl(IAntiforgery antiforgery, IOptions<AntiforgeryOptions> options, IServiceProvider serviceProvider)
        {
            this.antiforgery = antiforgery ?? throw new ArgumentNullException(nameof(antiforgery));
            this.options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public string CookieName => string.IsNullOrEmpty(this.options.Cookie.Name)
            ? DefaultCookieName
            : this.options.Cookie.Name;

        /// <inheritdoc />
        public Task ValidateAsync(string cookieToken, string headerToken)
        {
            if (string.IsNullOrEmpty(cookieToken) || string.IsNullOrEmpty(headerToken))
            {
                throw new AntiforgeryValidationException("The required anti-forgery cookie token or request token was not supplied.");
            }

            // ASP.NET Core validates the cookie/request token pair through a request, so the supplied pair
            // is presented on a request carrying the configured cookie name, header name and form field
            // name. Validation faults (AntiforgeryValidationException) when the pair does not match, which
            // preserves the original Validate(cookieToken, headerToken) contract.
            var validationContext = new DefaultHttpContext { RequestServices = this.serviceProvider };
            validationContext.Request.Method = HttpMethods.Post;
            validationContext.Request.Headers.Cookie = string.Format(
                CultureInfo.InvariantCulture, "{0}={1}", this.CookieName, cookieToken);
            validationContext.Request.Headers[string.IsNullOrEmpty(this.options.HeaderName) ? DefaultHeaderName : this.options.HeaderName] = headerToken;
            validationContext.Request.ContentType = "application/x-www-form-urlencoded";
            validationContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>
            {
                { string.IsNullOrEmpty(this.options.FormFieldName) ? DefaultFormFieldName : this.options.FormFieldName, headerToken },
            });

            return this.antiforgery.ValidateRequestAsync(validationContext);
        }
    }
}
