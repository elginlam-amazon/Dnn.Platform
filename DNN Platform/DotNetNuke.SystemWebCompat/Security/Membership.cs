// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Security
{
    using System.Configuration.Provider;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Replacement for the System.Web <c>Membership</c> API on .NET 10.
    /// </summary>
    /// <remarks>
    /// The providers that were configured in the <c>system.web/membership</c> section of the <c>web.config</c> are
    /// registered with <see cref="Initialize(MembershipProvider, int, string)"/> during the application startup.
    /// </remarks>
    public static class Membership
    {
        private const string PunctuationCharacters = "!@#$%^&*()_-+=[{]};:>|./?";
        private const string AlphanumericCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        private static readonly MembershipProviderCollection ProvidersCollection = new MembershipProviderCollection();
        private static MembershipProvider provider;

        /// <summary>Gets the default membership provider.</summary>
        public static MembershipProvider Provider => provider ?? throw new ProviderException("No membership provider has been configured, call Membership.Initialize during the application startup.");

        /// <summary>Gets the configured membership providers.</summary>
        public static MembershipProviderCollection Providers => ProvidersCollection;

        /// <summary>Gets the time window in minutes after the last activity during which a user is considered online.</summary>
        public static int UserIsOnlineTimeWindow { get; private set; } = 15;

        /// <summary>Gets the name of the hash algorithm used for hashed passwords.</summary>
        public static string HashAlgorithmType { get; private set; } = "SHA256";

        /// <summary>Gets or sets the application name of the default provider.</summary>
        public static string ApplicationName
        {
            get => Provider.ApplicationName;
            set => Provider.ApplicationName = value;
        }

        /// <summary>Gets a value indicating whether the passwords can be reset.</summary>
        public static bool EnablePasswordReset => Provider.EnablePasswordReset;

        /// <summary>Gets a value indicating whether the passwords can be retrieved.</summary>
        public static bool EnablePasswordRetrieval => Provider.EnablePasswordRetrieval;

        /// <summary>Gets a value indicating whether a password question and answer are required.</summary>
        public static bool RequiresQuestionAndAnswer => Provider.RequiresQuestionAndAnswer;

        /// <summary>Gets the number of invalid password attempts before the user is locked out.</summary>
        public static int MaxInvalidPasswordAttempts => Provider.MaxInvalidPasswordAttempts;

        /// <summary>Gets the window in minutes for the invalid password attempts.</summary>
        public static int PasswordAttemptWindow => Provider.PasswordAttemptWindow;

        /// <summary>Gets the minimum password length.</summary>
        public static int MinRequiredPasswordLength => Provider.MinRequiredPasswordLength;

        /// <summary>Gets the minimum number of special characters of a password.</summary>
        public static int MinRequiredNonAlphanumericCharacters => Provider.MinRequiredNonAlphanumericCharacters;

        /// <summary>Gets the regular expression used to validate a password.</summary>
        public static string PasswordStrengthRegularExpression => Provider.PasswordStrengthRegularExpression;

        /// <summary>Registers the default membership provider (replaces the <c>system.web/membership</c> configuration section).</summary>
        /// <param name="defaultProvider">The initialized default provider.</param>
        /// <param name="userIsOnlineTimeWindow">The online time window in minutes.</param>
        /// <param name="hashAlgorithmType">The name of the hash algorithm used for hashed passwords.</param>
        public static void Initialize(MembershipProvider defaultProvider, int userIsOnlineTimeWindow, string hashAlgorithmType)
        {
            if (defaultProvider == null)
            {
                throw new ArgumentNullException(nameof(defaultProvider));
            }

            lock (ProvidersCollection)
            {
                if (ProvidersCollection[defaultProvider.Name] == null)
                {
                    ProvidersCollection.Add(defaultProvider);
                }

                provider = defaultProvider;
                UserIsOnlineTimeWindow = userIsOnlineTimeWindow > 0 ? userIsOnlineTimeWindow : UserIsOnlineTimeWindow;
                HashAlgorithmType = string.IsNullOrEmpty(hashAlgorithmType) ? HashAlgorithmType : hashAlgorithmType;
            }
        }

        /// <summary>Creates a user.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="password">The password.</param>
        /// <returns>The created user.</returns>
        public static MembershipUser CreateUser(string username, string password)
        {
            return CreateUser(username, password, null);
        }

        /// <summary>Creates a user.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="password">The password.</param>
        /// <param name="email">The email address.</param>
        /// <returns>The created user.</returns>
        public static MembershipUser CreateUser(string username, string password, string email)
        {
            var user = CreateUser(username, password, email, null, null, true, out var status);
            if (user == null)
            {
                throw new MembershipCreateUserException(status);
            }

            return user;
        }

        /// <summary>Creates a user.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="password">The password.</param>
        /// <param name="email">The email address.</param>
        /// <param name="passwordQuestion">The password question.</param>
        /// <param name="passwordAnswer">The password answer.</param>
        /// <param name="isApproved">Whether the user is approved.</param>
        /// <param name="status">The status of the operation.</param>
        /// <returns>The created user or <see langword="null"/>.</returns>
        public static MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, out MembershipCreateStatus status)
        {
            return CreateUser(username, password, email, passwordQuestion, passwordAnswer, isApproved, null, out status);
        }

        /// <summary>Creates a user.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="password">The password.</param>
        /// <param name="email">The email address.</param>
        /// <param name="passwordQuestion">The password question.</param>
        /// <param name="passwordAnswer">The password answer.</param>
        /// <param name="isApproved">Whether the user is approved.</param>
        /// <param name="providerUserKey">The unique identifier of the user, or <see langword="null"/>.</param>
        /// <param name="status">The status of the operation.</param>
        /// <returns>The created user or <see langword="null"/>.</returns>
        public static MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, object providerUserKey, out MembershipCreateStatus status)
        {
            username = username?.Trim();
            email = email?.Trim();
            passwordQuestion = passwordQuestion?.Trim();
            passwordAnswer = passwordAnswer?.Trim();
            if (string.IsNullOrEmpty(username) || username.Contains(',', StringComparison.Ordinal))
            {
                status = MembershipCreateStatus.InvalidUserName;
                return null;
            }

            if (string.IsNullOrEmpty(password))
            {
                status = MembershipCreateStatus.InvalidPassword;
                return null;
            }

            return Provider.CreateUser(username, password, email, passwordQuestion, passwordAnswer, isApproved, providerUserKey, out status);
        }

        /// <summary>Validates the user name and password.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="password">The password.</param>
        /// <returns>A value indicating whether the credentials are valid.</returns>
        public static bool ValidateUser(string username, string password)
        {
            return Provider.ValidateUser(username, password);
        }

        /// <summary>Gets the user with the given name.</summary>
        /// <param name="username">The user name.</param>
        /// <returns>The user or <see langword="null"/>.</returns>
        public static MembershipUser GetUser(string username)
        {
            return GetUser(username, false);
        }

        /// <summary>Gets the user with the given name.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="userIsOnline">Whether the last activity date is updated.</param>
        /// <returns>The user or <see langword="null"/>.</returns>
        public static MembershipUser GetUser(string username, bool userIsOnline)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("The user name must not be empty.", nameof(username));
            }

            return Provider.GetUser(username.Trim(), userIsOnline);
        }

        /// <summary>Gets the user with the given unique identifier.</summary>
        /// <param name="providerUserKey">The unique identifier.</param>
        /// <returns>The user or <see langword="null"/>.</returns>
        public static MembershipUser GetUser(object providerUserKey)
        {
            return GetUser(providerUserKey, false);
        }

        /// <summary>Gets the user with the given unique identifier.</summary>
        /// <param name="providerUserKey">The unique identifier.</param>
        /// <param name="userIsOnline">Whether the last activity date is updated.</param>
        /// <returns>The user or <see langword="null"/>.</returns>
        public static MembershipUser GetUser(object providerUserKey, bool userIsOnline)
        {
            if (providerUserKey == null)
            {
                throw new ArgumentNullException(nameof(providerUserKey));
            }

            return Provider.GetUser(providerUserKey, userIsOnline);
        }

        /// <summary>Gets the user name associated with an email address.</summary>
        /// <param name="emailToMatch">The email address.</param>
        /// <returns>The user name or <see langword="null"/>.</returns>
        public static string GetUserNameByEmail(string emailToMatch)
        {
            return Provider.GetUserNameByEmail(emailToMatch?.Trim());
        }

        /// <summary>Updates the user.</summary>
        /// <param name="user">The user.</param>
        public static void UpdateUser(MembershipUser user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            Providers[user.ProviderName].UpdateUser(user);
        }

        /// <summary>Deletes the user and all related data.</summary>
        /// <param name="username">The user name.</param>
        /// <returns>A value indicating whether the user was deleted.</returns>
        public static bool DeleteUser(string username)
        {
            return DeleteUser(username, true);
        }

        /// <summary>Deletes the user.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="deleteAllRelatedData">Whether the related data is deleted as well.</param>
        /// <returns>A value indicating whether the user was deleted.</returns>
        public static bool DeleteUser(string username, bool deleteAllRelatedData)
        {
            return Provider.DeleteUser(username?.Trim(), deleteAllRelatedData);
        }

        /// <summary>Gets the number of users which are online.</summary>
        /// <returns>The number of users.</returns>
        public static int GetNumberOfUsersOnline()
        {
            return Provider.GetNumberOfUsersOnline();
        }

        /// <summary>Generates a random password.</summary>
        /// <param name="length">The length of the password (1 to 128).</param>
        /// <param name="numberOfNonAlphanumericCharacters">The minimum number of special characters.</param>
        /// <returns>The password.</returns>
        public static string GeneratePassword(int length, int numberOfNonAlphanumericCharacters)
        {
            if (length < 1 || length > 128)
            {
                throw new ArgumentException("The length of the password must be between 1 and 128.", nameof(length));
            }

            if (numberOfNonAlphanumericCharacters > length || numberOfNonAlphanumericCharacters < 0)
            {
                throw new ArgumentException("The number of non alphanumeric characters must be between 0 and the length of the password.", nameof(numberOfNonAlphanumericCharacters));
            }

            var characters = new char[length];
            for (var i = 0; i < length; i++)
            {
                characters[i] = i < numberOfNonAlphanumericCharacters
                    ? PunctuationCharacters[RandomNumberGenerator.GetInt32(PunctuationCharacters.Length)]
                    : AlphanumericCharacters[RandomNumberGenerator.GetInt32(AlphanumericCharacters.Length)];
            }

            // shuffle, so that the special characters are not at the beginning
            RandomNumberGenerator.Shuffle(characters.AsSpan());
            var password = new StringBuilder(length).Append(characters).ToString();

            // avoid strings which are rejected by the request validation (e.g. "<a" or "&#")
            return password.Contains("&#", StringComparison.Ordinal) ? GeneratePassword(length, numberOfNonAlphanumericCharacters) : password;
        }
    }
}
