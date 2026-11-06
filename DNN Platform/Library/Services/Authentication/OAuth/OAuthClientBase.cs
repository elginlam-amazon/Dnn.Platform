// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Authentication.OAuth
{
    using System.Net;
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Data;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Security.Membership;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using SystemHttpMethod = System.Net.Http.HttpMethod;

    /// <summary>A base class for an OAuth client.</summary>
    public abstract class OAuthClientBase
    {
        protected const string OAuthTokenKey = "oauth_token";

        // oAuth 1
        private const string OAuthParameterPrefix = "oauth_";
        private const string OAuthConsumerKeyKey = "oauth_consumer_key";
        private const string OAuthCallbackKey = "oauth_callback";
        private const string OAuthVersionKey = "oauth_version";
        private const string OAuthSignatureKey = "oauth_signature";
        private const string OAuthSignatureMethodKey = "oauth_signature_method";
        private const string OAuthTimestampKey = "oauth_timestamp";
        private const string OAuthNonceKey = "oauth_nonce";
        private const string OAuthTokenSecretKey = "oauth_token_secret";
        private const string OAuthVerifierKey = "oauth_verifier";
        private const string OAuthCallbackConfirmedKey = "oauth_callback_confirmed";

        // oAuth 2
        private const string OAuthClientIdKey = "client_id";
        private const string OAuthClientSecretKey = "client_secret";
        private const string OAuthRedirectUriKey = "redirect_uri";
        private const string OAuthGrantTypeKey = "grant_type";
        private const string OAuthCodeKey = "code";

        private const string UnreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

        // DNN-6265 - Support OAuth V2 optional parameter resource, which is required by Microsoft Azure Active
        // Directory implementation of OAuth V2
        private const string OAuthResourceKey = "resource";

        /// <summary>The named <see cref="HttpClient"/> the platform's OAuth clients use.</summary>
        public const string HttpClientName = "DnnOAuth";

        private static readonly ILogger Logger = DnnLoggingController.GetLogger<OAuthClientBase>();

        private readonly Random random = new Random();
        private readonly IHttpClientFactory httpClientFactory;

        /// <summary>Initializes a new instance of the <see cref="OAuthClientBase"/> class.</summary>
        /// <param name="portalId">The portal ID.</param>
        /// <param name="mode">The auth mode.</param>
        /// <param name="service">The service name.</param>
        protected OAuthClientBase(int portalId, AuthMode mode, string service)
            : this(portalId, mode, service, Globals.GetCurrentServiceProvider().GetRequiredService<IHttpClientFactory>())
        {
        }

        /// <summary>Initializes a new instance of the <see cref="OAuthClientBase"/> class.</summary>
        /// <param name="portalId">The portal ID.</param>
        /// <param name="mode">The auth mode.</param>
        /// <param name="service">The service name.</param>
        /// <param name="httpClientFactory">The factory used to create the <see cref="HttpClient"/> instances that talk to the OAuth provider.</param>
        protected OAuthClientBase(int portalId, AuthMode mode, string service, IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            // Set default Expiry to 14 days
            // oAuth v1 tokens do not expire
            // oAuth v2 tokens have an expiry
            this.AuthTokenExpiry = new TimeSpan(14, 0, 0, 0);
            this.Service = service;

            this.APIKey = OAuthConfigBase.GetConfig(this.Service, portalId).APIKey;
            this.APISecret = OAuthConfigBase.GetConfig(this.Service, portalId).APISecret;
            this.Mode = mode;

            this.CallbackUri = this.Mode == AuthMode.Login
                                    ? new Uri(Globals.LoginURL(string.Empty, false))
                                    : new Uri(Globals.RegisterURL(string.Empty, string.Empty));
        }

        public virtual bool PrefixServiceToUserName
        {
            get { return true; }
        }

        public virtual bool AutoMatchExistingUsers
        {
            get { return false; }
        }

        public Uri CallbackUri { get; set; }

        public string Service { get; set; }

        protected virtual string UserGuidKey => string.Empty;

        /// <summary>Gets the cookie path the OAuth cookies are scoped to.</summary>
        private static string CookiePath =>
            !string.IsNullOrEmpty(Globals.ApplicationPath) ? Globals.ApplicationPath : "/";

        // oAuth 1
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        protected string OAuthVerifier => GetRequestValue(OAuthVerifierKey);

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        protected string VerificationCode => GetRequestValue(OAuthCodeKey);

        protected string APIKey { get; set; }

        protected string APISecret { get; set; }

        protected AuthMode Mode { get; set; }

        protected string OAuthVersion { get; set; }

        protected HttpMethod TokenMethod { get; set; }

        protected Uri RequestTokenEndpoint { get; set; }

        protected HttpMethod RequestTokenMethod { get; set; }

        protected string TokenSecret { get; set; }

        protected string UserGuid { get; set; }

        protected string SignatureMethod { get; set; } = "HMAC-SHA1";

#pragma warning disable CA5350
        protected Func<KeyedHashAlgorithm> SignatureMethodAlgorithm { get; set; } = () => new HMACSHA1();
#pragma warning restore CA5350

        // oAuth 1 and 2
        protected Uri AuthorizationEndpoint { get; set; }

        protected string AuthToken { get; set; }

        protected TimeSpan AuthTokenExpiry { get; set; }

        protected Uri MeGraphEndpoint { get; set; }

        protected Uri TokenEndpoint { get; set; }

        protected string OAuthHeaderCode { get; set; }

        // oAuth 2
        protected string AuthTokenName { get; set; }

        protected string Scope { get; set; }

        protected string AccessToken { get; set; }

        // DNN-6265 Support "Optional" Resource Parameter required by Azure AD Oauth V2 Solution
        protected string APIResource { get; set; }

        /// <summary>
        /// This is a different URL Encode implementation since the default .NET one outputs the percent encoding in lower case.
        /// While this is not a problem with the percent encoding spec, it is used in upper case throughout OAuth.
        /// </summary>
        /// <param name="value">The value to URL encode.</param>
        /// <returns>Returns a URL encoded string.</returns>
        public static string UrlEncode(string value)
        {
            var result = new StringBuilder();

            foreach (char symbol in value)
            {
                if (UnreservedChars.IndexOf(symbol) != -1)
                {
                    result.Append(symbol);
                }
                else
                {
                    result.Append('%' + string.Format(CultureInfo.InvariantCulture, "{0:X2}", (int)symbol));
                }
            }

            return result.ToString();
        }

        /// <summary>Authenticates (and if needed auto-registers) the DNN user behind the OAuth identity.</summary>
        /// <param name="user">The user data returned by the OAuth provider.</param>
        /// <param name="settings">The portal settings.</param>
        /// <param name="ipAddress">The client IP address.</param>
        /// <param name="addCustomProperties">A callback that lets the provider add its own profile properties.</param>
        /// <param name="onAuthenticated">A callback raised once the user has been authenticated.</param>
        /// <returns>A task that completes once the token cookie has been written.</returns>
        public virtual Task AuthenticateUserAsync(UserData user, PortalSettings settings, string ipAddress, Action<NameValueCollection> addCustomProperties, Action<UserAuthenticatedEventArgs> onAuthenticated)
        {
            var loginStatus = UserLoginStatus.LOGIN_FAILURE;

            string userName = this.PrefixServiceToUserName ? this.Service + "-" + user.Id : user.Id;
            string token = this.Service + "-" + user.Id;

            UserInfo objUserInfo;

            if (this.AutoMatchExistingUsers)
            {
                objUserInfo = MembershipProvider.Instance().GetUserByUserName(settings.PortalId, userName);
                if (objUserInfo != null)
                {
                    // user already exists... lets check for a token next...
                    var dnnAuthToken = MembershipProvider.Instance().GetUserByAuthToken(settings.PortalId, token, this.Service);
                    if (dnnAuthToken == null)
                    {
                        DataProvider.Instance().AddUserAuthentication(objUserInfo.UserID, this.Service, token, objUserInfo.UserID);
                    }
                }
            }

            objUserInfo = UserController.ValidateUser(
                settings.PortalId,
                userName,
                string.Empty,
                this.Service,
                token,
                settings.PortalName,
                ipAddress,
                ref loginStatus);

            // Raise UserAuthenticated Event
            var eventArgs = new UserAuthenticatedEventArgs(objUserInfo, token, loginStatus, this.Service)
            {
                AutoRegister = true,
                UserName = userName,
            };

            var profileProperties = new NameValueCollection();

            if (objUserInfo == null || (string.IsNullOrEmpty(objUserInfo.FirstName) && !string.IsNullOrEmpty(user.FirstName)))
            {
                profileProperties.Add("FirstName", user.FirstName);
            }

            if (objUserInfo == null || (string.IsNullOrEmpty(objUserInfo.LastName) && !string.IsNullOrEmpty(user.LastName)))
            {
                profileProperties.Add("LastName", user.LastName);
            }

            if (objUserInfo == null || (string.IsNullOrEmpty(objUserInfo.Email) && !string.IsNullOrEmpty(user.Email)))
            {
                profileProperties.Add("Email", user.PreferredEmail);
            }

            if (objUserInfo == null || (string.IsNullOrEmpty(objUserInfo.DisplayName) && !string.IsNullOrEmpty(user.DisplayName)))
            {
                profileProperties.Add("DisplayName", user.DisplayName);
            }

            if (objUserInfo == null || (string.IsNullOrEmpty(objUserInfo.Profile.GetPropertyValue("ProfileImage")) && !string.IsNullOrEmpty(user.ProfileImage)))
            {
                profileProperties.Add("ProfileImage", user.ProfileImage);
            }

            if (objUserInfo == null || (string.IsNullOrEmpty(objUserInfo.Profile.GetPropertyValue("Website")) && !string.IsNullOrEmpty(user.Website)))
            {
                profileProperties.Add("Website", user.Website);
            }

            if ((objUserInfo == null || string.IsNullOrEmpty(objUserInfo.Profile.GetPropertyValue("PreferredLocale"))) && !string.IsNullOrEmpty(user.Locale))
            {
                if (LocaleController.IsValidCultureName(user.Locale.Replace('_', '-')))
                {
                    profileProperties.Add("PreferredLocale", user.Locale.Replace('_', '-'));
                }
                else
                {
                    profileProperties.Add("PreferredLocale", settings.CultureCode);
                }
            }

            if (objUserInfo == null || string.IsNullOrEmpty(objUserInfo.Profile.GetPropertyValue("PreferredTimeZone")))
            {
                if (string.IsNullOrEmpty(user.TimeZoneInfo))
                {
                    int timeZone;
                    if (int.TryParse(user.Timezone, out timeZone))
                    {
                        var timeZoneInfo = Localization.ConvertLegacyTimeZoneOffsetToTimeZoneInfo(timeZone);

                        profileProperties.Add("PreferredTimeZone", timeZoneInfo.Id);
                    }
                }
                else
                {
                    profileProperties.Add("PreferredTimeZone", user.TimeZoneInfo);
                }
            }

            addCustomProperties(profileProperties);

            eventArgs.Profile = profileProperties;

            if (this.Mode == AuthMode.Login)
            {
                this.SaveTokenCookie(string.Empty);
            }

            onAuthenticated(eventArgs);

            return Task.CompletedTask;
        }

        /// <summary>Runs the OAuth authorization flow.</summary>
        /// <returns>The result of the authorization attempt.</returns>
        public virtual Task<AuthorisationResult> AuthorizeAsync()
        {
            if (this.OAuthVersion == "1.0")
            {
                return this.AuthorizeV1Async();
            }

            return this.AuthorizeV2Async();
        }

        /// <summary>Generates a signature using the <see cref="SignatureMethodAlgorithm"/>.</summary>
        /// <param name="url">The full url that needs to be signed including its non OAuth url parameters.</param>
        /// <param name="token">The token, if available. If not available pass <see langword="null"/> or <see cref="string.Empty"/>.</param>
        /// <param name="tokenSecret">The token secret, if available. If not available pass <see langword="null"/> or <see cref="string.Empty"/>.</param>
        /// <param name="callbackurl">The callback URL.</param>
        /// <param name="oauthVerifier">This value MUST be included when exchanging Request Tokens for Access Tokens. Otherwise, pass <see langword="null"/> or <see cref="string.Empty"/>.</param>
        /// <param name="httpMethod">The http method used. Must be a valid HTTP method verb (POST, GET, PUT, etc.).</param>
        /// <param name="timeStamp">The Unix-formatted timestamp.</param>
        /// <param name="nonce">The nonce.</param>
        /// <param name="normalizedUrl">A normalized version of <paramref name="url"/>.</param>
        /// <param name="requestParameters">The OAuth request parameters.</param>
        /// <returns>A base64 string of the hash value.</returns>
        public string GenerateSignature(Uri url, string token, string tokenSecret, string callbackurl, string oauthVerifier, string httpMethod, string timeStamp, string nonce, out string normalizedUrl, out List<QueryParameter> requestParameters)
        {
            string signatureBase = this.GenerateSignatureBase(url, token, callbackurl, oauthVerifier, httpMethod, timeStamp, nonce, out normalizedUrl, out requestParameters);

            using var hashAlgorithm = this.SignatureMethodAlgorithm();
            hashAlgorithm.Key = Encoding.ASCII.GetBytes($"{UrlEncode(this.APISecret)}&{(string.IsNullOrEmpty(tokenSecret) ? string.Empty : UrlEncode(tokenSecret))}");

            return GenerateSignatureUsingHash(signatureBase, hashAlgorithm);
        }

        /// <summary>Gets the authorized user's data from the OAuth provider.</summary>
        /// <typeparam name="TUserData">The provider-specific user data type.</typeparam>
        /// <returns>The user data, or <see langword="null"/> when the current user is not authorized.</returns>
        public virtual async Task<TUserData> GetCurrentUserAsync<TUserData>()
            where TUserData : UserData
        {
            this.LoadTokenCookie(string.Empty);

            if (!this.IsCurrentUserAuthorized())
            {
                return null;
            }

            var accessToken = string.IsNullOrEmpty(this.AccessToken) ? "access_token=" + this.AuthToken : this.AccessToken + "=" + this.AuthToken;
            string responseText = (this.OAuthVersion == "1.0")
                            ? await this.ExecuteAuthorizedRequestAsync(HttpMethod.GET, this.MeGraphEndpoint)
                            : await this.ExecuteWebRequestAsync(HttpMethod.GET, GenerateRequestUri(this.MeGraphEndpoint.ToString(), accessToken), null, string.Empty);
            var user = Json.Deserialize<TUserData>(responseText);
            return user;
        }

        public bool HaveVerificationCode()
        {
            return (this.OAuthVersion == "1.0") ? (this.OAuthVerifier != null) : (this.VerificationCode != null);
        }

        public bool IsCurrentService()
        {
            string service = GetRequestValue("state");
            return !string.IsNullOrEmpty(service) && service == this.Service;
        }

        public bool IsCurrentUserAuthorized()
        {
            return !string.IsNullOrEmpty(this.AuthToken);
        }

        /// <summary>Builds the URL the user's browser must be sent to in order to authorize this application.</summary>
        /// <returns>The provider's authorization URL, including the query string.</returns>
        /// <remarks>
        /// <para>
        /// On WebForms the outbound leg of the OAuth 2.0 flow was a postback whose handler called
        /// <see cref="AuthorizeAsync"/>, which answered with an HTTP redirect
        /// (<see cref="AuthorisationResult.RequestingCode"/>). That is not possible from a Blazor
        /// event handler, because <see cref="HttpContextSource.Current"/> — and therefore the
        /// response to redirect — is <see langword="null"/> once the SignalR circuit has taken
        /// over. Login components instead obtain this URL during the initial (static) render and
        /// render their login button as an ordinary link to it, so the browser performs the
        /// navigation itself. The user-visible result is the same as the redirect it replaces.
        /// </para>
        /// <para>
        /// This is the same URL <see cref="AuthorizeAsync"/> redirects to, built by the same code,
        /// so the requested <see cref="Scope"/>, client id, callback and state cannot drift apart.
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">The client is configured for OAuth 1.0.</exception>
        public string GetAuthorizationUrl()
        {
            if (this.OAuthVersion == "1.0")
            {
                // OAuth 1.0 cannot produce this URL up front: it must first exchange credentials
                // for a request token (see AuthorizeV1Async), which is a server-to-server call
                // that has to happen while a request is in flight.
                throw new InvalidOperationException(
                    "An OAuth 1.0 authorization URL cannot be built in advance; it requires a request token. Use AuthorizeAsync during request processing instead.");
            }

            return this.AuthorizationEndpoint + "?" + this.GetAuthorizationParameters().ToNormalizedString();
        }

        public void RemoveToken()
        {
            HttpContextSource.Current?.Response.Cookies.Delete(
                this.AuthTokenName,
                new CookieOptions { Path = CookiePath, });
        }

        /// <summary>Generate the timestamp for the signature.</summary>
        /// <returns>A string representation of <see cref="DateTime.Now"/> in the Unix timestamp format.</returns>
        protected virtual string GenerateTimeStamp()
        {
            // Default implementation of UNIX time of the current UTC time
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Generate a nonce.</summary>
        /// <returns>A 6-7 character length nonce string.</returns>
        protected virtual string GenerateNonce()
        {
            // Just a simple implementation of a random number between 123400 and 9999999
            return this.random.Next(123400, 9999999).ToString(CultureInfo.InvariantCulture);
        }

        protected virtual TimeSpan GetExpiry(string responseText)
        {
            return TimeSpan.MinValue;
        }

        protected virtual string GetToken(string responseText)
        {
            return responseText;
        }

        /// <summary>
        /// Reads the multi-value token cookie written by <c>SaveTokenCookie</c>.
        /// </summary>
        /// <remarks>
        /// ASP.NET's <c>HttpCookie.Values</c> sub-keys do not exist in ASP.NET Core, so the three
        /// values are stored as a single URL-encoded <c>key=value&amp;...</c> payload — which is
        /// exactly the wire format ASP.NET used for sub-keyed cookies, so existing cookies still
        /// parse.
        /// </remarks>
        /// <param name="suffix">The cookie name suffix (<see cref="string.Empty"/> or <c>_request</c>).</param>
        protected void LoadTokenCookie(string suffix)
        {
            var request = HttpContextSource.Current?.Request;
            if (request == null || !request.Cookies.TryGetValue(this.AuthTokenName + suffix, out var cookieValue) || string.IsNullOrEmpty(cookieValue))
            {
                return;
            }

            var values = QueryStringUtils.ParseQueryString(cookieValue);
            if (values.Count == 0)
            {
                return;
            }

            this.AuthToken = values[OAuthTokenKey];
            this.TokenSecret = values[OAuthTokenSecretKey];
            if (!string.IsNullOrEmpty(this.UserGuidKey))
            {
                this.UserGuid = values[this.UserGuidKey];
            }
        }

        private static string ComputeHash(HashAlgorithm hashAlgorithm, string data)
        {
            if (hashAlgorithm == null)
            {
                throw new ArgumentNullException(nameof(hashAlgorithm));
            }

            if (string.IsNullOrEmpty(data))
            {
                throw new ArgumentNullException(nameof(data));
            }

            byte[] dataBuffer = Encoding.ASCII.GetBytes(data);
            byte[] hashBytes = hashAlgorithm.ComputeHash(dataBuffer);

            return Convert.ToBase64String(hashBytes);
        }

        private static string GenerateSignatureUsingHash(string signatureBase, HashAlgorithm hash)
        {
            return ComputeHash(hash, signatureBase);
        }

        private static List<QueryParameter> GetQueryParameters(string parameters)
        {
            if (parameters.StartsWith("?", StringComparison.Ordinal))
            {
                parameters = parameters.Remove(0, 1);
            }

            var result = new List<QueryParameter>();

            if (!string.IsNullOrEmpty(parameters))
            {
                string[] p = parameters.Split('&');
                foreach (string s in p)
                {
                    if (!string.IsNullOrEmpty(s) && !s.StartsWith(OAuthParameterPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        if (s.IndexOf('=') > -1)
                        {
                            string[] temp = s.Split('=');
                            result.Add(new QueryParameter(temp[0], temp[1]));
                        }
                        else
                        {
                            result.Add(new QueryParameter(s, string.Empty));
                        }
                    }
                }
            }

            return result;
        }

        private static Uri GenerateRequestUri(string url, string parameters)
        {
            if (string.IsNullOrEmpty(parameters))
            {
                return new Uri(url);
            }

            var separator = url.Contains("?") ? "&" : "?";
            return new Uri($"{url}{separator}{parameters}");
        }

        private async Task<AuthorisationResult> AuthorizeV1Async()
        {
            if (!this.IsCurrentUserAuthorized())
            {
                if (!this.HaveVerificationCode())
                {
                    string ret = null;

                    string response = await this.RequestTokenAsync();

                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        // response contains token and token secret. We only need the token.
                        NameValueCollection qs = QueryStringUtils.ParseQueryString(response);

                        if (qs[OAuthCallbackConfirmedKey] != null)
                        {
                            if (qs[OAuthCallbackConfirmedKey] != "true")
                            {
                                throw new OAuthException("OAuth callback not confirmed.");
                            }
                        }

                        if (qs[OAuthTokenKey] != null)
                        {
                            ret = this.AuthorizationEndpoint + "?" + OAuthTokenKey + "=" + qs[OAuthTokenKey];

                            this.AuthToken = qs[OAuthTokenKey];
                            this.TokenSecret = qs[OAuthTokenSecretKey];
                            this.SaveTokenCookie("_request");
                        }
                    }

                    if (ret != null)
                    {
                        HttpContextSource.CurrentOrThrow.Response.Redirect(ret);
                    }

                    return AuthorisationResult.RequestingCode;
                }

                await this.ExchangeRequestTokenForTokenAsync();
            }

            return AuthorisationResult.Authorized;
        }

        /// <summary>Builds the OAuth 2.0 authorization request parameters.</summary>
        /// <returns>The query parameters, in the order the WebForms implementation sent them.</returns>
        /// <remarks>
        /// Shared by <see cref="AuthorizeV2Async"/> (which redirects to them) and
        /// <see cref="GetAuthorizationUrl"/> (which hands them to a login component as a link),
        /// so the requested scope and the callback can never differ between the two paths.
        /// </remarks>
        private List<QueryParameter> GetAuthorizationParameters()
        {
            return new List<QueryParameter>
            {
                new QueryParameter("scope", this.Scope),
                new QueryParameter(OAuthClientIdKey, this.APIKey),
                new QueryParameter(OAuthRedirectUriKey, WebUtility.UrlEncode(this.CallbackUri.ToString())),
                new QueryParameter("state", this.Service),
                new QueryParameter("response_type", "code"),
            };
        }

        private async Task<AuthorisationResult> AuthorizeV2Async()
        {
            string errorReason = GetRequestValue("error_reason");
            bool userDenied = errorReason != null;
            if (userDenied)
            {
                return AuthorisationResult.Denied;
            }

            if (!this.HaveVerificationCode())
            {
                HttpContextSource.CurrentOrThrow.Response.Redirect(this.GetAuthorizationUrl());
                return AuthorisationResult.RequestingCode;
            }

            await this.ExchangeCodeForTokenAsync();

            return string.IsNullOrEmpty(this.AuthToken) ? AuthorisationResult.Denied : AuthorisationResult.Authorized;
        }

        private async Task ExchangeCodeForTokenAsync()
        {
            var parameters = new List<QueryParameter>
            {
                new QueryParameter(OAuthClientIdKey, this.APIKey),
                new QueryParameter(OAuthRedirectUriKey, WebUtility.UrlEncode(this.CallbackUri.ToString())),

                // DNN-6265 Support for OAuth V2 Secrets which are not URL Friendly
                new QueryParameter(OAuthClientSecretKey, WebUtility.UrlEncode(this.APISecret)),
                new QueryParameter(OAuthGrantTypeKey, "authorization_code"),
                new QueryParameter(OAuthCodeKey, this.VerificationCode),
            };

            // DNN-6265 Support for OAuth V2 optional parameter
            if (!string.IsNullOrEmpty(this.APIResource))
            {
                parameters.Add(new QueryParameter("resource", this.APIResource));
            }

            var responseText = await this.ExecuteWebRequestAsync(this.TokenMethod, this.TokenEndpoint, parameters.ToNormalizedString(), string.Empty);

            this.AuthToken = this.GetToken(responseText);
            this.AuthTokenExpiry = this.GetExpiry(responseText);
        }

        private async Task ExchangeRequestTokenForTokenAsync()
        {
            this.LoadTokenCookie("_request");

            string response = await this.ExecuteAuthorizedRequestAsync(HttpMethod.POST, this.TokenEndpoint);

            if (response.Length > 0)
            {
                // Store the Token and Token Secret
                NameValueCollection qs = QueryStringUtils.ParseQueryString(response);
                if (qs[OAuthTokenKey] != null)
                {
                    this.AuthToken = qs[OAuthTokenKey];
                }

                if (qs[OAuthTokenSecretKey] != null)
                {
                    this.TokenSecret = qs[OAuthTokenSecretKey];
                }

                if (qs[this.UserGuidKey] != null)
                {
                    this.UserGuid = qs[this.UserGuidKey];
                }
            }
        }

        private async Task<string> ExecuteAuthorizedRequestAsync(HttpMethod method, Uri uri)
        {
            string outUrl;
            List<QueryParameter> requestParameters;

            string nonce = this.GenerateNonce();
            string timeStamp = this.GenerateTimeStamp();

            string verifier = (uri == this.TokenEndpoint) ? this.OAuthVerifier : string.Empty;

            // Generate Signature
            string sig = this.GenerateSignature(
                uri,
                this.AuthToken,
                this.TokenSecret,
                string.Empty,
                verifier,
                method.ToString(),
                timeStamp,
                nonce,
                out outUrl,
                out requestParameters);

            var headerParameters = new List<QueryParameter>
                                       {
                                           new QueryParameter(OAuthConsumerKeyKey, this.APIKey),
                                           new QueryParameter(OAuthNonceKey, nonce),
                                           new QueryParameter(OAuthSignatureKey, sig),
                                           new QueryParameter(OAuthSignatureMethodKey, this.SignatureMethod),
                                           new QueryParameter(OAuthTimestampKey, timeStamp),
                                           new QueryParameter(OAuthTokenKey, this.AuthToken),
                                           new QueryParameter(OAuthVersionKey, this.OAuthVersion),
                                       };
            if (uri == this.TokenEndpoint)
            {
                headerParameters.Add(new QueryParameter(OAuthVerifierKey, this.OAuthVerifier));
            }

            string ret = await this.ExecuteWebRequestAsync(method, uri, string.Empty, headerParameters.ToAuthorizationString());

            return ret;
        }

        /// <summary>
        /// Executes an HTTP request against the OAuth provider using a pooled
        /// <see cref="HttpClient"/> from <see cref="IHttpClientFactory"/>.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="uri">The target URI.</param>
        /// <param name="parameters">The form-encoded parameters (body for POST, query string otherwise).</param>
        /// <param name="authHeader">The raw value for the <c>Authorization</c> header, if any.</param>
        /// <returns>The response body, or <see langword="null"/> when the request failed.</returns>
        private async Task<string> ExecuteWebRequestAsync(HttpMethod method, Uri uri, string parameters, string authHeader)
        {
            HttpRequestMessage request;

            if (method == HttpMethod.POST)
            {
                request = new HttpRequestMessage(SystemHttpMethod.Post, uri)
                {
                    Content = new StringContent(parameters ?? string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded"),
                };

                if (!string.IsNullOrEmpty(this.OAuthHeaderCode))
                {
                    byte[] api64 = Encoding.UTF8.GetBytes(this.APIKey + ":" + this.APISecret);
                    string api64Encoded = Convert.ToBase64String(api64);

                    // Authentication providers needing an "Authorization: Basic/bearer base64(clientID:clientSecret)" header. OAuthHeaderCode might be: Basic/Bearer/empty.
                    request.Headers.Authorization = new AuthenticationHeaderValue(this.OAuthHeaderCode, api64Encoded);
                }
            }
            else
            {
                request = new HttpRequestMessage(
                    ToSystemHttpMethod(method),
                    GenerateRequestUri(uri.ToString(), parameters));
            }

            // Add Headers — the OAuth 1 signature is passed as a pre-built Authorization value.
            if (!string.IsNullOrEmpty(authHeader))
            {
                request.Headers.TryAddWithoutValidation("Authorization", authHeader);
            }

            var client = this.httpClientFactory.CreateClient(HttpClientName);

            try
            {
                using (request)
                using (var response = await client.SendAsync(request))
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.OAuthClientBaseWebResponseException(
                            new HttpRequestException($"The OAuth provider returned {(int)response.StatusCode} ({response.ReasonPhrase}) for {uri}."),
                            responseContent);
                        return null;
                    }

                    return responseContent;
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.OAuthClientBaseWebResponseException(ex, ex.Message);
            }
            catch (TaskCanceledException ex)
            {
                // The HttpClient timeout surfaces as a cancellation.
                Logger.OAuthClientBaseWebResponseException(ex, ex.Message);
            }

            return null;
        }

        /// <summary>Maps DNN's <see cref="HttpMethod"/> enum onto <see cref="SystemHttpMethod"/>.</summary>
        private static SystemHttpMethod ToSystemHttpMethod(HttpMethod method)
        {
            switch (method)
            {
                case HttpMethod.POST:
                    return SystemHttpMethod.Post;
                case HttpMethod.PUT:
                    return SystemHttpMethod.Put;
                case HttpMethod.DELETE:
                    return SystemHttpMethod.Delete;
                default:
                    return SystemHttpMethod.Get;
            }
        }

        /// <summary>
        /// The ASP.NET Core replacement for the legacy <c>Request.Params[key]</c> collection, which
        /// searched the query string, then the form, then the cookies.
        /// </summary>
        private static string GetRequestValue(string key)
        {
            var request = HttpContextSource.Current?.Request;
            if (request == null)
            {
                return null;
            }

            var value = request.Query[key].ToString();
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (request.HasFormContentType)
            {
                value = request.Form[key].ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return request.Cookies.TryGetValue(key, out var cookieValue) ? cookieValue : null;
        }

        private string GenerateSignatureBase(Uri url, string token, string callbackUrl, string oauthVerifier, string httpMethod, string timeStamp, string nonce, out string normalizedUrl, out List<QueryParameter> requestParameters)
        {
            if (token == null)
            {
                token = string.Empty;
            }

            if (string.IsNullOrEmpty(httpMethod))
            {
                throw new ArgumentNullException(nameof(httpMethod));
            }

            requestParameters = GetQueryParameters(url.Query);
            requestParameters.Add(new QueryParameter(OAuthVersionKey, this.OAuthVersion));
            requestParameters.Add(new QueryParameter(OAuthNonceKey, nonce));
            requestParameters.Add(new QueryParameter(OAuthTimestampKey, timeStamp));
            requestParameters.Add(new QueryParameter(OAuthSignatureMethodKey, this.SignatureMethod));
            requestParameters.Add(new QueryParameter(OAuthConsumerKeyKey, this.APIKey));

            if (!string.IsNullOrEmpty(callbackUrl))
            {
                requestParameters.Add(new QueryParameter(OAuthCallbackKey, UrlEncode(callbackUrl)));
            }

            if (!string.IsNullOrEmpty(oauthVerifier))
            {
                requestParameters.Add(new QueryParameter(OAuthVerifierKey, oauthVerifier));
            }

            if (!string.IsNullOrEmpty(token))
            {
                requestParameters.Add(new QueryParameter(OAuthTokenKey, token));
            }

            requestParameters.Sort(new QueryParameterComparer());

            normalizedUrl = $"{url.Scheme}://{url.Host}";
            if (!((url.Scheme == "http" && url.Port == 80) || (url.Scheme == "https" && url.Port == 443)))
            {
                normalizedUrl += ":" + url.Port;
            }

            normalizedUrl += url.AbsolutePath;
            string normalizedRequestParameters = requestParameters.ToNormalizedString();

            var signatureBase = new StringBuilder();
            signatureBase.AppendFormat(CultureInfo.InvariantCulture, "{0}&", httpMethod.ToUpperInvariant());
            signatureBase.AppendFormat(CultureInfo.InvariantCulture, "{0}&", UrlEncode(normalizedUrl));
            signatureBase.AppendFormat(CultureInfo.InvariantCulture, "{0}", UrlEncode(normalizedRequestParameters));

            return signatureBase.ToString();
        }

        private async Task<string> RequestTokenAsync()
        {
            string nonce = this.GenerateNonce();
            string timeStamp = this.GenerateTimeStamp();

            string sig = this.GenerateSignature(
                this.RequestTokenEndpoint,
                string.Empty,
                string.Empty,
                this.CallbackUri.OriginalString,
                string.Empty,
                this.RequestTokenMethod.ToString(),
                timeStamp,
                nonce,
                out var outUrl,
                out _);

            var headerParameters = new List<QueryParameter>
                                       {
                                           new QueryParameter(OAuthCallbackKey, this.CallbackUri.OriginalString),
                                           new QueryParameter(OAuthConsumerKeyKey, this.APIKey),
                                           new QueryParameter(OAuthNonceKey, nonce),
                                           new QueryParameter(OAuthSignatureKey, sig),
                                           new QueryParameter(OAuthSignatureMethodKey, this.SignatureMethod),
                                           new QueryParameter(OAuthTimestampKey, timeStamp),
                                           new QueryParameter(OAuthVersionKey, this.OAuthVersion),
                                       };

            string ret = await this.ExecuteWebRequestAsync(this.RequestTokenMethod, new Uri(outUrl), string.Empty, headerParameters.ToAuthorizationString());

            return ret;
        }

        private void SaveTokenCookie(string suffix)
        {
            var response = HttpContextSource.Current?.Response;
            if (response == null)
            {
                throw new InvalidOperationException("The OAuth token cookie cannot be written because there is no HTTP response in flight.");
            }

            // ASP.NET Core has no sub-keyed cookies; store the same key/value payload ASP.NET
            // wrote on the wire so the cookie format is unchanged.
            var values = QueryStringUtils.ParseQueryString(string.Empty);
            values[OAuthTokenKey] = this.AuthToken;
            values[OAuthTokenSecretKey] = this.TokenSecret;
            if (!string.IsNullOrEmpty(this.UserGuidKey))
            {
                values[this.UserGuidKey] = this.UserGuid;
            }

            response.Cookies.Append(
                this.AuthTokenName + suffix,
                values.ToString(),
                new CookieOptions
                {
                    Path = CookiePath,
                    Expires = DateTimeOffset.Now.Add(this.AuthTokenExpiry),
                });
        }
    }
}
