// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Components
{
    using DotNetNuke.Services.Localization;

    /// <summary>Localization helper for the control bar.</summary>
    /// <remarks>
    /// Deliberately still routed through DNN's own <see cref="Localization"/> engine rather than
    /// <c>IStringLocalizer</c>. <c>IStringLocalizer</c> replaces the ASP.NET
    /// <c>App_GlobalResources</c>/<c>GetGlobalResourceObject</c> mechanism, which this class never used:
    /// <see cref="Localization.GetString(string,string)"/> resolves
    /// <c>admin/ControlPanel/App_LocalResources/ControlBar.resx</c> through DNN's portal-aware chain —
    /// per-portal resource overrides under the portal home directory, then the host override, then the
    /// shipped file, with DNN's own locale fallback. <c>IStringLocalizer</c> reads satellite assemblies via
    /// <c>ResourceManager</c> and knows nothing about portals, so switching would silently drop every
    /// portal and host resource override for the control bar. The resource keys and the resource file are
    /// therefore unchanged, and the lookup is cross-platform because DNN's localization engine itself was
    /// migrated off <c>System.Web</c>.
    /// <c>AddLocalization</c>/<c>UseRequestLocalization</c> are registered by the host
    /// (<c>DotNetNuke.Library</c> <c>Startup</c> and <c>DnnPlatformApplicationExtensions</c>), so the
    /// request culture that DNN's engine reads is set up correctly either way.
    /// </remarks>
    public class LocalizationHelper
    {
        private const string ResourceFile = "admin/ControlPanel/App_LocalResources/ControlBar";

        /// <summary>Gets the localized string corresponding to the <paramref name="key"/>, for the control bar.</summary>
        /// <param name="key">The resource key to find.</param>
        /// <returns>The localized Text.</returns>
        public static string GetControlBarString(string key)
        {
            return Localization.GetString(key, ResourceFile);
        }
    }
}
