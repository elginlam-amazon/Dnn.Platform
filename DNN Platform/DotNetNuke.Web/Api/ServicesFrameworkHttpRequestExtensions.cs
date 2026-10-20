// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Formatting;
    using System.Net.Http.Headers;
    using System.Text;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Newtonsoft.Json;

    /// <summary>
    /// The ASP.NET Web API request helpers (<c>Request.CreateResponse</c>, <c>Request.CreateErrorResponse</c>, ...) on top of the
    /// ASP.NET Core <see cref="HttpRequest"/>, so that services framework controllers can keep returning <see cref="HttpResponseMessage"/>
    /// instances (they are written to the response by the <see cref="HttpResponseMessageOutputFormatter"/>).
    /// </summary>
    public static class ServicesFrameworkHttpRequestExtensions
    {
        /// <summary>Gets the key of the request content in the <see cref="HttpContext.Items"/>.</summary>
        internal static readonly object ContentKey = new object();

        private const string JsonMediaType = "application/json";

        /// <summary>Gets the JSON serializer settings used for the response content (the defaults of the ASP.NET Web API JSON formatter).</summary>
        public static JsonSerializerSettings SerializerSettings { get; } = new JsonSerializerSettings();

        /// <summary>Creates a response without content.</summary>
        /// <param name="request">The request.</param>
        /// <param name="statusCode">The status code.</param>
        /// <returns>The response message.</returns>
        public static HttpResponseMessage CreateResponse(this HttpRequest request, HttpStatusCode statusCode)
        {
            return new HttpResponseMessage(statusCode);
        }

        /// <summary>Creates a response with JSON content.</summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="statusCode">The status code.</param>
        /// <param name="value">The value serialized as JSON.</param>
        /// <returns>The response message.</returns>
        public static HttpResponseMessage CreateResponse<T>(this HttpRequest request, HttpStatusCode statusCode, T value)
        {
            return request.CreateResponse(statusCode, value, JsonMediaType);
        }

        /// <summary>Creates a response with content.</summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="statusCode">The status code.</param>
        /// <param name="value">The value (serialized as JSON unless it is a string and the media type is not JSON).</param>
        /// <param name="mediaType">The media type of the content.</param>
        /// <returns>The response message.</returns>
        public static HttpResponseMessage CreateResponse<T>(this HttpRequest request, HttpStatusCode statusCode, T value, string mediaType)
        {
            mediaType = string.IsNullOrEmpty(mediaType) ? JsonMediaType : mediaType;
            var isJson = mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);
            var content = !isJson && value is string text
                ? text
                : JsonConvert.SerializeObject(value, SerializerSettings);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, mediaType),
            };
        }

        /// <summary>Creates a response with content.</summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="statusCode">The status code.</param>
        /// <param name="value">The value.</param>
        /// <param name="mediaType">The media type of the content.</param>
        /// <returns>The response message.</returns>
        public static HttpResponseMessage CreateResponse<T>(this HttpRequest request, HttpStatusCode statusCode, T value, MediaTypeHeaderValue mediaType)
        {
            return request.CreateResponse(statusCode, value, mediaType?.MediaType);
        }

        /// <summary>Creates a response with content serialized by the given (ASP.NET Web API) formatter.</summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="statusCode">The status code.</param>
        /// <param name="value">The value.</param>
        /// <param name="formatter">The formatter.</param>
        /// <param name="mediaType">The media type of the content.</param>
        /// <returns>The response message.</returns>
        public static HttpResponseMessage CreateResponse<T>(this HttpRequest request, HttpStatusCode statusCode, T value, MediaTypeFormatter formatter, string mediaType)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new ObjectContent<T>(value, formatter, mediaType),
            };
        }

        /// <summary>Creates a response with content serialized by the given (ASP.NET Web API) formatter.</summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="request">The request.</param>
        /// <param name="statusCode">The status code.</param>
        /// <param name="value">The value.</param>
        /// <param name="formatter">The formatter.</param>
        /// <returns>The response message.</returns>
        public static HttpResponseMessage CreateResponse<T>(this HttpRequest request, HttpStatusCode statusCode, T value, MediaTypeFormatter formatter)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new ObjectContent<T>(value, formatter),
            };
        }

        /// <summary>Creates an error response (<c>{ "Message": "..." }</c>).</summary>
        /// <param name="request">The request.</param>
        /// <param name="statusCode">The status code.</param>
        /// <param name="message">The error message.</param>
        /// <returns>The response message.</returns>
        public static HttpResponseMessage CreateErrorResponse(this HttpRequest request, HttpStatusCode statusCode, string message)
        {
            return request.CreateResponse(statusCode, new HttpErrorMessage(message));
        }

        /// <summary>Creates an error response for an exception.</summary>
        /// <param name="request">The request.</param>
        /// <param name="statusCode">The status code.</param>
        /// <param name="exception">The exception.</param>
        /// <returns>The response message.</returns>
        public static HttpResponseMessage CreateErrorResponse(this HttpRequest request, HttpStatusCode statusCode, Exception exception)
        {
            return request.CreateErrorResponse(statusCode, exception?.Message);
        }

        /// <summary>Creates an error response for an exception.</summary>
        /// <param name="request">The request.</param>
        /// <param name="statusCode">The status code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="exception">The exception (its details are not sent to the client).</param>
        /// <returns>The response message.</returns>
        public static HttpResponseMessage CreateErrorResponse(this HttpRequest request, HttpStatusCode statusCode, string message, Exception exception)
        {
            return request.CreateErrorResponse(statusCode, message);
        }

        /// <summary>Creates an error response for an invalid model state.</summary>
        /// <param name="request">The request.</param>
        /// <param name="statusCode">The status code.</param>
        /// <param name="modelState">The model state.</param>
        /// <returns>The response message.</returns>
        public static HttpResponseMessage CreateErrorResponse(this HttpRequest request, HttpStatusCode statusCode, ModelStateDictionary modelState)
        {
            var errors = modelState?
                .Where(entry => entry.Value.Errors.Count > 0)
                .ToDictionary(entry => entry.Key, entry => entry.Value.Errors.Select(error => string.IsNullOrEmpty(error.ErrorMessage) ? error.Exception?.Message : error.ErrorMessage).ToArray());

            return request.CreateResponse(statusCode, new { Message = "The request is invalid.", ModelState = errors, });
        }

        /// <summary>Creates a response without content (for the authentication message handlers).</summary>
        /// <param name="request">The request message.</param>
        /// <param name="statusCode">The status code.</param>
        /// <returns>The response message.</returns>
        public static HttpResponseMessage CreateResponse(this HttpRequestMessage request, HttpStatusCode statusCode)
        {
            return new HttpResponseMessage(statusCode) { RequestMessage = request, };
        }

        /// <summary>Creates a response with JSON content (for the authentication message handlers).</summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="request">The request message.</param>
        /// <param name="statusCode">The status code.</param>
        /// <param name="value">The value serialized as JSON.</param>
        /// <returns>The response message.</returns>
        public static HttpResponseMessage CreateResponse<T>(this HttpRequestMessage request, HttpStatusCode statusCode, T value)
        {
            return new HttpResponseMessage(statusCode)
            {
                RequestMessage = request,
                Content = new StringContent(JsonConvert.SerializeObject(value, SerializerSettings), Encoding.UTF8, JsonMediaType),
            };
        }

        /// <summary>Creates an error response (for the authentication message handlers).</summary>
        /// <param name="request">The request message.</param>
        /// <param name="statusCode">The status code.</param>
        /// <param name="message">The error message.</param>
        /// <returns>The response message.</returns>
        public static HttpResponseMessage CreateErrorResponse(this HttpRequestMessage request, HttpStatusCode statusCode, string message)
        {
            return request.CreateResponse(statusCode, new HttpErrorMessage(message));
        }

        /// <summary>Gets the query string parameters of the request.</summary>
        /// <param name="request">The request.</param>
        /// <returns>The name/value pairs of the query string.</returns>
        public static IEnumerable<KeyValuePair<string, string>> GetQueryNameValuePairs(this HttpRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return request.Query.SelectMany(entry => entry.Value.Select(value => new KeyValuePair<string, string>(entry.Key, value)));
        }
    }
}
