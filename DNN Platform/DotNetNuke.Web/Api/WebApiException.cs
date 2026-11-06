// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Net;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>Represents a non-successful response while executing a WebApi call.</summary>
    /// <remarks>
    /// The legacy exception carried the <c>HttpResponseMessage</c> of the failed call. ASP.NET Core
    /// produces results rather than response messages, so the exception carries the same information the
    /// consumers read off that message: the status code and the reason phrase, plus the response body.
    /// </remarks>
    public class WebApiException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="WebApiException"/> class, with the specified inner exception and status code that caused the exception.</summary>
        /// <param name="innerException">The original exception.</param>
        /// <param name="statusCode">The status code of the request.</param>
        public WebApiException(Exception innerException, HttpStatusCode statusCode)
            : base(innerException.Message, innerException)
        {
            this.StatusCode = statusCode;
        }

        /// <summary>Initializes a new instance of the <see cref="WebApiException"/> class.</summary>
        /// <param name="innerException">The original exception.</param>
        /// <param name="statusCode">The status code of the request.</param>
        /// <param name="body">The body from the GET response.</param>
        public WebApiException(Exception innerException, HttpStatusCode statusCode, string body)
            : this(innerException, statusCode)
        {
            this.Body = body;
        }

        /// <summary>Initializes a new instance of the <see cref="WebApiException"/> class.</summary>
        /// <param name="innerException">The original exception.</param>
        /// <param name="statusCode">The status code of the request.</param>
        /// <param name="reasonPhrase">The reason phrase of the response.</param>
        /// <param name="body">The body from the GET response.</param>
        public WebApiException(Exception innerException, HttpStatusCode statusCode, string reasonPhrase, string body)
            : this(innerException, statusCode, body)
        {
            this.ReasonPhrase = reasonPhrase;
        }

        /// <summary>Gets body from the Get Response. Available when exception is thrown as well.</summary>
        public string Body { get; }

        /// <summary>Gets the status code of the request. Replaces reading <c>Result.StatusCode</c>.</summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>Gets the reason phrase of the response, when one was supplied.</summary>
        public string ReasonPhrase { get; }

        /// <summary>Deserialize <see cref="Body"/> from JSON.</summary>
        /// <returns>A dynamic <see cref="JContainer"/>.</returns>
        public dynamic BodyAsJson()
        {
            return JsonConvert.DeserializeObject<JContainer>(this.Body);
        }
    }
}
