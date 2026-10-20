// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Security
{
    /// <summary>Exposes and updates the membership user information in the membership data store.</summary>
    [Serializable]
    public class MembershipUser
    {
        private string comment;
        private DateTime lastLoginDate;
        private DateTime lastActivityDate;

        /// <summary>Initializes a new instance of the <see cref="MembershipUser"/> class.</summary>
        /// <param name="providerName">The name of the provider which stores the user.</param>
        /// <param name="name">The user name.</param>
        /// <param name="providerUserKey">The unique identifier of the user in the data store.</param>
        /// <param name="email">The email address.</param>
        /// <param name="passwordQuestion">The password question.</param>
        /// <param name="comment">The comment.</param>
        /// <param name="isApproved">Whether the user is approved.</param>
        /// <param name="isLockedOut">Whether the user is locked out.</param>
        /// <param name="creationDate">The creation date.</param>
        /// <param name="lastLoginDate">The last login date.</param>
        /// <param name="lastActivityDate">The last activity date.</param>
        /// <param name="lastPasswordChangedDate">The date of the last password change.</param>
        /// <param name="lastLockoutDate">The date of the last lockout.</param>
        public MembershipUser(string providerName, string name, object providerUserKey, string email, string passwordQuestion, string comment, bool isApproved, bool isLockedOut, DateTime creationDate, DateTime lastLoginDate, DateTime lastActivityDate, DateTime lastPasswordChangedDate, DateTime lastLockoutDate)
        {
            this.ProviderName = providerName ?? throw new ArgumentNullException(nameof(providerName));
            this.UserName = name?.Trim();
            this.ProviderUserKey = providerUserKey;
            this.Email = email?.Trim();
            this.PasswordQuestion = passwordQuestion?.Trim();
            this.comment = comment;
            this.IsApproved = isApproved;
            this.IsLockedOut = isLockedOut;
            this.CreationDate = creationDate.ToUniversalTime();
            this.lastLoginDate = lastLoginDate.ToUniversalTime();
            this.lastActivityDate = lastActivityDate.ToUniversalTime();
            this.LastPasswordChangedDate = lastPasswordChangedDate.ToUniversalTime();
            this.LastLockoutDate = lastLockoutDate.ToUniversalTime();
        }

        /// <summary>Initializes a new instance of the <see cref="MembershipUser"/> class.</summary>
        protected MembershipUser()
        {
        }

        /// <summary>Gets the user name.</summary>
        public virtual string UserName { get; }

        /// <summary>Gets the unique identifier of the user in the data store.</summary>
        public virtual object ProviderUserKey { get; }

        /// <summary>Gets or sets the email address.</summary>
        public virtual string Email { get; set; }

        /// <summary>Gets the password question.</summary>
        public virtual string PasswordQuestion { get; private set; }

        /// <summary>Gets or sets the comment.</summary>
        public virtual string Comment
        {
            get => this.comment;
            set => this.comment = value;
        }

        /// <summary>Gets or sets a value indicating whether the user can be authenticated.</summary>
        public virtual bool IsApproved { get; set; }

        /// <summary>Gets a value indicating whether the user is locked out.</summary>
        public virtual bool IsLockedOut { get; private set; }

        /// <summary>Gets the date of the last lockout.</summary>
        public virtual DateTime LastLockoutDate { get; private set; }

        /// <summary>Gets the creation date.</summary>
        public virtual DateTime CreationDate { get; }

        /// <summary>Gets or sets the last login date.</summary>
        public virtual DateTime LastLoginDate
        {
            get => this.lastLoginDate.ToLocalTime();
            set => this.lastLoginDate = value.ToUniversalTime();
        }

        /// <summary>Gets or sets the last activity date.</summary>
        public virtual DateTime LastActivityDate
        {
            get => this.lastActivityDate.ToLocalTime();
            set => this.lastActivityDate = value.ToUniversalTime();
        }

        /// <summary>Gets the date of the last password change.</summary>
        public virtual DateTime LastPasswordChangedDate { get; private set; }

        /// <summary>Gets a value indicating whether the user is currently online.</summary>
        public virtual bool IsOnline
        {
            get
            {
                var onlineSpan = TimeSpan.FromMinutes(Membership.UserIsOnlineTimeWindow);
                return this.lastActivityDate > DateTime.UtcNow.Subtract(onlineSpan);
            }
        }

        /// <summary>Gets the name of the provider which stores the user.</summary>
        public virtual string ProviderName { get; }

        private MembershipProvider Provider => Membership.Providers[this.ProviderName] ?? throw new InvalidOperationException($"The membership provider '{this.ProviderName}' is not configured.");

        /// <inheritdoc />
        public override string ToString() => this.UserName;

        /// <summary>Gets the password of the user.</summary>
        /// <returns>The password.</returns>
        public virtual string GetPassword()
        {
            return this.Provider.GetPassword(this.UserName, null);
        }

        /// <summary>Gets the password of the user.</summary>
        /// <param name="passwordAnswer">The answer of the password question.</param>
        /// <returns>The password.</returns>
        public virtual string GetPassword(string passwordAnswer)
        {
            return this.Provider.GetPassword(this.UserName, passwordAnswer);
        }

        /// <summary>Changes the password of the user.</summary>
        /// <param name="oldPassword">The current password.</param>
        /// <param name="newPassword">The new password.</param>
        /// <returns>A value indicating whether the password was changed.</returns>
        public virtual bool ChangePassword(string oldPassword, string newPassword)
        {
            if (!this.Provider.ChangePassword(this.UserName, oldPassword, newPassword))
            {
                return false;
            }

            this.Refresh();
            return true;
        }

        /// <summary>Changes the password question and answer of the user.</summary>
        /// <param name="password">The password.</param>
        /// <param name="newPasswordQuestion">The new password question.</param>
        /// <param name="newPasswordAnswer">The new password answer.</param>
        /// <returns>A value indicating whether the question and answer were changed.</returns>
        public virtual bool ChangePasswordQuestionAndAnswer(string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            if (!this.Provider.ChangePasswordQuestionAndAnswer(this.UserName, password, newPasswordQuestion, newPasswordAnswer))
            {
                return false;
            }

            this.Refresh();
            return true;
        }

        /// <summary>Resets the password of the user to a new, automatically generated password.</summary>
        /// <returns>The new password.</returns>
        public virtual string ResetPassword()
        {
            return this.ResetPassword(null);
        }

        /// <summary>Resets the password of the user to a new, automatically generated password.</summary>
        /// <param name="passwordAnswer">The answer of the password question.</param>
        /// <returns>The new password.</returns>
        public virtual string ResetPassword(string passwordAnswer)
        {
            var password = this.Provider.ResetPassword(this.UserName, passwordAnswer);
            if (!string.IsNullOrEmpty(password))
            {
                this.Refresh();
            }

            return password;
        }

        /// <summary>Clears the locked-out state of the user.</summary>
        /// <returns>A value indicating whether the user was unlocked.</returns>
        public virtual bool UnlockUser()
        {
            if (!this.Provider.UnlockUser(this.UserName))
            {
                return false;
            }

            this.Refresh();
            return !this.IsLockedOut;
        }

        private void Refresh()
        {
            var user = this.Provider.GetUser(this.UserName, false);
            if (user == null)
            {
                return;
            }

            this.Email = user.Email;
            this.comment = user.Comment;
            this.IsApproved = user.IsApproved;
            this.IsLockedOut = user.IsLockedOut;
            this.LastLockoutDate = user.LastLockoutDate.ToUniversalTime();
            this.lastLoginDate = user.LastLoginDate.ToUniversalTime();
            this.lastActivityDate = user.LastActivityDate.ToUniversalTime();
            this.LastPasswordChangedDate = user.LastPasswordChangedDate.ToUniversalTime();
            this.PasswordQuestion = user.PasswordQuestion;
        }
    }
}
