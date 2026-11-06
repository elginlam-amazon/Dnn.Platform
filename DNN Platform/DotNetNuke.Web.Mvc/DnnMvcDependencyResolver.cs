// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Services.DependencyInjection;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Resolves services out of the current DNN request scope for the MVC Modules of DNN.
    /// </summary>
    /// <remarks>
    /// This replaces the legacy <c>System.Web.Mvc.IDependencyResolver</c> implementation. ASP.NET Core
    /// activates controllers from <c>HttpContext.RequestServices</c> on its own; this type remains for the
    /// DNN module code which needs an <see cref="IServiceProvider"/> bound to the ambient DNN scope.
    /// </remarks>
    internal sealed class DnnMvcDependencyResolver : IServiceProvider
    {
        private readonly IServiceProvider serviceProvider;

        /// <summary>Initializes a new instance of the <see cref="DnnMvcDependencyResolver"/> class.</summary>
        /// <param name="serviceProvider">The service provider.</param>
        public DnnMvcDependencyResolver(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>Returns the specified service from the scope.</summary>
        /// <param name="serviceType">
        /// The service to be retrieved.
        /// </param>
        /// <returns>
        /// The retrieved service.
        /// </returns>
        public object GetService(Type serviceType)
        {
            return this.GetScopedServiceProvider().GetService(serviceType);
        }

        /// <summary>Returns the specified services from the scope.</summary>
        /// <param name="serviceType">
        /// The service to be retrieved.
        /// </param>
        /// <returns>
        /// The retrieved service.
        /// </returns>
        public IEnumerable<object> GetServices(Type serviceType)
        {
            return this.GetScopedServiceProvider().GetServices(serviceType);
        }

        private IServiceProvider GetScopedServiceProvider()
        {
            var accessor = this.serviceProvider.GetRequiredService<IScopeAccessor>();
            var scope = accessor.GetScope();
            if (scope != null)
            {
                return scope.ServiceProvider;
            }

            throw new InvalidOperationException("IServiceScope not provided");
        }
    }
}
