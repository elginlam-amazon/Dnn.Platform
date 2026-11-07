// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client
{
    using DotNetNuke.DependencyInjection;
    using DotNetNuke.Web.Client.Providers;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Registers the client resource rendering pipeline services.</summary>
    public class Startup : IDnnStartup
    {
        /// <inheritdoc />
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpContextAccessor();

#pragma warning disable CS0618 // Composite files (bundling) are deprecated but the behavior is preserved
            services.AddSingleton<ICompositeFileProcessor, DnnCompositeFileProcessingProvider>();
#pragma warning restore CS0618

            services.AddScoped<DnnPageHeaderProvider>();
            services.AddScoped<DnnBodyProvider>();
            services.AddScoped<DnnFormBottomProvider>();
        }
    }
}
