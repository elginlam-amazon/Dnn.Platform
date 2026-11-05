// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Security.Authentication
{
    using System;

    /// <summary>
    /// A platform authentication ticket. Replaces
    /// <c>System.Web.Security.FormsAuthenticationTicket</c>, which does not exist on modern .NET.
    /// </summary>
    /// <remarks>
    /// The ticket carries the same information the forms-authentication ticket carried, and is
    /// protected/unprotected by <see cref="IAuthenticationCookieService"/> using ASP.NET Core
    /// data protection instead of the removed <c>machineKey</c> pipeline.
    /// </remarks>
    public class DnnAuthenticationTicket
    {
        /// <summary>Initializes a new instance of the <see cref="DnnAuthenticationTicket"/> class.</summary>
        public DnnAuthenticationTicket()
        {
            this.Version = 2;
            this.IssueDate = DateTime.Now;
            this.CookiePath = "/";
            this.UserData = string.Empty;
        }

        /// <summary>Initializes a new instance of the <see cref="DnnAuthenticationTicket"/> class.</summary>
        /// <param name="name">The authenticated user name.</param>
        /// <param name="isPersistent">Whether the ticket survives browser restarts.</param>
        /// <param name="timeoutMinutes">The lifetime of the ticket in minutes.</param>
        public DnnAuthenticationTicket(string name, bool isPersistent, int timeoutMinutes)
            : this()
        {
            this.Name = name;
            this.IsPersistent = isPersistent;
            this.Expiration = this.IssueDate.AddMinutes(timeoutMinutes);
        }

        /// <summary>Gets or sets the ticket format version.</summary>
        public int Version { get; set; }

        /// <summary>Gets or sets the authenticated user name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the date the ticket was issued.</summary>
        public DateTime IssueDate { get; set; }

        /// <summary>Gets or sets the date the ticket expires.</summary>
        public DateTime Expiration { get; set; }

        /// <summary>Gets or sets a value indicating whether the ticket survives browser restarts.</summary>
        public bool IsPersistent { get; set; }

        /// <summary>Gets or sets the application-specific payload carried by the ticket.</summary>
        public string UserData { get; set; }

        /// <summary>Gets or sets the cookie path the ticket is scoped to.</summary>
        public string CookiePath { get; set; }

        /// <summary>Gets a value indicating whether the ticket has expired.</summary>
        public bool Expired => this.Expiration < DateTime.Now;
    }
}
