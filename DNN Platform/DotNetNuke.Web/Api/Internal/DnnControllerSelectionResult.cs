// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System.Net;

    /// <summary>The outcome of resolving a DNN service controller within the namespaces of a route.</summary>
    internal sealed class DnnControllerSelectionResult
    {
        /// <summary>Initializes a new instance of the <see cref="DnnControllerSelectionResult"/> class for a successful selection.</summary>
        /// <param name="controllerTypeFullName">The full name of the selected controller type.</param>
        private DnnControllerSelectionResult(string controllerTypeFullName)
        {
            this.ControllerTypeFullName = controllerTypeFullName;
        }

        /// <summary>Initializes a new instance of the <see cref="DnnControllerSelectionResult"/> class for a failed selection.</summary>
        /// <param name="statusCode">The status code to respond with.</param>
        /// <param name="message">The message to respond with.</param>
        private DnnControllerSelectionResult(HttpStatusCode statusCode, string message)
        {
            this.StatusCode = statusCode;
            this.Message = message;
        }

        /// <summary>Gets a value indicating whether exactly one controller was found.</summary>
        public bool Success => this.ControllerTypeFullName != null;

        /// <summary>Gets the full name of the selected controller type, or <see langword="null"/>.</summary>
        public string ControllerTypeFullName { get; }

        /// <summary>Gets the status code to respond with when the selection failed.</summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>Gets the message to respond with when the selection failed.</summary>
        public string Message { get; }

        /// <summary>Creates a successful result.</summary>
        /// <param name="controllerTypeFullName">The full name of the selected controller type.</param>
        /// <returns>A <see cref="DnnControllerSelectionResult"/>.</returns>
        public static DnnControllerSelectionResult Found(string controllerTypeFullName)
        {
            return new DnnControllerSelectionResult(controllerTypeFullName);
        }

        /// <summary>Creates a failed result.</summary>
        /// <param name="statusCode">The status code to respond with.</param>
        /// <param name="message">The message to respond with.</param>
        /// <returns>A <see cref="DnnControllerSelectionResult"/>.</returns>
        public static DnnControllerSelectionResult Failure(HttpStatusCode statusCode, string message)
        {
            return new DnnControllerSelectionResult(statusCode, message);
        }
    }
}
