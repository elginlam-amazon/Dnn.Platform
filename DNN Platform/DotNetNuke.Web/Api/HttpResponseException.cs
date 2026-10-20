// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Net;
    using System.Net.Http;

    /// <summary>
    /// An exception which aborts a services framework action with the given response (the equivalent of the ASP.NET Web API
    /// <c>System.Web.Http.HttpResponseException</c>, handled by the <see cref="DnnExceptionFilterAttribute"/>).
    /// </summary>
    [Serializable]
    public class HttpResponseException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="HttpResponseException"/> class.</summary>
        public HttpResponseException()
            : this(HttpStatusCode.InternalServerError)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="HttpResponseException"/> class.</summary>
        /// <param name="statusCode">The status code of the response.</param>
        public HttpResponseException(HttpStatusCode statusCode)
            : this(new HttpResponseMessage(statusCode))
        {
        }

        /// <summary>Initializes a new instance of the <see cref="HttpResponseException"/> class.</summary>
        /// <param name="response">The response.</param>
        public HttpResponseException(HttpResponseMessage response)
            : base("Processing of the HTTP request resulted in an exception. Please see the HTTP response returned by the 'Response' property of this exception for details.")
        {
            this.Response = response ?? throw new ArgumentNullException(nameof(response));
        }

        /// <summary>Initializes a new instance of the <see cref="HttpResponseException"/> class.</summary>
        /// <param name="message">The message.</param>
        public HttpResponseException(string message)
            : base(message)
        {
            this.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        /// <summary>Initializes a new instance of the <see cref="HttpResponseException"/> class.</summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public HttpResponseException(string message, Exception innerException)
            : base(message, innerException)
        {
            this.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        /// <summary>Gets the response.</summary>
        public HttpResponseMessage Response { get; }
    }
}
