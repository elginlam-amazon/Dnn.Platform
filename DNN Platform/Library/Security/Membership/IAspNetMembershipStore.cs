// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Security.Membership
{
    using System;

    using DotNetNuke.Common.Options;

    /// <summary>
    /// The platform's replacement for <c>System.Web.Security.Membership</c> /
    /// <c>SqlMembershipProvider</c>, which do not exist on modern .NET.
    /// </summary>
    /// <remarks>
    /// The implementation calls the same <c>aspnet_Membership_*</c> / <c>aspnet_Users_*</c>
    /// stored procedures in the same database and applies the same password encoding, so
    /// existing credentials keep working unchanged.
    /// </remarks>
    public interface IAspNetMembershipStore
    {
        /// <summary>Gets the membership settings (the replacement for the legacy <c>&lt;membership&gt;</c> configuration).</summary>
        MembershipOptions Options { get; }

        /// <summary>Gets a user by name.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="updateLastActivity">Whether to stamp the user's last-activity date.</param>
        /// <returns>The user, or <see langword="null"/> when no such user exists.</returns>
        MembershipUser GetUser(string username, bool updateLastActivity);

        /// <summary>Gets a user by its store identifier.</summary>
        /// <param name="providerUserKey">The store identifier.</param>
        /// <returns>The user, or <see langword="null"/> when no such user exists.</returns>
        MembershipUser GetUser(Guid providerUserKey);

        /// <summary>Creates a user.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="password">The clear-text password.</param>
        /// <param name="email">The e-mail address.</param>
        /// <param name="passwordQuestion">The password question, or <see langword="null"/>.</param>
        /// <param name="passwordAnswer">The password answer, or <see langword="null"/>.</param>
        /// <param name="isApproved">Whether the account starts out approved.</param>
        /// <param name="status">The outcome of the operation.</param>
        /// <returns>The created user, or <see langword="null"/> when creation failed.</returns>
        MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, out MembershipCreateStatus status);

        /// <summary>Deletes a user.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="deleteAllRelatedData">Whether to delete the user's profile and role data too.</param>
        /// <returns><see langword="true"/> when the user was deleted.</returns>
        bool DeleteUser(string username, bool deleteAllRelatedData);

        /// <summary>Persists the mutable properties (e-mail, comment, approval, dates) of a user.</summary>
        /// <param name="user">The user to persist.</param>
        void UpdateUser(MembershipUser user);

        /// <summary>Verifies a user's credentials, updating the lock-out counters exactly as the legacy provider did.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="password">The clear-text password.</param>
        /// <returns><see langword="true"/> when the credentials are valid.</returns>
        bool ValidateUser(string username, string password);

        /// <summary>Changes a user's password.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="oldPassword">The current password.</param>
        /// <param name="newPassword">The new password.</param>
        /// <returns><see langword="true"/> when the password was changed.</returns>
        bool ChangePassword(string username, string oldPassword, string newPassword);

        /// <summary>Changes a user's password question and answer.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="password">The current password.</param>
        /// <param name="newPasswordQuestion">The new question.</param>
        /// <param name="newPasswordAnswer">The new answer.</param>
        /// <returns><see langword="true"/> when the question and answer were changed.</returns>
        bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer);

        /// <summary>Retrieves a user's password. Requires <see cref="MembershipOptions.EnablePasswordRetrieval"/>.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="passwordAnswer">The answer to the password question, when required.</param>
        /// <returns>The password.</returns>
        string GetPassword(string username, string passwordAnswer);

        /// <summary>Resets a user's password to a newly generated one.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="passwordAnswer">The answer to the password question, when required.</param>
        /// <returns>The new password.</returns>
        string ResetPassword(string username, string passwordAnswer);

        /// <summary>Clears the lock-out on an account.</summary>
        /// <param name="username">The user name.</param>
        /// <returns><see langword="true"/> when the account was unlocked.</returns>
        bool UnlockUser(string username);

        /// <summary>Generates a random password.</summary>
        /// <param name="length">The length of the password.</param>
        /// <param name="numberOfNonAlphanumericCharacters">The minimum number of non-alphanumeric characters.</param>
        /// <returns>The generated password.</returns>
        string GeneratePassword(int length, int numberOfNonAlphanumericCharacters);
    }
}
