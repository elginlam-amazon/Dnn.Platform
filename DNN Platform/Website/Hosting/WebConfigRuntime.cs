// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Website.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Web;
    using System.Web.Compilation;
    using System.Web.Security;
    using System.Xml;

    /// <summary>
    /// Bridges the DNN <c>web.config</c> to the ASP.NET Core host: System.Configuration (<c>ConfigurationManager</c>) is pointed to a
    /// runtime copy of the <c>web.config</c> without the IIS / System.Web sections (which have no section handlers on .NET), and the
    /// <c>system.web</c> / <c>system.webServer</c> settings (forms authentication, membership, HTTP modules and handlers) are read
    /// from the XML to configure the compatibility layers.
    /// </summary>
    internal sealed class WebConfigRuntime
    {
        private const string RuntimeConfigFolder = "App_Data";
        private const string RuntimeConfigFileName = "web.runtime.config";

        private static readonly string[] IisSections = ["system.web", "system.webServer", "system.codedom", "runtime", "location", "system.web.extensions", "system.serviceModel"];

        private readonly XmlDocument webConfig;

        private WebConfigRuntime(string contentRootPath, XmlDocument webConfig)
        {
            this.ContentRootPath = contentRootPath;
            this.webConfig = webConfig;
        }

        /// <summary>Gets the content root of the site.</summary>
        public string ContentRootPath { get; }

        /// <summary>Gets the path of the runtime configuration used by System.Configuration.</summary>
        public string RuntimeConfigPath => Path.Combine(this.ContentRootPath, RuntimeConfigFolder, RuntimeConfigFileName);

        /// <summary>Loads the <c>web.config</c> of the site and points System.Configuration to its runtime copy.</summary>
        /// <param name="contentRootPath">The content root of the site.</param>
        /// <returns>The <see cref="WebConfigRuntime"/>.</returns>
        public static WebConfigRuntime Initialize(string contentRootPath)
        {
            var webConfigPath = Path.Combine(contentRootPath, "web.config");
            var webConfig = new XmlDocument { XmlResolver = null, };
            if (File.Exists(webConfigPath))
            {
                using var reader = XmlReader.Create(webConfigPath, new XmlReaderSettings { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit, });
                webConfig.Load(reader);
            }
            else
            {
                webConfig.LoadXml("<configuration />");
            }

            var runtime = new WebConfigRuntime(contentRootPath, webConfig);
            runtime.WriteRuntimeConfig();

            // must happen before the first access to System.Configuration.ConfigurationManager
            AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", runtime.RuntimeConfigPath);
            return runtime;
        }

        /// <summary>Configures <see cref="FormsAuthentication"/> from the <c>system.web/authentication/forms</c> element.</summary>
        public void ConfigureFormsAuthentication()
        {
            var forms = this.webConfig.SelectSingleNode("configuration/system.web/authentication/forms") as XmlElement;
            if (forms == null)
            {
                return;
            }

            var timeout = int.TryParse(forms.GetAttribute("timeout"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) ? minutes : 30;
            FormsAuthentication.Configure(
                forms.GetAttribute("name"),
                forms.GetAttribute("path"),
                forms.GetAttribute("domain"),
                bool.TryParse(forms.GetAttribute("requireSSL"), out var requireSsl) && requireSsl,
                !bool.TryParse(forms.GetAttribute("slidingExpiration"), out var sliding) || sliding,
                TimeSpan.FromMinutes(timeout),
                forms.GetAttribute("loginUrl"),
                forms.GetAttribute("defaultUrl"));
        }

        /// <summary>Configures <see cref="Membership"/> from the <c>system.web/membership</c> element.</summary>
        public void ConfigureMembership()
        {
            var membership = this.webConfig.SelectSingleNode("configuration/system.web/membership") as XmlElement;
            if (membership == null)
            {
                return;
            }

            var defaultProviderName = membership.GetAttribute("defaultProvider");
            var providerElement = membership.SelectNodes("providers/add").OfType<XmlElement>()
                .FirstOrDefault(e => string.Equals(e.GetAttribute("name"), defaultProviderName, StringComparison.OrdinalIgnoreCase));
            if (providerElement == null)
            {
                return;
            }

            var typeName = providerElement.GetAttribute("type");
            var providerType = typeName.StartsWith("System.Web.Security.SqlMembershipProvider", StringComparison.Ordinal)
                ? typeof(SqlMembershipProvider)
                : BuildManager.GetType(typeName, true, true);

            var config = new NameValueCollection();
            foreach (XmlAttribute attribute in providerElement.Attributes)
            {
                if (!string.Equals(attribute.Name, "name", StringComparison.Ordinal) && !string.Equals(attribute.Name, "type", StringComparison.Ordinal))
                {
                    config[attribute.Name] = attribute.Value;
                }
            }

            var provider = (MembershipProvider)Activator.CreateInstance(providerType);
            provider.Initialize(providerElement.GetAttribute("name"), config);

            var window = int.TryParse(membership.GetAttribute("userIsOnlineTimeWindow"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) ? minutes : 15;
            Membership.Initialize(provider, window, membership.GetAttribute("hashAlgorithmType"));
        }

        /// <summary>Gets the HTTP modules registered in the <c>system.webServer/modules</c> element.</summary>
        /// <returns>The module names and types (modules whose type cannot be loaded, e.g. IIS / System.Web modules, are skipped).</returns>
        public IEnumerable<KeyValuePair<string, Type>> GetHttpModules()
        {
            var modules = this.webConfig.SelectNodes("configuration/system.webServer/modules/add")?.OfType<XmlElement>() ?? [];
            foreach (var module in modules)
            {
                var type = BuildManager.GetType(module.GetAttribute("type"), false, true);
                if (type != null && typeof(IHttpModule).IsAssignableFrom(type))
                {
                    yield return new KeyValuePair<string, Type>(module.GetAttribute("name"), type);
                }
            }
        }

        /// <summary>Gets the HTTP handlers registered in the <c>system.webServer/handlers</c> element.</summary>
        /// <returns>The route patterns and handler types (handlers whose type cannot be loaded are skipped).</returns>
        public IEnumerable<KeyValuePair<string, Type>> GetHttpHandlers()
        {
            var handlers = this.webConfig.SelectNodes("configuration/system.webServer/handlers/add")?.OfType<XmlElement>() ?? [];
            foreach (var handler in handlers)
            {
                var path = handler.GetAttribute("path");
                var type = BuildManager.GetType(handler.GetAttribute("type"), false, true);
                if (string.IsNullOrEmpty(path) || path.Contains('/', StringComparison.Ordinal) || type == null || !typeof(IHttpHandler).IsAssignableFrom(type))
                {
                    continue;
                }

                // IIS wildcards ("*.captcha.aspx") become route parameters
                var pattern = path.StartsWith("*", StringComparison.Ordinal) ? "{file}" + path.Substring(1) : path;
                if (!pattern.Contains('*', StringComparison.Ordinal))
                {
                    yield return new KeyValuePair<string, Type>(pattern, type);
                }
            }
        }

        /// <summary>Gets the value of a <c>system.web/machineKey</c> attribute.</summary>
        /// <param name="attributeName">The attribute name.</param>
        /// <returns>The value, or <see cref="string.Empty"/>.</returns>
        public string GetMachineKeyAttribute(string attributeName)
        {
            return (this.webConfig.SelectSingleNode("configuration/system.web/machineKey") as XmlElement)?.GetAttribute(attributeName) ?? string.Empty;
        }

        private static void RemoveSystemWebSections(XmlElement configSections)
        {
            // sections and section groups of System.Web assemblies cannot be loaded on .NET
            var toRemove = configSections.SelectNodes(".//section|.//sectionGroup").OfType<XmlElement>()
                .Where(e => e.GetAttribute("type").StartsWith("System.Web", StringComparison.Ordinal) || IisSections.Contains(e.GetAttribute("name"), StringComparer.Ordinal))
                .ToList();
            foreach (var element in toRemove)
            {
                element.ParentNode?.RemoveChild(element);
            }
        }

        private void WriteRuntimeConfig()
        {
            var runtimeConfig = new XmlDocument { XmlResolver = null, };
            var root = runtimeConfig.CreateElement("configuration");
            runtimeConfig.AppendChild(runtimeConfig.CreateXmlDeclaration("1.0", "utf-8", null));
            runtimeConfig.AppendChild(root);

            var source = this.webConfig.DocumentElement;
            if (source != null)
            {
                foreach (XmlNode child in source.ChildNodes)
                {
                    if (child is not XmlElement element || IisSections.Contains(element.Name, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    var copy = (XmlElement)runtimeConfig.ImportNode(element, true);
                    if (string.Equals(element.Name, "configSections", StringComparison.Ordinal))
                    {
                        RemoveSystemWebSections(copy);
                    }

                    root.AppendChild(copy);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(this.RuntimeConfigPath));
            using var writer = XmlWriter.Create(this.RuntimeConfigPath, new XmlWriterSettings { Indent = true, });
            runtimeConfig.Save(writer);
        }
    }
}
