// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Website.Localization.Admin.Modules
{
    /// <summary>Marker type for the resources in <c>admin/Modules/App_LocalResources/ModuleSettings.ascx.resx</c>.</summary>
    /// <remarks>
    /// Used with <see cref="Microsoft.Extensions.Localization.IStringLocalizer{T}"/>, which
    /// reads this type's resources through the logical name mapped in the project file. The
    /// <c>.resx</c> itself stays where it is so that DNN's own portal-aware lookup
    /// (<c>.Portal-N.resx</c> / <c>.Host.resx</c> overrides written by the Language Editor)
    /// and DNN language-pack installation keep working against the same file.
    /// DNN resource keys carry their suffix (for example <c>MyLabel.Text</c>,
    /// <c>MyLabel.ToolTip</c>), so lookups must include it.
    /// </remarks>
    public class ModuleSettings
    {
    }
}
