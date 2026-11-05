// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.GeneratedImage
{
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;

    /// <summary>A server-side cache for generated images.</summary>
    /// <remarks>
    /// <c>TryTransmitIfContains</c> became asynchronous because its ASP.NET Core replacement for
    /// <c>HttpResponse.TransmitFile</c> (<see cref="SendFileResponseExtensions.SendFileAsync(HttpResponse, string, System.Threading.CancellationToken)"/>)
    /// is asynchronous.
    /// </remarks>
    internal interface IImageStore
    {
        void Add(string id, byte[] data);

        Task<bool> TryTransmitIfContainsAsync(string id, HttpResponse response);

        void ForcePurgeFromServerCache(string cacheId);
    }
}
