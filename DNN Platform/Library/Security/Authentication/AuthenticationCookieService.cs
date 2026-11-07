// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Security.Authentication
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Security.Claims;
    using System.Text;
    using System.Threading.Tasks;

    using DotNetNuke.Common.Options;
    using DotNetNuke.Framework;

    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Options;

    /// <summary>The default <see cref="IAuthenticationCookieService"/>, backed by cookie authentication and data protection.</summary>
    public class AuthenticationCookieService : IAuthenticationCookieService
    {
        private const char FieldSeparator = '\u001f';

        private readonly IOptionsMonitor<AuthenticationCookieOptions> options;
        private readonly IDataProtector protector;

        /// <summary>Initializes a new instance of the <see cref="AuthenticationCookieService"/> class.</summary>
        /// <param name="options">The cookie settings.</param>
        /// <param name="dataProtectionProvider">The application's data-protection provider.</param>
        public AuthenticationCookieService(IOptionsMonitor<AuthenticationCookieOptions> options, IDataProtectionProvider dataProtectionProvider)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.protector = (dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider)))
                .CreateProtector("DotNetNuke.Security.Authentication.AuthenticationCookie", "v1");
        }

        /// <summary>
        /// Gets the service registered with the application's dependency-injection container.
        /// </summary>
        /// <remarks>Provided for the platform's static authentication call sites. Prefer injecting <see cref="IAuthenticationCookieService"/>.</remarks>
        public static IAuthenticationCookieService Current =>
            ServiceLocator.GetRequiredService<IAuthenticationCookieService>();

        /// <inheritdoc />
        public AuthenticationCookieOptions Options => this.options.CurrentValue;

        /// <inheritdoc />
        public string CookieName => this.Options.Name;

        /// <inheritdoc />
        public string CookieDomain => this.Options.Domain ?? string.Empty;

        /// <inheritdoc />
        public string CookiePath => string.IsNullOrEmpty(this.Options.Path) ? "/" : this.Options.Path;

        /// <inheritdoc />
        public bool RequireSsl => this.Options.RequireSsl;

        /// <inheritdoc />
        public TimeSpan Timeout => TimeSpan.FromMinutes(this.Options.TimeoutMinutes);

        /// <inheritdoc />
        public string Protect(DnnAuthenticationTicket ticket)
        {
            if (ticket is null)
            {
                throw new ArgumentNullException(nameof(ticket));
            }

            var payload = string.Join(
                FieldSeparator,
                ticket.Version.ToString(CultureInfo.InvariantCulture),
                ticket.Name ?? string.Empty,
                ticket.IssueDate.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),
                ticket.Expiration.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),
                ticket.IsPersistent ? "1" : "0",
                ticket.CookiePath ?? "/",
                ticket.UserData ?? string.Empty);

            return Convert.ToBase64String(this.protector.Protect(Encoding.UTF8.GetBytes(payload)));
        }

        /// <inheritdoc />
        public DnnAuthenticationTicket Unprotect(string protectedValue)
        {
            if (string.IsNullOrEmpty(protectedValue))
            {
                return null;
            }

            string payload;
            try
            {
                payload = Encoding.UTF8.GetString(this.protector.Unprotect(Convert.FromBase64String(protectedValue)));
            }
            catch (FormatException)
            {
                // Not a value this application produced.
                return null;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // Tampered with, or protected by a different key ring.
                return null;
            }

            var fields = payload.Split(FieldSeparator);
            if (fields.Length < 7)
            {
                return null;
            }

            return new DnnAuthenticationTicket
            {
                Version = int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) ? version : 2,
                Name = fields[1],
                IssueDate = new DateTime(long.Parse(fields[2], CultureInfo.InvariantCulture), DateTimeKind.Utc).ToLocalTime(),
                Expiration = new DateTime(long.Parse(fields[3], CultureInfo.InvariantCulture), DateTimeKind.Utc).ToLocalTime(),
                IsPersistent = fields[4] == "1",
                CookiePath = fields[5],
                UserData = fields[6],
            };
        }

        /// <inheritdoc />
        public async Task SignInAsync(HttpContext context, string userName, bool isPersistent, IEnumerable<Claim> claims, string cookieDomain)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrEmpty(userName))
            {
                throw new ArgumentException("A user name is required to sign in.", nameof(userName));
            }

            var allClaims = new List<Claim> { new Claim(ClaimTypes.Name, userName) };
            if (claims is not null)
            {
                allClaims.AddRange(claims.Where(claim => claim is not null && claim.Type != ClaimTypes.Name));
            }

            var identity = new ClaimsIdentity(allClaims, DnnAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);

            var properties = new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(this.Timeout),
                AllowRefresh = this.Options.SlidingExpiration,
            };

            if (!string.IsNullOrEmpty(cookieDomain))
            {
                // Remembered so that a portal-group sign-in can expire the very same cookie later.
                properties.Items[DnnAuthenticationDefaults.CookieDomainPropertyKey] = cookieDomain;
            }

            await context.SignInAsync(DnnAuthenticationDefaults.AuthenticationScheme, principal, properties);
        }

        /// <inheritdoc />
        public Task SignOutAsync(HttpContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return context.SignOutAsync(DnnAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
