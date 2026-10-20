// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Security
{
    using System.Globalization;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Replacement for the System.Web <c>FormsAuthentication</c> API on .NET 10.
    /// </summary>
    /// <remarks>
    /// Tickets are serialized and protected with <see cref="MachineKey"/> (ASP.NET Core data protection through the System.Web adapters).
    /// The settings that were read from the <c>system.web/authentication/forms</c> section of the <c>web.config</c> are configured
    /// with <see cref="Configure"/> during the application startup.
    /// </remarks>
    public static class FormsAuthentication
    {
        private const string ProtectionPurpose = "System.Web.Security.FormsAuthentication.Ticket";
        private const byte SerializationFormatVersion = 1;

        /// <summary>Gets the name of the forms authentication cookie.</summary>
        public static string FormsCookieName { get; private set; } = ".DOTNETNUKE";

        /// <summary>Gets the path of the forms authentication cookie.</summary>
        public static string FormsCookiePath { get; private set; } = "/";

        /// <summary>Gets the domain of the forms authentication cookie.</summary>
        public static string CookieDomain { get; private set; }

        /// <summary>Gets a value indicating whether the forms authentication cookie requires SSL.</summary>
        public static bool RequireSSL { get; private set; }

        /// <summary>Gets a value indicating whether sliding expiration is enabled.</summary>
        public static bool SlidingExpiration { get; private set; } = true;

        /// <summary>Gets the validity of the authentication ticket.</summary>
        public static TimeSpan Timeout { get; private set; } = TimeSpan.FromMinutes(60);

        /// <summary>Gets the login URL.</summary>
        public static string LoginUrl { get; private set; } = "~/login.aspx";

        /// <summary>Gets the default URL.</summary>
        public static string DefaultUrl { get; private set; } = "~/default.aspx";

        /// <summary>Gets the SameSite mode of the forms authentication cookie.</summary>
        public static SameSiteMode CookieSameSite { get; private set; } = SameSiteMode.Lax;

        /// <summary>Configures the forms authentication settings (replaces the <c>system.web/authentication/forms</c> configuration section).</summary>
        /// <param name="formsCookieName">The cookie name.</param>
        /// <param name="formsCookiePath">The cookie path.</param>
        /// <param name="cookieDomain">The cookie domain.</param>
        /// <param name="requireSsl">Whether the cookie requires SSL.</param>
        /// <param name="slidingExpiration">Whether sliding expiration is enabled.</param>
        /// <param name="timeout">The ticket validity.</param>
        /// <param name="loginUrl">The login URL.</param>
        /// <param name="defaultUrl">The default URL.</param>
        public static void Configure(string formsCookieName, string formsCookiePath, string cookieDomain, bool requireSsl, bool slidingExpiration, TimeSpan timeout, string loginUrl, string defaultUrl)
        {
            FormsCookieName = string.IsNullOrEmpty(formsCookieName) ? FormsCookieName : formsCookieName;
            FormsCookiePath = string.IsNullOrEmpty(formsCookiePath) ? FormsCookiePath : formsCookiePath;
            CookieDomain = string.IsNullOrEmpty(cookieDomain) ? null : cookieDomain;
            RequireSSL = requireSsl;
            SlidingExpiration = slidingExpiration;
            Timeout = timeout > TimeSpan.Zero ? timeout : Timeout;
            LoginUrl = string.IsNullOrEmpty(loginUrl) ? LoginUrl : loginUrl;
            DefaultUrl = string.IsNullOrEmpty(defaultUrl) ? DefaultUrl : defaultUrl;
        }

        /// <summary>Creates a protected string representation of the ticket that is suitable for a cookie or a URL.</summary>
        /// <param name="ticket">The ticket.</param>
        /// <returns>The protected ticket.</returns>
        public static string Encrypt(FormsAuthenticationTicket ticket)
        {
            if (ticket == null)
            {
                throw new ArgumentNullException(nameof(ticket));
            }

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(SerializationFormatVersion);
                writer.Write(ticket.Version);
                writer.Write(ticket.Name);
                writer.Write(ticket.IssueDateUtc.Ticks);
                writer.Write(ticket.ExpirationUtc.Ticks);
                writer.Write(ticket.IsPersistent);
                writer.Write(ticket.UserData);
                writer.Write(ticket.CookiePath);
            }

            var protectedData = MachineKey.Protect(stream.ToArray(), ProtectionPurpose);
            return Convert.ToHexString(protectedData);
        }

        /// <summary>Creates a ticket from a value created by <see cref="Encrypt"/>.</summary>
        /// <param name="encryptedTicket">The protected ticket.</param>
        /// <returns>The ticket, or <see langword="null"/> if the value is not a valid ticket.</returns>
        public static FormsAuthenticationTicket Decrypt(string encryptedTicket)
        {
            if (string.IsNullOrEmpty(encryptedTicket))
            {
                throw new ArgumentException("The encrypted ticket must not be empty.", nameof(encryptedTicket));
            }

            try
            {
                var data = MachineKey.Unprotect(Convert.FromHexString(encryptedTicket), ProtectionPurpose);
                if (data == null)
                {
                    return null;
                }

                using var reader = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
                if (reader.ReadByte() != SerializationFormatVersion)
                {
                    return null;
                }

                var version = reader.ReadInt32();
                var name = reader.ReadString();
                var issueDate = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                var expiration = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                var isPersistent = reader.ReadBoolean();
                var userData = reader.ReadString();
                var cookiePath = reader.ReadString();
                return new FormsAuthenticationTicket(version, name, issueDate, expiration, isPersistent, userData, cookiePath);
            }
            catch (Exception ex) when (ex is FormatException || ex is CryptographicException || ex is EndOfStreamException || ex is IOException)
            {
                return null;
            }
        }

        /// <summary>Renews the ticket when sliding expiration is enabled and more than half of the validity has elapsed.</summary>
        /// <param name="ticket">The ticket.</param>
        /// <returns>The renewed ticket or the given ticket.</returns>
        public static FormsAuthenticationTicket RenewTicketIfOld(FormsAuthenticationTicket ticket)
        {
            if (ticket == null)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            var validity = ticket.ExpirationUtc - ticket.IssueDateUtc;
            if (now - ticket.IssueDateUtc <= TimeSpan.FromTicks(validity.Ticks / 2))
            {
                return ticket;
            }

            return new FormsAuthenticationTicket(ticket.Version, ticket.Name, now.ToLocalTime(), now.Add(validity).ToLocalTime(), ticket.IsPersistent, ticket.UserData, ticket.CookiePath);
        }

        /// <summary>Creates the authentication cookie for the given user.</summary>
        /// <param name="userName">The user name.</param>
        /// <param name="createPersistentCookie">Whether a persistent cookie is created.</param>
        /// <returns>The cookie.</returns>
        public static HttpCookie GetAuthCookie(string userName, bool createPersistentCookie)
        {
            return GetAuthCookie(userName, createPersistentCookie, FormsCookiePath);
        }

        /// <summary>Creates the authentication cookie for the given user.</summary>
        /// <param name="userName">The user name.</param>
        /// <param name="createPersistentCookie">Whether a persistent cookie is created.</param>
        /// <param name="strCookiePath">The cookie path.</param>
        /// <returns>The cookie.</returns>
        public static HttpCookie GetAuthCookie(string userName, bool createPersistentCookie, string strCookiePath)
        {
            var now = DateTime.Now;
            var ticket = new FormsAuthenticationTicket(2, userName, now, now.Add(Timeout), createPersistentCookie, string.Empty, strCookiePath ?? FormsCookiePath);
            var cookie = new HttpCookie(FormsCookieName, Encrypt(ticket))
            {
                HttpOnly = true,
                Path = ticket.CookiePath,
                Secure = RequireSSL,
                SameSite = CookieSameSite,
            };

            if (CookieDomain != null)
            {
                cookie.Domain = CookieDomain;
            }

            if (createPersistentCookie)
            {
                cookie.Expires = ticket.Expiration;
            }

            return cookie;
        }

        /// <summary>Creates the authentication cookie for the given user and adds it to the current response.</summary>
        /// <param name="userName">The user name.</param>
        /// <param name="createPersistentCookie">Whether a persistent cookie is created.</param>
        public static void SetAuthCookie(string userName, bool createPersistentCookie)
        {
            SetAuthCookie(userName, createPersistentCookie, FormsCookiePath);
        }

        /// <summary>Creates the authentication cookie for the given user and adds it to the current response.</summary>
        /// <param name="userName">The user name.</param>
        /// <param name="createPersistentCookie">Whether a persistent cookie is created.</param>
        /// <param name="strCookiePath">The cookie path.</param>
        public static void SetAuthCookie(string userName, bool createPersistentCookie, string strCookiePath)
        {
            var context = GetCurrentContext();
            context.Response.Cookies.Remove(FormsCookieName);
            context.Response.Cookies.Add(GetAuthCookie(userName, createPersistentCookie, strCookiePath));
        }

        /// <summary>Removes the forms authentication ticket from the browser.</summary>
        public static void SignOut()
        {
            var context = GetCurrentContext();
            context.Response.Cookies.Remove(FormsCookieName);
            var cookie = new HttpCookie(FormsCookieName, string.Empty)
            {
                HttpOnly = true,
                Path = FormsCookiePath,
                Secure = RequireSSL,
                Expires = new DateTime(1999, 10, 12, 0, 0, 0, DateTimeKind.Utc),
            };

            if (CookieDomain != null)
            {
                cookie.Domain = CookieDomain;
            }

            context.Response.Cookies.Add(cookie);
        }

        /// <summary>Gets the URL to redirect to after a successful login.</summary>
        /// <param name="userName">The user name.</param>
        /// <param name="createPersistentCookie">Whether a persistent cookie is created.</param>
        /// <returns>The URL.</returns>
        public static string GetRedirectUrl(string userName, bool createPersistentCookie)
        {
            var returnUrl = HttpContext.Current?.Request.QueryString["ReturnUrl"];
            if (!string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith("/", StringComparison.Ordinal) && !returnUrl.StartsWith("//", StringComparison.Ordinal))
            {
                return returnUrl;
            }

            return VirtualPathUtility.ToAbsolute(DefaultUrl);
        }

        /// <summary>Redirects the browser to the login URL.</summary>
        public static void RedirectToLoginPage()
        {
            var context = GetCurrentContext();
            var returnUrl = HttpUtility.UrlEncode(context.Request.RawUrl);
            var loginUrl = VirtualPathUtility.ToAbsolute(LoginUrl);
            var separator = loginUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            context.Response.Redirect(string.Format(CultureInfo.InvariantCulture, "{0}{1}ReturnUrl={2}", loginUrl, separator, returnUrl), false);
        }

        private static HttpContext GetCurrentContext()
        {
            return HttpContext.Current ?? throw new InvalidOperationException("There is no current HTTP context.");
        }
    }
}
