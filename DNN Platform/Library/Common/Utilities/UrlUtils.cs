// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Common.Utilities
{
    using System.Text.Encodings.Web;
    using System;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Abstractions.Security;
    using DotNetNuke.Entities.Controllers;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Security;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    /// <summary>Provides utilities for dealing with DNN's URLs. Consider using <see cref="System.Uri"/> if applicable.</summary>
    public static partial class UrlUtils
    {
        private static INavigationManager NavigationManager => Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();

        /// <summary>Combines two URLs, trimming any slashes between them.</summary>
        /// <param name="baseUrl">The base URL.</param>
        /// <param name="relativeUrl">The URL to add to the base URL.</param>
        /// <returns>A new URL that combines <paramref name="baseUrl"/> and <paramref name="relativeUrl"/>.</returns>
        public static string Combine(string baseUrl, string relativeUrl)
        {
            if (baseUrl.Length == 0)
            {
                return relativeUrl;
            }

            if (relativeUrl.Length == 0)
            {
                return baseUrl;
            }

            baseUrl = baseUrl.TrimEnd('/', '\\');
            relativeUrl = relativeUrl.TrimStart('/', '\\');
            return $"{baseUrl}/{relativeUrl}";
        }

        /// <summary>Decodes a base64 encoded value generated via <see cref="EncodeParameter"/>.</summary>
        /// <param name="value">The encoded value.</param>
        /// <returns>The decoded value.</returns>
        public static string DecodeParameter(string value)
        {
            value = value.Replace('-', '+').Replace('_', '/').Replace('$', '=');
            byte[] arrBytes = Convert.FromBase64String(value);
            return Encoding.UTF8.GetString(arrBytes);
        }

        /// <summary>Decrypts an encrypted value generated via <see cref="EncryptParameter(string)"/>. Decrypted using the current portal's <see cref="IPortalSettings.GUID"/>.</summary>
        /// <param name="value">The encrypted value.</param>
        /// <returns>The decrypted value.</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking ICryptographyProvider")]
        public static partial string DecryptParameter(string value)
            => DecryptParameter(Globals.GetCurrentServiceProvider().GetRequiredService<ICryptographyProvider>(), value);

        /// <summary>Decrypts an encrypted value generated via <see cref="EncryptParameter(ICryptographyProvider,string)"/>. Decrypted using the current portal's <see cref="IPortalSettings.GUID"/>.</summary>
        /// <param name="cryptographyProvider">The cryptography provider.</param>
        /// <param name="value">The encrypted value.</param>
        /// <returns>The decrypted value.</returns>
        public static string DecryptParameter(ICryptographyProvider cryptographyProvider, string value)
            => DecryptParameter(cryptographyProvider, value, PortalController.Instance.GetCurrentSettings().GUID.ToString());

        /// <summary>Decrypts an encrypted value generated via <see cref="EncryptParameter(string,string)"/>.</summary>
        /// <param name="value">The encrypted value.</param>
        /// <param name="encryptionKey">The key used to encrypt the value.</param>
        /// <returns>The decrypted value.</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking ICryptographyProvider")]
        public static partial string DecryptParameter(string value, string encryptionKey)
        {
            return DecryptParameter(Globals.GetCurrentServiceProvider().GetRequiredService<ICryptographyProvider>(), value, encryptionKey);
        }

        /// <summary>Decrypts an encrypted value generated via <see cref="EncryptParameter(ICryptographyProvider,string,string)"/>.</summary>
        /// <param name="cryptographyProvider">The cryptography provider.</param>
        /// <param name="value">The encrypted value.</param>
        /// <param name="encryptionKey">The key used to encrypt the value.</param>
        /// <returns>The decrypted value.</returns>
        public static string DecryptParameter(ICryptographyProvider cryptographyProvider, string value, string encryptionKey)
        {
            return DecryptParameter(DecryptWithCryptographyProvider, value, encryptionKey);

            string DecryptWithCryptographyProvider(string message, string passphrase)
            {
                return cryptographyProvider.DecryptParameter(message, passphrase, cryptographyProvider.EncryptParameterAlgorithmName);
            }
        }

        /// <summary>Encodes a value (using base64) for placing in a URL.</summary>
        /// <param name="value">The value to encode.</param>
        /// <returns>The encoded value.</returns>
        public static string EncodeParameter(string value)
        {
            byte[] arrBytes = Encoding.UTF8.GetBytes(value);
            var toEncode = new StringBuilder(Convert.ToBase64String(arrBytes));
            toEncode.Replace('+', '-');
            toEncode.Replace('/', '_');
            toEncode.Replace('=', '$');
            return toEncode.ToString();
        }

        /// <summary>Encrypt a parameter for placing in a URL. Encrypted using the current portal's <see cref="IPortalSettings.GUID"/>.</summary>
        /// <param name="value">The value to encrypt.</param>
        /// <returns>The encrypted value.</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking ICryptographyProvider")]
        public static partial string EncryptParameter(string value)
            => EncryptParameter(Globals.GetCurrentServiceProvider().GetRequiredService<ICryptographyProvider>(), value);

        /// <summary>Encrypt a parameter for placing in a URL. Encrypted using the current portal's <see cref="IPortalSettings.GUID"/>.</summary>
        /// <param name="cryptographyProvider">The cryptography provider.</param>
        /// <param name="value">The value to encrypt.</param>
        /// <returns>The encrypted value.</returns>
        public static string EncryptParameter(ICryptographyProvider cryptographyProvider, string value)
            => EncryptParameter(cryptographyProvider, value, PortalController.Instance.GetCurrentSettings().GUID.ToString());

        /// <summary>Encrypt a parameter for placing in a URL.</summary>
        /// <param name="value">The value to encrypt.</param>
        /// <param name="encryptionKey">The key to use when encrypting the value. This key must be used to decrypt the value.</param>
        /// <returns>The encrypted value.</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking ICryptographyProvider")]
        public static partial string EncryptParameter(string value, string encryptionKey)
            => EncryptParameter(Globals.GetCurrentServiceProvider().GetRequiredService<ICryptographyProvider>(), value, encryptionKey);

        /// <summary>Encrypt a parameter for placing in a URL.</summary>
        /// <param name="cryptographyProvider">The cryptography provider.</param>
        /// <param name="value">The value to encrypt.</param>
        /// <param name="encryptionKey">The key to use when encrypting the value. This key must be used to decrypt the value.</param>
        /// <returns>The encrypted value.</returns>
        public static string EncryptParameter(ICryptographyProvider cryptographyProvider, string value, string encryptionKey)
        {
            var encryptedValue = cryptographyProvider.EncryptParameter(value, encryptionKey).EncryptedMessage;
            var parameterValue = new StringBuilder(encryptedValue);

            // [DNN-8257] - Can't do URLEncode/URLDecode as it introduces issues on decryption (with / = %2f), so we use a modified Base64
            parameterValue.Replace('/', '_');
            parameterValue.Replace('+', '-');
            parameterValue.Replace("=", "%3d");
            return parameterValue.ToString();
        }

        /// <summary>Gets the name from a query string pair.</summary>
        /// <param name="pair">The pair, e.g. <c>"name=value"</c>.</param>
        /// <returns>The name.</returns>
        public static string GetParameterName(string pair)
        {
            var length = pair.IndexOf('=');
            if (length == -1)
            {
                length = pair.Length;
            }

            return pair.Substring(0, length);
        }

        /// <summary>Gets the value from a query string pair.</summary>
        /// <param name="pair">The pair, e.g. <c>"name=value"</c>.</param>
        /// <returns>The value.</returns>
        public static string GetParameterValue(string pair)
        {
            var start = pair.IndexOf('=') + 1;
            if (start == 0)
            {
                return string.Empty;
            }

            return pair.Substring(start);
        }

        /// <summary>
        /// getQSParamsForNavigateURL builds up a new querystring. This is necessary
        /// in order to prep for navigateUrl.
        /// we don't ever want a tabid, a ctl and a language parameter in the qs
        /// either, the portalid param is not allowed when the tab is a supertab
        /// (because NavigateUrl adds the portalId param to the qs).
        /// </summary>
        /// <returns>The query-string parameters collection in <c>"key=value"</c> format.</returns>
        public static string[] GetQSParamsForNavigateURL()
        {
            string returnValue = string.Empty;
            var coll = HttpContextSource.CurrentOrThrow.Request.Query;
            foreach (var key in coll.Keys)
            {
                if (key != null)
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "tabid":
                        case "ctl":
                        case "language":
                            // skip parameter
                            break;
                        default:
                            if (key.Equals("portalid", StringComparison.OrdinalIgnoreCase) && TabController.CurrentPage.IsSuperTab)
                            {
                                // skip parameter
                                // navigateURL adds portalid to querystring if tab is superTab
                            }
                            else
                            {
                                foreach (var value in coll[key])
                                {
                                    if (!string.IsNullOrEmpty(returnValue))
                                    {
                                        returnValue += "&";
                                    }

                                    returnValue += key + "=" + value;
                                }
                            }

                            break;
                    }
                }
            }

            // return the new querystring as a string array
            return returnValue.Split('&');
        }

        /// <summary>
        /// check if connection is HTTPS
        /// or is HTTP but with ssloffload enabled on a secure page.
        /// </summary>
        /// <param name="request">current request.</param>
        /// <returns>true if HTTPS or if HTTP with an SSL offload header value, false otherwise.</returns>
        public static bool IsSecureConnectionOrSslOffload(HttpRequest request)
            => IsSecureConnectionOrSslOffload(Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettingsService>(), request);

        /// <summary>
        /// check if connection is HTTPS
        /// or is HTTP but with SSL offload enabled on a secure page.
        /// </summary>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="request">current request.</param>
        /// <returns>true if HTTPS or if HTTP with an SSL offload header value, false otherwise.</returns>
        public static bool IsSecureConnectionOrSslOffload(IHostSettingsService hostSettingsService, HttpRequest request)
        {
            return request.IsHttps || IsSslOffloadEnabled(hostSettingsService, request);
        }

        /// <summary>Gets a value indicating whether SSL/HTTPS offloading is enabled for the given <paramref name="request"/>.</summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> if offloading is in use, otherwise <see langword="false"/>.</returns>
        [DnnDeprecated(10, 2, 2, "Use overload taking IHostSettingsService")]
        public static partial bool IsSslOffloadEnabled(HttpRequest request)
            => IsSslOffloadEnabled(Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettingsService>(), request);

        /// <summary>Gets a value indicating whether SSL/HTTPS offloading is enabled for the given <paramref name="request"/>.</summary>
        /// <param name="hostSettingsService">The host settings service.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> if offloading is in use, otherwise <see langword="false"/>.</returns>
        public static bool IsSslOffloadEnabled(IHostSettingsService hostSettingsService, HttpRequest request)
        {
            var sslOffloadHeader = hostSettingsService.GetString("SSLOffloadHeader", string.Empty);

            // if the sslOffloadHeader variable has been set check to see if a request header with that type exists
            if (string.IsNullOrEmpty(sslOffloadHeader))
            {
                return false;
            }

            var sslOffloadValue = string.Empty;
            if (sslOffloadHeader.Contains(":"))
            {
                var settingParts = sslOffloadHeader.Split(':');
                sslOffloadHeader = settingParts[0];
                sslOffloadValue = settingParts[1];
            }

            var sslOffload = request.Headers[sslOffloadHeader].ToString();
            return !string.IsNullOrEmpty(sslOffload) && (string.IsNullOrWhiteSpace(sslOffloadValue) || sslOffloadValue == sslOffload);
        }

        /// <summary>Opens the given <paramref name="url"/> in a new browser window.</summary>
        /// <param name="jsRuntime">The JavaScript runtime used to call <c>window.open</c>.</param>
        /// <param name="url">The URL to open.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// Replaces the WebForms implementation which registered a <c>window.open</c> start-up
        /// script on the page via <c>Page.ClientScript.RegisterStartupScript</c>.
        /// </remarks>
        public static ValueTask OpenNewWindow(IJSRuntime jsRuntime, string url)
        {
            if (jsRuntime is null)
            {
                throw new ArgumentNullException(nameof(jsRuntime));
            }

            return jsRuntime.InvokeVoidAsync("window.open", url, "new");
        }

        /// <summary>Builds the URL (or <c>dnnModal.show</c> script) used to open <paramref name="url"/> in a pop-up.</summary>
        /// <param name="url">The URL to show in the pop-up.</param>
        /// <param name="portalSettings">The portal settings.</param>
        /// <param name="onClickEvent">Whether the result is used in an onClick handler (rather than as a <c>javascript:</c> URL).</param>
        /// <param name="responseRedirect">Whether the result is used for a server-side redirect.</param>
        /// <returns>The pop-up URL or script.</returns>
        public static string PopUpUrl(string url, PortalSettings portalSettings, bool onClickEvent, bool responseRedirect)
        {
            return PopUpUrl(url, portalSettings, onClickEvent, responseRedirect, 550, 950);
        }

        /// <summary>Builds the URL (or <c>dnnModal.show</c> script) used to open <paramref name="url"/> in a pop-up.</summary>
        /// <param name="url">The URL to show in the pop-up.</param>
        /// <param name="portalSettings">The portal settings.</param>
        /// <param name="onClickEvent">Whether the result is used in an onClick handler (rather than as a <c>javascript:</c> URL).</param>
        /// <param name="responseRedirect">Whether the result is used for a server-side redirect.</param>
        /// <param name="windowHeight">The pop-up height.</param>
        /// <param name="windowWidth">The pop-up width.</param>
        /// <returns>The pop-up URL or script.</returns>
        public static string PopUpUrl(string url, PortalSettings portalSettings, bool onClickEvent, bool responseRedirect, int windowHeight, int windowWidth)
        {
            return PopUpUrl(url, portalSettings, onClickEvent, responseRedirect, windowHeight, windowWidth, true, string.Empty);
        }

        /// <summary>Builds the URL (or <c>dnnModal.show</c> script) used to open <paramref name="url"/> in a pop-up.</summary>
        /// <param name="url">The URL to show in the pop-up.</param>
        /// <param name="portalSettings">The portal settings.</param>
        /// <param name="onClickEvent">Whether the result is used in an onClick handler (rather than as a <c>javascript:</c> URL).</param>
        /// <param name="responseRedirect">Whether the result is used for a server-side redirect.</param>
        /// <param name="windowHeight">The pop-up height.</param>
        /// <param name="windowWidth">The pop-up width.</param>
        /// <param name="refresh">Whether to refresh the opener when the pop-up closes.</param>
        /// <param name="closingUrl">The URL to navigate to when the pop-up closes.</param>
        /// <returns>The pop-up URL or script.</returns>
        /// <remarks>The WebForms overloads taking a <c>System.Web.UI.Control</c> were removed; the control argument was never used.</remarks>
        public static string PopUpUrl(string url, PortalSettings portalSettings, bool onClickEvent, bool responseRedirect, int windowHeight, int windowWidth, bool refresh, string closingUrl)
        {
            var currentRequest = HttpContextSource.Current?.Request;
            if (currentRequest != null && UrlUtils.IsSecureConnectionOrSslOffload(currentRequest))
            {
                url = url.Replace("http://", "https://");
            }

            var popUpUrl = url;

            // ensure delimiters are not used
            if (!popUpUrl.Contains("dnnModal.show"))
            {
                popUpUrl = EscapeUriStringCompat(url);
                popUpUrl = popUpUrl.Replace("\"", string.Empty);
                popUpUrl = popUpUrl.Replace("'", string.Empty);
            }

            var delimiter = popUpUrl.Contains("?") ? '&' : '?';

            // create the querystring for use on a Response.Redirect
            if (responseRedirect)
            {
                popUpUrl = $"{popUpUrl}{delimiter}popUp=true";
            }
            else
            {
                if (!popUpUrl.Contains("dnnModal.show"))
                {
                    closingUrl = (closingUrl != Null.NullString) ? closingUrl : string.Empty;
                    popUpUrl =
                        $"javascript:dnnModal.show('{JavaScriptEncoder.Default.Encode(popUpUrl)}{delimiter}popUp=true',/*showReturn*/{onClickEvent.ToString().ToLowerInvariant()},{windowHeight},{windowWidth},{refresh.ToString().ToLowerInvariant()},'{JavaScriptEncoder.Default.Encode(closingUrl)}')";
                }
                else
                {
                    // Determines if the resulting JS call should return or not.
                    if (popUpUrl.Contains("/*showReturn*/false"))
                    {
                        popUpUrl = popUpUrl.Replace("/*showReturn*/false", "/*showReturn*/" + onClickEvent.ToString().ToLowerInvariant());
                    }
                    else
                    {
                        popUpUrl = popUpUrl.Replace("/*showReturn*/true", "/*showReturn*/" + onClickEvent.ToString().ToLowerInvariant());
                    }
                }
            }

            // Removes the javascript txt for onClick scripts
            if (onClickEvent && popUpUrl.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                popUpUrl = popUpUrl.Replace("javascript:", string.Empty);
            }

            return popUpUrl;
        }

        /// <summary>Creates a URL (or script) to close a pop-up.</summary>
        /// <param name="refresh">Whether to refresh the page when the pop-up is closed.</param>
        /// <param name="url">The URL.</param>
        /// <param name="onClickEvent">Whether to generate a script for an onClick event (rather than a URL with a <c>javascript:</c> protocol).</param>
        /// <returns>The URL or script.</returns>
        public static string ClosePopUp(bool refresh, string url, bool onClickEvent)
        {
            var protocol = onClickEvent ? string.Empty : "javascript:";
            var refreshBool = refresh.ToString().ToLowerInvariant();
            var urlString = JavaScriptEncoder.Default.Encode(url);
            return $"{protocol}dnnModal.closePopUp({refreshBool}, '{urlString}')";
        }

        /// <summary>Replaces a query string parameter's value in a URL.</summary>
        /// <param name="url">The URL.</param>
        /// <param name="param">The parameter name.</param>
        /// <param name="newValue">The parameter value.</param>
        /// <returns>The updated URL.</returns>
        [DnnDeprecated(10, 0, 2, "Use overload taking IHostSettings")]
        public static partial string ReplaceQSParam(string url, string param, string newValue)
            => ReplaceQSParam(Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>(), url, param, newValue);

        /// <summary>Replaces a query string parameter's value in a URL.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="url">The URL.</param>
        /// <param name="param">The parameter name.</param>
        /// <param name="newValue">The parameter value.</param>
        /// <returns>The updated URL.</returns>
        public static string ReplaceQSParam(IHostSettings hostSettings, string url, string param, string newValue)
        {
            if (hostSettings.UseFriendlyUrls)
            {
                var escapedReplacementValue = newValue.Replace("$1", "$$1").Replace("$2", "$$2").Replace("$3", "$$3").Replace("$4", "$$4");
                return Regex.Replace(url, $@"(.*)({Regex.Escape(param)}/)([^/]+)(/.*)", $"$1$2{escapedReplacementValue}$4", RegexOptions.IgnoreCase);
            }
            else
            {
                var escapedReplacementValue = newValue.Replace("$1", "$$1").Replace("$2", "$$2").Replace("$3", "$$3").Replace("$4", "$$4").Replace("$5", "$$5");
                return Regex.Replace(url, $@"(.*)(&|\?)({Regex.Escape(param)}=)([^&\?]+)(.*)", $"$1$2$3{escapedReplacementValue}$5", RegexOptions.IgnoreCase);
            }
        }

        /// <summary>Removes the query string parameter with the given name from the URL.</summary>
        /// <param name="url">The URL.</param>
        /// <param name="param">The parameter name.</param>
        /// <returns>The updated URL.</returns>
        [DnnDeprecated(10, 0, 2, "Use overload taking IHostSettings")]
        public static partial string StripQSParam(string url, string param)
            => StripQSParam(Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>(), url, param);

        /// <summary>Removes the query string parameter with the given name from the URL.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="url">The URL.</param>
        /// <param name="param">The parameter name.</param>
        /// <returns>The updated URL.</returns>
        public static string StripQSParam(IHostSettings hostSettings, string url, string param)
        {
            if (hostSettings.UseFriendlyUrls)
            {
                return Regex.Replace(url, $"(.*)({Regex.Escape(param)}/[^/]+/)(.*)", "$1$3", RegexOptions.IgnoreCase);
            }
            else
            {
                return Regex.Replace(url, $@"(.*)(&|\?)({Regex.Escape(param)}=)([^&\?]+)([&\?])?(.*)", "$1$2$6", RegexOptions.IgnoreCase).Replace("(.*)([&\\?]$)", "$1");
            }
        }

        /// <summary>Determines whether a <paramref name="url"/> is valid as a return URL.</summary>
        /// <param name="url">The URL string.</param>
        /// <returns>The normalized return URL or <see cref="string.Empty"/>.</returns>
        public static string ValidReturnUrl(string url)
        {
            try
            {
                if (string.IsNullOrEmpty(url))
                {
                    // DNN-10193: returns the same value as passed in rather than empty string
                    return url;
                }

                url = url.Replace('\\', '/');
                if (url.IndexOf("data:", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    return string.Empty;
                }

                // clean the return url to avoid possible XSS attack.
#pragma warning disable CS0618 // Type or member is obsolete
                var cleanUrl = PortalSecurity.Instance.InputFilter(url, PortalSecurity.FilterFlag.NoScripting);
#pragma warning restore CS0618 // Type or member is obsolete
                if (url != cleanUrl)
                {
                    return string.Empty;
                }

                // redirect url should never contain a protocol ( if it does, it is likely a cross-site request forgery attempt )
                var urlWithNoQuery = url;
                if (urlWithNoQuery.IndexOf('?') > -1)
                {
                    urlWithNoQuery = urlWithNoQuery.Substring(0, urlWithNoQuery.IndexOf("?", StringComparison.InvariantCultureIgnoreCase));
                }

                if (urlWithNoQuery.IndexOf(':') > -1)
                {
                    var portalSettings = PortalSettings.Current;
                    var aliasWithHttp = Globals.AddHTTP(((IPortalAliasInfo)portalSettings.PortalAlias).HttpAlias);
                    var uri1 = new Uri(url);
                    var uri2 = new Uri(aliasWithHttp);

                    // protocol switching (HTTP <=> HTTPS) is allowed by not being checked here
                    if (!string.Equals(uri1.DnsSafeHost, uri2.DnsSafeHost, StringComparison.OrdinalIgnoreCase))
                    {
                        return string.Empty;
                    }

                    // this check is mainly for child portals
                    if (!uri1.AbsolutePath.StartsWith(uri2.AbsolutePath, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return string.Empty;
                    }
                }

                while (url.StartsWith("///", StringComparison.Ordinal))
                {
                    url = url.Substring(1);
                }

                if (url.StartsWith("//", StringComparison.Ordinal))
                {
                    var urlWithNoProtocol = url.Substring(2);
                    var portalSettings = PortalSettings.Current;

                    // note: this can redirect from parent to child and vice versa
                    if (!urlWithNoProtocol.StartsWith(((IPortalAliasInfo)portalSettings.PortalAlias).HttpAlias + "/", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return string.Empty;
                    }
                }

                return url;
            }
            catch (UriFormatException)
            {
                return string.Empty;
            }
        }

        /// <summary>Determines whether the current page is being shown in a pop-up.</summary>
        /// <returns><see langword="true"/> if the current page is in a pop-up, otherwise <see langword="false"/>.</returns>
        public static bool InPopUp()
        {
            var request = HttpContextSource.Current?.Request;
            return request != null && IsPopUp(request.GetDisplayUrl());
        }

        /// <summary>Determines whether the given URL is for a page being shown in a pop-up.</summary>
        /// <param name="url">The URL.</param>
        /// <returns><see langword="true"/> if the URL is for a page in a pop-up, otherwise <see langword="false"/>.</returns>
        public static bool IsPopUp(string url)
        {
            return url.IndexOf("popUp=true", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Redirect current response to 404 error page or output 404 content if error page not defined.</summary>
        /// <param name="response">The response.</param>
        /// <param name="portalSetting">The portal settings.</param>
        public static void Handle404Exception(HttpResponse response, IPortalSettings portalSetting)
        {
            Handle404ExceptionAsync(response, portalSetting).GetAwaiter().GetResult();
        }

        /// <summary>Redirect current response to 404 error page or output 404 content if error page not defined.</summary>
        /// <param name="response">The response.</param>
        /// <param name="portalSetting">The portal settings.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task Handle404ExceptionAsync(HttpResponse response, IPortalSettings portalSetting)
        {
            if (response is null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (portalSetting?.ErrorPage404 > Null.NullInteger)
            {
                response.Redirect(NavigationManager.NavigateURL(portalSetting.ErrorPage404, string.Empty, "status=404"));
            }
            else
            {
                // Response.ClearContent()/Status/TrySkipIisCustomErrors have no ASP.NET Core
                // equivalent: Clear() resets the (not yet started) response, the reason phrase is
                // derived from the status code, and IIS custom errors are bypassed because the
                // body is written here.
                response.Clear();
                response.StatusCode = 404;
                response.ContentType = "text/plain";
                await response.WriteAsync("404 Not Found");
            }
        }

        /// <summary>Decrypts an encrypted value generated via <see cref="EncryptParameter(ICryptographyProvider,string,string)"/>.</summary>
        /// <param name="decrypt">A function which takes the encrypted message and passphrase and returns the decrypted message.</param>
        /// <param name="value">The encrypted value.</param>
        /// <param name="encryptionKey">The key used to encrypt the value.</param>
        /// <returns>The decrypted value.</returns>
        internal static string DecryptParameter(Func<string, string, string> decrypt, string value, string encryptionKey)
        {
            // [DNN-8257] - Can't do URLEncode/URLDecode as it introduces issues on decryption (with / = %2f), so we use a modified Base64
            var toDecrypt = new StringBuilder(value);
            toDecrypt.Replace('_', '/');
            toDecrypt.Replace('-', '+');
            toDecrypt.Replace("%3d", "=");
            return decrypt(toDecrypt.ToString(), encryptionKey);
        }

        /// <summary>
        /// Percent-encodes the characters that are never legal in a URI, leaving the reserved
        /// delimiters intact so that the URL's structure survives.
        /// </summary>
        /// <param name="value">The URL to escape.</param>
        /// <returns>The escaped URL.</returns>
        /// <remarks>
        /// This replaces the obsolete <c>Uri.EscapeUriString</c> (SYSLIB0013). It deliberately
        /// escapes only the "unsafe" set that method escaped — <c>Uri.EscapeDataString</c> cannot
        /// be substituted here because it also encodes <c>/</c>, <c>?</c>, <c>&amp;</c> and
        /// <c>=</c>, which would destroy the URL being wrapped for the pop-up.
        /// </remarks>
        private static string EscapeUriStringCompat(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            const string unsafeChars = " \"<>{}|\\^`";
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                if (character <= 127 && unsafeChars.IndexOf(character) < 0)
                {
                    builder.Append(character);
                    continue;
                }

                // Non-ASCII and unsafe characters are UTF-8 encoded, then percent-encoded
                // byte by byte — the same transformation Uri.EscapeUriString applied.
                foreach (var b in Encoding.UTF8.GetBytes(character.ToString()))
                {
                    builder.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
                }
            }

            return builder.ToString();
        }
    }
}
