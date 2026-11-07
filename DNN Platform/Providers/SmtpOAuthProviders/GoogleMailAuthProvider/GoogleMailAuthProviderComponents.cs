// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.GoogleMailAuthProvider
{
    using System.Reflection;

    /// <summary>Exposes the assembly which carries this extension's Blazor components.</summary>
    /// <remarks>
    /// The routable component that replaced this extension's page — <c>Authorize.razor</c>
    /// (<c>/Providers/SmtpOAuthProviders/GoogleMail/Authorize.aspx</c>, the Google OAuth callback
    /// URL registered with Google) — lives in THIS assembly, which is a Razor Class Library, not
    /// in the host. It is only reachable when the host names this assembly BOTH in
    /// <c>MapRazorComponents&lt;App&gt;().AddAdditionalAssemblies(...)</c> in
    /// <c>DNN Platform/Website/Program.cs</c> AND in the <c>Router</c>'s
    /// <c>AdditionalAssemblies</c> in <c>DNN Platform/Website/Components/Routes.razor</c>; with
    /// only one of the two the component compiles but its route intermittently returns 404.
    /// Same contract as <c>DNNConnect.CKEditorProvider.CKEditorProviderComponents</c>.
    /// </remarks>
    public static class GoogleMailAuthProviderComponents
    {
        /// <summary>Gets the assembly containing this extension's Blazor components.</summary>
        public static Assembly Assembly => typeof(GoogleMailAuthProviderComponents).Assembly;
    }
}
