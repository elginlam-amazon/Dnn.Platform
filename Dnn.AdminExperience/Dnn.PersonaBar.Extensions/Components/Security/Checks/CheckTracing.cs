// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.Security.Components.Checks
{
    using System;
    using System.IO;
    using System.Xml;

    using DotNetNuke.Common;

    public class CheckTracing : IAuditCheck
    {
        /// <inheritdoc />
        public string Id => "CheckTracing";

        /// <inheritdoc />
        public bool LazyLoad => false;

        /// <inheritdoc />
        public CheckResult Execute()
        {
            var result = new CheckResult(SeverityEnum.Unverified, this.Id);

            result.Severity = EnableTrace() ? SeverityEnum.Failure : SeverityEnum.Pass;

            return result;
        }

        private static bool EnableTrace()
        {
            return PageLevelTraceEnabled() || AppLevelTraceEnabled();
        }

        private static bool PageLevelTraceEnabled()
        {
            try
            {
                var defaultPagePath = Path.Combine(Globals.ApplicationMapPath, "Default.aspx");
                using var reader = new StreamReader(File.OpenRead(defaultPagePath));
                var pageDefine = reader.ReadLine();
                if (!string.IsNullOrEmpty(pageDefine))
                {
                    return pageDefine.ToLowerInvariant().Contains("trace=\"true\"");
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool AppLevelTraceEnabled()
        {
            try
            {
                var configPath = Path.Combine(Globals.ApplicationMapPath, "web.config");
                if (!File.Exists(configPath))
                {
                    return false;
                }

                var document = new XmlDocument { XmlResolver = null };
                using (var reader = XmlReader.Create(configPath, new XmlReaderSettings { XmlResolver = null }))
                {
                    document.Load(reader);
                }

                var traceNode = document.SelectSingleNode("/configuration/system.web/trace");
                var enabledAttribute = traceNode?.Attributes?["enabled"];
                return enabledAttribute != null
                    && bool.TryParse(enabledAttribute.Value, out var enabled)
                    && enabled;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
