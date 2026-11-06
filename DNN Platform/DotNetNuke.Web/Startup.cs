// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web
{
    using DotNetNuke.DependencyInjection;
    using DotNetNuke.Web.Api.Extensions;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Configures services for The DotNetNuke.Web project.</summary>
    /// <remarks>
    /// The whole graph lives in <see cref="DnnWebApiHostWiringExtensions.AddDnnWebApi"/>, which the host
    /// calls from <c>Website/Program.cs</c> with its <c>IConfiguration</c>. This <see cref="IDnnStartup"/>
    /// registers the same graph minus the configuration binding, so that a host which only calls
    /// <c>AddDnnPlatform</c> still gets the services framework. Every registration uses <c>TryAdd</c>, so
    /// running both paths registers each service exactly once.
    /// </remarks>
    public class Startup : IDnnStartup
    {
        /// <inheritdoc />
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDnnWebApiCore();
        }
    }
}
