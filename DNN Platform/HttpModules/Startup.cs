// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.HttpModules;

using DotNetNuke.DependencyInjection;
using DotNetNuke.Entities.Urls;
using DotNetNuke.HttpModules.UrlRewrite;

using Microsoft.Extensions.DependencyInjection;

/// <summary>The <see cref="IDnnStartup"/> for <c>DotNetNuke.HttpModules</c>.</summary>
public class Startup : IDnnStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        // The rewriter the installation is configured for (basic or advanced). Resolved by
        // the Library's DnnUrlRewriterMiddleware.
        services.AddScoped<UrlRewriterBase>(serviceProvider => UrlRewriterFactory.GetUrlRewriterInstance(serviceProvider));

        // Every middleware ported from this project's IHttpModule implementations reaches the
        // current request through HttpContextSource (which is fed by IHttpContextAccessor), and
        // DnnServiceScopeMiddleware publishes the per-request scope on the same context.
        // TryAdd-based, so this is a no-op when the host already registered it.
        services.AddHttpContextAccessor();

        // The DNN services the ported middleware inject (IHostSettings, IApplicationStatusInfo,
        // IPortalController, IHostSettingsService, IEventLogger, IUserRequestIPAddressController,
        // IRoleController, IUserController, IRedirectionController, PersonalizationController)
        // are all registered by DotNetNuke.Startup in the Library and are therefore available
        // to the middleware below without being re-registered here.
    }
}
