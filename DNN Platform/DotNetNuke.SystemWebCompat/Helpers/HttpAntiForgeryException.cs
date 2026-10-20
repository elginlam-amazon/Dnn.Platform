// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Helpers
{
    /// <summary>The exception that is thrown when the anti-forgery tokens are invalid.</summary>
    [Serializable]
    public sealed class HttpAntiForgeryException : HttpException
    {
        /// <summary>Initializes a new instance of the <see cref="HttpAntiForgeryException"/> class.</summary>
        public HttpAntiForgeryException()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="HttpAntiForgeryException"/> class.</summary>
        /// <param name="message">The message.</param>
        public HttpAntiForgeryException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="HttpAntiForgeryException"/> class.</summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public HttpAntiForgeryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
