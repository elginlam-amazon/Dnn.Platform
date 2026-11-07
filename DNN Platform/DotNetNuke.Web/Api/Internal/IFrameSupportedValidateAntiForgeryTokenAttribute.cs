// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>An implementation of <see cref="ValidateAntiForgeryTokenAttribute"/> that supports iframes.</summary>
    /// <remarks>
    /// This is the relaxation that makes the <c>DnnFileUpload</c> iframe flow work: an iframe POST cannot
    /// set a request header, so the token arrives as the <c>__RequestVerificationToken</c> field of the
    /// posted form (or, as on net48, as a query string value). The base check runs first and this
    /// fallback is only consulted when it failed, so no request is accepted that the base filter would
    /// have accepted on stricter grounds — the relaxation only widens where the request token may be
    /// read from, never which token is accepted.
    /// </remarks>
    public class IFrameSupportedValidateAntiForgeryTokenAttribute : ValidateAntiForgeryTokenAttribute
    {
        private const string RequestVerificationTokenName = "__RequestVerificationToken";

        /// <inheritdoc />
        protected override async Task<Tuple<bool, string>> IsAuthorizedAsync(AuthorizationFilterContext actionContext)
        {
            var result = await base.IsAuthorizedAsync(actionContext);
            if (result.Item1)
            {
                return SuccessResult;
            }

            try
            {
                var request = actionContext?.HttpContext?.Request;
                var token = request?.Query[RequestVerificationTokenName].ToString() ?? string.Empty;

                // The iframe upload flow posts the token as a form field; the legacy handler only looked
                // at the query string because the Web API 2 form reader was not available at this stage.
                // Both are accepted now, which is what makes the DnnFileUpload/FileUploadController
                // multipart POST work.
                if (string.IsNullOrEmpty(token) && request?.HasFormContentType == true)
                {
                    var form = await request.ReadFormAsync();
                    token = form[RequestVerificationTokenName].ToString();
                }

                var cookieValue = GetAntiForgeryCookieValue(actionContext);

                await AntiForgery.Instance.ValidateAsync(cookieValue, token);
            }
            catch (Exception e)
            {
                return new Tuple<bool, string>(false, e.Message);
            }

            return SuccessResult;
        }
    }
}
