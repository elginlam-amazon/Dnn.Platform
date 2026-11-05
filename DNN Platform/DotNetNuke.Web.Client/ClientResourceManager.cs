// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable once CheckNamespace
namespace DotNetNuke.Web.Client.ClientResourceManagement
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Xml;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Web.Client.Providers;
    using DotNetNuke.Web.Client.ResourceManager;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>Provides the ability to request that client resources (JavaScript and CSS) be loaded on the client browser.</summary>
    [DnnDeprecated(10, 2, 0, "Please use IClientResourceController instead.")]
    public partial class ClientResourceManager
    {
        /// <summary>The default CSS provider.</summary>
        internal const string DefaultCssProvider = "DnnPageHeaderProvider";

        /// <summary>The default javascript provider.</summary>
        internal const string DefaultJsProvider = "DnnBodyProvider";

        /// <summary>The <c>appSettings</c> key holding the route the composite (bundle) endpoint is served from.</summary>
        internal const string BundleRoutePathSettingKey = "ClientResourceManagement.BundleRoutePath";

        /// <summary>The <c>appSettings</c> key holding the folder the generated composite (bundle) files are persisted to.</summary>
        internal const string CompositeFilePathSettingKey = "ClientResourceManagement.CompositeFilePath";

        /// <summary>The default folder the generated composite (bundle) files are persisted to.</summary>
        internal const string DefaultCompositeFilePath = "~/App_Data/ClientDependency";

        private const string LegacyClientDependencySectionXPath = "section[@name='clientDependency']";

        private static readonly ILogger Logger = DnnLoggingController.GetLogger<ClientResourceManager>();
        private static readonly Dictionary<string, bool> FileExistsCache = new Dictionary<string, bool>();
        private static readonly ReaderWriterLockSlim LockFileExistsCache = new ReaderWriterLockSlim();

        /// <summary>Adds the necessary configuration to website root <c>web.config</c> to use the client resource management component.</summary>
        /// <remarks>
        /// On .NET Framework this registered the <c>clientDependency</c> configuration section, the
        /// <c>ClientDependencyModule</c> HTTP module and the <c>DependencyHandler.axd</c> handler. None of those types
        /// exist on modern .NET: the rendering providers are registered through dependency injection (see
        /// <see cref="DotNetNuke.Web.Client.Startup"/>) and the composite files are served by
        /// <see cref="CompositeFileBundleEndpoint"/>. The installer contract is preserved — this method now writes the
        /// modern equivalent configuration (the bundle endpoint route and the composite file output folder) so that a
        /// site which has been installed or upgraded has those values persisted in its <c>web.config</c>.
        /// </remarks>
        public static void AddConfiguration()
        {
            var configPath = GetWebConfigPath();
            if (string.IsNullOrEmpty(configPath))
            {
                return;
            }

            var xmlDoc = LoadConfig(configPath);

            var appSettingsConfig = xmlDoc.DocumentElement?.SelectSingleNode("appSettings");
            if (appSettingsConfig == null)
            {
                var appSettingsFragment = xmlDoc.CreateDocumentFragment();
                appSettingsFragment.InnerXml = "<appSettings />";
                xmlDoc.DocumentElement?.AppendChild(appSettingsFragment);
                appSettingsConfig = xmlDoc.DocumentElement?.SelectSingleNode("appSettings");
            }

            if (appSettingsConfig == null)
            {
                return;
            }

            AddAppSetting(xmlDoc, appSettingsConfig, BundleRoutePathSettingKey, CompositeFileBundleEndpoint.BundleRoutePath);
            AddAppSetting(xmlDoc, appSettingsConfig, CompositeFilePathSettingKey, DefaultCompositeFilePath);

            // Save Config
            xmlDoc.Save(configPath);
        }

        /// <summary>Remove the client dependency configuration from the <c>web.config</c>.</summary>
        /// <remarks>
        /// This is upgrade-cleanup logic: an existing site's <c>web.config</c> still contains the legacy
        /// <c>clientDependency</c> section, the <c>ClientDependencyModule</c> module and the
        /// <c>ClientDependencyHandler</c> handler, all of which reference types that no longer exist. They must be
        /// stripped out or the site fails to start.
        /// </remarks>
        public static void RemoveConfiguration()
        {
            Logger.ClientResourceManagerRemovingClientDependencyFromWebConfig();

            var configPath = GetWebConfigPath();
            if (string.IsNullOrEmpty(configPath))
            {
                return;
            }

            var xmlDoc = LoadConfig(configPath);

            // Config Sections
            var sectionsConfig = xmlDoc.DocumentElement?.SelectSingleNode("configSections");
            var clientDependencySectionConfig = sectionsConfig?.SelectSingleNode(LegacyClientDependencySectionXPath);
            if (clientDependencySectionConfig != null)
            {
                Logger.ClientResourceManagerRemovingClientDependencyConfigSection();
                sectionsConfig.RemoveChild(clientDependencySectionConfig);
            }

            // Module Config
            var systemWebServerModulesConfig = xmlDoc.DocumentElement?.SelectSingleNode("system.webServer/modules");
            var moduleConfig = systemWebServerModulesConfig?.SelectSingleNode("add[@name=\"ClientDependencyModule\"]");
            if (moduleConfig != null)
            {
                Logger.ClientResourceManagerRemovingClientDependencyModule();
                systemWebServerModulesConfig.RemoveChild(moduleConfig);
            }

            // Handler Config
            var systemWebServerHandlersConfig = xmlDoc.DocumentElement?.SelectSingleNode("system.webServer/handlers");
            var handlerConfig = systemWebServerHandlersConfig?.SelectSingleNode("add[@name=\"ClientDependencyHandler\"]");
            if (handlerConfig != null)
            {
                Logger.ClientResourceManagerRemovingClientDependencyHandler();
                systemWebServerHandlersConfig.RemoveChild(handlerConfig);
            }

            // ClientDependency Config
            var clientDependencyConfig = xmlDoc.DocumentElement?.SelectSingleNode("clientDependency");
            if (clientDependencyConfig != null)
            {
                Logger.ClientResourceManagerRemovingClientDependencyElement();
                xmlDoc.DocumentElement?.RemoveChild(clientDependencyConfig);
            }

            // Save Config
            xmlDoc.Save(configPath);
        }

        /// <summary>Checks if the legacy ClientDependency configuration is still present in the site's <c>web.config</c>.</summary>
        /// <returns>A value indicating whether the legacy <c>clientDependency</c> configuration section is declared.</returns>
        /// <remarks>
        /// The ClientDependency component itself no longer exists, so this is a legacy-detection probe used to decide
        /// whether <see cref="RemoveConfiguration"/> still has work to do.
        /// </remarks>
        public static bool IsInstalled()
        {
            var configPath = GetWebConfigPath();
            if (string.IsNullOrEmpty(configPath))
            {
                return false;
            }

            var xmlDoc = LoadConfig(configPath);

            return xmlDoc.DocumentElement?.SelectSingleNode("configSections")?.SelectSingleNode(LegacyClientDependencySectionXPath) != null;
        }

        /// <summary>Registers a stylesheet that has an admin level priority.</summary>
        /// <param name="context">The HTTP context of the request on which to register the style.</param>
        /// <param name="filePath">The path to the CSS stylesheet.</param>
        public static void RegisterAdminStylesheet(HttpContext context, string filePath)
        {
            RegisterStyleSheet(context, filePath, Client.FileOrder.Css.AdminCss);
        }

        /// <summary>Registers the <c>default.css</c> stylesheet.</summary>
        /// <param name="context">The HTTP context of the request on which to register the style.</param>
        /// <param name="filePath">The path to the CSS stylesheet.</param>
        public static void RegisterDefaultStylesheet(HttpContext context, string filePath)
        {
            RegisterStyleSheet(context, filePath, (int)Client.FileOrder.Css.DefaultCss, DefaultCssProvider, "dnndefault", "10.0.0");
        }

        /// <summary>Registers a stylesheet for a specific feature.</summary>
        /// <param name="context">The HTTP context of the request on which to register the style.</param>
        /// <param name="filePath">The path to the CSS stylesheet.</param>
        public static void RegisterFeatureStylesheet(HttpContext context, string filePath)
        {
            RegisterStyleSheet(context, filePath, Client.FileOrder.Css.FeatureCss);
        }

        /// <summary>Registers a stylesheet specific for Internet Explorer.</summary>
        /// <param name="context">The HTTP context of the request on which to register the style.</param>
        /// <param name="filePath">The path to the CSS stylesheet.</param>
        /// <remarks>
        /// ASP.NET Core has no equivalent of <c>HttpRequest.Browser</c> (the browser capabilities files are a
        /// .NET Framework only feature), so the original behavior is preserved by sniffing the <c>User-Agent</c>
        /// request header for Internet Explorer / Trident instead.
        /// </remarks>
        [DnnDeprecated(10, 0, 2, "Internet Explorer is no longer supported.")]
        public static partial void RegisterIEStylesheet(HttpContext context, string filePath)
        {
            if (IsInternetExplorer(context))
            {
                RegisterStyleSheet(context, filePath, Client.FileOrder.Css.IeCss);
            }
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        public static void RegisterScript(HttpContext context, string filePath)
        {
            RegisterScript(context, filePath, htmlAttributes: null);
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="htmlAttributes">A dictionary of HTML attributes to use for the <c>script</c> tag. The key being the attribute name and the value its value.</param>
        public static void RegisterScript(HttpContext context, string filePath, IDictionary<string, string> htmlAttributes)
        {
            RegisterScript(context, filePath, Client.FileOrder.Js.DefaultPriority, htmlAttributes);
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        public static void RegisterScript(HttpContext context, string filePath, int priority)
        {
            RegisterScript(context, filePath, priority, htmlAttributes: null);
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="htmlAttributes">A dictionary of HTML attributes to use for the <c>script</c> tag. The key being the attribute name and the value its value.</param>
        public static void RegisterScript(HttpContext context, string filePath, int priority, IDictionary<string, string> htmlAttributes)
        {
            RegisterScript(context, filePath, priority, DefaultJsProvider, htmlAttributes);
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        public static void RegisterScript(HttpContext context, string filePath, Client.FileOrder.Js priority)
        {
            RegisterScript(context, filePath, priority, htmlAttributes: null);
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="htmlAttributes">A dictionary of HTML attributes to use for the <c>script</c> tag. The key being the attribute name and the value its value.</param>
        public static void RegisterScript(HttpContext context, string filePath, Client.FileOrder.Js priority, IDictionary<string, string> htmlAttributes)
        {
            RegisterScript(context, filePath, (int)priority, DefaultJsProvider, htmlAttributes);
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The name of the provider responsible for rendering the script output.</param>
        public static void RegisterScript(HttpContext context, string filePath, Client.FileOrder.Js priority, string provider)
        {
            RegisterScript(context, filePath, priority, provider, htmlAttributes: null);
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The name of the provider responsible for rendering the script output.</param>
        /// /// <param name="htmlAttributes">A dictionary of HTML attributes to use for the <c>script</c> tag. The key being the attribute name and the value its value.</param>
        public static void RegisterScript(HttpContext context, string filePath, Client.FileOrder.Js priority, string provider, IDictionary<string, string> htmlAttributes)
        {
            RegisterScript(context, filePath, (int)priority, provider, htmlAttributes);
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The name of the provider responsible for rendering the script output.</param>
        public static void RegisterScript(HttpContext context, string filePath, int priority, string provider)
        {
            RegisterScript(context, filePath, priority, provider, htmlAttributes: null);
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The name of the provider responsible for rendering the script output.</param>
        /// <param name="htmlAttributes">A dictionary of HTML attributes to use for the <c>script</c> tag. The key being the attribute name and the value its value.</param>
        public static void RegisterScript(HttpContext context, string filePath, int priority, string provider, IDictionary<string, string> htmlAttributes)
        {
            RegisterScript(context, filePath, priority, provider, string.Empty, string.Empty, htmlAttributes);
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The name of the provider responsible for rendering the script output.</param>
        /// <param name="name">Name of framework like Bootstrap, Angular, etc.</param>
        /// <param name="version">Version number of framework.</param>
        public static void RegisterScript(HttpContext context, string filePath, int priority, string provider, string name, string version)
        {
            RegisterScript(context, filePath, priority, provider, name, version, htmlAttributes: null);
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The name of the provider responsible for rendering the script output.</param>
        /// <param name="name">Name of framework like Bootstrap, Angular, etc.</param>
        /// <param name="version">Version number of framework.</param>
        /// <param name="htmlAttributes">A dictionary of HTML attributes to use for the <c>script</c> tag. The key being the attribute name and the value its value.</param>
        public static void RegisterScript(
            HttpContext context,
            string filePath,
            int priority,
            string provider,
            string name,
            string version,
            IDictionary<string, string> htmlAttributes)
        {
            var controller = GetClientResourcesController(context);
            var script = controller.CreateScript(filePath)
                .SetPriority(priority)
                .SetProvider(provider)
                .SetNameAndVersion(name, version, false);
            if (htmlAttributes is not null)
            {
                foreach (var attribute in htmlAttributes)
                {
                    script = script.AddAttribute(attribute.Key, attribute.Value);
                }
            }

            script.Register();
        }

        /// <summary>Requests that a CSS file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        public static void RegisterStyleSheet(HttpContext context, string filePath)
        {
            RegisterStyleSheet(context, filePath, (int)Client.FileOrder.Css.DefaultPriority, DefaultCssProvider, htmlAttributes: null);
        }

        /// <summary>Requests that a CSS file be registered on the client browser.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="htmlAttributes">A dictionary of HTML attributes to use for the <c>link</c> tag. The key being the attribute name and the value its value.</param>
        public static void RegisterStyleSheet(HttpContext context, string filePath, IDictionary<string, string> htmlAttributes)
        {
            RegisterStyleSheet(context, filePath, (int)Client.FileOrder.Css.DefaultPriority, DefaultCssProvider, htmlAttributes);
        }

        /// <summary>Requests that a CSS file be registered on the client browser. Defaults to rendering in the page header.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        public static void RegisterStyleSheet(HttpContext context, string filePath, int priority)
        {
            RegisterStyleSheet(context, filePath, priority, DefaultCssProvider, htmlAttributes: null);
        }

        /// <summary>Requests that a CSS file be registered on the client browser. Defaults to rendering in the page header.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="htmlAttributes">A dictionary of HTML attributes to use for the <c>link</c> tag. The key being the attribute name and the value its value.</param>
        public static void RegisterStyleSheet(HttpContext context, string filePath, int priority, IDictionary<string, string> htmlAttributes)
        {
            RegisterStyleSheet(context, filePath, priority, DefaultCssProvider, htmlAttributes);
        }

        /// <summary>Requests that a CSS file be registered on the client browser. Defaults to rendering in the page header.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        public static void RegisterStyleSheet(HttpContext context, string filePath, Client.FileOrder.Css priority)
        {
            RegisterStyleSheet(context, filePath, (int)priority, DefaultCssProvider, htmlAttributes: null);
        }

        /// <summary>Requests that a CSS file be registered on the client browser. Defaults to rendering in the page header.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="htmlAttributes">A dictionary of HTML attributes to use for the <c>link</c> tag. The key being the attribute name and the value its value.</param>
        public static void RegisterStyleSheet(HttpContext context, string filePath, Client.FileOrder.Css priority, IDictionary<string, string> htmlAttributes)
        {
            RegisterStyleSheet(context, filePath, (int)priority, DefaultCssProvider, htmlAttributes);
        }

        /// <summary>Requests that a CSS file be registered on the client browser. Allows for overriding the default provider.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The provider name to be used to render the css file on the page.</param>
        public static void RegisterStyleSheet(HttpContext context, string filePath, int priority, string provider)
        {
            RegisterStyleSheet(context, filePath, priority, provider, string.Empty, string.Empty, htmlAttributes: null);
        }

        /// <summary>Requests that a CSS file be registered on the client browser. Allows for overriding the default provider.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The provider name to be used to render the css file on the page.</param>
        /// <param name="htmlAttributes">A dictionary of HTML attributes to use for the <c>link</c> tag. The key being the attribute name and the value its value.</param>
        public static void RegisterStyleSheet(HttpContext context, string filePath, int priority, string provider, IDictionary<string, string> htmlAttributes)
        {
            RegisterStyleSheet(context, filePath, priority, provider, string.Empty, string.Empty, htmlAttributes);
        }

        /// <summary>Requests that a CSS file be registered on the client browser. Allows for overriding the default provider.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The provider name to be used to render the css file on the page.</param>
        /// <param name="name">Name of framework like Bootstrap, Angular, etc.</param>
        /// <param name="version">Version number of framework.</param>
        public static void RegisterStyleSheet(HttpContext context, string filePath, int priority, string provider, string name, string version)
        {
            RegisterStyleSheet(context, filePath, priority, provider, name, version, htmlAttributes: null);
        }

        /// <summary>Requests that a CSS file be registered on the client browser. Allows for overriding the default provider.</summary>
        /// <param name="context">The HTTP context of the current request. Used to get a reference to the client resource controller.</param>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The provider name to be used to render the css file on the page.</param>
        /// <param name="name">Name of framework like Bootstrap, Angular, etc.</param>
        /// <param name="version">Version number of framework.</param>
        /// <param name="htmlAttributes">A dictionary of HTML attributes to use for the <c>link</c> tag. The key being the attribute name and the value its value.</param>
        public static void RegisterStyleSheet(HttpContext context, string filePath, int priority, string provider, string name, string version, IDictionary<string, string> htmlAttributes)
        {
            var fileExists = false;

            // Some "legacy URLs" could be using their own query string versioning scheme (and we've forced them to use the new API through re-routing PageBase.RegisterStyleSheet
            // Ensure that physical CSS files with query strings have their query strings removed
            // Ignore absolute urls, they will not exist locally
            if (!Uri.TryCreate(filePath, UriKind.Absolute, out _) && filePath.Contains(".css?"))
            {
                var filePathSansQueryString = RemoveQueryString(filePath);
                if (File.Exists(MapPath(context, filePathSansQueryString)))
                {
                    fileExists = true;
                    filePath = filePathSansQueryString;
                }
            }
            else if (filePath.Contains("WebResource.axd"))
            {
                fileExists = true;
            }

            if (!fileExists && !FileExists(context, filePath))
            {
                return;
            }

            var controller = GetClientResourcesController(context);
            var stylesheet = controller.CreateStylesheet(filePath)
                .SetPriority(priority)
                .SetProvider(provider)
                .SetNameAndVersion(name, version, false);
            if (htmlAttributes is not null)
            {
                foreach (var attribute in htmlAttributes)
                {
                    stylesheet = stylesheet.AddAttribute(attribute.Key, attribute.Value);
                }
            }

            stylesheet.Register();
        }

        /// <summary>Clear the default composite files so that it can be generated next time.</summary>
        public static void ClearCache()
        {
        }

        /// <summary>Clears the cache used for file existence.</summary>
        /// <param name="path">The path for the file.</param>
        public static void ClearFileExistsCache(string path)
        {
            LockFileExistsCache.EnterWriteLock();
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    FileExistsCache.Clear();
                }
                else
                {
                    FileExistsCache.Remove(path.ToLowerInvariant());
                }
            }
            finally
            {
                LockFileExistsCache.ExitWriteLock();
            }
        }

        /// <summary>Enables the async postback handler.</summary>
        public static void EnableAsyncPostBackHandler()
        {
            var context = GetCurrentHttpContext();
            if (context != null && !context.Items.ContainsKey("AsyncPostBackHandlerEnabled"))
            {
                context.Items.Add("AsyncPostBackHandlerEnabled", true);
            }
        }

        private static void AddAppSetting(XmlDocument xmlDoc, XmlNode appSettingsConfig, string key, string value)
        {
            if (appSettingsConfig.SelectSingleNode($"add[@key='{key}']") != null)
            {
                return;
            }

            var xmlFrag = xmlDoc.CreateDocumentFragment();
            xmlFrag.InnerXml = $"""<add key="{key}" value="{value}" />""";
            appSettingsConfig.AppendChild(xmlFrag);
        }

        private static XmlDocument LoadConfig(string configPath)
        {
            var xmlDoc = new XmlDocument { XmlResolver = null };
            using (var configReader = XmlReader.Create(configPath, new XmlReaderSettings { XmlResolver = null, }))
            {
                xmlDoc.Load(configReader);
            }

            return xmlDoc;
        }

        private static bool IsInternetExplorer(HttpContext context)
        {
            var userAgent = context?.Request?.Headers.UserAgent.ToString();
            if (string.IsNullOrEmpty(userAgent))
            {
                return false;
            }

            // "MSIE" identifies Internet Explorer up to and including version 10,
            // "Trident" identifies Internet Explorer 11 (whose user agent no longer contains "MSIE").
            return userAgent.Contains("MSIE", StringComparison.OrdinalIgnoreCase)
                   || userAgent.Contains("Trident", StringComparison.OrdinalIgnoreCase)
                   || userAgent.Contains("Internet Explorer", StringComparison.OrdinalIgnoreCase);
        }

        private static bool FileExists(HttpContext context, string filePath)
        {
            // remove query string for the file exists check, won't impact the absoluteness, so just do it either way.
            filePath = RemoveQueryString(filePath);
            var cacheKey = filePath.ToLowerInvariant();

            // cache css file paths
            if (!FileExistsCache.ContainsKey(cacheKey))
            {
                // apply lock after IF, locking is more expensive than worst case scenario (check disk twice)
                LockFileExistsCache.EnterWriteLock();
                try
                {
                    FileExistsCache[cacheKey] = IsAbsoluteUrl(filePath) || File.Exists(MapPath(context, filePath));
                }
                finally
                {
                    LockFileExistsCache.ExitWriteLock();
                }
            }

            // return if file exists from cache
            LockFileExistsCache.EnterReadLock();
            try
            {
                return FileExistsCache[cacheKey];
            }
            finally
            {
                LockFileExistsCache.ExitReadLock();
            }
        }

        private static bool IsAbsoluteUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out _);
        }

        private static string RemoveQueryString(string filePath)
        {
            var queryStringPosition = filePath.IndexOf('?');
            return queryStringPosition != -1 ? filePath.Substring(0, queryStringPosition) : filePath;
        }

        /// <summary>Resolves a virtual path into a physical path, replacing <c>HttpServerUtility.MapPath</c>.</summary>
        /// <param name="context">The HTTP context of the current request, used to resolve <see cref="IApplicationStatusInfo"/>.</param>
        /// <param name="virtualPath">The application-relative virtual path.</param>
        /// <returns>The physical path, or <see cref="string.Empty"/> when the application root cannot be determined.</returns>
        private static string MapPath(HttpContext context, string virtualPath)
        {
            var applicationMapPath = GetApplicationMapPath(context);
            return MapPath(applicationMapPath, virtualPath);
        }

        private static string MapPath(string applicationMapPath, string virtualPath)
        {
            if (string.IsNullOrEmpty(applicationMapPath) || string.IsNullOrEmpty(virtualPath))
            {
                return string.Empty;
            }

            var relativePath = virtualPath;
            if (relativePath.StartsWith("~/", StringComparison.Ordinal))
            {
                relativePath = relativePath.Substring(2);
            }
            else if (relativePath.StartsWith('~') || relativePath.StartsWith('/'))
            {
                relativePath = relativePath.Substring(1);
            }

            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(applicationMapPath, relativePath);
        }

        private static string GetApplicationMapPath(HttpContext context)
        {
            var serviceProvider = context?.RequestServices ?? DependencyInjection.TryGetCurrentServiceProvider();
            return serviceProvider?.GetService<IApplicationStatusInfo>()?.ApplicationMapPath;
        }

        /// <summary>Gets the physical path of the website root <c>web.config</c>.</summary>
        /// <returns>The physical path, or <see cref="string.Empty"/> when the application root cannot be determined.</returns>
        /// <remarks>
        /// This replaces <c>HostingEnvironment.MapPath("~/web.config")</c>. These configuration methods are called by
        /// the installer/upgrader outside of a request, so the application root comes from
        /// <see cref="IApplicationStatusInfo.ApplicationMapPath"/> resolved from the current service provider rather
        /// than from a request's <see cref="HttpContext.RequestServices"/>.
        /// </remarks>
        private static string GetWebConfigPath()
        {
            return MapPath(GetApplicationMapPath(GetCurrentHttpContext()), "~/web.config");
        }

        private static HttpContext GetCurrentHttpContext()
        {
            return DependencyInjection.TryGetCurrentServiceProvider()?.GetService<IHttpContextAccessor>()?.HttpContext;
        }

        private static IClientResourceController GetClientResourcesController(HttpContext context)
        {
            return context.RequestServices.GetRequiredService<IClientResourceController>();
        }
    }
}
