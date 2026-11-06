// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.HttpModules.Services
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// Initializes DNN for requests to its ASP.NET Web API Services Framework and scrubs the
    /// response headers it must not reveal. This is the ASP.NET Core replacement for the
    /// <c>ServicesModule</c> <c>IHttpModule</c>, which hooked <c>BeginRequest</c> and
    /// <c>PreSendRequestHeaders</c>.
    /// </summary>
    /// <remarks>
    /// <b>Pipeline position:</b> pre-routing, so that <c>Initialize.Init</c> has run (and the
    /// install/upgrade redirect has been applied) before a service endpoint is selected. The
    /// <c>PreSendRequestHeaders</c> work moves to a <see cref="HttpResponse.OnStarting(Func{Task})"/>
    /// callback, which is the Core equivalent hook — the last point at which headers can still
    /// be modified.
    /// </remarks>
    public sealed class DnnServicesFrameworkMiddleware
    {
        /// <inheritdoc cref="Globals.ServicesFrameworkRegex"/>
        public static readonly Regex ServiceApi = Globals.ServicesFrameworkRegex;

        private readonly RequestDelegate next;

        /// <summary>Initializes a new instance of the <see cref="DnnServicesFrameworkMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public DnnServicesFrameworkMiddleware(RequestDelegate next)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>Initializes DNN for Services Framework URLs and registers the header scrub.</summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var rawUrl = context.Request.GetRawUrl();
            if (!string.IsNullOrEmpty(rawUrl) && ServiceApi.IsMatch(rawUrl.ToLowerInvariant()))
            {
                Initialize.Init(context);
            }

            context.Response.OnStarting(
                static state =>
                {
                    var ctx = (HttpContext)state;

                    // WEB API should not send cookies and other specific headers in response;
                    // they reveal too much info and are security risk
                    var headers = ctx.Response.Headers;
                    headers.Remove("Server");

                    // DNN-8325
                    ////if (ServiceApi.IsMatch(ctx.Request.GetRawUrl().ToLowerInvariant()))
                    ////{
                    ////   headers.Remove("Set-Cookie");
                    ////}
                    return Task.CompletedTask;
                },
                context);

            await this.next(context);
        }
    }
}
