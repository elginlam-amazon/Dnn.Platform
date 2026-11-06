// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System.Collections.Generic;

    /// <summary>
    /// The ordered collection of DNN authentication message handlers. This replaces
    /// <c>GlobalConfiguration.Configuration.MessageHandlers</c>, into which the legacy
    /// <c>ServicesRoutingManager.RegisterRoutes</c> added the <c>AuthMessageHandlerBase</c>
    /// instances configured in the <c>&lt;messageHandlers&gt;</c> configuration section.
    /// </summary>
    /// <remarks>
    /// The order of this collection is the order in which the handlers authenticate a request, and it is
    /// the same order the legacy message-handler chain used: the configured handlers first (in
    /// configuration order), then the WebForms cookie-auth bridge last. The DNN request context is
    /// established before all of them by <see cref="DnnContextMiddleware"/>.
    /// </remarks>
    public interface IDnnAuthMessageHandlerCollection : IReadOnlyList<IDnnAuthMessageHandler>
    {
        /// <summary>Appends a handler to the end of the chain.</summary>
        /// <param name="handler">The handler to append.</param>
        void Add(IDnnAuthMessageHandler handler);
    }
}
