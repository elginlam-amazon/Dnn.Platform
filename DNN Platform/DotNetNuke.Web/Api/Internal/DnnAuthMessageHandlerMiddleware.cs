// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Threading.Tasks;

    using DotNetNuke.Instrumentation;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Drives the DNN services framework authentication handler chain, which used to be the
    /// <c>DelegatingHandler</c> chain in <c>HttpConfiguration.MessageHandlers</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ordering is behaviour-critical and is preserved exactly:
    /// </para>
    /// <list type="number">
    /// <item><description>Inbound (<c>OnAuthenticateRequest</c>): the handlers run in the order
    /// <c>ServicesRoutingManager.RegisterAuthenticationHandlers</c> added them — the
    /// <c>&lt;messageHandlers&gt;</c> entries in configuration order first, then
    /// <c>WebFormsAuthMessageHandler</c> LAST. The first handler to return <see langword="true"/>
    /// terminates inbound processing, which is what returning an <c>HttpResponseMessage</c> from
    /// <c>OnInboundRequest</c> did.</description></item>
    /// <item><description>Outbound (<c>OnSendResponse</c>): the handlers run in REVERSE order, because a
    /// <c>DelegatingHandler</c> chain unwinds from the innermost handler outwards. This is what keeps the
    /// <c>WWW-Authenticate</c> challenge headers of a 401 in the same order on the wire.</description></item>
    /// </list>
    /// <para>
    /// The outbound phase is hooked onto <see cref="HttpResponse.OnStarting(Func{Task})"/> so that it sees
    /// the final status code regardless of who produced it — including the 401 that
    /// <c>AuthFilterContext.HandleUnauthorizedRequest</c> produces from an authorization filter, which is
    /// the request that the <c>Basic</c>/<c>Digest</c> handlers turn into a challenge.
    /// </para>
    /// </remarks>
    public class DnnAuthMessageHandlerMiddleware
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<DnnAuthMessageHandlerMiddleware>();

        private readonly RequestDelegate next;
        private readonly IDnnAuthMessageHandlerCollection handlers;

        /// <summary>Initializes a new instance of the <see cref="DnnAuthMessageHandlerMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="handlers">The ordered authentication handler chain.</param>
        public DnnAuthMessageHandlerMiddleware(RequestDelegate next, IDnnAuthMessageHandlerCollection handlers)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        }

        /// <summary>Runs the authentication handler chain around the rest of the pipeline.</summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (this.handlers.Count == 0)
            {
                await this.next(context);
                return;
            }

            // SCOPE GATE. The legacy chain was HttpConfiguration.MessageHandlers, i.e. services framework
            // requests only. In the single Core pipeline, running it for every request would make the
            // Basic/Digest handlers append WWW-Authenticate to ANY 401 in the application -- including page
            // 401s, which would pop a browser credential dialog instead of redirecting to the DNN login page.
            // Both the inbound loop and the outbound OnStarting hook must be inside this gate.
            if (!DnnServiceEndpoint.IsServiceRequest(context))
            {
                await this.next(context);
                return;
            }

            context.Response.OnStarting(
                state =>
                {
                    var httpContext = (HttpContext)state;

                    // Reverse order: a DelegatingHandler chain unwinds from the inside out.
                    for (var i = this.handlers.Count - 1; i >= 0; i--)
                    {
                        var handler = this.handlers[i];
                        try
                        {
                            handler.OnSendResponse(httpContext);
                        }
                        catch (Exception ex)
                        {
                            Logger.DnnAuthMessageHandlerMiddlewareOnSendResponseFailed(ex, handler.AuthScheme);
                        }
                    }

                    return Task.CompletedTask;
                },
                context);

            foreach (var handler in this.handlers)
            {
                if (handler.OnAuthenticateRequest(context))
                {
                    // The handler produced the response itself; inbound processing terminates here.
                    return;
                }
            }

            await this.next(context);
        }
    }
}
