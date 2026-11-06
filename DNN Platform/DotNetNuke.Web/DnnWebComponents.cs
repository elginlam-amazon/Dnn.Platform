// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web
{
    using System.Reflection;

    /// <summary>Exposes the assembly which carries this project's Blazor components.</summary>
    /// <remarks>
    /// The routable and non-routable components produced by the WebForms-control migration
    /// (<c>UI/WebControls</c>, <c>Mvp</c>) live in THIS assembly, not in the host. They are only
    /// discoverable when the host names this assembly BOTH in
    /// <c>MapRazorComponents&lt;App&gt;().AddAdditionalAssemblies(...)</c> and in the
    /// <c>Router</c>'s <c>AdditionalAssemblies</c> in <c>Routes.razor</c>; with only one of the two
    /// the components compile but their routes return 404.
    /// </remarks>
    public static class DnnWebComponents
    {
        /// <summary>Gets the assembly containing this project's Blazor components.</summary>
        public static Assembly Assembly => typeof(DnnWebComponents).Assembly;
    }
}
