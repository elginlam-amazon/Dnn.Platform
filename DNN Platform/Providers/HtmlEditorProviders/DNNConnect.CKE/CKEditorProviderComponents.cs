// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DNNConnect.CKEditorProvider
{
    using System.Reflection;

    /// <summary>Exposes the assembly which carries this extension's Blazor components.</summary>
    /// <remarks>
    /// The routable components that replaced this extension's two pages — <c>Options.razor</c>
    /// (<c>/Providers/HtmlEditorProviders/DNNConnect.CKE/Options.aspx</c>) and
    /// <c>Browser/Browser.razor</c>
    /// (<c>/Providers/HtmlEditorProviders/DNNConnect.CKE/Browser/Browser.aspx</c>) — live in THIS
    /// assembly, which is a Razor Class Library, not in the host. They are only reachable when the
    /// host names this assembly BOTH in <c>MapRazorComponents&lt;App&gt;().AddAdditionalAssemblies(...)</c>
    /// in <c>DNN Platform/Website/Program.cs</c> AND in the <c>Router</c>'s
    /// <c>AdditionalAssemblies</c> in <c>DNN Platform/Website/Components/Routes.razor</c>; with
    /// only one of the two the components compile but their routes intermittently return 404.
    /// Same contract as <c>DotNetNuke.Web.DnnWebComponents</c>.
    /// </remarks>
    public static class CKEditorProviderComponents
    {
        /// <summary>Gets the assembly containing this extension's Blazor components.</summary>
        public static Assembly Assembly => typeof(CKEditorProviderComponents).Assembly;
    }
}
