// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Security
{
    using System.Collections.Specialized;
    using System.Configuration;
    using System.Configuration.Provider;
    using System.Data;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;

    using Microsoft.Data.SqlClient;

    /// <summary>
    /// Membership provider for the ASP.NET SQL Server membership schema (<c>aspnet_Membership</c>, <c>aspnet_Users</c> and the
    /// <c>aspnet_Membership_*</c> stored procedures), compatible with the data written by the System.Web <c>SqlMembershipProvider</c>.
    /// </summary>
    /// <remarks>
    /// Clear and hashed passwords are supported. Passwords stored with the <see cref="MembershipPasswordFormat.Encrypted"/>
    /// format were encrypted with the legacy machine key algorithms, which are not available on .NET 10.
    /// </remarks>
    public class SqlMembershipProvider : MembershipProvider
    {
        private const int SaltSize = 16;
        private const int PasswordSize = 14;
        private const int StatusUserNotFound = 1;
        private const int StatusLockedOut = 99;

        private string connectionString;
        private string applicationName;
        private bool enablePasswordRetrieval;
        private bool enablePasswordReset;
        private bool requiresQuestionAndAnswer;
        private bool requiresUniqueEmail;
        private int maxInvalidPasswordAttempts;
        private int passwordAttemptWindow;
        private int minRequiredPasswordLength;
        private int minRequiredNonAlphanumericCharacters;
        private string passwordStrengthRegularExpression;
        private MembershipPasswordFormat passwordFormat;
        private int commandTimeout;

        /// <inheritdoc />
        public override string ApplicationName
        {
            get => this.applicationName;
            set
            {
                if (string.IsNullOrEmpty(value) || value.Length > 256)
                {
                    throw new ProviderException("The application name must be between 1 and 256 characters.");
                }

                this.applicationName = value;
            }
        }

        /// <inheritdoc />
        public override bool EnablePasswordRetrieval => this.enablePasswordRetrieval;

        /// <inheritdoc />
        public override bool EnablePasswordReset => this.enablePasswordReset;

        /// <inheritdoc />
        public override bool RequiresQuestionAndAnswer => this.requiresQuestionAndAnswer;

        /// <inheritdoc />
        public override bool RequiresUniqueEmail => this.requiresUniqueEmail;

        /// <inheritdoc />
        public override int MaxInvalidPasswordAttempts => this.maxInvalidPasswordAttempts;

        /// <inheritdoc />
        public override int PasswordAttemptWindow => this.passwordAttemptWindow;

        /// <inheritdoc />
        public override MembershipPasswordFormat PasswordFormat => this.passwordFormat;

        /// <inheritdoc />
        public override int MinRequiredPasswordLength => this.minRequiredPasswordLength;

        /// <inheritdoc />
        public override int MinRequiredNonAlphanumericCharacters => this.minRequiredNonAlphanumericCharacters;

        /// <inheritdoc />
        public override string PasswordStrengthRegularExpression => this.passwordStrengthRegularExpression;

        /// <summary>Initializes the provider.</summary>
        /// <param name="name">The name of the provider.</param>
        /// <param name="config">
        /// The settings of the <c>add</c> element of the provider (<c>connectionString</c> or <c>connectionStringName</c>,
        /// <c>applicationName</c>, <c>passwordFormat</c>, ...).
        /// </param>
        public override void Initialize(string name, NameValueCollection config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (string.IsNullOrEmpty(name))
            {
                name = "SqlMembershipProvider";
            }

            if (string.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Stores and retrieves membership data from a Microsoft SQL Server database.");
            }

            base.Initialize(name, config);

            this.enablePasswordRetrieval = GetBoolean(config, "enablePasswordRetrieval", false);
            this.enablePasswordReset = GetBoolean(config, "enablePasswordReset", true);
            this.requiresQuestionAndAnswer = GetBoolean(config, "requiresQuestionAndAnswer", true);
            this.requiresUniqueEmail = GetBoolean(config, "requiresUniqueEmail", true);
            this.maxInvalidPasswordAttempts = GetInt32(config, "maxInvalidPasswordAttempts", 5);
            this.passwordAttemptWindow = GetInt32(config, "passwordAttemptWindow", 10);
            this.minRequiredPasswordLength = GetInt32(config, "minRequiredPasswordLength", 7);
            this.minRequiredNonAlphanumericCharacters = GetInt32(config, "minRequiredNonalphanumericCharacters", 1);
            this.passwordStrengthRegularExpression = config["passwordStrengthRegularExpression"]?.Trim() ?? string.Empty;
            this.commandTimeout = GetInt32(config, "commandTimeout", 30);
            this.ApplicationName = string.IsNullOrEmpty(config["applicationName"]) ? "/" : config["applicationName"];

            var format = config["passwordFormat"] ?? "Hashed";
            if (!Enum.TryParse(format, true, out this.passwordFormat))
            {
                throw new ProviderException($"The password format '{format}' is not supported.");
            }

            if (this.passwordFormat == MembershipPasswordFormat.Hashed && this.enablePasswordRetrieval)
            {
                throw new ProviderException("Hashed passwords cannot be retrieved, set enablePasswordRetrieval to false.");
            }

            this.connectionString = config["connectionString"];
            if (string.IsNullOrEmpty(this.connectionString))
            {
                var connectionStringName = config["connectionStringName"];
                this.connectionString = string.IsNullOrEmpty(connectionStringName) ? null : ConfigurationManager.ConnectionStrings[connectionStringName]?.ConnectionString;
            }

            if (string.IsNullOrEmpty(this.connectionString))
            {
                throw new ProviderException("The connection string of the membership provider is not configured.");
            }
        }

        /// <inheritdoc />
        public override MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, object providerUserKey, out MembershipCreateStatus status)
        {
            if (!this.IsValidPassword(password))
            {
                status = MembershipCreateStatus.InvalidPassword;
                return null;
            }

            var salt = GenerateSalt();
            var encodedPassword = this.EncodePassword(password, this.passwordFormat, salt);
            if (encodedPassword.Length > 128)
            {
                status = MembershipCreateStatus.InvalidPassword;
                return null;
            }

            string encodedAnswer = null;
            if (!string.IsNullOrEmpty(passwordAnswer))
            {
                encodedAnswer = this.EncodePassword(passwordAnswer.ToLowerInvariant(), this.passwordFormat, salt);
                if (encodedAnswer.Length > 128)
                {
                    status = MembershipCreateStatus.InvalidAnswer;
                    return null;
                }
            }
            else if (this.requiresQuestionAndAnswer)
            {
                status = MembershipCreateStatus.InvalidAnswer;
                return null;
            }

            if (this.requiresQuestionAndAnswer && string.IsNullOrEmpty(passwordQuestion))
            {
                status = MembershipCreateStatus.InvalidQuestion;
                return null;
            }

            if (this.requiresUniqueEmail && string.IsNullOrEmpty(email))
            {
                status = MembershipCreateStatus.InvalidEmail;
                return null;
            }

            if (providerUserKey != null && providerUserKey is not Guid)
            {
                status = MembershipCreateStatus.InvalidProviderUserKey;
                return null;
            }

            var now = RoundToSeconds(DateTime.UtcNow);
            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Membership_CreateUser");
            command.Parameters.AddWithValue("@ApplicationName", this.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@Password", encodedPassword);
            command.Parameters.AddWithValue("@PasswordSalt", salt);
            command.Parameters.AddWithValue("@Email", (object)email ?? DBNull.Value);
            command.Parameters.AddWithValue("@PasswordQuestion", (object)passwordQuestion ?? DBNull.Value);
            command.Parameters.AddWithValue("@PasswordAnswer", (object)encodedAnswer ?? DBNull.Value);
            command.Parameters.AddWithValue("@IsApproved", isApproved);
            command.Parameters.AddWithValue("@UniqueEmail", this.requiresUniqueEmail ? 1 : 0);
            command.Parameters.AddWithValue("@PasswordFormat", (int)this.passwordFormat);
            command.Parameters.AddWithValue("@CurrentTimeUtc", now);
            var userIdParameter = command.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier);
            userIdParameter.Direction = ParameterDirection.InputOutput;
            userIdParameter.Value = providerUserKey ?? DBNull.Value;
            var returnValue = AddReturnValue(command);
            command.ExecuteNonQuery();

            var result = returnValue.Value as int? ?? -1;
            if (result < 0 || result > (int)MembershipCreateStatus.ProviderError)
            {
                result = (int)MembershipCreateStatus.ProviderError;
            }

            status = (MembershipCreateStatus)result;
            if (status != MembershipCreateStatus.Success)
            {
                return null;
            }

            var userId = (Guid)userIdParameter.Value;
            var localNow = now.ToLocalTime();
            return new MembershipUser(this.Name, username, userId, email, passwordQuestion, null, isApproved, false, localNow, localNow, localNow, localNow, new DateTime(1754, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToLocalTime());
        }

        /// <inheritdoc />
        public override bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            if (!this.CheckPassword(username, password, false, false, out var salt, out var format))
            {
                return false;
            }

            string encodedAnswer = null;
            if (!string.IsNullOrEmpty(newPasswordAnswer))
            {
                encodedAnswer = this.EncodePassword(newPasswordAnswer.Trim().ToLowerInvariant(), format, salt);
            }

            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Membership_ChangePasswordQuestionAndAnswer");
            command.Parameters.AddWithValue("@ApplicationName", this.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@NewPasswordQuestion", (object)newPasswordQuestion ?? DBNull.Value);
            command.Parameters.AddWithValue("@NewPasswordAnswer", (object)encodedAnswer ?? DBNull.Value);
            var returnValue = AddReturnValue(command);
            command.ExecuteNonQuery();
            return (returnValue.Value as int? ?? -1) == 0;
        }

        /// <inheritdoc />
        public override string GetPassword(string username, string answer)
        {
            if (!this.enablePasswordRetrieval)
            {
                throw new NotSupportedException("The membership provider is not configured to support password retrieval.");
            }

            var salt = this.GetPasswordWithFormat(username, false, out _, out _).Salt;
            string encodedAnswer = null;
            if (this.requiresQuestionAndAnswer)
            {
                if (string.IsNullOrEmpty(answer))
                {
                    throw new ArgumentException("The password answer must not be empty.", nameof(answer));
                }

                encodedAnswer = this.EncodePassword(answer.Trim().ToLowerInvariant(), this.passwordFormat, salt);
            }

            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Membership_GetPassword");
            command.Parameters.AddWithValue("@ApplicationName", this.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@MaxInvalidPasswordAttempts", this.maxInvalidPasswordAttempts);
            command.Parameters.AddWithValue("@PasswordAttemptWindow", this.passwordAttemptWindow);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            if (encodedAnswer != null)
            {
                command.Parameters.AddWithValue("@PasswordAnswer", encodedAnswer);
            }

            var returnValue = AddReturnValue(command);
            string password = null;
            var format = MembershipPasswordFormat.Clear;
            using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
            {
                if (reader.Read())
                {
                    password = reader.GetString(0);
                    format = (MembershipPasswordFormat)reader.GetInt32(1);
                }
            }

            ThrowIfPasswordStatusFailed(returnValue.Value as int? ?? -1);
            return this.UnencodePassword(password, format);
        }

        /// <inheritdoc />
        public override bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            if (!this.CheckPassword(username, oldPassword, false, false, out var salt, out var format))
            {
                return false;
            }

            if (!this.IsValidPassword(newPassword))
            {
                throw new ArgumentException("The new password does not meet the password requirements.", nameof(newPassword));
            }

            var encodedPassword = this.EncodePassword(newPassword, format, salt);
            if (encodedPassword.Length > 128)
            {
                throw new ArgumentException("The new password is too long.", nameof(newPassword));
            }

            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Membership_SetPassword");
            command.Parameters.AddWithValue("@ApplicationName", this.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@NewPassword", encodedPassword);
            command.Parameters.AddWithValue("@PasswordSalt", salt);
            command.Parameters.AddWithValue("@PasswordFormat", (int)format);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            var returnValue = AddReturnValue(command);
            command.ExecuteNonQuery();
            ThrowIfPasswordStatusFailed(returnValue.Value as int? ?? -1);
            return true;
        }

        /// <inheritdoc />
        public override string ResetPassword(string username, string answer)
        {
            if (!this.enablePasswordReset)
            {
                throw new NotSupportedException("The membership provider is not configured to support password resets.");
            }

            var stored = this.GetPasswordWithFormat(username, false, out var status, out _);
            ThrowIfPasswordStatusFailed(status);
            var salt = stored.Salt;
            var format = stored.Format;

            string encodedAnswer = null;
            if (this.requiresQuestionAndAnswer)
            {
                if (string.IsNullOrEmpty(answer))
                {
                    throw new ArgumentException("The password answer must not be empty.", nameof(answer));
                }

                encodedAnswer = this.EncodePassword(answer.Trim().ToLowerInvariant(), format, salt);
            }

            var newPassword = Membership.GeneratePassword(Math.Max(this.minRequiredPasswordLength, PasswordSize), this.minRequiredNonAlphanumericCharacters);

            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Membership_ResetPassword");
            command.Parameters.AddWithValue("@ApplicationName", this.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@NewPassword", this.EncodePassword(newPassword, format, salt));
            command.Parameters.AddWithValue("@MaxInvalidPasswordAttempts", this.maxInvalidPasswordAttempts);
            command.Parameters.AddWithValue("@PasswordAttemptWindow", this.passwordAttemptWindow);
            command.Parameters.AddWithValue("@PasswordSalt", salt);
            command.Parameters.AddWithValue("@PasswordFormat", (int)format);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            if (encodedAnswer != null)
            {
                command.Parameters.AddWithValue("@PasswordAnswer", encodedAnswer);
            }

            var returnValue = AddReturnValue(command);
            command.ExecuteNonQuery();
            ThrowIfPasswordStatusFailed(returnValue.Value as int? ?? -1);
            return newPassword;
        }

        /// <inheritdoc />
        public override void UpdateUser(MembershipUser user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Membership_UpdateUser");
            command.Parameters.AddWithValue("@ApplicationName", this.ApplicationName);
            command.Parameters.AddWithValue("@UserName", user.UserName);
            command.Parameters.AddWithValue("@Email", (object)user.Email ?? DBNull.Value);
            command.Parameters.AddWithValue("@Comment", (object)user.Comment ?? DBNull.Value);
            command.Parameters.AddWithValue("@IsApproved", user.IsApproved ? 1 : 0);
            command.Parameters.AddWithValue("@LastLoginDate", user.LastLoginDate.ToUniversalTime());
            command.Parameters.AddWithValue("@LastActivityDate", user.LastActivityDate.ToUniversalTime());
            command.Parameters.AddWithValue("@UniqueEmail", this.requiresUniqueEmail ? 1 : 0);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            var returnValue = AddReturnValue(command);
            command.ExecuteNonQuery();
            var status = returnValue.Value as int? ?? -1;
            if (status != 0)
            {
                throw new ProviderException(status == (int)MembershipCreateStatus.DuplicateEmail ? "The email address is already in use." : "The user could not be updated.");
            }
        }

        /// <inheritdoc />
        public override bool ValidateUser(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return false;
            }

            return this.CheckPassword(username, password, true, true, out _, out _);
        }

        /// <inheritdoc />
        public override bool UnlockUser(string userName)
        {
            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Membership_UnlockUser");
            command.Parameters.AddWithValue("@ApplicationName", this.ApplicationName);
            command.Parameters.AddWithValue("@UserName", userName);
            var returnValue = AddReturnValue(command);
            command.ExecuteNonQuery();
            return (returnValue.Value as int? ?? -1) == 0;
        }

        /// <inheritdoc />
        public override MembershipUser GetUser(object providerUserKey, bool userIsOnline)
        {
            if (providerUserKey is not Guid userId)
            {
                throw new ArgumentException("The provider user key must be a Guid.", nameof(providerUserKey));
            }

            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Membership_GetUserByUserId");
            command.Parameters.AddWithValue("@UserId", userId);
            command.Parameters.AddWithValue("@UpdateLastActivity", userIsOnline);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            using var reader = command.ExecuteReader(CommandBehavior.SingleRow);
            if (!reader.Read())
            {
                return null;
            }

            // Email, PasswordQuestion, Comment, IsApproved, CreateDate, LastLoginDate, LastActivityDate, LastPasswordChangedDate, UserName, IsLockedOut, LastLockoutDate
            return new MembershipUser(
                this.Name,
                GetNullableString(reader, 8),
                userId,
                GetNullableString(reader, 0),
                GetNullableString(reader, 1),
                GetNullableString(reader, 2),
                reader.GetBoolean(3),
                reader.GetBoolean(9),
                ToLocal(reader.GetDateTime(4)),
                ToLocal(reader.GetDateTime(5)),
                ToLocal(reader.GetDateTime(6)),
                ToLocal(reader.GetDateTime(7)),
                ToLocal(reader.GetDateTime(10)));
        }

        /// <inheritdoc />
        public override MembershipUser GetUser(string username, bool userIsOnline)
        {
            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Membership_GetUserByName");
            command.Parameters.AddWithValue("@ApplicationName", this.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@UpdateLastActivity", userIsOnline);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            using var reader = command.ExecuteReader(CommandBehavior.SingleRow);
            if (!reader.Read())
            {
                return null;
            }

            // Email, PasswordQuestion, Comment, IsApproved, CreateDate, LastLoginDate, LastActivityDate, LastPasswordChangedDate, UserId, IsLockedOut, LastLockoutDate
            return new MembershipUser(
                this.Name,
                username,
                reader.GetGuid(8),
                GetNullableString(reader, 0),
                GetNullableString(reader, 1),
                GetNullableString(reader, 2),
                reader.GetBoolean(3),
                reader.GetBoolean(9),
                ToLocal(reader.GetDateTime(4)),
                ToLocal(reader.GetDateTime(5)),
                ToLocal(reader.GetDateTime(6)),
                ToLocal(reader.GetDateTime(7)),
                ToLocal(reader.GetDateTime(10)));
        }

        /// <inheritdoc />
        public override string GetUserNameByEmail(string email)
        {
            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Membership_GetUserByEmail");
            command.Parameters.AddWithValue("@ApplicationName", this.ApplicationName);
            command.Parameters.AddWithValue("@Email", (object)email ?? DBNull.Value);
            using var reader = command.ExecuteReader();
            string username = null;
            if (reader.Read())
            {
                username = GetNullableString(reader, 0);
                if (this.requiresUniqueEmail && reader.Read())
                {
                    throw new ProviderException("More than one user has the specified email address.");
                }
            }

            return username;
        }

        /// <inheritdoc />
        public override bool DeleteUser(string username, bool deleteAllRelatedData)
        {
            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Users_DeleteUser");
            command.Parameters.AddWithValue("@ApplicationName", this.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);

            // 1 = membership, 15 = membership, roles, profile and personalization
            command.Parameters.AddWithValue("@TablesToDeleteFrom", deleteAllRelatedData ? 15 : 1);
            var deleted = command.Parameters.Add("@NumTablesDeletedFrom", SqlDbType.Int);
            deleted.Direction = ParameterDirection.Output;
            command.ExecuteNonQuery();
            return (deleted.Value as int? ?? 0) > 0;
        }

        /// <inheritdoc />
        public override int GetNumberOfUsersOnline()
        {
            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Membership_GetNumberOfUsersOnline");
            command.Parameters.AddWithValue("@ApplicationName", this.ApplicationName);
            command.Parameters.AddWithValue("@MinutesSinceLastInActive", Membership.UserIsOnlineTimeWindow);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            var returnValue = AddReturnValue(command);
            command.ExecuteNonQuery();
            return returnValue.Value as int? ?? -1;
        }

        /// <summary>Encodes a password (or password answer) the same way as the System.Web <c>SqlMembershipProvider</c>.</summary>
        /// <param name="password">The password.</param>
        /// <param name="format">The password format.</param>
        /// <param name="salt">The base64 encoded salt.</param>
        /// <returns>The encoded password.</returns>
        protected virtual string EncodePassword(string password, MembershipPasswordFormat format, string salt)
        {
            if (password == null)
            {
                return null;
            }

            switch (format)
            {
                case MembershipPasswordFormat.Clear:
                    return password;
                case MembershipPasswordFormat.Hashed:
                    return HashPassword(password, salt);
                default:
                    throw new ProviderException("Encrypted passwords use the .NET Framework machine key encryption, which is not available on .NET. Convert the passwords to the hashed format.");
            }
        }

        /// <summary>Decodes a stored password.</summary>
        /// <param name="password">The stored password.</param>
        /// <param name="format">The password format.</param>
        /// <returns>The password.</returns>
        protected virtual string UnencodePassword(string password, MembershipPasswordFormat format)
        {
            switch (format)
            {
                case MembershipPasswordFormat.Clear:
                    return password;
                case MembershipPasswordFormat.Hashed:
                    throw new ProviderException("Hashed passwords cannot be decoded.");
                default:
                    throw new ProviderException("Encrypted passwords use the .NET Framework machine key encryption, which is not available on .NET. Convert the passwords to the hashed format.");
            }
        }

        private static string GenerateSalt()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltSize));
        }

        private static string HashPassword(string password, string salt)
        {
            var passwordBytes = Encoding.Unicode.GetBytes(password);
            var saltBytes = Convert.FromBase64String(salt);
            using var algorithm = CreateHashAlgorithm(Membership.HashAlgorithmType);
            byte[] hash;
            if (algorithm is KeyedHashAlgorithm keyed)
            {
                // the salt is used as (repeated / truncated) key of keyed hash algorithms
                var key = new byte[keyed.Key.Length];
                for (var offset = 0; offset < key.Length;)
                {
                    var length = Math.Min(saltBytes.Length, key.Length - offset);
                    Buffer.BlockCopy(saltBytes, 0, key, offset, length);
                    offset += length;
                }

                keyed.Key = key;
                hash = keyed.ComputeHash(passwordBytes);
            }
            else
            {
                var all = new byte[saltBytes.Length + passwordBytes.Length];
                Buffer.BlockCopy(saltBytes, 0, all, 0, saltBytes.Length);
                Buffer.BlockCopy(passwordBytes, 0, all, saltBytes.Length, passwordBytes.Length);
                hash = algorithm.ComputeHash(all);
            }

            return Convert.ToBase64String(hash);
        }

        [Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "SHA1 hashes are only computed to verify passwords stored by the .NET Framework provider.")]
        [Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Primitives", Justification = "MD5 hashes are only computed to verify passwords stored by the .NET Framework provider.")]
        private static HashAlgorithm CreateHashAlgorithm(string name)
        {
            switch (name?.ToUpperInvariant())
            {
                case "SHA1":
                    return SHA1.Create();
                case "MD5":
                    return MD5.Create();
                case "SHA384":
                    return SHA384.Create();
                case "SHA512":
                    return SHA512.Create();
                case "HMACSHA1":
                    return new HMACSHA1();
                case "HMACSHA256":
                    return new HMACSHA256();
                case "HMACSHA384":
                    return new HMACSHA384();
                case "HMACSHA512":
                    return new HMACSHA512();
                case "SHA256":
                case null:
                case "":
                    return SHA256.Create();
                default:
                    throw new ProviderException($"The hash algorithm '{name}' is not supported.");
            }
        }

        private static bool GetBoolean(NameValueCollection config, string name, bool defaultValue)
        {
            var value = config[name];
            return string.IsNullOrEmpty(value) ? defaultValue : bool.Parse(value);
        }

        private static int GetInt32(NameValueCollection config, string name, int defaultValue)
        {
            var value = config[name];
            return string.IsNullOrEmpty(value) ? defaultValue : int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static string GetNullableString(SqlDataReader reader, int index)
        {
            return reader.IsDBNull(index) ? null : reader.GetString(index);
        }

        private static DateTime ToLocal(DateTime utc)
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        }

        private static DateTime RoundToSeconds(DateTime utcDateTime)
        {
            return new DateTime(utcDateTime.Year, utcDateTime.Month, utcDateTime.Day, utcDateTime.Hour, utcDateTime.Minute, utcDateTime.Second, DateTimeKind.Utc);
        }

        private static SqlParameter AddReturnValue(SqlCommand command)
        {
            var parameter = command.Parameters.Add("@ReturnValue", SqlDbType.Int);
            parameter.Direction = ParameterDirection.ReturnValue;
            return parameter;
        }

        private static void ThrowIfPasswordStatusFailed(int status)
        {
            switch (status)
            {
                case 0:
                    return;
                case StatusUserNotFound:
                    throw new ProviderException("The user was not found.");
                case StatusLockedOut:
                    throw new MembershipPasswordException("The user is locked out.");
                case 3:
                    throw new MembershipPasswordException("The password answer is wrong.");
                default:
                    throw new ProviderException($"The membership operation failed with status {status.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        private SqlConnection OpenConnection()
        {
            var connection = new SqlConnection(this.connectionString);
            connection.Open();
            return connection;
        }

        private SqlCommand CreateCommand(SqlConnection connection, string storedProcedure)
        {
            return new SqlCommand(storedProcedure, connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = this.commandTimeout, };
        }

        private bool IsValidPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < this.minRequiredPasswordLength)
            {
                return false;
            }

            if (password.Count(c => !char.IsLetterOrDigit(c)) < this.minRequiredNonAlphanumericCharacters)
            {
                return false;
            }

            return string.IsNullOrEmpty(this.passwordStrengthRegularExpression) || Regex.IsMatch(password, this.passwordStrengthRegularExpression, RegexOptions.None, TimeSpan.FromSeconds(2));
        }

        private (string Password, MembershipPasswordFormat Format, string Salt, bool IsApproved) GetPasswordWithFormat(string username, bool updateLastLoginActivityDate, out int status, out (int FailedPasswordAttemptCount, DateTime LastLoginDate, DateTime LastActivityDate) activity)
        {
            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Membership_GetPasswordWithFormat");
            command.Parameters.AddWithValue("@ApplicationName", this.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@UpdateLastLoginActivityDate", updateLastLoginActivityDate);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            var returnValue = AddReturnValue(command);
            var result = (Password: (string)null, Format: MembershipPasswordFormat.Clear, Salt: (string)null, IsApproved: false);
            activity = default;
            using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
            {
                // Password, PasswordFormat, PasswordSalt, FailedPasswordAttemptCount, FailedPasswordAnswerAttemptCount, IsApproved, LastLoginDate, LastActivityDate
                if (reader.Read())
                {
                    result = (reader.GetString(0), (MembershipPasswordFormat)reader.GetInt32(1), reader.GetString(2), reader.GetBoolean(5));
                    activity = (reader.GetInt32(3), reader.GetDateTime(6), reader.GetDateTime(7));
                }
            }

            status = returnValue.Value as int? ?? StatusUserNotFound;
            if (result.Password == null && status == 0)
            {
                status = StatusUserNotFound;
            }

            return result;
        }

        private bool CheckPassword(string username, string password, bool updateLastLoginActivityDate, bool failIfNotApproved, out string salt, out MembershipPasswordFormat format)
        {
            var stored = this.GetPasswordWithFormat(username, updateLastLoginActivityDate, out var status, out var activity);
            salt = stored.Salt;
            format = stored.Format;
            if (status != 0 || (!stored.IsApproved && failIfNotApproved))
            {
                return false;
            }

            var encodedPassword = this.EncodePassword(password, stored.Format, stored.Salt);
            var isPasswordCorrect = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored.Password), Encoding.UTF8.GetBytes(encodedPassword));
            if (isPasswordCorrect && activity.FailedPasswordAttemptCount == 0 && !updateLastLoginActivityDate)
            {
                return true;
            }

            var now = DateTime.UtcNow;
            using var connection = this.OpenConnection();
            using var command = this.CreateCommand(connection, "dbo.aspnet_Membership_UpdateUserInfo");
            command.Parameters.AddWithValue("@ApplicationName", this.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@IsPasswordCorrect", isPasswordCorrect);
            command.Parameters.AddWithValue("@UpdateLastLoginActivityDate", updateLastLoginActivityDate);
            command.Parameters.AddWithValue("@MaxInvalidPasswordAttempts", this.maxInvalidPasswordAttempts);
            command.Parameters.AddWithValue("@PasswordAttemptWindow", this.passwordAttemptWindow);
            command.Parameters.AddWithValue("@CurrentTimeUtc", now);
            command.Parameters.AddWithValue("@LastLoginDate", isPasswordCorrect ? now : activity.LastLoginDate);
            command.Parameters.AddWithValue("@LastActivityDate", isPasswordCorrect ? now : activity.LastActivityDate);
            command.ExecuteNonQuery();
            return isPasswordCorrect;
        }
    }
}
