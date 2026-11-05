// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Security.Membership
{
    using System;
    using System.Data;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Options;
    using DotNetNuke.Framework;

    using Microsoft.Data.SqlClient;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// The platform's own implementation of the ASP.NET membership store, replacing
    /// <c>System.Web.Security.Membership</c> and <c>SqlMembershipProvider</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class deliberately calls the stock <c>aspnet_Membership_*</c> and
    /// <c>aspnet_Users_*</c> stored procedures that <c>aspnet_regsql</c> installed into the DNN
    /// database, and reproduces the legacy password encoding byte-for-byte
    /// (<see cref="EncodePassword"/>). That is what keeps every already-stored password hash
    /// verifiable: the lock-out windows, failure counters and date stamping stay in the
    /// database where they always were, and only the parts that used to live in
    /// <c>System.Web</c> (password encoding, salt generation, password generation) are
    /// reimplemented here.
    /// </para>
    /// <para>
    /// Settings come from <see cref="MembershipOptions"/> (the <c>Membership</c> configuration
    /// section) instead of <c>&lt;system.web&gt;&lt;membership&gt;</c>.
    /// </para>
    /// </remarks>
    public class AspNetMembershipStore : IAspNetMembershipStore
    {
        private const string Punctuations = "!@#$%^&*()_-+=[{]};:>|./?";
        private const int SaltSizeInBytes = 16;

        private readonly IOptionsMonitor<MembershipOptions> options;
        private readonly IOptionsMonitor<MachineKeyOptions> machineKey;

        /// <summary>Initializes a new instance of the <see cref="AspNetMembershipStore"/> class.</summary>
        /// <param name="options">The membership settings.</param>
        /// <param name="machineKey">The application key material, used only for the (non-default) <c>Encrypted</c> password format.</param>
        public AspNetMembershipStore(IOptionsMonitor<MembershipOptions> options, IOptionsMonitor<MachineKeyOptions> machineKey)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.machineKey = machineKey ?? throw new ArgumentNullException(nameof(machineKey));
        }

        /// <summary>
        /// Gets the store registered with the application's dependency-injection container.
        /// </summary>
        /// <remarks>
        /// Provided for the platform's many static membership call sites (and for
        /// <see cref="MembershipUser"/> instances that came back from the cache, which cannot
        /// carry a service reference across serialization). Prefer injecting
        /// <see cref="IAspNetMembershipStore"/>.
        /// </remarks>
        public static IAspNetMembershipStore Current =>
            ServiceLocator.GetRequiredService<IAspNetMembershipStore>();

        /// <inheritdoc />
        public MembershipOptions Options => this.options.CurrentValue;

        /// <summary>Gets the connection string of the membership database.</summary>
        private string ConnectionString
        {
            get
            {
                var name = this.Options.ConnectionStringName;
                var connectionString = string.IsNullOrEmpty(name)
                    ? null
                    : ConfigurationSource.GetConnectionString(name);

                return string.IsNullOrEmpty(connectionString)
                    ? Data.DataProvider.Instance().ConnectionString
                    : connectionString;
            }
        }

        /// <inheritdoc />
        public MembershipUser GetUser(string username, bool updateLastActivity)
        {
            if (string.IsNullOrEmpty(username))
            {
                return null;
            }

            using var command = this.CreateCommand("dbo.aspnet_Membership_GetUserByName", out var connection);
            command.Parameters.AddWithValue("@ApplicationName", this.Options.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            command.Parameters.AddWithValue("@UpdateLastActivity", updateLastActivity);
            var returnValue = AddReturnValue(command);

            using (connection)
            {
                connection.Open();
                using var reader = command.ExecuteReader(CommandBehavior.SingleRow);
                if (!reader.Read())
                {
                    return null;
                }

                var user = new MembershipUser(this)
                {
                    UserName = username,
                    Email = GetString(reader, 0),
                    PasswordQuestion = GetString(reader, 1),
                    Comment = GetString(reader, 2),
                    IsApproved = reader.GetBoolean(3),
                    CreationDate = ToLocal(reader.GetDateTime(4)),
                    LastLoginDate = ToLocal(reader.GetDateTime(5)),
                    LastActivityDate = ToLocal(reader.GetDateTime(6)),
                    LastPasswordChangedDate = ToLocal(reader.GetDateTime(7)),
                    ProviderUserKey = reader.GetGuid(8),
                    IsLockedOut = reader.GetBoolean(9),
                    LastLockoutDate = ToLocal(reader.GetDateTime(10)),
                };

                reader.Close();
                _ = returnValue;
                return user;
            }
        }

        /// <inheritdoc />
        public MembershipUser GetUser(Guid providerUserKey)
        {
            using var command = this.CreateCommand("dbo.aspnet_Membership_GetUserByUserId", out var connection);
            command.Parameters.AddWithValue("@UserId", providerUserKey);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            command.Parameters.AddWithValue("@UpdateLastActivity", false);
            AddReturnValue(command);

            using (connection)
            {
                connection.Open();
                using var reader = command.ExecuteReader(CommandBehavior.SingleRow);
                if (!reader.Read())
                {
                    return null;
                }

                return new MembershipUser(this)
                {
                    Email = GetString(reader, 0),
                    PasswordQuestion = GetString(reader, 1),
                    Comment = GetString(reader, 2),
                    IsApproved = reader.GetBoolean(3),
                    CreationDate = ToLocal(reader.GetDateTime(4)),
                    LastLoginDate = ToLocal(reader.GetDateTime(5)),
                    LastActivityDate = ToLocal(reader.GetDateTime(6)),
                    LastPasswordChangedDate = ToLocal(reader.GetDateTime(7)),
                    UserName = GetString(reader, 8),
                    IsLockedOut = reader.GetBoolean(9),
                    LastLockoutDate = ToLocal(reader.GetDateTime(10)),
                    ProviderUserKey = providerUserKey,
                };
            }
        }

        /// <inheritdoc />
        public MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, out MembershipCreateStatus status)
        {
            var settings = this.Options;

            if (!this.IsValidPassword(password))
            {
                status = MembershipCreateStatus.InvalidPassword;
                return null;
            }

            if (string.IsNullOrEmpty(username) || username.Contains(',', StringComparison.Ordinal))
            {
                status = MembershipCreateStatus.InvalidUserName;
                return null;
            }

            if (settings.RequiresQuestionAndAnswer && string.IsNullOrEmpty(passwordQuestion))
            {
                status = MembershipCreateStatus.InvalidQuestion;
                return null;
            }

            if (settings.RequiresQuestionAndAnswer && string.IsNullOrEmpty(passwordAnswer))
            {
                status = MembershipCreateStatus.InvalidAnswer;
                return null;
            }

            var salt = GenerateSalt();
            var encodedPassword = this.EncodePassword(password, settings.PasswordFormat, salt);
            var encodedAnswer = string.IsNullOrEmpty(passwordAnswer)
                ? null
                : this.EncodePassword(passwordAnswer.ToLower(CultureInfo.InvariantCulture), settings.PasswordFormat, salt);

            var createDate = DateTime.UtcNow;
            using var command = this.CreateCommand("dbo.aspnet_Membership_CreateUser", out var connection);
            command.Parameters.AddWithValue("@ApplicationName", settings.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@Password", encodedPassword);
            command.Parameters.AddWithValue("@PasswordSalt", salt);
            command.Parameters.AddWithValue("@Email", (object)email ?? DBNull.Value);
            command.Parameters.AddWithValue("@PasswordQuestion", (object)passwordQuestion ?? DBNull.Value);
            command.Parameters.AddWithValue("@PasswordAnswer", (object)encodedAnswer ?? DBNull.Value);
            command.Parameters.AddWithValue("@IsApproved", isApproved);
            command.Parameters.AddWithValue("@CurrentTimeUtc", createDate);
            command.Parameters.AddWithValue("@CreateDate", createDate);
            command.Parameters.AddWithValue("@UniqueEmail", settings.RequiresUniqueEmail ? 1 : 0);
            command.Parameters.AddWithValue("@PasswordFormat", settings.PasswordFormat);

            var userId = new SqlParameter("@UserId", SqlDbType.UniqueIdentifier) { Direction = ParameterDirection.InputOutput, Value = DBNull.Value };
            command.Parameters.Add(userId);
            var returnValue = AddReturnValue(command);

            using (connection)
            {
                connection.Open();
                command.ExecuteNonQuery();

                var statusCode = returnValue.Value is int code ? code : -1;
                switch (statusCode)
                {
                    case 0:
                        status = MembershipCreateStatus.Success;
                        break;
                    case 2:
                        status = MembershipCreateStatus.DuplicateUserName;
                        return null;
                    case 6:
                        status = MembershipCreateStatus.DuplicateEmail;
                        return null;
                    case 10:
                        status = MembershipCreateStatus.DuplicateProviderUserKey;
                        return null;
                    case 11:
                        status = MembershipCreateStatus.InvalidProviderUserKey;
                        return null;
                    default:
                        // REVIEW: the rarely-returned codes of aspnet_Membership_CreateUser all
                        // mean "the store refused the user", which the legacy provider also
                        // surfaced as ProviderError. Verify against your database if you rely
                        // on a more specific status.
                        status = MembershipCreateStatus.ProviderError;
                        return null;
                }

                return new MembershipUser(this)
                {
                    UserName = username,
                    Email = email,
                    PasswordQuestion = passwordQuestion,
                    IsApproved = isApproved,
                    ProviderUserKey = userId.Value is Guid id ? id : (object)null,
                    CreationDate = ToLocal(createDate),
                    LastLoginDate = ToLocal(createDate),
                    LastActivityDate = ToLocal(createDate),
                    LastPasswordChangedDate = ToLocal(createDate),
                    LastLockoutDate = ToLocal(createDate),
                };
            }
        }

        /// <inheritdoc />
        public bool DeleteUser(string username, bool deleteAllRelatedData)
        {
            using var command = this.CreateCommand("dbo.aspnet_Users_DeleteUser", out var connection);
            command.Parameters.AddWithValue("@ApplicationName", this.Options.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);

            // The legacy provider passed 0xF (membership, profile, roles, personalization) when
            // asked to delete all related data, and 1 (membership only) otherwise.
            command.Parameters.AddWithValue("@TablesToDeleteFrom", deleteAllRelatedData ? 0xF : 1);
            var deletedFrom = new SqlParameter("@NumTablesDeletedFrom", SqlDbType.Int) { Direction = ParameterDirection.Output };
            command.Parameters.Add(deletedFrom);
            AddReturnValue(command);

            using (connection)
            {
                connection.Open();
                command.ExecuteNonQuery();
                return deletedFrom.Value is int count && count > 0;
            }
        }

        /// <inheritdoc />
        public void UpdateUser(MembershipUser user)
        {
            if (user is null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            using var command = this.CreateCommand("dbo.aspnet_Membership_UpdateUser", out var connection);
            command.Parameters.AddWithValue("@ApplicationName", this.Options.ApplicationName);
            command.Parameters.AddWithValue("@UserName", user.UserName);
            command.Parameters.AddWithValue("@Email", (object)user.Email ?? DBNull.Value);
            command.Parameters.AddWithValue("@Comment", (object)user.Comment ?? DBNull.Value);
            command.Parameters.AddWithValue("@IsApproved", user.IsApproved);
            command.Parameters.AddWithValue("@LastLoginDate", user.LastLoginDate.ToUniversalTime());
            command.Parameters.AddWithValue("@LastActivityDate", user.LastActivityDate.ToUniversalTime());
            command.Parameters.AddWithValue("@UniqueEmail", this.Options.RequiresUniqueEmail ? 1 : 0);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            var returnValue = AddReturnValue(command);

            using (connection)
            {
                connection.Open();
                command.ExecuteNonQuery();
                var statusCode = returnValue.Value is int code ? code : -1;
                switch (statusCode)
                {
                    case 0:
                        return;
                    case 1:
                        throw new MembershipStoreException($"The membership store has no user named '{user.UserName}'.");
                    case 7:
                        throw new MembershipStoreException("The e-mail address is already in use by another user.");
                    default:
                        throw new MembershipStoreException($"Updating the membership record for '{user.UserName}' failed with status {statusCode}.");
                }
            }
        }

        /// <inheritdoc />
        public bool ValidateUser(string username, string password)
        {
            return this.CheckPassword(username, password, updateLastLoginActivityDate: true, failIfNotApproved: true);
        }

        /// <inheritdoc />
        public bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            if (!this.CheckPassword(username, oldPassword, updateLastLoginActivityDate: false, failIfNotApproved: false))
            {
                return false;
            }

            if (!this.IsValidPassword(newPassword))
            {
                throw new ArgumentException("The new password does not meet the configured password requirements.", nameof(newPassword));
            }

            var salt = GenerateSalt();
            var encoded = this.EncodePassword(newPassword, this.Options.PasswordFormat, salt);

            using var command = this.CreateCommand("dbo.aspnet_Membership_SetPassword", out var connection);
            command.Parameters.AddWithValue("@ApplicationName", this.Options.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@NewPassword", encoded);
            command.Parameters.AddWithValue("@PasswordSalt", salt);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            command.Parameters.AddWithValue("@PasswordFormat", this.Options.PasswordFormat);
            var returnValue = AddReturnValue(command);

            using (connection)
            {
                connection.Open();
                command.ExecuteNonQuery();
                return returnValue.Value is int code && code == 0;
            }
        }

        /// <inheritdoc />
        public bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            if (!this.CheckPassword(username, password, updateLastLoginActivityDate: false, failIfNotApproved: false))
            {
                return false;
            }

            // The answer is stored with the user's current salt, exactly as the legacy provider did.
            var format = this.GetPasswordFormat(username, out var salt);
            var encodedAnswer = string.IsNullOrEmpty(newPasswordAnswer)
                ? null
                : this.EncodePassword(newPasswordAnswer.ToLower(CultureInfo.InvariantCulture), format, salt);

            using var command = this.CreateCommand("dbo.aspnet_Membership_ChangePasswordQuestionAndAnswer", out var connection);
            command.Parameters.AddWithValue("@ApplicationName", this.Options.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@NewPasswordQuestion", (object)newPasswordQuestion ?? DBNull.Value);
            command.Parameters.AddWithValue("@NewPasswordAnswer", (object)encodedAnswer ?? DBNull.Value);
            var returnValue = AddReturnValue(command);

            using (connection)
            {
                connection.Open();
                command.ExecuteNonQuery();
                return returnValue.Value is int code && code == 0;
            }
        }

        /// <inheritdoc />
        public string GetPassword(string username, string passwordAnswer)
        {
            var settings = this.Options;
            if (!settings.EnablePasswordRetrieval)
            {
                throw new NotSupportedException("Password retrieval is not enabled for this installation (Membership:EnablePasswordRetrieval).");
            }

            string encodedAnswer = null;
            if (!string.IsNullOrEmpty(passwordAnswer))
            {
                var answerFormat = this.GetPasswordFormat(username, out var answerSalt);
                encodedAnswer = this.EncodePassword(passwordAnswer.ToLower(CultureInfo.InvariantCulture), answerFormat, answerSalt);
            }

            using var command = this.CreateCommand("dbo.aspnet_Membership_GetPassword", out var connection);
            command.Parameters.AddWithValue("@ApplicationName", settings.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@MaxInvalidPasswordAttempts", settings.MaxInvalidPasswordAttempts);
            command.Parameters.AddWithValue("@PasswordAttemptWindow", settings.PasswordAttemptWindow);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            command.Parameters.AddWithValue("@PasswordAnswer", (object)encodedAnswer ?? DBNull.Value);
            var returnValue = AddReturnValue(command);

            using (connection)
            {
                connection.Open();
                string storedPassword = null;
                var passwordFormat = settings.PasswordFormat;
                using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (reader.Read())
                    {
                        storedPassword = GetString(reader, 0);
                        passwordFormat = reader.GetInt32(1);
                    }
                }

                var statusCode = returnValue.Value is int code ? code : -1;
                switch (statusCode)
                {
                    case 0:
                        break;
                    case 1:
                        throw new MembershipStoreException($"The membership store has no user named '{username}'.");
                    case 3:
                        throw new MembershipStoreException("The supplied password answer is incorrect.");
                    case 99:
                        throw new MembershipStoreException($"The account '{username}' is locked out.");
                    default:
                        throw new MembershipStoreException($"Retrieving the password for '{username}' failed with status {statusCode}.");
                }

                return this.UnEncodePassword(storedPassword, passwordFormat);
            }
        }

        /// <inheritdoc />
        public string ResetPassword(string username, string passwordAnswer)
        {
            var settings = this.Options;
            if (!settings.EnablePasswordReset)
            {
                throw new NotSupportedException("Password reset is not enabled for this installation (Membership:EnablePasswordReset).");
            }

            var newPassword = this.GeneratePassword(
                Math.Max(settings.MinRequiredPasswordLength, 14),
                settings.MinRequiredNonAlphanumericCharacters);

            var newSalt = GenerateSalt();
            var encodedPassword = this.EncodePassword(newPassword, settings.PasswordFormat, newSalt);
            string encodedAnswer = null;
            if (!string.IsNullOrEmpty(passwordAnswer))
            {
                var answerFormat = this.GetPasswordFormat(username, out var answerSalt);
                encodedAnswer = this.EncodePassword(passwordAnswer.ToLower(CultureInfo.InvariantCulture), answerFormat, answerSalt);
            }

            using var command = this.CreateCommand("dbo.aspnet_Membership_ResetPassword", out var connection);
            command.Parameters.AddWithValue("@ApplicationName", settings.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@NewPassword", encodedPassword);
            command.Parameters.AddWithValue("@MaxInvalidPasswordAttempts", settings.MaxInvalidPasswordAttempts);
            command.Parameters.AddWithValue("@PasswordAttemptWindow", settings.PasswordAttemptWindow);
            command.Parameters.AddWithValue("@PasswordSalt", newSalt);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            command.Parameters.AddWithValue("@PasswordFormat", settings.PasswordFormat);
            command.Parameters.AddWithValue("@PasswordAnswer", (object)encodedAnswer ?? DBNull.Value);
            var returnValue = AddReturnValue(command);

            using (connection)
            {
                connection.Open();
                command.ExecuteNonQuery();
                var statusCode = returnValue.Value is int code ? code : -1;
                switch (statusCode)
                {
                    case 0:
                        return newPassword;
                    case 1:
                        throw new MembershipStoreException($"The membership store has no user named '{username}'.");
                    case 3:
                        throw new MembershipStoreException("The supplied password answer is incorrect.");
                    case 99:
                        throw new MembershipStoreException($"The account '{username}' is locked out.");
                    default:
                        throw new MembershipStoreException($"Resetting the password for '{username}' failed with status {statusCode}.");
                }
            }
        }

        /// <inheritdoc />
        public bool UnlockUser(string username)
        {
            using var command = this.CreateCommand("dbo.aspnet_Membership_UnlockUser", out var connection);
            command.Parameters.AddWithValue("@ApplicationName", this.Options.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            var returnValue = AddReturnValue(command);

            using (connection)
            {
                connection.Open();
                command.ExecuteNonQuery();
                return returnValue.Value is int code && code == 0;
            }
        }

        /// <inheritdoc />
        public string GeneratePassword(int length, int numberOfNonAlphanumericCharacters)
        {
            if (length < 1 || length > 128)
            {
                throw new ArgumentException("The password length must be between 1 and 128 characters.", nameof(length));
            }

            if (numberOfNonAlphanumericCharacters > length || numberOfNonAlphanumericCharacters < 0)
            {
                throw new ArgumentException(
                    "The number of non-alphanumeric characters must not be negative nor longer than the password.",
                    nameof(numberOfNonAlphanumericCharacters));
            }

            // Ported verbatim from System.Web.Security.Membership.GeneratePassword so generated
            // passwords keep the same shape (and keep passing DNN's own password validators).
            while (true)
            {
                var buffer = new byte[length];
                var characters = new char[length];
                var nonAlphanumericCount = 0;

                RandomNumberGenerator.Fill(buffer);

                for (var i = 0; i < length; i++)
                {
                    var value = buffer[i] % 87;
                    if (value < 10)
                    {
                        characters[i] = (char)('0' + value);
                    }
                    else if (value < 36)
                    {
                        characters[i] = (char)('A' + value - 10);
                    }
                    else if (value < 62)
                    {
                        characters[i] = (char)('a' + value - 36);
                    }
                    else
                    {
                        characters[i] = Punctuations[value - 62];
                        nonAlphanumericCount++;
                    }
                }

                if (nonAlphanumericCount < numberOfNonAlphanumericCharacters)
                {
                    for (var i = 0; i < numberOfNonAlphanumericCharacters - nonAlphanumericCount; i++)
                    {
                        int position;
                        do
                        {
                            position = RandomNumberGenerator.GetInt32(length);
                        }
                        while (!char.IsLetterOrDigit(characters[position]));

                        characters[position] = Punctuations[RandomNumberGenerator.GetInt32(Punctuations.Length)];
                    }
                }

                var password = new string(characters);

                // The legacy implementation rejected candidates that look like markup so that a
                // generated password can never be mistaken for a script injection.
                if (password.IndexOf('<', StringComparison.Ordinal) < 0 &&
                    password.IndexOf('&', StringComparison.Ordinal) < 0)
                {
                    return password;
                }
            }
        }

        private static string GenerateSalt()
        {
            var buffer = new byte[SaltSizeInBytes];
            RandomNumberGenerator.Fill(buffer);
            return Convert.ToBase64String(buffer);
        }

        private static SqlParameter AddReturnValue(SqlCommand command)
        {
            var returnValue = new SqlParameter("@ReturnValue", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };
            command.Parameters.Add(returnValue);
            return returnValue;
        }

        private static string GetString(IDataRecord record, int index)
        {
            return record.IsDBNull(index) ? null : record.GetString(index);
        }

        private static DateTime ToLocal(DateTime utc)
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        }

        private SqlCommand CreateCommand(string procedureName, out SqlConnection connection)
        {
            connection = new SqlConnection(this.ConnectionString);
            var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedureName;
            command.CommandTimeout = this.Options.CommandTimeout;
            return command;
        }

        /// <summary>Reads the password format and salt that a user's credentials are stored with.</summary>
        /// <param name="username">The user name.</param>
        /// <param name="salt">The user's password salt.</param>
        /// <returns>The user's password format.</returns>
        private int GetPasswordFormat(string username, out string salt)
        {
            this.GetPasswordWithFormat(username, false, out var status, out _, out var format, out salt, out _, out _, out _, out _, out _);
            if (status != 0)
            {
                throw new MembershipStoreException($"The membership store has no user named '{username}'.");
            }

            return format;
        }

        private void GetPasswordWithFormat(
            string username,
            bool updateLastLoginActivityDate,
            out int status,
            out string password,
            out int passwordFormat,
            out string passwordSalt,
            out int failedPasswordAttemptCount,
            out int failedPasswordAnswerAttemptCount,
            out bool isApproved,
            out DateTime lastLoginDate,
            out DateTime lastActivityDate)
        {
            using var command = this.CreateCommand("dbo.aspnet_Membership_GetPasswordWithFormat", out var connection);
            command.Parameters.AddWithValue("@ApplicationName", this.Options.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@UpdateLastLoginActivityDate", updateLastLoginActivityDate);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            var returnValue = AddReturnValue(command);

            password = null;
            passwordFormat = this.Options.PasswordFormat;
            passwordSalt = null;
            failedPasswordAttemptCount = 0;
            failedPasswordAnswerAttemptCount = 0;
            isApproved = false;
            lastLoginDate = DateTime.UtcNow;
            lastActivityDate = DateTime.UtcNow;

            using (connection)
            {
                connection.Open();
                using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (reader.Read())
                    {
                        password = GetString(reader, 0);
                        passwordFormat = reader.GetInt32(1);
                        passwordSalt = GetString(reader, 2);
                        failedPasswordAttemptCount = reader.GetInt32(3);
                        failedPasswordAnswerAttemptCount = reader.GetInt32(4);
                        isApproved = reader.GetBoolean(5);
                        lastLoginDate = reader.GetDateTime(6);
                        lastActivityDate = reader.GetDateTime(7);
                    }
                }

                status = returnValue.Value is int code ? code : -1;
            }
        }

        /// <summary>
        /// Verifies a password, updating the failure counters and the lock-out state in the
        /// database exactly as the legacy provider did.
        /// </summary>
        /// <param name="username">The user name.</param>
        /// <param name="password">The clear-text password to verify.</param>
        /// <param name="updateLastLoginActivityDate">Whether a success stamps the login/activity dates.</param>
        /// <param name="failIfNotApproved">Whether an unapproved account fails verification.</param>
        /// <returns><see langword="true"/> when the password is correct.</returns>
        private bool CheckPassword(string username, string password, bool updateLastLoginActivityDate, bool failIfNotApproved)
        {
            if (string.IsNullOrEmpty(username) || password is null)
            {
                return false;
            }

            this.GetPasswordWithFormat(
                username,
                updateLastLoginActivityDate,
                out var status,
                out var storedPassword,
                out var passwordFormat,
                out var salt,
                out var failedPasswordAttemptCount,
                out var failedPasswordAnswerAttemptCount,
                out var isApproved,
                out var lastLoginDate,
                out var lastActivityDate);

            if (status != 0)
            {
                return false;
            }

            if (!isApproved && failIfNotApproved)
            {
                return false;
            }

            var encoded = this.EncodePassword(password, passwordFormat, salt);
            var isPasswordCorrect = string.Equals(storedPassword, encoded, StringComparison.Ordinal);

            if (isPasswordCorrect && failedPasswordAttemptCount == 0 && failedPasswordAnswerAttemptCount == 0)
            {
                return true;
            }

            this.UpdateUserInfo(username, isPasswordCorrect, updateLastLoginActivityDate, lastLoginDate, lastActivityDate);
            return isPasswordCorrect;
        }

        private void UpdateUserInfo(string username, bool isPasswordCorrect, bool updateLastLoginActivityDate, DateTime lastLoginDate, DateTime lastActivityDate)
        {
            var settings = this.Options;
            using var command = this.CreateCommand("dbo.aspnet_Membership_UpdateUserInfo", out var connection);
            command.Parameters.AddWithValue("@ApplicationName", settings.ApplicationName);
            command.Parameters.AddWithValue("@UserName", username);
            command.Parameters.AddWithValue("@IsPasswordCorrect", isPasswordCorrect);
            command.Parameters.AddWithValue("@UpdateLastLoginActivityDate", updateLastLoginActivityDate);
            command.Parameters.AddWithValue("@MaxInvalidPasswordAttempts", settings.MaxInvalidPasswordAttempts);
            command.Parameters.AddWithValue("@PasswordAttemptWindow", settings.PasswordAttemptWindow);
            command.Parameters.AddWithValue("@CurrentTimeUtc", DateTime.UtcNow);
            command.Parameters.AddWithValue("@LastLoginDate", isPasswordCorrect ? DateTime.UtcNow : lastLoginDate);
            command.Parameters.AddWithValue("@LastActivityDate", isPasswordCorrect ? DateTime.UtcNow : lastActivityDate);
            AddReturnValue(command);

            using (connection)
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private bool IsValidPassword(string password)
        {
            var settings = this.Options;
            if (string.IsNullOrEmpty(password) || password.Length < settings.MinRequiredPasswordLength)
            {
                return false;
            }

            if (settings.MinRequiredNonAlphanumericCharacters > 0)
            {
                var nonAlphanumeric = 0;
                foreach (var character in password)
                {
                    if (!char.IsLetterOrDigit(character))
                    {
                        nonAlphanumeric++;
                    }
                }

                if (nonAlphanumeric < settings.MinRequiredNonAlphanumericCharacters)
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(settings.PasswordStrengthRegularExpression) &&
                !Regex.IsMatch(password, settings.PasswordStrengthRegularExpression))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Encodes a password exactly the way <c>SqlMembershipProvider.EncodePassword</c> did.
        /// </summary>
        /// <remarks>
        /// <b>This algorithm must not change.</b> Every password already in the database was
        /// encoded with it; any deviation makes stored hashes unverifiable. The salt is
        /// base64-decoded and prefixed to the UTF-16LE password bytes, and the configured
        /// <see cref="MembershipOptions.HashAlgorithmType"/> is applied — the keyed variant
        /// (an <c>HMAC*</c> name) keys the MAC with the salt and hashes only the password, the
        /// plain variant hashes salt + password, just as ASP.NET did.
        /// </remarks>
        /// <param name="password">The clear-text password.</param>
        /// <param name="passwordFormat">0 = Clear, 1 = Hashed, 2 = Encrypted.</param>
        /// <param name="salt">The base64 password salt.</param>
        /// <returns>The encoded password as stored in the database.</returns>
        private string EncodePassword(string password, int passwordFormat, string salt)
        {
            if (passwordFormat == 0)
            {
                // Clear
                return password;
            }

            var passwordBytes = Encoding.Unicode.GetBytes(password);
            var saltBytes = Convert.FromBase64String(salt);
            var allBytes = new byte[saltBytes.Length + passwordBytes.Length];
            Buffer.BlockCopy(saltBytes, 0, allBytes, 0, saltBytes.Length);
            Buffer.BlockCopy(passwordBytes, 0, allBytes, saltBytes.Length, passwordBytes.Length);

            if (passwordFormat == 1)
            {
                // Hashed
                var algorithmName = this.Options.HashAlgorithmType;
                if (!string.IsNullOrEmpty(algorithmName) &&
                    algorithmName.StartsWith("HMAC", StringComparison.OrdinalIgnoreCase))
                {
                    using var keyedHash = CreateKeyedHashAlgorithm(algorithmName, saltBytes);
                    return Convert.ToBase64String(keyedHash.ComputeHash(passwordBytes));
                }

                using var hash = CreateHashAlgorithm(algorithmName);
                return Convert.ToBase64String(hash.ComputeHash(allBytes));
            }

            // Encrypted
            return Convert.ToBase64String(this.EncryptPassword(allBytes));
        }

        /// <summary>Reverses <see cref="EncodePassword"/> for the retrievable formats.</summary>
        /// <param name="encodedPassword">The stored password.</param>
        /// <param name="passwordFormat">0 = Clear, 1 = Hashed, 2 = Encrypted.</param>
        /// <returns>The clear-text password.</returns>
        private string UnEncodePassword(string encodedPassword, int passwordFormat)
        {
            switch (passwordFormat)
            {
                case 0:
                    return encodedPassword;
                case 1:
                    throw new NotSupportedException("Hashed passwords cannot be retrieved. Reset the password instead.");
                default:
                    var decrypted = this.DecryptPassword(Convert.FromBase64String(encodedPassword));

                    // The legacy format prefixed the salt (16 bytes) to the UTF-16LE password.
                    return Encoding.Unicode.GetString(decrypted, SaltSizeInBytes, decrypted.Length - SaltSizeInBytes);
            }
        }

        /// <summary>
        /// Protects the (non-default) <c>Encrypted</c> password format with Triple-DES in CBC
        /// mode under the application's decryption key, with the IV prefixed to the cipher
        /// text — the legacy <c>machineKey</c> encrypted-password layout.
        /// </summary>
        /// <param name="plainText">The salt + password bytes.</param>
        /// <returns>The IV followed by the cipher text.</returns>
        private byte[] EncryptPassword(byte[] plainText)
        {
            using var algorithm = this.CreatePasswordCipher();
            algorithm.GenerateIV();
            using var transform = algorithm.CreateEncryptor();
            var cipherText = transform.TransformFinalBlock(plainText, 0, plainText.Length);
            var result = new byte[algorithm.IV.Length + cipherText.Length];
            Buffer.BlockCopy(algorithm.IV, 0, result, 0, algorithm.IV.Length);
            Buffer.BlockCopy(cipherText, 0, result, algorithm.IV.Length, cipherText.Length);
            return result;
        }

        /// <summary>Reverses <see cref="EncryptPassword"/>.</summary>
        /// <param name="cipherText">The IV followed by the cipher text.</param>
        /// <returns>The salt + password bytes.</returns>
        private byte[] DecryptPassword(byte[] cipherText)
        {
            using var algorithm = this.CreatePasswordCipher();
            var ivLength = algorithm.BlockSize / 8;
            var iv = new byte[ivLength];
            Buffer.BlockCopy(cipherText, 0, iv, 0, ivLength);
            algorithm.IV = iv;
            using var transform = algorithm.CreateDecryptor();
            return transform.TransformFinalBlock(cipherText, ivLength, cipherText.Length - ivLength);
        }

        private SymmetricAlgorithm CreatePasswordCipher()
        {
            var key = this.machineKey.CurrentValue?.DecryptionKey;
            if (string.IsNullOrEmpty(key))
            {
                throw new MembershipStoreException(
                    "The Encrypted password format requires \"MachineKey:DecryptionKey\" to be configured with the same value the installation has always used.");
            }

            var algorithm = TripleDES.Create();
            algorithm.Mode = CipherMode.CBC;
            algorithm.Padding = PaddingMode.PKCS7;
            algorithm.Key = HexToBytes(key, 24);
            return algorithm;
        }

        private static byte[] HexToBytes(string hex, int byteCount)
        {
            var bytes = new byte[byteCount];
            for (var i = 0; i < byteCount; i++)
            {
                var offset = i * 2;
                bytes[i] = offset + 1 < hex.Length
                    ? Convert.ToByte(hex.Substring(offset, 2), 16)
                    : (byte)0;
            }

            return bytes;
        }

        private static HashAlgorithm CreateHashAlgorithm(string name)
        {
            switch ((name ?? string.Empty).ToUpperInvariant())
            {
                case "":
                    // Fail fast rather than guess. DNN shipped hashAlgorithmType="SHA256", but an
                    // installation that OMITTED the attribute on .NET Framework fell back to
                    // ASP.NET's own default of HMACSHA256 (keyed from <machineKey validation>).
                    // Silently picking unkeyed SHA256 for such an install would make every stored
                    // password hash unverifiable — a mass user lockout with no error. A loud
                    // startup failure is far cheaper.
                    throw new MembershipStoreException(
                        "\"Membership:HashAlgorithmType\" must be set explicitly to the algorithm this " +
                        "installation has always used. DNN shipped \"SHA256\"; an installation whose " +
                        "web.config omitted <membership hashAlgorithmType=\"...\"> was using " +
                        "\"HMACSHA256\". Setting the wrong value will lock out every user.");
                case "SHA256":
                case "SHA-256":
                    return SHA256.Create();
                case "SHA1":
                case "SHA-1":
                    return SHA1.Create();
                case "SHA384":
                case "SHA-384":
                    return SHA384.Create();
                case "SHA512":
                case "SHA-512":
                    return SHA512.Create();
                case "MD5":
                    return MD5.Create();
                default:
                    throw new MembershipStoreException($"\"Membership:HashAlgorithmType\" has an unsupported value '{name}'.");
            }
        }

        private static KeyedHashAlgorithm CreateKeyedHashAlgorithm(string name, byte[] key)
        {
            switch (name.ToUpperInvariant())
            {
                case "HMACSHA256":
                    return new HMACSHA256(key);
                case "HMACSHA1":
                    return new HMACSHA1(key);
                case "HMACSHA384":
                    return new HMACSHA384(key);
                case "HMACSHA512":
                    return new HMACSHA512(key);
                case "HMACMD5":
                    return new HMACMD5(key);
                default:
                    throw new MembershipStoreException($"\"Membership:HashAlgorithmType\" has an unsupported keyed value '{name}'.");
            }
        }
    }
}
