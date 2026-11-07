// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvp
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    using DotNetNuke.Internal.SourceGenerators;

    /// <summary>The contract every DNN MVP presenter satisfies.</summary>
    /// <remarks>Replaces the non generic <c>WebFormsMvp.IPresenter</c>.</remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public partial interface IMvpPresenter
    {
        /// <summary>Detaches the presenter from its view.</summary>
        void ReleaseView();
    }
}
