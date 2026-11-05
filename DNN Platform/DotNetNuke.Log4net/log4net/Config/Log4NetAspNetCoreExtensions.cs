// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
//
// Licensed to the Apache Software Foundation (ASF) under one or more
// contributor license agreements. See the NOTICE file distributed with
// this work for additional information regarding copyright ownership.
// The ASF licenses this file to you under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with
// the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using log4net.Util;

namespace log4net.Config
{
    /// <summary>
    /// Wires log4net's ASP.NET Core integration points into a host.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On .NET Framework, log4net reached the current request through the ambient
    /// <c>HttpContext.Current</c> and the application's settings through
    /// <c>System.Configuration.ConfigurationManager</c>. Neither exists on modern .NET, so the
    /// equivalents — <see cref="AspNetContextAccessor.HttpContextAccessor"/> and
    /// <see cref="Log4NetSettings.Configuration"/> — must be supplied by the host exactly once.
    /// </para>
    /// <para>
    /// Rather than requiring the host to assign those statics by hand, this class performs the
    /// assignment itself. A single call during start-up enables the request-aware features
    /// (the <c>%aspnet-context</c>, <c>%aspnet-request</c>, <c>%aspnet-session</c> and
    /// <c>%aspnet-cache</c> pattern keywords, <see cref="log4net.Appender.AspNetTraceAppender"/>)
    /// and makes <c>%appsetting</c>, <c>AdoNetAppender.AppSettingsKey</c> and
    /// <c>AdoNetAppender.ConnectionStringName</c> read from the host's configuration:
    /// </para>
    /// <code lang="C#">
    /// builder.Services.AddLog4NetAspNetCore();
    /// </code>
    /// <para>
    /// The registration is idempotent and order-independent: it adds an
    /// <see cref="IStartupFilter"/>, so the assignment happens after the service provider is
    /// built but before the first request is handled. Hosts that are not web applications simply
    /// do not call it, and every integration point degrades to its documented
    /// not-available behaviour.
    /// </para>
    /// </remarks>
    public static class Log4NetAspNetCoreExtensions
    {
        /// <summary>
        /// Registers log4net's ASP.NET Core integration so that the current
        /// <c>HttpContext</c> and the host's <see cref="IConfiguration"/> become visible to
        /// log4net appenders, layouts and pattern converters.
        /// </summary>
        /// <param name="services">The service collection to add the integration to.</param>
        /// <returns>The same <paramref name="services"/> instance, to allow chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is <c>null</c>.
        /// </exception>
        [System.CLSCompliant(false)]
        public static IServiceCollection AddLog4NetAspNetCore(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            // IHttpContextAccessor is what replaces HttpContext.Current; TryAdd keeps this safe
            // to call alongside AddHttpContextAccessor or a second AddLog4NetAspNetCore call.
            services.AddHttpContextAccessor();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IStartupFilter, Log4NetAspNetCoreStartupFilter>());

            return services;
        }

        /// <summary>
        /// Assigns log4net's ASP.NET Core integration points directly from a built
        /// <see cref="IServiceProvider"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this when the host cannot use <see cref="AddLog4NetAspNetCore"/> — for example a
        /// generic host, or a web host that configures logging after <c>Build()</c>. Calling it
        /// more than once is harmless. Any integration point whose service is not registered is
        /// left at its current value, so a partially configured host still starts.
        /// </para>
        /// </remarks>
        /// <param name="serviceProvider">The application's service provider.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serviceProvider"/> is <c>null</c>.
        /// </exception>
        [System.CLSCompliant(false)]
        public static void UseLog4NetAspNetCore(this IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            IHttpContextAccessor httpContextAccessor =
                (IHttpContextAccessor)serviceProvider.GetService(typeof(IHttpContextAccessor));
            if (httpContextAccessor != null)
            {
                AspNetContextAccessor.HttpContextAccessor = httpContextAccessor;
            }
            else
            {
                LogLog.Warn(
                    typeof(Log4NetAspNetCoreExtensions),
                    "No IHttpContextAccessor is registered; log4net's request-aware patterns will render "
                    + SystemInfo.NotAvailableText
                    + ". Call services.AddHttpContextAccessor() or services.AddLog4NetAspNetCore().");
            }

            IConfiguration configuration =
                (IConfiguration)serviceProvider.GetService(typeof(IConfiguration));
            if (configuration != null)
            {
                Log4NetSettings.Configuration = configuration;
            }
        }

        /// <summary>
        /// Performs the one-time assignment of log4net's ASP.NET Core integration points during
        /// application start-up.
        /// </summary>
        private sealed class Log4NetAspNetCoreStartupFilter : IStartupFilter
        {
            private readonly IServiceProvider serviceProvider;

            /// <summary>Initializes a new instance of the <see cref="Log4NetAspNetCoreStartupFilter"/> class.</summary>
            /// <param name="serviceProvider">The application's root service provider.</param>
            public Log4NetAspNetCoreStartupFilter(IServiceProvider serviceProvider)
            {
                this.serviceProvider = serviceProvider;
            }

            /// <summary>Assigns the integration points, then returns the pipeline unchanged.</summary>
            /// <param name="next">The next pipeline configuration step.</param>
            /// <returns>The unmodified <paramref name="next"/> configuration action.</returns>
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                this.serviceProvider.UseLog4NetAspNetCore();
                return next;
            }
        }
    }
}
