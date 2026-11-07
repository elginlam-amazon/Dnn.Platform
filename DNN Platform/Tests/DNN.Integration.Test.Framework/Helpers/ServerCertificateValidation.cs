// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DNN.Integration.Test.Framework.Helpers
{
    /// <summary>
    /// Opt-in replacement for the net48 global
    /// <c>ServicePointManager.ServerCertificateValidationCallback</c> hook that SSL-enabled
    /// integration fixtures used to install so the test host would accept the development
    /// site's self-signed certificate.
    /// </summary>
    /// <remarks>
    /// On modern .NET <c>ServicePointManager</c> is obsolete and has no effect on
    /// <c>SocketsHttpHandler</c>-based clients, so certificate validation is configured per
    /// <see cref="System.Net.Http.HttpClientHandler"/> instead. A fixture sets
    /// <see cref="AcceptAny"/> to <see langword="true"/> and every connector-created handler
    /// honours it.
    /// </remarks>
    public static class ServerCertificateValidation
    {
        /// <summary>
        /// Gets or sets a value indicating whether the HTTP clients created by the test
        /// framework accept any server certificate (including self-signed and otherwise
        /// invalid ones). Defaults to <see langword="false"/>, i.e. normal validation.
        /// </summary>
        public static bool AcceptAny { get; set; }
    }
}
