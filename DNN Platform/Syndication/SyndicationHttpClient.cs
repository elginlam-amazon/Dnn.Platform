// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Syndication
{
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Downloads feed content over a single, long-lived <see cref="HttpClient"/>.</summary>
    /// <remarks>
    /// Replaces <c>WebClient</c> (removed from the supported API surface in modern .NET). A single
    /// static client is used so that the connection pool is shared; a new client per download would
    /// exhaust sockets. The synchronous path uses <see cref="HttpClient.Send(HttpRequestMessage, CancellationToken)"/>
    /// rather than blocking on an asynchronous call, so no sync-over-async wait is introduced.
    /// </remarks>
    internal static class SyndicationHttpClient
    {
        private static readonly HttpClient Client = new HttpClient();

        /// <summary>Downloads the content at the given URL.</summary>
        /// <param name="url">The URL to download.</param>
        /// <returns>The downloaded bytes.</returns>
        internal static byte[] Download(string url)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = Client.Send(request, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
            response.EnsureSuccessStatusCode();

            using var content = response.Content.ReadAsStream();
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            return buffer.ToArray();
        }

        /// <summary>Downloads the content at the given URL.</summary>
        /// <param name="url">The URL to download.</param>
        /// <param name="cancellationToken">A token to cancel the download.</param>
        /// <returns>A task which resolves to the downloaded bytes.</returns>
        internal static async Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await Client
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
