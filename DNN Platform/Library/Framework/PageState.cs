// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    /// <summary>A pair of page state and control state, as persisted by a <see cref="PageStatePersister"/>.</summary>
    /// <remarks>Replaces the <c>System.Web.UI.Pair</c> the WebForms persisters stored.</remarks>
    public sealed class PageState
    {
        /// <summary>Gets or sets the state of the page itself (the former <c>ViewState</c>).</summary>
        public object ViewState { get; set; }

        /// <summary>Gets or sets the state of the page's controls (the former <c>ControlState</c>).</summary>
        public object ControlState { get; set; }
    }
}
