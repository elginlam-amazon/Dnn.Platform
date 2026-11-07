// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Providers.AspNetClientCapabilityProvider
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Common;
    using DotNetNuke.Services.ClientCapability;

    using Microsoft.AspNetCore.Http;

    /// <summary>AspNet.BrowserDetector implementation of <see cref="DotNetNuke.Services.ClientCapability.IClientCapabilityProvider"/>.</summary>
    public class AspNetClientCapabilityProvider : ClientCapabilityProvider
    {
        private const int DesktopScreenPixelsWidth = 1024;
        private const int DesktopScreenPixelsHeight = 768;
        private const int MobileScreenPixelsWidth = 480;
        private const int MobileScreenPixelsHeight = 800;

        private static readonly object AllCapabilitiesLock = new object();

        private static readonly string[] MobileAgentTokens =
        {
            "mobile", "android", "iphone", "ipod", "ipad", "windows phone", "windows ce", "blackberry",
            "bb10", "opera mini", "opera mobi", "iemobile", "symbian", "webos", "palm", "kindle",
            "silk", "fennec", "maemo", "meego", "midp", "netfront", "nokia", "playbook", "tablet", "wap",
        };

        private static readonly object AllClientCapabilityValuesLock = new object();
        private static IQueryable<IClientCapability> allCapabilities;
        private static Dictionary<string, List<string>> allClientCapabilityValues;
        private static IDictionary<string, int> highPiorityCapabilityValues;
        private static IDictionary<string, string> sampleProperies;

        /// <inheritdoc />
        public override bool SupportsTabletDetection
        {
            get { return false; }
        }

        /// <summary>
        /// Gets all of the capabilities which are known to this provider, that is the capabilities
        /// of the browser issuing the current request.
        /// </summary>
        /// <remarks>
        /// On ASP.NET Core there is no ambient request context, so when this is accessed outside
        /// of a request (application start-up, a background thread or a Blazor Server interactive
        /// circuit) there is no user agent to inspect and the generic sample capabilities are
        /// returned instead of throwing. That fallback is deliberately NOT cached, so the first
        /// real request still populates the cache with actual browser capabilities.
        /// </remarks>
        private static IQueryable<IClientCapability> AllCapabilities
        {
            get
            {
                if (allCapabilities == null)
                {
                    var request = HttpContextSource.Current?.Request;
                    if (request == null)
                    {
                        return new List<IClientCapability> { new AspNetClientCapability(SampleProperties) }.AsQueryable();
                    }

                    lock (AllCapabilitiesLock)
                    {
                        if (allCapabilities == null)
                        {
                            var capabilities = new List<IClientCapability> { new AspNetClientCapability(request) };
                            allCapabilities = capabilities.AsQueryable();
                        }
                    }
                }

                return allCapabilities;
            }
        }

        private static IDictionary<string, List<string>> ClientCapabilityValues
        {
            get
            {
                if (allClientCapabilityValues == null)
                {
                    lock (AllClientCapabilityValuesLock)
                    {
                        if (allClientCapabilityValues == null)
                        {
                            allClientCapabilityValues = new Dictionary<string, List<string>>();

                            // TODO :Implement
                            ////foreach (var property in DataProvider.Properties)
                            ////{
                            ////   var values = property.Values.Select(value => value.Name).ToList();
                            ////   _allClientCapabilityValues.Add(property.Name, values);
                            ////}
                        }
                    }

                    var props = HighPiorityCapabilityValues;
                    allClientCapabilityValues = allClientCapabilityValues.OrderByDescending(
                        kvp =>
                        {
                            if (props.ContainsKey(kvp.Key))
                            {
                                return HighPiorityCapabilityValues[kvp.Key];
                            }

                            return 0;
                        }).ThenBy(kvp => kvp.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
                }

                return allClientCapabilityValues;
            }
        }

        private static IDictionary<string, int> HighPiorityCapabilityValues
        {
            get
            {
                return highPiorityCapabilityValues ?? (highPiorityCapabilityValues = new Dictionary<string, int>
                {
                    { "IsMobile", 100 },
                    { "IsTablet", 95 },
                    { "PlatformName", 90 },
                    { "BrowserName", 85 },
                    { "BrowserVersion", 80 },
                    { "HasTouchScreen", 75 },
                    { "PlatformVersion", 70 },
                    { "ScreenPixelsWidth", 65 },
                    { "ScreenPixelsHeight", 60 },
                    { "HardwareVendor", 55 },
                });
            }
        }

        private static IDictionary<string, string> SampleProperties
        {
            get
            {
                return sampleProperies ??
                       (sampleProperies = new Dictionary<string, string>
                       {
                           { "Id", "UNKNOWN" },
                           { "IsMobile", "false" },
                           { "ScreenPixelsWidth", "600" },
                           { "ScreenPixelsHeight", "800" },
                           { "IsTablet", "false" },
                           { "HasTouchScreen", "false" },
                           { "BrowserName", "???" },
                           { "SupportsFlash", "false" },
                       });
            }
        }

        /// <summary>Gets the browser capabilities for a request, by parsing its user agent.</summary>
        /// <param name="headers">The request headers, may be <see langword="null"/> when no request is available.</param>
        /// <param name="userAgent">The request's user agent.</param>
        /// <returns>A <see cref="BrowserCapabilities"/> instance, never <see langword="null"/>.</returns>
        /// <remarks>
        /// Replaces the .NET Framework <c>BrowserCapabilitiesFactory</c> and its <c>.browser</c>
        /// definition files, which do not exist on modern .NET. The browser name/version, the
        /// mobile flag and the device screen estimate are derived from the user-agent string, and
        /// the resulting capability bag is keyed case-insensitively just as the old
        /// <c>HttpCapabilitiesBase.Capabilities</c> hash table was.
        /// </remarks>
        public static BrowserCapabilities GetBrowserCapabilities(IHeaderDictionary headers, string userAgent)
        {
            userAgent ??= string.Empty;
            var browserCaps = new BrowserCapabilities();

            // the empty key holds the raw user agent, as it did on ASP.NET.
            browserCaps.Capabilities[string.Empty] = userAgent;

            var lowerAgent = userAgent.ToLowerInvariant();
            var isMobile = IsMobileUserAgent(lowerAgent);
            var browserName = GetBrowserName(lowerAgent);
            var browserVersion = GetBrowserVersion(lowerAgent, browserName);

            browserCaps.Browser = browserName;
            browserCaps.Version = browserVersion;
            browserCaps.Id = browserName;
            browserCaps.IsMobileDevice = isMobile;
            browserCaps.ScreenPixelsWidth = isMobile ? MobileScreenPixelsWidth : DesktopScreenPixelsWidth;
            browserCaps.ScreenPixelsHeight = isMobile ? MobileScreenPixelsHeight : DesktopScreenPixelsHeight;

            browserCaps.Capabilities["Id"] = browserCaps.Id;
            browserCaps.Capabilities["browser"] = browserName;
            browserCaps.Capabilities["BrowserName"] = browserName;
            browserCaps.Capabilities["version"] = browserVersion;
            browserCaps.Capabilities["BrowserVersion"] = browserVersion;
            browserCaps.Capabilities["IsMobile"] = isMobile.ToString(CultureInfo.InvariantCulture);
            browserCaps.Capabilities["isMobileDevice"] = isMobile.ToString(CultureInfo.InvariantCulture);
            browserCaps.Capabilities["ScreenPixelsWidth"] = browserCaps.ScreenPixelsWidth.ToString(CultureInfo.InvariantCulture);
            browserCaps.Capabilities["ScreenPixelsHeight"] = browserCaps.ScreenPixelsHeight.ToString(CultureInfo.InvariantCulture);
            browserCaps.Capabilities["CookiesCapable"] = bool.TrueString;

            if (headers != null)
            {
                // the ASP.NET factory also considered the request headers, keep them available.
                foreach (var header in headers)
                {
                    browserCaps.Capabilities["header:" + header.Key] = header.Value.ToString();
                }
            }

            return browserCaps;
        }

        /// <inheritdoc />
        public override IClientCapability GetClientCapability(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
            {
                return new AspNetClientCapability(SampleProperties);
            }

            return new AspNetClientCapability(userAgent, GetBrowserCapabilities(null, userAgent));
        }

        /// <inheritdoc />
        /// <remarks>
        /// Looking a device up by its identifier is not a capability of this provider: it detects
        /// the device of the current request from its user agent and has no device database to
        /// query. The behavior is unchanged from the .NET Framework version, which threw for the
        /// same reason.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Always thrown; this provider cannot resolve a device by its identifier.</exception>
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", Justification = "Breaking change")]
        public override IClientCapability GetClientCapabilityById(string deviceId)
        {
            Requires.NotNullOrEmpty("deviceId", deviceId);

            throw new InvalidOperationException($"Can't get device capability for the id '{deviceId}'");
        }

        /// <inheritdoc />
        public override IDictionary<string, List<string>> GetAllClientCapabilityValues()
        {
            return ClientCapabilityValues;
        }

        /// <inheritdoc />
        public override IQueryable<IClientCapability> GetAllClientCapabilities()
        {
            return AllCapabilities;
        }

        /// <summary>Determines whether a (lowercase) user agent belongs to a mobile device.</summary>
        /// <param name="lowerAgent">The lowercase user agent.</param>
        /// <returns><see langword="true"/> when the user agent contains a known mobile token.</returns>
        private static bool IsMobileUserAgent(string lowerAgent)
        {
            return MobileAgentTokens.Any(token => lowerAgent.Contains(token, StringComparison.Ordinal));
        }

        /// <summary>Gets the browser name from a (lowercase) user agent.</summary>
        /// <param name="lowerAgent">The lowercase user agent.</param>
        /// <returns>The browser name, or <c>Unknown</c> when it cannot be determined.</returns>
        private static string GetBrowserName(string lowerAgent)
        {
            if (lowerAgent.Length == 0)
            {
                return "Unknown";
            }

            // the order matters, most browsers impersonate the ones below them.
            if (lowerAgent.Contains("edg", StringComparison.Ordinal))
            {
                return "Edge";
            }

            if (lowerAgent.Contains("opr/", StringComparison.Ordinal) || lowerAgent.Contains("opera", StringComparison.Ordinal))
            {
                return "Opera";
            }

            if (lowerAgent.Contains("samsungbrowser", StringComparison.Ordinal))
            {
                return "SamsungBrowser";
            }

            if (lowerAgent.Contains("chrome", StringComparison.Ordinal) || lowerAgent.Contains("crios", StringComparison.Ordinal))
            {
                return "Chrome";
            }

            if (lowerAgent.Contains("firefox", StringComparison.Ordinal) || lowerAgent.Contains("fxios", StringComparison.Ordinal))
            {
                return "Firefox";
            }

            if (lowerAgent.Contains("msie", StringComparison.Ordinal) || lowerAgent.Contains("trident", StringComparison.Ordinal))
            {
                return "IE";
            }

            if (lowerAgent.Contains("safari", StringComparison.Ordinal) || lowerAgent.Contains("applewebkit", StringComparison.Ordinal))
            {
                return "Safari";
            }

            return "Unknown";
        }

        /// <summary>Gets the browser version from a (lowercase) user agent.</summary>
        /// <param name="lowerAgent">The lowercase user agent.</param>
        /// <param name="browserName">The browser name, as returned by <c>GetBrowserName</c>.</param>
        /// <returns>The browser version, or an empty string when it cannot be determined.</returns>
        private static string GetBrowserVersion(string lowerAgent, string browserName)
        {
            string[] tokens;
            switch (browserName)
            {
                case "Edge":
                    tokens = new[] { "edge/", "edg/", "edgios/", "edga/" };
                    break;
                case "Opera":
                    tokens = new[] { "opr/", "opera/", "opera mini/" };
                    break;
                case "SamsungBrowser":
                    tokens = new[] { "samsungbrowser/" };
                    break;
                case "Chrome":
                    tokens = new[] { "chrome/", "crios/" };
                    break;
                case "Firefox":
                    tokens = new[] { "firefox/", "fxios/" };
                    break;
                case "IE":
                    tokens = new[] { "msie ", "rv:" };
                    break;
                case "Safari":
                    tokens = new[] { "version/", "safari/" };
                    break;
                default:
                    return string.Empty;
            }

            foreach (var token in tokens)
            {
                var index = lowerAgent.IndexOf(token, StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                var start = index + token.Length;
                var end = start;
                while (end < lowerAgent.Length && (char.IsDigit(lowerAgent[end]) || lowerAgent[end] == '.'))
                {
                    end++;
                }

                if (end > start)
                {
                    return lowerAgent.Substring(start, end - start).TrimEnd('.');
                }
            }

            return string.Empty;
        }
    }
}
