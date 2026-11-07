// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DNNConnect.CKEditorProvider
{
    using System;

    using DNNConnect.CKEditorProvider.Web;

    using DotNetNuke.DependencyInjection;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The <see cref="IDnnStartup"/> of the CKEditor HTML editor provider.</summary>
    /// <remarks>
    /// This extension's own service registrations belong here, not in the platform's
    /// <c>Program.cs</c>: <c>AddDnnPlatform</c> discovers and runs every <see cref="IDnnStartup"/>
    /// in the application, so an extension stays self-contained (the
    /// <c>DotNetNuke.Providers.Caching.SimpleWebFarmCachingProvider</c> precedent).
    /// </remarks>
    public class Startup : IDnnStartup
    {
        /// <inheritdoc />
        public void ConfigureServices(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            // The editor component replacing Web/EditorControl.cs. CKHtmlEditorProvider.Initialize
            // builds it off the request scope so that its many platform dependencies (host
            // settings, portal controller, client resources, event logger) are injected; declaring
            // it here makes that scope the component's documented DI home and lets consumers that
            // render it through Blazor's component activator obtain the same instance shape.
            services.AddTransient<EditorControl>();
        }
    }
}
