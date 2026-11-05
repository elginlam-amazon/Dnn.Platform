// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Implemented by controls (or pages) that answer a Client API callback.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "Breaking change")]
    public interface IClientAPICallbackEventHandler
    {
        /// <summary>Raises the callback event and produces the response sent back to the client.</summary>
        /// <param name="eventArgument">The argument sent by the client.</param>
        /// <returns>The response to send back to the client.</returns>
        string RaiseClientAPICallbackEvent(string eventArgument);
    }
}
