// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    /// <summary>
    /// The client side initializations the controls of this library asked for and that have not
    /// been performed yet.
    /// </summary>
    /// <remarks>
    /// The WebForms controls registered their initialization as a start-up script on the page.
    /// There is no page to register a script block on over a Blazor circuit, so the requests are
    /// queued here and drained by <c>DnnControlBase</c> from <c>OnAfterRenderAsync</c>, which runs
    /// them through <see cref="Microsoft.JSInterop.IJSRuntime"/>.
    /// </remarks>
    public static class ClientInitializationQueue
    {
        private static readonly ConcurrentQueue<ClientInitialization> Pending =
            new ConcurrentQueue<ClientInitialization>();

        /// <summary>Queues a client side initialization.</summary>
        /// <param name="initialization">The initialization to queue.</param>
        public static void Enqueue(ClientInitialization initialization)
        {
            ArgumentNullException.ThrowIfNull(initialization);

            Pending.Enqueue(initialization);
        }

        /// <summary>Takes every queued initialization out of the queue.</summary>
        /// <returns>The initializations that were queued, in the order they were asked for.</returns>
        public static IReadOnlyList<ClientInitialization> Drain()
        {
            var drained = new List<ClientInitialization>();
            while (Pending.TryDequeue(out var initialization))
            {
                drained.Add(initialization);
            }

            return drained;
        }
    }
}
