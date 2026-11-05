// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.ClientCapability
{
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Common;
    using DotNetNuke.ComponentModel;

    using Microsoft.AspNetCore.Http;

    public abstract class ClientCapabilityProvider : IClientCapabilityProvider
    {
        /// <summary>
        /// Gets the client capabilities of the request currently in flight, resolved through the
        /// <see cref="HttpContextSource"/> seam.
        /// </summary>
        /// <remarks>
        /// On ASP.NET Core there is no ambient request, so this returns <see langword="null"/>
        /// when there is none (for example during Blazor Server interactive rendering). Capture
        /// the value during the initial render instead of reading it from an event handler.
        /// </remarks>
        public static IClientCapability CurrentClientCapability
        {
            get
            {
                var request = HttpContextSource.Current?.Request;
                return request is null ? null : Instance().GetClientCapability(request);
            }
        }

        /// <summary>Gets a value indicating whether this provider supports detecting if the device is a tablet.</summary>
        public virtual bool SupportsTabletDetection => true;

        public static ClientCapabilityProvider Instance()
        {
            return ComponentFactory.GetComponent<ClientCapabilityProvider>();
        }

        /// <inheritdoc />
        public abstract IClientCapability GetClientCapability(string userAgent);

        /// <inheritdoc />
        public abstract IClientCapability GetClientCapabilityById(string clientId);

        /// <inheritdoc />
        public abstract IDictionary<string, List<string>> GetAllClientCapabilityValues();

        /// <inheritdoc />
        public abstract IQueryable<IClientCapability> GetAllClientCapabilities();

        /// <inheritdoc />
        public virtual IClientCapability GetClientCapability(HttpRequest httpRequest)
        {
            // ASP.NET Core has no HttpBrowserCapabilities / .browser capability files; the
            // User-Agent header is the only input, which is what the providers parse anyway.
            IClientCapability clientCapability = this.GetClientCapability(httpRequest.Headers.UserAgent.ToString());
            clientCapability.FacebookRequest = FacebookRequestController.GetFacebookDetailsFromRequest(httpRequest);

            return clientCapability;
        }
    }
}
