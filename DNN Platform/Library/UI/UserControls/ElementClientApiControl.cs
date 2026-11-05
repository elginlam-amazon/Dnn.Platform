// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.UserControls
{
    using DotNetNuke.UI.Utilities;

    /// <summary>
    /// Represents a single rendered HTML element of a Blazor component to the Client API, which
    /// still describes controls through <see cref="IClientApiControl"/>.
    /// </summary>
    /// <remarks>
    /// Blazor has no server side control tree, so helpers such as
    /// <c>DotNetNuke.Common.Utilities.Calendar.InvokePopupCal</c> (which only needs the client id of
    /// the element it writes the picked date into, plus the page to register its script include on)
    /// are handed one of these instead of a <c>System.Web.UI.Control</c>.
    /// </remarks>
    internal sealed class ElementClientApiControl : ClientApiControlBase
    {
        /// <summary>Initializes a new instance of the <see cref="ElementClientApiControl"/> class.</summary>
        /// <param name="id">The <c>id</c> attribute rendered on the element.</param>
        /// <param name="page">The page the element is rendered on, or <see langword="null"/> when it is unknown.</param>
        internal ElementClientApiControl(string id, IClientApiPage page)
        {
            this.Id = id;
            this.AssignedPage = page;
        }

        /// <summary>Gets the client id of the element, which for a Blazor element is its <c>id</c> attribute.</summary>
        public override string ClientId => this.Id;

        /// <summary>Gets the unique id of the element, which for a Blazor element is its <c>id</c> attribute.</summary>
        public override string UniqueId => this.Id;
    }
}
