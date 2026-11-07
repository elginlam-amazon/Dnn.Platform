// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common
{
    using System.Reflection;

    /// <summary>
    /// The marker the host uses to make this Razor Class Library's components discoverable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The platform's migrated <c>.ascx</c>/<c>.aspx</c> UI now lives in <c>.razor</c> components
    /// inside this assembly. Blazor only finds routable components in assemblies it has been told
    /// about, in BOTH places:
    /// </para>
    /// <list type="number">
    /// <item><description>the host's <c>Routes.razor</c>: <c>&lt;Router AppAssembly="@typeof(Program).Assembly" AdditionalAssemblies="new[] { DnnPlatformComponents.Assembly }"&gt;</c>;</description></item>
    /// <item><description>the host's <c>Program.cs</c>: <c>app.MapRazorComponents&lt;App&gt;().AddAdditionalAssemblies(DnnPlatformComponents.Assembly)</c>.</description></item>
    /// </list>
    /// <para>
    /// Static assets of this library resolve under <c>_content/DotNetNuke/</c> (the assembly name
    /// is <c>DotNetNuke</c>).
    /// </para>
    /// </remarks>
    public static class DnnPlatformComponents
    {
        /// <summary>Gets the assembly containing the platform's Blazor components.</summary>
        public static Assembly Assembly => typeof(DnnPlatformComponents).Assembly;
    }
}
