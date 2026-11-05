// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Collections;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Xml;
    using System.Xml.Serialization;
    using System.Xml.XPath;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// The client side capabilities of the known browsers, loaded once and then cached.
    /// </summary>
    /// <remarks>
    /// The WebForms implementation located <c>ClientAPICaps.config</c> through
    /// <c>Server.MapPath(ClientAPI.ScriptPath)</c> and fell back to the embedded resource of the same
    /// name. On ASP.NET Core the capabilities are resolved, in order of precedence, from the
    /// <see cref="DnnWebUtilityOptions.BrowserCapsPath"/> file override, from the
    /// <c>DnnWebUtility:BrowserCaps</c> configuration section and finally from the embedded
    /// <c>ClientAPICaps.config</c> resource. The load-and-cache-on-first-use behavior and the
    /// invalidation of the cache when the override file changes are preserved.
    /// </remarks>
    [Serializable]
    [XmlRoot("capabilities")]
    public class BrowserCaps
    {
        /// <summary>The key the loaded capabilities are cached under.</summary>
        private const string ClientapiCacheKey = "ClientAPICaps";

        /// <summary>The name of the configuration section the capabilities may be configured in.</summary>
        private const string BrowserCapsSectionName = DnnWebUtilityOptions.SectionName + ":BrowserCaps";

        /// <summary>The file name of the capabilities document, both as a file and as an embedded resource.</summary>
        private const string CapsFileName = "ClientAPICaps.config";

        private Hashtable functionalityDict;

        /// <summary>Gets or sets the capabilities of each piece of client side functionality.</summary>
        [XmlElement("functionality")]
        public FunctionalityCollection Functionality { get; set; }

        /// <summary>Gets a free-form lookup the Client API uses to memoize per-request capability answers.</summary>
        public Hashtable FunctionalityDictionary
        {
            get
            {
                this.functionalityDict ??= new Hashtable();
                return this.functionalityDict;
            }
        }

        /// <summary>Gets the browser capabilities, loading and caching them on first use.</summary>
        /// <returns>The browser capabilities, never <see langword="null"/>.</returns>
        public static BrowserCaps GetBrowserCaps()
        {
            var objCaps = DataCache.GetCache(ClientapiCacheKey) as BrowserCaps;

            if (objCaps is null)
            {
                var strPath = GetOverrideFilePath();
                var fileExists = !string.IsNullOrEmpty(strPath) && File.Exists(strPath);

                objCaps = new BrowserCaps();
                objCaps.Functionality = new FunctionalityCollection();

                if (fileExists)
                {
                    using (var objReader = new FileStream(strPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (var xmlReader = XmlReader.Create(objReader))
                        {
                            LoadFunctionality(objCaps, new XPathDocument(xmlReader));
                        }
                    }
                }
                else if (!TryLoadFromConfiguration(objCaps))
                {
                    using (var tr = new StreamReader(GetEmbeddedCapsStream()))
                    {
                        using (var xmlReader = XmlReader.Create(tr))
                        {
                            LoadFunctionality(objCaps, new XPathDocument(xmlReader));
                        }
                    }
                }

                // Set back into Cache
                if (fileExists)
                {
                    DataCache.SetCache(ClientapiCacheKey, objCaps, DataCache.CreateFileDependency(strPath));
                }
                else
                {
                    DataCache.SetCache(ClientapiCacheKey, objCaps, (MemoryCacheEntryOptions)null);
                }
            }

            return objCaps;
        }

        private static Browser GetBrowser(XPathNavigator objNav)
        {
            var objBrowser = new Browser();
            objBrowser.Contains = objNav.GetAttribute("contains", string.Empty);
            objBrowser.Name = objNav.GetAttribute("nm", string.Empty);
            var strMinVersion = objNav.GetAttribute("minversion", string.Empty);
            if (!string.IsNullOrEmpty(strMinVersion))
            {
                objBrowser.MinVersion = double.Parse(strMinVersion, CultureInfo.InvariantCulture);
            }

            return objBrowser;
        }

        private static void LoadFunctionality(BrowserCaps objCaps, XPathDocument objDoc)
        {
            if (objDoc is null)
            {
                return;
            }

            foreach (XPathNavigator objNavFunc in objDoc.CreateNavigator().Select("//functionality"))
            {
                var objFunc = new FunctionalityInfo();
                objFunc.Functionality = Enum.Parse<ClientAPI.ClientFunctionality>(objNavFunc.GetAttribute("nm", string.Empty));
                objFunc.Desc = objNavFunc.GetAttribute("desc", string.Empty);

                objFunc.Supports = new BrowserCollection();
                foreach (XPathNavigator objNavSupports in objNavFunc.CreateNavigator().Select("supports/browser"))
                {
                    objFunc.Supports.Add(GetBrowser(objNavSupports));
                }

                objFunc.Excludes = new BrowserCollection();
                foreach (XPathNavigator objNavExcludes in objNavFunc.CreateNavigator().Select("excludes/browser"))
                {
                    objFunc.Excludes.Add(GetBrowser(objNavExcludes));
                }

                objCaps.Functionality.Add(objFunc);
            }
        }

        private static bool TryLoadFromConfiguration(BrowserCaps objCaps)
        {
            var configuration = DnnWebUtilityContext.RequestServices?.GetService<IConfiguration>();
            var section = configuration?.GetSection(BrowserCapsSectionName);
            if (section is null || !section.Exists())
            {
                return false;
            }

            var any = false;
            foreach (var funcSection in section.GetChildren())
            {
                var name = funcSection["nm"];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var objFunc = new FunctionalityInfo();
                objFunc.Functionality = Enum.Parse<ClientAPI.ClientFunctionality>(name);
                objFunc.Desc = funcSection["desc"] ?? string.Empty;
                objFunc.Supports = ReadBrowsers(funcSection.GetSection("supports"));
                objFunc.Excludes = ReadBrowsers(funcSection.GetSection("excludes"));

                objCaps.Functionality.Add(objFunc);
                any = true;
            }

            return any;
        }

        private static BrowserCollection ReadBrowsers(IConfigurationSection section)
        {
            var browsers = new BrowserCollection();
            if (section is null || !section.Exists())
            {
                return browsers;
            }

            foreach (var browserSection in section.GetChildren())
            {
                var browser = new Browser();
                browser.Contains = browserSection["contains"] ?? string.Empty;
                browser.Name = browserSection["nm"] ?? string.Empty;
                var minVersion = browserSection["minversion"];
                if (!string.IsNullOrEmpty(minVersion))
                {
                    browser.MinVersion = double.Parse(minVersion, CultureInfo.InvariantCulture);
                }

                browsers.Add(browser);
            }

            return browsers;
        }

        private static string GetOverrideFilePath()
        {
            try
            {
                var configured = DnnWebUtilityContext.Options?.BrowserCapsPath;
                if (string.IsNullOrEmpty(configured))
                {
                    // Parity with the original Server.MapPath(ClientAPI.ScriptPath & "/ClientAPICaps.config"):
                    // a site that drops its own ClientAPICaps.config into the script folder must still
                    // override the embedded defaults even when no explicit path is configured.
                    var env = DnnWebUtilityContext.RequestServices?.GetService<IWebHostEnvironment>()
                              ?? DnnWebUtilityContext.ApplicationServices?.GetService<IWebHostEnvironment>();
                    var root = env?.WebRootPath;
                    if (string.IsNullOrEmpty(root))
                    {
                        root = env?.ContentRootPath;
                    }

                    if (string.IsNullOrEmpty(root))
                    {
                        return string.Empty;
                    }

                    var relative = (ClientAPI.ScriptPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar);
                    return Path.GetFullPath(Path.Combine(root, relative, CapsFileName));
                }

                return Path.GetFullPath(
                    Directory.Exists(configured) ? Path.Combine(configured, CapsFileName) : configured);
            }
            catch (Exception)
            {
                // ignore error - worried about people with reverse proxies and such...
                return string.Empty;
            }
        }

        private static Stream GetEmbeddedCapsStream()
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (resourceName.EndsWith(CapsFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return assembly.GetManifestResourceStream(resourceName);
                }
            }

            throw new FileNotFoundException(
                string.Format(CultureInfo.CurrentCulture, "The embedded resource '{0}' could not be found.", CapsFileName),
                CapsFileName);
        }
    }
}
