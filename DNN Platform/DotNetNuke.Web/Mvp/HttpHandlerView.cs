// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvp
{
    using System;
    using System.Threading.Tasks;

    using DotNetNuke.Internal.SourceGenerators;

    using Microsoft.AspNetCore.Http;

    /// <summary>A Model-View-Presenter request handler, formerly an <c>IHttpHandler</c>.</summary>
    /// <remarks>
    /// <para>
    /// Replaces <c>WebFormsMvp.Web.MvpHttpHandler</c>. The lifecycle it provided is preserved:
    /// the presenters declared with <see cref="PresenterBindingAttribute"/> are created, the
    /// <see cref="Load"/> event is raised so they can populate the model, the derived handler writes
    /// the response, and the presenters are released.
    /// </para>
    /// <para>
    /// A handler is reached through an endpoint rather than through <c>web.config</c>: declare its
    /// path with <see cref="MvpHandlerRouteAttribute"/> and
    /// <see cref="MvpEndpointRouteBuilderExtensions.MapDnnMvpEndpoints"/> maps it. A new instance is
    /// created per request, exactly as ASP.NET created a handler per request.
    /// </para>
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public abstract partial class HttpHandlerView : IHttpHandlerView
    {
        /// <inheritdoc />
        public event EventHandler Load;

        /// <inheritdoc />
        public bool ThrowExceptionIfNoPresenterBound => true;

        /// <summary>Gets the context of the request being handled.</summary>
        public HttpContext HttpContext { get; private set; }

        /// <summary>Gets the request being handled.</summary>
        public HttpRequest Request => this.HttpContext?.Request;

        /// <summary>Gets the response being written.</summary>
        public HttpResponse Response => this.HttpContext?.Response;

        /// <inheritdoc />
        public async Task HandleRequestAsync(HttpContext context)
        {
            this.HttpContext = context ?? throw new ArgumentNullException(nameof(context));

            using (MvpPresenterBinder.Bind(this))
            {
                // the presenters have subscribed; let them populate the model before the response
                // is written, which is what MvpHttpHandler did with the Load event.
                this.Load?.Invoke(this, EventArgs.Empty);

                await this.ProcessRequestAsync(context);
            }
        }

        /// <summary>Writes the response for the given request.</summary>
        /// <param name="context">The context of the request.</param>
        /// <returns>A task which completes once the response has been written.</returns>
        /// <remarks>Replaces <c>MvpHttpHandler.ProcessRequest(HttpContext)</c>.</remarks>
        protected abstract Task ProcessRequestAsync(HttpContext context);
    }
}
