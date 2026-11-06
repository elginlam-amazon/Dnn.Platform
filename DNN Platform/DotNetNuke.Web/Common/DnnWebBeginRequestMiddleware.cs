// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Common.Internal
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using DotNetNuke.Instrumentation;
    using DotNetNuke.Security.Authentication;
    using DotNetNuke.Security.Cookies;
    using DotNetNuke.Services.Installer.Blocker;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The port of <c>DotNetNukeHttpApplication.Application_BeginRequest</c> and
    /// <c>Application_Error</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>BeginRequest</c> did three things. Two of them already have a home on modern .NET:
    /// <c>Initialize.Init(app)</c> is called by <c>DotNetNuke.HttpModules</c>'
    /// <c>DnnServicesFrameworkMiddleware</c>/<c>DnnRequestFilterMiddleware</c>/
    /// <c>BasicUrlRewriter</c>, and <c>Initialize.RunSchedule(request)</c> is replaced by the
    /// platform's <c>SchedulerHostedService</c> (the scheduler no longer needs a request to tick).
    /// The third — expiring an authentication cookie whose persisted counterpart has already
    /// expired — has no other home, and is what this middleware carries, with the original
    /// static-asset and install-request short-circuits intact.
    /// </para>
    /// <para>
    /// <c>Application_Error</c>'s "dump all application errors" logging is carried by the
    /// <c>try</c>/<c>catch</c> below: it logs with the very same log messages and then rethrows, so
    /// the platform's <c>UseDnnExceptionHandler</c> branch still renders the DNN error page. The
    /// exception is never swallowed.
    /// </para>
    /// </remarks>
    public sealed class DnnWebBeginRequestMiddleware
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<DnnWebBeginRequestMiddleware>();

        /// <summary>The request endings which are served without any DNN request initialization.</summary>
        private static readonly string[] Endings = [".css", ".gif", ".jpeg", ".jpg", ".js", ".png", "scriptresource.axd", "webresource.axd",];

        private readonly RequestDelegate next;
        private readonly IAuthenticationCookieService cookieService;

        /// <summary>Initializes a new instance of the <see cref="DnnWebBeginRequestMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="cookieService">The authentication cookie service, which replaces <c>FormsAuthentication</c>.</param>
        public DnnWebBeginRequestMiddleware(RequestDelegate next, IAuthenticationCookieService cookieService)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.cookieService = cookieService ?? throw new ArgumentNullException(nameof(cookieService));
        }

        /// <summary>Invokes the middleware.</summary>
        /// <param name="context">The current request.</param>
        /// <returns>A task that completes when the rest of the pipeline has run.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            try
            {
                this.BeginRequest(context);
            }
            catch (Exception exception)
            {
                // BeginRequest never failed a request on net48 either: the runtime raised
                // Application_Error, which only logged. Keep that behaviour.
                Logger.ApplicationLogApplicationError(exception);
            }

            try
            {
                await this.next(context).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // Application_Error parity: log every unhandled error, then let the platform's
                // exception-handler branch decide what the client sees.
                Logger.ApplicationDumpingAllApplicationErrors();
                Logger.ApplicationLogApplicationError(exception);
                Logger.ApplicationEndDumpingAllApplicationErrors();
                throw;
            }
        }

        private static bool IsInstallOrUpgradeRequest(HttpRequest request)
        {
            var url = request.Path.Value ?? string.Empty;

            return url.EndsWith("webresource.axd", StringComparison.OrdinalIgnoreCase)
                   || url.EndsWith("scriptresource.axd", StringComparison.OrdinalIgnoreCase)
                   || url.EndsWith("captcha.aspx", StringComparison.OrdinalIgnoreCase)
                   || url.Contains("upgradewizard.aspx", StringComparison.OrdinalIgnoreCase)
                   || url.Contains("installwizard.aspx", StringComparison.OrdinalIgnoreCase)
                   || url.EndsWith("install.aspx", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInstallInProgress()
        {
            return InstallBlocker.Instance.IsInstallInProgress();
        }

        private void BeginRequest(HttpContext context)
        {
            var cookieName = this.cookieService.CookieName;
            var authCookieValue = context.Request.Cookies[cookieName];
            if (!string.IsNullOrEmpty(authCookieValue) && !IsInstallOrUpgradeRequest(context.Request))
            {
                // if the cookie is not in the database, then it is from before upgrading to 9.2.0 and don't fail
                var persisted = AuthCookieController.Instance.Find(authCookieValue);
                if (persisted != null && persisted.ExpiresOn <= DateTime.UtcNow)
                {
                    context.Response.Cookies.Append(
                        cookieName,
                        string.Empty,
                        new CookieOptions
                        {
                            Expires = DateTime.Now.AddDays(-1),
                            Path = this.cookieService.CookiePath,
                            Domain = string.IsNullOrEmpty(this.cookieService.CookieDomain) ? null : this.cookieService.CookieDomain,
                        });
                }
            }

            var requestUrl = context.Request.Path.Value ?? string.Empty;
            if (!requestUrl.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)
                && !requestUrl.EndsWith('/')
                && Endings.Any(ending => requestUrl.EndsWith(ending, StringComparison.OrdinalIgnoreCase)))
            {
                // A static asset: no DNN request initialization at all.
                return;
            }

            if (IsInstallInProgress())
            {
                return;
            }

            // Initialize.Init(context) and Initialize.RunSchedule(request) used to follow here; see
            // the remarks on this class for where each of them now lives.
        }
    }
}
