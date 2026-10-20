// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Security
{
    using System.Configuration.Provider;

    /// <summary>Defines the contract that ASP.NET implements to provide membership services using custom membership providers.</summary>
    public abstract class MembershipProvider : ProviderBase
    {
        /// <summary>Gets or sets the name of the application.</summary>
        public abstract string ApplicationName { get; set; }

        /// <summary>Gets a value indicating whether passwords can be retrieved.</summary>
        public abstract bool EnablePasswordRetrieval { get; }

        /// <summary>Gets a value indicating whether passwords can be reset.</summary>
        public abstract bool EnablePasswordReset { get; }

        /// <summary>Gets a value indicating whether a password question and answer are required.</summary>
        public abstract bool RequiresQuestionAndAnswer { get; }

        /// <summary>Gets the number of invalid password attempts before the user is locked out.</summary>
        public abstract int MaxInvalidPasswordAttempts { get; }

        /// <summary>Gets the window in minutes for the invalid password attempts.</summary>
        public abstract int PasswordAttemptWindow { get; }

        /// <summary>Gets a value indicating whether the email addresses must be unique.</summary>
        public abstract bool RequiresUniqueEmail { get; }

        /// <summary>Gets the format of the stored passwords.</summary>
        public abstract MembershipPasswordFormat PasswordFormat { get; }

        /// <summary>Gets the minimum password length.</summary>
        public abstract int MinRequiredPasswordLength { get; }

        /// <summary>Gets the minimum number of special characters of a password.</summary>
        public abstract int MinRequiredNonAlphanumericCharacters { get; }

        /// <summary>Gets the regular expression used to validate a password.</summary>
        public abstract string PasswordStrengthRegularExpression { get; }

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
        public abstract MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, object providerUserKey, out MembershipCreateStatus status);

        /// <summary>Changes the password question and answer.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="password">The password.</param>
        /// <param name="newPasswordQuestion">The new question.</param>
        /// <param name="newPasswordAnswer">The new answer.</param>
        /// <returns>A value indicating whether the operation succeeded.</returns>
        public abstract bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer);

        /// <summary>Gets the password of a user.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="answer">The password answer.</param>
        /// <returns>The password.</returns>
        public abstract string GetPassword(string username, string answer);

        /// <summary>Changes the password of a user.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="oldPassword">The current password.</param>
        /// <param name="newPassword">The new password.</param>
        /// <returns>A value indicating whether the operation succeeded.</returns>
        public abstract bool ChangePassword(string username, string oldPassword, string newPassword);

        /// <summary>Resets the password of a user.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="answer">The password answer.</param>
        /// <returns>The new password.</returns>
        public abstract string ResetPassword(string username, string answer);

        /// <summary>Updates a user.</summary>
        /// <param name="user">The user.</param>
        public abstract void UpdateUser(MembershipUser user);

        /// <summary>Validates the user name and password.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="password">The password.</param>
        /// <returns>A value indicating whether the credentials are valid.</returns>
        public abstract bool ValidateUser(string username, string password);

        /// <summary>Unlocks a user.</summary>
        /// <param name="userName">The user name.</param>
        /// <returns>A value indicating whether the user was unlocked.</returns>
        public abstract bool UnlockUser(string userName);

        /// <summary>Gets a user by its unique identifier.</summary>
        /// <param name="providerUserKey">The unique identifier.</param>
        /// <param name="userIsOnline">Whether the last activity date is updated.</param>
        /// <returns>The user or <see langword="null"/>.</returns>
        public abstract MembershipUser GetUser(object providerUserKey, bool userIsOnline);

        /// <summary>Gets a user by its name.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="userIsOnline">Whether the last activity date is updated.</param>
        /// <returns>The user or <see langword="null"/>.</returns>
        public abstract MembershipUser GetUser(string username, bool userIsOnline);

        /// <summary>Gets the user name associated with an email address.</summary>
        /// <param name="email">The email address.</param>
        /// <returns>The user name or <see langword="null"/>.</returns>
        public abstract string GetUserNameByEmail(string email);

        /// <summary>Deletes a user.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="deleteAllRelatedData">Whether the related data is deleted as well.</param>
        /// <returns>A value indicating whether the user was deleted.</returns>
        public abstract bool DeleteUser(string username, bool deleteAllRelatedData);

        /// <summary>Gets the number of users which are online.</summary>
        /// <returns>The number of users.</returns>
        public abstract int GetNumberOfUsersOnline();
    }
}
