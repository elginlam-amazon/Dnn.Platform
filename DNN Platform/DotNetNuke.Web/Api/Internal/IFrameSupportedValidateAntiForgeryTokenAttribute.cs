// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Linq;

    using Microsoft.AspNetCore.Mvc.Filters;

    /// <summary>An implementation of <see cref="ValidateAntiForgeryTokenAttribute"/> that supports iframes.</summary>
    public class IFrameSupportedValidateAntiForgeryTokenAttribute : ValidateAntiForgeryTokenAttribute
    {
        /// <inheritdoc />
        protected override Tuple<bool, string> IsAuthorized(FilterContext actionContext)
        {
            var result = base.IsAuthorized(actionContext);
            if (result.Item1)
            {
                return SuccessResult;
            }

            try
            {
                var token = actionContext.HttpContext.Request.Query["__RequestVerificationToken"].FirstOrDefault() ?? string.Empty;

                var cookieValue = GetAntiForgeryCookieValue(actionContext);

                AntiForgery.Instance.Validate(cookieValue, token);
            }
            catch (Exception e)
            {
                return new Tuple<bool, string>(false, e.Message);
            }

            return SuccessResult;
        }
    }
}
