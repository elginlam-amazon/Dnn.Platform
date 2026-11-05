// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Net.Http.Headers;

    /// <summary>
    /// Base class for DNN's request handlers, ported from <c>System.Web.IHttpHandler</c> to
    /// ASP.NET Core.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The handler is no longer registered in <c>web.config</c>; instead it is invoked from an
    /// endpoint. Map it like this:
    /// </para>
    /// <code>
    /// app.Map("/MyHandler.ashx", (HttpContext ctx) => new MyHandler().ProcessRequestAsync(ctx));
    /// </code>
    /// <para>
    /// Derived classes implement <see cref="HandleRequestAsync"/> (the async replacement for the
    /// old <c>HandleRequest</c>) and <see cref="ValidateParameters"/>. All other members keep the
    /// names and meanings they had on .NET Framework; only the types they use moved from
    /// <c>System.Web</c> to <c>Microsoft.AspNetCore.Http</c>.
    /// </para>
    /// </remarks>
    public abstract class BaseHttpHandler
    {
        private HttpContext context;

        /// <summary>Gets the <see cref="HttpContext"/> object for the incoming HTTP request.</summary>
        public HttpContext Context
        {
            get
            {
                return this.context;
            }
        }

        /// <summary>Gets the <see cref="HttpRequest"/> object for the incoming HTTP request.</summary>
        public HttpRequest Request
        {
            get
            {
                return this.Context.Request;
            }
        }

        /// <summary>
        ///   Gets the <see cref="HttpResponse"/> object associated with this request. This object
        ///   allows you to send HTTP response data to a client and contains information about that response.
        /// </summary>
        public HttpResponse Response
        {
            get
            {
                return this.Context.Response;
            }
        }

        /// <summary>Gets the string representation of the body of the incoming request.</summary>
        /// <remarks>
        /// Prefer <see cref="GetContentAsync"/>. This synchronous property is kept for call-site
        /// compatibility and requires a seekable (buffered) request body, which
        /// <see cref="ProcessRequestAsync"/> arranges by calling
        /// <see cref="HttpRequestRewindExtensions.EnableBuffering(HttpRequest)"/>.
        /// </remarks>
        public string Content
        {
            get
            {
                return this.GetContentAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        ///   Gets a value indicating whether this handler
        ///   requires users to be authenticated.
        /// </summary>
        /// <value>
        ///   <see langword="true"/> if authentication is required
        ///   otherwise, <see langword="false"/>.
        /// </value>
        public virtual bool RequiresAuthentication
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        ///   Gets a value indicating whether the requester
        ///   has the necessary permissions.
        /// </summary>
        /// <remarks>
        ///   By default all authenticated users have permissions.
        ///   This property is only enforced if <see cref = "RequiresAuthentication" /> is <see langword="true"/>.
        /// </remarks>
        /// <value>
        ///   <see langword="true"/> if the user has the appropriate permissions
        ///   otherwise, <see langword="false"/>.
        /// </value>
        public virtual bool HasPermission
        {
            get
            {
                return this.Context.User?.Identity?.IsAuthenticated == true;
            }
        }

        /// <summary>Gets the content MIME type for the response object.</summary>
        /// <value>
        /// The content MIME type for the response object.
        /// </value>
        public virtual string ContentMimeType
        {
            get
            {
                return "text/plain";
            }
        }

        /// <summary>Gets the content encoding for the response object.</summary>
        /// <value>
        /// The content encoding for the response object.
        /// </value>
        public virtual Encoding ContentEncoding
        {
            get
            {
                return Encoding.UTF8;
            }
        }

        /// <summary>Gets a value indicating whether this handler instance can be reused for more than one request.</summary>
        /// <remarks>On ASP.NET Core a new handler instance is created per endpoint invocation, so this is informational only.</remarks>
        public virtual bool IsReusable
        {
            get
            {
                return true;
            }
        }

        /// <summary>Processes the incoming HTTP request.</summary>
        /// <param name="context">The context of the current request.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ProcessRequestAsync(HttpContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));

            // Allow Content/GetContentAsync to read (and re-read) the body.
            context.Request.EnableBuffering();

            this.SetResponseCachePolicy(this.Response.Headers);

            if (!this.ValidateParameters())
            {
                this.RespondInternalError();
                return;
            }

            if (this.RequiresAuthentication && !this.HasPermission)
            {
                this.RespondForbidden();
                return;
            }

            this.Response.ContentType = $"{this.ContentMimeType}; charset={this.ContentEncoding.WebName}";

            await this.HandleRequestAsync();
        }

        /// <summary>Gets the string representation of the body of the incoming request.</summary>
        /// <returns>The request body as a string.</returns>
        public async Task<string> GetContentAsync()
        {
            var body = this.Request.Body;
            if (body.CanSeek)
            {
                body.Position = 0;
            }

            using (var reader = new StreamReader(body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            {
                var content = await reader.ReadToEndAsync();
                if (body.CanSeek)
                {
                    body.Position = 0;
                }

                return content;
            }
        }

        /// <summary>
        ///   Handles the request. This is where you put your
        ///   business logic.
        /// </summary>
        /// <remarks>
        ///   <p>This method should result in one (or more) of the following calls:</p>
        ///   <p><code>await this.Response.WriteAsync(text);</code></p>
        ///   <p><code>await this.Response.Body.WriteAsync(bytes);</code></p>
        ///   <p><code>await this.Response.SendFileAsync(path);</code></p>
        ///   <p><code>await someStream.CopyToAsync(this.Response.Body);</code></p>
        ///   <p>
        ///     If you want a download box to show up with a pre-populated filename, add this call
        ///     here (supplying a real filename):
        ///   </p>
        ///   <p><code>this.Response.Headers.ContentDisposition = "attachment; filename=\"" + filename + "\"";</code></p>
        /// </remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task HandleRequestAsync();

        /// <summary>
        ///   Validates the parameters. Inheriting classes must
        ///   implement this and return true if the parameters are
        ///   valid, otherwise false.
        /// </summary>
        /// <returns><see langword="true"/> if the parameters are valid,
        ///   otherwise <see langword="false"/>.</returns>
        public abstract bool ValidateParameters();

        /// <summary>
        ///   Sets the cache policy. Unless a handler overrides
        ///   this method, handlers will not allow a response to be
        ///   cached.
        /// </summary>
        /// <param name="headers">The response headers on which to set the cache policy.</param>
        public virtual void SetResponseCachePolicy(IHeaderDictionary headers)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            headers[HeaderNames.CacheControl] = "no-cache, no-store, must-revalidate";
            headers[HeaderNames.Pragma] = "no-cache";
            headers[HeaderNames.Expires] = "0";
        }

        /// <summary>
        ///   Helper method used to Respond to the request
        ///   that the file was not found.
        /// </summary>
        protected void RespondFileNotFound()
        {
            this.Response.StatusCode = (int)HttpStatusCode.NotFound;
        }

        /// <summary>
        ///   Helper method used to Respond to the request
        ///   that an error occurred in processing the request.
        /// </summary>
        protected void RespondInternalError()
        {
            this.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }

        /// <summary>
        ///   Helper method used to Respond to the request
        ///   that the request in attempting to access a resource
        ///   that the user does not have access to.
        /// </summary>
        protected void RespondForbidden()
        {
            this.Response.StatusCode = (int)HttpStatusCode.Forbidden;
        }
    }
}
