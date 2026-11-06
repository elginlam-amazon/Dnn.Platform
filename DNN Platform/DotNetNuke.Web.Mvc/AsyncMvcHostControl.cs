// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvc
{
    using DotNetNuke.UI.Modules;

    /// <summary>Hosts an MVC module control whose action executes asynchronously.</summary>
    /// <remarks>
    /// Was a WebForms control that registered a <c>PageAsyncTask</c> in <c>OnInit</c> and deferred
    /// rendering to a second async task so that it ran after the WebForms async point. There is no
    /// such async point on ASP.NET Core: the module execution and the rendering of its output are
    /// simply awaited in <see cref="MvcHostControl.OnInitializedAsync"/>, which this class marks as
    /// the asynchronous module control implementation.
    /// </remarks>
    public class AsyncMvcHostControl : MvcHostControl, IAsyncModuleControl
    {
        /// <summary>Initializes a new instance of the <see cref="AsyncMvcHostControl"/> class.</summary>
        public AsyncMvcHostControl()
            : base()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="AsyncMvcHostControl"/> class.</summary>
        /// <param name="controlKey">The module control key.</param>
        public AsyncMvcHostControl(string controlKey)
            : base(controlKey)
        {
        }
    }
}
