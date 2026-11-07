// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using DotNetNuke.UI.Utilities;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Presents a rendered element of a Property Editor control to the DNN Client API.
    /// </summary>
    /// <remarks>
    /// The Client API helpers (<c>Calendar.InvokePopupCal</c>, <c>DNNClientAPI</c>, <c>ClientAPI</c>)
    /// took a <c>System.Web.UI.Control</c> and now take the <see cref="IClientApiControl"/>
    /// abstraction. The Property Editor no longer has a server control tree — it renders elements
    /// directly — so this adapter represents a single rendered element by the <c>id</c> it was given,
    /// which is all those helpers read (<see cref="IClientApiControl.ClientId"/> plus
    /// <see cref="IClientApiControl.Page"/> for script registration).
    /// </remarks>
    internal sealed class PropertyEditorClientApiControl : ClientApiControlBase
    {
        /// <summary>Initializes a new instance of the <see cref="PropertyEditorClientApiControl"/> class.</summary>
        /// <param name="id">The <c>id</c> of the rendered element.</param>
        internal PropertyEditorClientApiControl(string id)
        {
            this.Id = id;

            // IClientApiPage is the scoped page seam registered by AddDnnWebUtility(); when the host
            // has not registered it the Client API helpers skip script registration (they null-check
            // Page) rather than failing.
            this.AssignedPage = DotNetNuke.Common.Globals.GetCurrentServiceProvider()?.GetService<IClientApiPage>();
        }
    }
}
