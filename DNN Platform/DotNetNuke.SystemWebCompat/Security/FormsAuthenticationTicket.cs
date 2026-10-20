// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Security
{
    /// <summary>Replacement for the System.Web forms authentication ticket on .NET 10.</summary>
    [Serializable]
    public sealed class FormsAuthenticationTicket
    {
        /// <summary>Initializes a new instance of the <see cref="FormsAuthenticationTicket"/> class.</summary>
        /// <param name="name">The user name.</param>
        /// <param name="isPersistent">Whether the ticket is persisted in a persistent cookie.</param>
        /// <param name="timeout">The validity of the ticket in minutes.</param>
        public FormsAuthenticationTicket(string name, bool isPersistent, int timeout)
            : this(2, name, DateTime.Now, DateTime.Now.AddMinutes(timeout), isPersistent, string.Empty, FormsAuthentication.FormsCookiePath)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="FormsAuthenticationTicket"/> class.</summary>
        /// <param name="version">The ticket version.</param>
        /// <param name="name">The user name.</param>
        /// <param name="issueDate">The (local) issue date.</param>
        /// <param name="expiration">The (local) expiration date.</param>
        /// <param name="isPersistent">Whether the ticket is persisted in a persistent cookie.</param>
        /// <param name="userData">User specific data.</param>
        public FormsAuthenticationTicket(int version, string name, DateTime issueDate, DateTime expiration, bool isPersistent, string userData)
            : this(version, name, issueDate, expiration, isPersistent, userData, FormsAuthentication.FormsCookiePath)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="FormsAuthenticationTicket"/> class.</summary>
        /// <param name="version">The ticket version.</param>
        /// <param name="name">The user name.</param>
        /// <param name="issueDate">The (local) issue date.</param>
        /// <param name="expiration">The (local) expiration date.</param>
        /// <param name="isPersistent">Whether the ticket is persisted in a persistent cookie.</param>
        /// <param name="userData">User specific data.</param>
        /// <param name="cookiePath">The cookie path.</param>
        public FormsAuthenticationTicket(int version, string name, DateTime issueDate, DateTime expiration, bool isPersistent, string userData, string cookiePath)
        {
            this.Version = version;
            this.Name = name ?? string.Empty;
            this.IssueDateUtc = issueDate.ToUniversalTime();
            this.ExpirationUtc = expiration.ToUniversalTime();
            this.IsPersistent = isPersistent;
            this.UserData = userData ?? string.Empty;
            this.CookiePath = cookiePath ?? "/";
        }

        /// <summary>Gets the ticket version.</summary>
        public int Version { get; }

        /// <summary>Gets the user name.</summary>
        public string Name { get; }

        /// <summary>Gets the local issue date.</summary>
        public DateTime IssueDate => this.IssueDateUtc.ToLocalTime();

        /// <summary>Gets the UTC issue date.</summary>
        public DateTime IssueDateUtc { get; }

        /// <summary>Gets the local expiration date.</summary>
        public DateTime Expiration => this.ExpirationUtc.ToLocalTime();

        /// <summary>Gets the UTC expiration date.</summary>
        public DateTime ExpirationUtc { get; }

        /// <summary>Gets a value indicating whether the ticket is expired.</summary>
        public bool Expired => this.ExpirationUtc < DateTime.UtcNow;

        /// <summary>Gets a value indicating whether the ticket is persisted in a persistent cookie.</summary>
        public bool IsPersistent { get; }

        /// <summary>Gets the user specific data.</summary>
        public string UserData { get; }

        /// <summary>Gets the cookie path.</summary>
        public string CookiePath { get; }
    }
}
