// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.PersonaBar.SiteSettings.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Dnn.PersonaBar.SiteSettings.Services.Dto;
    using DotNetNuke.Common;

    internal static class KpvExtension
    {
        public static IEnumerable<LocalizationEntry> MapEntries(this IEnumerable<KeyValuePair<string, string>> list)
        {
            var appPath = Globals.ApplicationMapPath;
            var appPathLen = appPath.Length;
            // The application map path may use either separator depending on the host OS, so test for both
            // before accounting for the separator that follows the application path in each entry's value.
            if (appPath.Length == 0 ||
                (appPath[appPath.Length - 1] != Path.DirectorySeparatorChar &&
                 appPath[appPath.Length - 1] != Path.AltDirectorySeparatorChar))
            {
                appPathLen++;
            }

            return list.Select(kpv => new LocalizationEntry
            {
                Name = kpv.Key,
                NewValue = (kpv.Value.StartsWith(appPath, StringComparison.OrdinalIgnoreCase) ? kpv.Value.Substring(appPathLen) : kpv.Value).Replace(@"\", @"/"),
            });
        }
    }
}
