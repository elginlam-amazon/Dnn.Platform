// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Providers.Caching.SimpleWebFarmCachingProvider
{
    using System;
    using System.Net;
    using System.Net.Http;

    using DotNetNuke.DependencyInjection;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The <see cref="IDnnStartup"/> for the simple web farm caching provider.</summary>
    /// <remarks>
    /// Registers the named <see cref="HttpClient"/> used to notify the other servers in the web farm.
    /// The handler carries the settings the legacy <c>HttpWebRequest</c> applied per request
    /// (default credentials and a cookie container), and the client carries the 5 second budget which
    /// the legacy code enforced with <c>ThreadPool.RegisterWaitForSingleObject</c>.
    /// </remarks>
    public class Startup : IDnnStartup
    {
        /// <inheritdoc />
        public void ConfigureServices(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services
                .AddHttpClient(
                    SimpleWebFarmCachingProvider.SyncHttpClientName,
                    client => client.Timeout = TimeSpan.FromSeconds(5))
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    UseDefaultCredentials = true,
                    UseCookies = true,
                    CookieContainer = new CookieContainer(),
                });
        }
    }
}
