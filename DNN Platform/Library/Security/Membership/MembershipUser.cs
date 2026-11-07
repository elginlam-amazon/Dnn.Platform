// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Security.Membership
{
    using System;

    /// <summary>
    /// A single user as stored in the ASP.NET membership tables. Replaces
    /// <c>System.Web.Security.MembershipUser</c>, which does not exist on modern .NET.
    /// </summary>
    /// <remarks>
    /// The property and method surface mirrors the legacy type (including the instance methods
    /// that act on the store) so that the platform's membership code reads the same. The
    /// behavior is delegated to <see cref="IAspNetMembershipStore"/>, which talks to the very
    /// same <c>aspnet_Membership_*</c> stored procedures and uses the very same password
    /// encoding the legacy provider used.
    /// </remarks>
    [Serializable]
    public class MembershipUser
    {
        [NonSerialized]
        private IAspNetMembershipStore store;

        /// <summary>Initializes a new instance of the <see cref="MembershipUser"/> class.</summary>
        public MembershipUser()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MembershipUser"/> class.</summary>
        /// <param name="store">The store this user was read from and which persists changes to it.</param>
        public MembershipUser(IAspNetMembershipStore store)
        {
            this.store = store;
        }

        /// <summary>Gets or sets the user name.</summary>
        public string UserName { get; set; }

        /// <summary>Gets or sets the user's unique identifier in the membership store (the legacy <c>ProviderUserKey</c>).</summary>
        public object ProviderUserKey { get; set; }

        /// <summary>Gets or sets the user's e-mail address.</summary>
        public string Email { get; set; }

        /// <summary>Gets or sets the password question.</summary>
        public string PasswordQuestion { get; set; }

        /// <summary>Gets or sets the administrative comment.</summary>
        public string Comment { get; set; }

        /// <summary>Gets or sets a value indicating whether the account is approved.</summary>
        public bool IsApproved { get; set; }

        /// <summary>Gets or sets a value indicating whether the account is locked out.</summary>
        public bool IsLockedOut { get; set; }

        /// <summary>Gets or sets the date the account was created.</summary>
        public DateTime CreationDate { get; set; }

        /// <summary>Gets or sets the date of the last successful login.</summary>
        public DateTime LastLoginDate { get; set; }

        /// <summary>Gets or sets the date of the last activity.</summary>
        public DateTime LastActivityDate { get; set; }

        /// <summary>Gets or sets the date the password was last changed.</summary>
        public DateTime LastPasswordChangedDate { get; set; }

        /// <summary>Gets or sets the date the account was last locked out.</summary>
        public DateTime LastLockoutDate { get; set; }

        /// <summary>Gets or sets a value indicating whether the user is currently online.</summary>
        public bool IsOnline { get; set; }

        /// <summary>Changes the user's password.</summary>
        /// <param name="oldPassword">The current password.</param>
        /// <param name="newPassword">The new password.</param>
        /// <returns><see langword="true"/> when the password was changed.</returns>
        public bool ChangePassword(string oldPassword, string newPassword)
        {
            return this.Store.ChangePassword(this.UserName, oldPassword, newPassword);
        }

        /// <summary>Changes the user's password question and answer.</summary>
        /// <param name="password">The current password.</param>
        /// <param name="newPasswordQuestion">The new question.</param>
        /// <param name="newPasswordAnswer">The new answer.</param>
        /// <returns><see langword="true"/> when the question and answer were changed.</returns>
        public bool ChangePasswordQuestionAndAnswer(string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            var changed = this.Store.ChangePasswordQuestionAndAnswer(this.UserName, password, newPasswordQuestion, newPasswordAnswer);
            if (changed)
            {
                this.PasswordQuestion = newPasswordQuestion;
            }

            return changed;
        }

        /// <summary>Gets the user's password. Requires password retrieval to be enabled.</summary>
        /// <returns>The password.</returns>
        public string GetPassword()
        {
            return this.Store.GetPassword(this.UserName, null);
        }

        /// <summary>Gets the user's password after verifying the password answer.</summary>
        /// <param name="passwordAnswer">The answer to the password question.</param>
        /// <returns>The password.</returns>
        public string GetPassword(string passwordAnswer)
        {
            return this.Store.GetPassword(this.UserName, passwordAnswer);
        }

        /// <summary>Resets the user's password to a new, generated password.</summary>
        /// <returns>The new password.</returns>
        public string ResetPassword()
        {
            return this.Store.ResetPassword(this.UserName, null);
        }

        /// <summary>Resets the user's password after verifying the password answer.</summary>
        /// <param name="passwordAnswer">The answer to the password question.</param>
        /// <returns>The new password.</returns>
        public string ResetPassword(string passwordAnswer)
        {
            return this.Store.ResetPassword(this.UserName, passwordAnswer);
        }

        /// <summary>Clears the lock-out on the account.</summary>
        /// <returns><see langword="true"/> when the account was unlocked.</returns>
        public bool UnlockUser()
        {
            var unlocked = this.Store.UnlockUser(this.UserName);
            if (unlocked)
            {
                this.IsLockedOut = false;
            }

            return unlocked;
        }

        /// <summary>Gets the store backing this instance.</summary>
        private IAspNetMembershipStore Store =>
            this.store ??= AspNetMembershipStore.Current;
    }
}
