// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.HttpModules.RequestFilter
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utils;
    using DotNetNuke.Instrumentation;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Applies the host's request-filter rules (<c>DotNetNuke.config</c>
    /// <c>/configuration/blockrequests/rule</c>) to every request. This is the ASP.NET Core
    /// replacement for the <c>RequestFilterModule</c> <c>IHttpModule</c>, which hooked
    /// <c>HttpApplication.BeginRequest</c>.
    /// </summary>
    /// <remarks>
    /// <b>Pipeline position:</b> pre-routing, so a blocked request never reaches an endpoint.
    /// </remarks>
    public sealed class DnnRequestFilterMiddleware
    {
        private const string InstalledKey = "httprequestfilter.attemptedinstall";

        private readonly RequestDelegate next;
        private readonly IHostSettings hostSettings;
        private readonly IApplicationStatusInfo appStatus;

        /// <summary>Initializes a new instance of the <see cref="DnnRequestFilterMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="appStatus">The application status.</param>
        public DnnRequestFilterMiddleware(RequestDelegate next, IHostSettings hostSettings, IApplicationStatusInfo appStatus)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.hostSettings = hostSettings ?? throw new ArgumentNullException(nameof(hostSettings));
            this.appStatus = appStatus ?? throw new ArgumentNullException(nameof(appStatus));
        }

        /// <summary>Filters the current request and, unless a rule blocked it, runs the rest of the pipeline.</summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (this.FilterRequest(context))
            {
                // A rule ended the request (redirect / 301 / 404): do not call the next middleware,
                // which is what the legacy Response.End()/Redirect(…, true) achieved.
                return;
            }

            await this.next(context);
        }

        /// <summary>
        /// Resolves a legacy IIS server variable against the ASP.NET Core request. ASP.NET Core has
        /// no <c>Request.ServerVariables</c> collection, so each variable the rules can name is
        /// mapped explicitly.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <param name="variableName">The server variable name, for example <c>REMOTE_ADDR</c>.</param>
        /// <param name="mapped">Set to <see langword="true"/> when the variable is known.</param>
        /// <returns>The value of the variable, or <see langword="null"/>/empty when it has none.</returns>
        private string GetServerVariable(HttpContext context, string variableName, out bool mapped)
        {
            mapped = true;
            var request = context.Request;

            // Any HTTP_XXX_YYY variable is the request header Xxx-Yyy.
            if (variableName.StartsWith("HTTP_", StringComparison.OrdinalIgnoreCase))
            {
                var headerName = variableName.Substring("HTTP_".Length).Replace('_', '-');
                return request.Headers.TryGetValue(headerName, out var headerValues)
                    ? headerValues.ToString()
                    : null;
            }

            switch (variableName.ToUpperInvariant())
            {
                case "REMOTE_ADDR":
                    return context.Connection.RemoteIpAddress?.ToString();
                case "REMOTE_HOST":
                    return context.Connection.RemoteIpAddress?.ToString();
                case "REMOTE_PORT":
                    return context.Connection.RemotePort.ToString(CultureInfo.InvariantCulture);
                case "LOCAL_ADDR":
                    return context.Connection.LocalIpAddress?.ToString();
                case "SERVER_ADDR":
                    return context.Connection.LocalIpAddress?.ToString();
                case "SERVER_PORT":
                    return request.Host.Port?.ToString(CultureInfo.InvariantCulture)
                        ?? context.Connection.LocalPort.ToString(CultureInfo.InvariantCulture);
                case "SERVER_NAME":
                    return request.Host.Host;
                case "SERVER_PROTOCOL":
                    return request.Protocol;
                case "SERVER_PORT_SECURE":
                    return request.IsHttps ? "1" : "0";
                case "HTTPS":
                    return request.IsHttps ? "on" : "off";
                case "REQUEST_METHOD":
                    return request.Method;
                case "CONTENT_TYPE":
                    return request.ContentType;
                case "CONTENT_LENGTH":
                    return request.ContentLength?.ToString(CultureInfo.InvariantCulture);
                case "QUERY_STRING":
                    // IIS exposes the query string without the leading '?'.
                    return request.QueryString.HasValue ? request.QueryString.Value.TrimStart('?') : string.Empty;
                case "URL":
                    return request.PathBase.Add(request.Path).Value;
                case "PATH_INFO":
                    return request.PathBase.Add(request.Path).Value;
                case "SCRIPT_NAME":
                    return request.PathBase.Add(request.Path).Value;
                case "HTTP_URL":
                    return request.GetRawUrl();
                case "AUTH_TYPE":
                    return context.User?.Identity?.AuthenticationType;
                case "AUTH_USER":
                case "LOGON_USER":
                case "REMOTE_USER":
                    return context.User?.Identity?.Name;
                case "APPL_PHYSICAL_PATH":
                    return this.appStatus.ApplicationMapPath;
                default:
                    mapped = false;
                    return null;
            }
        }

        private bool FilterRequest(HttpContext context)
        {
            if (context.Items == null)
            {
                return false;
            }

            var request = context.Request;
            Globals.GetCurrentServiceProvider().GetRequiredService<LogRequestContext>()?.AddToLogContext("DnnRequestId", Guid.NewGuid().ToString());

            if (!Initialize.ProcessHttpModule(request, true, true))
            {
                return false;
            }

            // Carry out first time initialization tasks
            Initialize.Init(context);

            // only do this if we haven't already attempted an installation.  This prevents a late
            // response-start callback from trying to add this item way too late.  We only want the
            // first run through to do anything.
            // also, we use the context to store whether we've attempted an add, as it's thread-safe and
            // scoped to the request.  An instance of this middleware can service multiple requests at
            // the same time, so we cannot use a member variable.
            if (context.Items.ContainsKey(InstalledKey))
            {
                return false;
            }

            // log the installation attempt in the HttpContext
            // must do this first as several IF statements
            // below skip full processing of this method
            context.Items.Add(InstalledKey, true);
            var settings = RequestFilterSettings.GetSettings(this.hostSettings, this.appStatus);
            if (settings == null || settings.Rules.Count == 0 || !settings.Enabled)
            {
                return false;
            }

            foreach (var rule in settings.Rules)
            {
                // Added ability to determine the specific value types for addresses
                // this check was necessary so that your rule could deal with IPv4 or IPv6
                // To use this mode, add ":IPv4" or ":IPv6" to your servervariable name.
                var varArray = rule.ServerVariable.Split(':');
                var varVal = this.GetServerVariable(context, varArray[0], out var mapped);
                if (!mapped)
                {
                    // Never silently ignore a rule: ASP.NET Core cannot resolve this variable, so
                    // the rule would stop blocking requests it used to block.
                    DotNetNuke.Services.Exceptions.Exceptions.LogException(
                        new RequestFilterException(
                            $"Request filter rule skipped: the server variable '{varArray[0]}' has no ASP.NET Core equivalent and could not be resolved. Add a mapping in {nameof(DnnRequestFilterMiddleware)}.{nameof(this.GetServerVariable)} or change the rule in DotNetNuke.config."));
                    continue;
                }

                if (varArray[0].EndsWith("_ADDR", StringComparison.InvariantCultureIgnoreCase) && varArray.Length > 1)
                {
                    varVal = varArray[1] switch
                    {
                        "IPv4" => NetworkUtils.GetAddress(varVal, AddressType.IPv4),
                        "IPv6" => NetworkUtils.GetAddress(varVal, AddressType.IPv4),
                        _ => varVal,
                    };
                }

                if (!string.IsNullOrEmpty(varVal))
                {
                    if (rule.Matches(varVal))
                    {
                        if (rule.Execute(context))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
