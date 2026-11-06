// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.ExportImport
{
    /// <summary>
    /// Marker type for the shared resources of this module, backed by
    /// <c>App_LocalResources/ExportImport.resx</c>.
    /// </summary>
    /// <remarks>
    /// Used with <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/> to read the
    /// localized strings of this extension, replacing the legacy
    /// <c>DotNetNuke.Services.Localization.Localization.GetString(key, resourceFile)</c> lookups.
    /// The <c>.resx</c> stays in <c>App_LocalResources</c> so the DNN package still installs it;
    /// the project file maps it onto this type's name via the resource's logical name.
    /// Note that the DNN resource keys carry a <c>.Text</c> suffix (which
    /// <c>Localization.GetString</c> used to append implicitly), so lookups through the localizer
    /// must include it, for example <c>localizer["NotPortalAdmin.Text"]</c>.
    /// </remarks>
    public class SharedResources
    {
    }
}
