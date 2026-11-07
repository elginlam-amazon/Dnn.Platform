// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Common.Options
{
    /// <summary>
    /// The replacement for the <c>web.config</c> <c>&lt;system.web&gt;&lt;membership&gt;</c> element and
    /// its <c>AspNetSqlMembershipProvider</c> entry, neither of which exists on modern .NET.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The defaults below are the values DNN has always shipped in its own <c>web.config</c>
    /// (<c>hashAlgorithmType="SHA256"</c>, <c>applicationName="DotNetNuke"</c>,
    /// <c>passwordFormat="Hashed"</c>, <c>minRequiredPasswordLength="7"</c>, …) so an existing
    /// installation that does not add a <c>Membership</c> section keeps behaving identically.
    /// </para>
    /// <para>
    /// <b>Do not change <see cref="PasswordFormat"/>, <see cref="HashAlgorithmType"/> or
    /// <see cref="ApplicationName"/> on an existing installation.</b> Stored password hashes
    /// were produced with those exact values; changing any of them makes every existing
    /// password un-verifiable and locks all users out.
    /// </para>
    /// </remarks>
    public class MembershipOptions
    {
        /// <summary>The configuration section name.</summary>
        public const string SectionName = "Membership";

        /// <summary>Gets or sets the application name that scopes users in the ASP.NET membership tables (legacy <c>applicationName</c>).</summary>
        public string ApplicationName { get; set; } = "DotNetNuke";

        /// <summary>Gets or sets the name of the connection string holding the membership database (legacy <c>connectionStringName</c>).</summary>
        public string ConnectionStringName { get; set; } = "SiteSqlServer";

        /// <summary>Gets or sets the password storage format, 0 = Clear, 1 = Hashed, 2 = Encrypted (legacy <c>passwordFormat</c>).</summary>
        public int PasswordFormat { get; set; } = 1;

        /// <summary>
        /// Gets or sets the hash algorithm used for hashed passwords (legacy
        /// <c>&lt;membership hashAlgorithmType="…"&gt;</c>). A name starting with <c>HMAC</c>
        /// selects the keyed-hash code path, exactly as ASP.NET did.
        /// </summary>
        public string HashAlgorithmType { get; set; } = "SHA256";

        /// <summary>Gets or sets a value indicating whether passwords can be retrieved (legacy <c>enablePasswordRetrieval</c>).</summary>
        public bool EnablePasswordRetrieval { get; set; }

        /// <summary>Gets or sets a value indicating whether passwords can be reset (legacy <c>enablePasswordReset</c>).</summary>
        public bool EnablePasswordReset { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether a password question and answer are required (legacy <c>requiresQuestionAndAnswer</c>).</summary>
        public bool RequiresQuestionAndAnswer { get; set; }

        /// <summary>Gets or sets a value indicating whether every user must have a unique e-mail address (legacy <c>requiresUniqueEmail</c>).</summary>
        public bool RequiresUniqueEmail { get; set; }

        /// <summary>Gets or sets the minimum password length (legacy <c>minRequiredPasswordLength</c>).</summary>
        public int MinRequiredPasswordLength { get; set; } = 7;

        /// <summary>Gets or sets the minimum number of non-alphanumeric characters (legacy <c>minRequiredNonalphanumericCharacters</c>).</summary>
        public int MinRequiredNonAlphanumericCharacters { get; set; }

        /// <summary>Gets or sets the number of failed attempts before the account is locked out (legacy <c>maxInvalidPasswordAttempts</c>).</summary>
        public int MaxInvalidPasswordAttempts { get; set; } = 5;

        /// <summary>Gets or sets the window, in minutes, over which failed attempts are counted (legacy <c>passwordAttemptWindow</c>).</summary>
        public int PasswordAttemptWindow { get; set; } = 10;

        /// <summary>Gets or sets a regular expression the password must match, or empty for none (legacy <c>passwordStrengthRegularExpression</c>).</summary>
        public string PasswordStrengthRegularExpression { get; set; } = string.Empty;

        /// <summary>Gets or sets the window, in minutes, in which a user counts as "online" (legacy <c>userIsOnlineTimeWindow</c>).</summary>
        public int UserIsOnlineTimeWindow { get; set; } = 15;

        /// <summary>Gets or sets the command timeout, in seconds, for membership database calls.</summary>
        public int CommandTimeout { get; set; } = 30;
    }
}
