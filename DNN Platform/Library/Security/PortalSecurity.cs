// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Security
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Security.Claims;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Lists;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Entities.Users.Social;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Security.Authentication;
    using DotNetNuke.Security.Cookies;
    using DotNetNuke.Services.Cryptography;
    using ICryptographyProvider = DotNetNuke.Abstractions.Security.ICryptographyProvider;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A variety of security-related utility functions.</summary>
    public partial class PortalSecurity
    {
        /// <summary>A <see cref="PortalSecurity"/> instance.</summary>
        public static readonly PortalSecurity Instance = ActivatorUtilities.GetServiceOrCreateInstance<PortalSecurity>(Globals.DependencyProvider);

        private const string EncryptedStringAlgorithmSeparator = "___|^Algorithm^|___";
        private const string EncryptedStringInitializationVectorSeparator = "___|^IV^|___";

        private const string RoleFriendPrefix = "FRIEND:";
        private const string RoleFollowerPrefix = "FOLLOWER:";
        private const string RoleOwnerPrefix = "OWNER:";

        private const string BadStatementExpression = ";|--|\bcreate\b|\bdrop\b|\bselect\b|\binsert\b|\bdelete\b|\bupdate\b|\bunion\b|sp_|xp_|\bexec\b|\bexecute\b|/\\*.*\\*/|\bdeclare\b|\bwaitfor\b|%|&";

        private const RegexOptions RxOptions = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled;

        private static readonly DateTime OldExpiryTime = new DateTime(1999, 1, 1);

        private static readonly Regex StripTagsRegex = new Regex("<[^<>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex BadStatementRegex = new Regex(BadStatementExpression, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ControlCharactersRegex = new Regex(@"\p{C}+", RegexOptions.Compiled);
        private static readonly Regex ControlCharacterToWhitespaceRegex = new Regex(@"\r\n|\r|\n|\t", RegexOptions.Compiled);

        private static readonly Regex[] RxListStrings = new[]
        {
            new Regex("<script[^>]*>.*?</script[^><]*>", RxOptions),
            new Regex("<script", RxOptions),
            new Regex("<input[^>]*>.*?</input[^><]*>", RxOptions),
            new Regex("<object[^>]*>.*?</object[^><]*>", RxOptions),
            new Regex("<embed[^>]*>.*?</embed[^><]*>", RxOptions),
            new Regex("<applet[^>]*>.*?</applet[^><]*>", RxOptions),
            new Regex("<form[^>]*>.*?</form[^><]*>", RxOptions),
            new Regex("<option[^>]*>.*?</option[^><]*>", RxOptions),
            new Regex("<select[^>]*>.*?</select[^><]*>", RxOptions),
            new Regex("<source[^>]*>.*?</source[^><]*>", RxOptions),
            new Regex("<iframe[^>]*>.*?</iframe[^><]*>", RxOptions),
            new Regex("<iframe.*?<", RxOptions),
            new Regex("<iframe.*?", RxOptions),
            new Regex("<ilayer[^>]*>.*?</ilayer[^><]*>", RxOptions),
            new Regex("<form[^>]*>", RxOptions),
            new Regex("</form[^><]*>", RxOptions),
            new Regex("\bonerror\b", RxOptions),
            new Regex("\bonload\b", RxOptions),
            new Regex("\bonfocus\b", RxOptions),
            new Regex("\bonblur\b", RxOptions),
            new Regex("\bonclick\b", RxOptions),
            new Regex("\bondblclick\b", RxOptions),
            new Regex("\bonchange\b", RxOptions),
            new Regex("\bonselect\b", RxOptions),
            new Regex("\bonsubmit\b", RxOptions),
            new Regex("\bonreset\b", RxOptions),
            new Regex("\bonkeydown\b", RxOptions),
            new Regex("\bonkeyup\b", RxOptions),
            new Regex("\bonkeypress\b", RxOptions),
            new Regex("\bonmousedown\b", RxOptions),
            new Regex("\bonmousemove\b", RxOptions),
            new Regex("\bonmouseout\b", RxOptions),
            new Regex("\bonmouseover\b", RxOptions),
            new Regex("\bonmouseup\b", RxOptions),
            new Regex("\bonreadystatechange\b", RxOptions),
            new Regex("\bonfinish\b", RxOptions),
            new Regex("javascript:", RxOptions),
            new Regex("vbscript:", RxOptions),
            new Regex("unescape", RxOptions),
            new Regex(@"alert[\s(&nbsp;)]*\([\s(&nbsp;)]*'?[\s(&nbsp;)]*[""(&quot;)]?", RxOptions),
            new Regex(@"eval*.\(", RxOptions),
        };

        private static readonly Regex DangerElementsRegex = new Regex(@"(<[^>]*?) on.*?\=(['""]*)[\s\S]*?(\2)( *)([^>]*?>)", RxOptions);
        private static readonly Regex DangerElementContentRegex = new Regex(@"on.*?\=(['""]*)[\s\S]*?(\1)( *)", RxOptions);

        private readonly ICryptographyProvider cryptographyProvider;
        private readonly ListController listController;
        private readonly IPortalController portalController;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IPortalGroupController portalGroupController;
        private readonly IAuthenticationCookieService authenticationCookieService;

        /// <summary>Initializes a new instance of the <see cref="PortalSecurity"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with ListController. Scheduled removal in v12.0.0.")]
        public PortalSecurity()
            : this(null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="PortalSecurity"/> class.</summary>
        /// <param name="cryptographyProvider">The cryptography provider.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with ListController. Scheduled removal in v12.0.0.")]
        public PortalSecurity(ICryptographyProvider cryptographyProvider)
            : this(cryptographyProvider, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="PortalSecurity"/> class.</summary>
        /// <param name="cryptographyProvider">The cryptography provider.</param>
        /// <param name="listController">The list controller.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="portalGroupController">The portal group controller.</param>
        public PortalSecurity(ICryptographyProvider cryptographyProvider, ListController listController, IPortalController portalController, IApplicationStatusInfo appStatus, IPortalGroupController portalGroupController)
        {
            this.cryptographyProvider = cryptographyProvider ?? Globals.GetCurrentServiceProvider().GetRequiredService<ICryptographyProvider>();
            this.listController = listController ?? Globals.GetCurrentServiceProvider().GetRequiredService<ListController>();
            this.portalController = portalController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>();
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.portalGroupController = portalGroupController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPortalGroupController>();
            this.authenticationCookieService = Globals.GetCurrentServiceProvider().GetRequiredService<IAuthenticationCookieService>();
        }

        /// <summary>
        /// The FilterFlag enum determines which filters are applied by the InputFilter
        /// function.  The Flags attribute allows the user to include multiple
        /// enumerated values in a single variable by OR'ing the individual values
        /// together.
        /// </summary>
        [Flags]
        [SuppressMessage("Microsoft.Design", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "Breaking change")]
        public enum FilterFlag
        {
            /// <summary>Replaces line breaks with <c>&lt;br&gt;</c> tags.</summary>
            MultiLine = 1,

            /// <summary>Removes text which could be interpreted as HTML markup.</summary>
            [Obsolete("Deprecated in DotNetNuke 9.8.1. A direct call to WebUtility.HtmlEncode should be used. Scheduled for removal in v11.0.0.")]
            NoMarkup = 2,

            /// <summary>Removes text which could be interpreted as JavaScript commands.</summary>
            [Obsolete("Deprecated in DotNetNuke 9.8.1. A direct call to WebUtility.HtmlEncode should be used. Scheduled for removal in v11.0.0.")]
            NoScripting = 4,

            /// <summary>Removes text which could be interpreted as SQL commands.</summary>
            [Obsolete("Deprecated in DotNetNuke 9.8.1. Parameterized SQL should be preferred for SQL Injection protection. Scheduled for removal in v11.0.0.")]
            NoSQL = 8,

            /// <summary>Removes angle brackets from the text.</summary>
            [Obsolete("Deprecated in DotNetNuke 9.8.1. Individual string replacement should be completed. Scheduled for removal in v11.0.0.")]
            NoAngleBrackets = 16,

            /// <summary>Removes matches with the site's profanity filter.</summary>
            NoProfanity = 32,

            /// <summary>Removes all Unicode control characters (like \0, \t, \n, \r, etc.) from the string.</summary>
            /// <remarks>The control characters \r\n, \r, \n, and \t are replaced with a single space instead of being removed.</remarks>
            NoControlCharacters = 64,
        }

        /// <summary>Determines the configuration source for the remove and replace functions.</summary>
        public enum ConfigType
        {
            /// <summary>Retrieve configuration via <see cref="DotNetNuke.Common.Lists.ListController"/>.</summary>
            ListController = 0,

            /// <summary>Retrieve configuration from an external file.</summary>
            ExternalFile = 1,
        }

        /// <summary>
        /// determines whether to use system (host) list, portal specific list, or combine both
        /// At present only supported by ConfigType.ListController.
        /// </summary>
        public enum FilterScope
        {
            /// <summary>Use the host-level list.</summary>
            SystemList = 0,

            /// <summary>Use a portal-specific list.</summary>
            PortalList = 1,

            /// <summary>Combine both lists.</summary>
            SystemAndPortalList = 2,
        }

        private enum RoleType
        {
            Security = 0,
            Friend = 1,
            Follower = 2,
            Owner = 3,
        }

        /// <summary>Forces the secure connection.</summary>
        public static void ForceSecureConnection()
        {
            // get current url
            var context = HttpContextSource.CurrentOrThrow;
            var url = context.Request.GetDisplayUrl();

            // if unsecure connection
            if (url.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase))
            {
                // switch to secure connection
                url = "https://" + url.Substring("http://".Length);

                // append ssl parameter to querystring to indicate secure connection processing has already occurred
                if (url.IndexOf("?", StringComparison.Ordinal) == -1)
                {
                    url = url + "?ssl=1";
                }
                else
                {
                    url = url + "&ssl=1";
                }

                // redirect to secure connection
                context.Response.Redirect(url);
            }
        }

        /// <summary>Gets the cookie domain for the portal group or from web.config.</summary>
        /// <param name="portalId">The portal ID.</param>
        /// <returns>Cookie domain for the portal group or from web.config.</returns>
        [DnnDeprecated(10, 2, 4, "Please use overload with IPortalController")]
        public static partial string GetCookieDomain(int portalId)
            => GetCookieDomain(
                Globals.GetCurrentServiceProvider().GetRequiredService<IPortalController>(),
                Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>(),
                Globals.GetCurrentServiceProvider().GetRequiredService<IPortalGroupController>(),
                portalId);

        /// <summary>Gets the cookie domain for the portal group or from web.config.</summary>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="portalGroupController">The portal group controller.</param>
        /// <param name="portalId">The portal ID.</param>
        /// <returns>Cookie domain for the portal group or from web.config.</returns>
        public static string GetCookieDomain(IPortalController portalController, IApplicationStatusInfo appStatus, IPortalGroupController portalGroupController, int portalId)
        {
            string cookieDomain = string.Empty;
            if (PortalController.IsMemberOfPortalGroup(portalController, portalId))
            {
                // set cookie domain for portal group
                var group = portalGroupController.GetPortalGroups().SingleOrDefault(p => p.MasterPortalId == PortalController.GetEffectivePortalId(portalController, appStatus, portalGroupController, portalId));

                if (@group != null
                        && !string.IsNullOrEmpty(@group.AuthenticationDomain)
                        && ((IPortalAliasInfo)PortalSettings.Current.PortalAlias).HttpAlias.Contains(@group.AuthenticationDomain))
                {
                    cookieDomain = @group.AuthenticationDomain;
                }

                if (string.IsNullOrEmpty(cookieDomain))
                {
                    cookieDomain = AuthenticationCookieService.Current.CookieDomain;
                }
            }
            else
            {
                // set cookie domain to be consistent with domain specification in web.config
                cookieDomain = AuthenticationCookieService.Current.CookieDomain;
            }

            return cookieDomain;
        }

        /// <summary>Determines whether the current user is denied for the given role(s).</summary>
        /// <param name="roles">The semicolon separated list of roles.</param>
        /// <returns><see langword="true"/> if the current user is denied from the provided specified roles; otherwise, <see langword="false"/>.</returns>
        public static bool IsDenied(string roles)
        {
            UserInfo objUserInfo = UserController.Instance.GetCurrentUserInfo();
            var settings = PortalController.Instance.GetCurrentSettings();
            return IsDenied(objUserInfo, settings, roles);
        }

        /// <summary>Determines whether the specified user is denied for the given roles.</summary>
        /// <param name="objUserInfo">The user information.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="roles">The semicolon separated list of roles.</param>
        /// <returns><see langword="true"/> if the specified user is denied; otherwise, <see langword="false"/>.</returns>
        public static bool IsDenied(UserInfo objUserInfo, PortalSettings settings, string roles)
            => IsDenied(objUserInfo, (IPortalSettings)settings, roles);

        /// <summary>Determines whether the specified user is denied for the given roles.</summary>
        /// <param name="objUserInfo">The user information.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="roles">The semicolon separated list of roles.</param>
        /// <returns><see langword="true"/> if the specified user is denied; otherwise, <see langword="false"/>.</returns>
        public static bool IsDenied(UserInfo objUserInfo, IPortalSettings settings, string roles)
        {
            // superuser always has full access
            if (objUserInfo.IsSuperUser)
            {
                return false;
            }

            bool isDenied = false;

            if (roles != null)
            {
                // permissions strings are encoded with Deny permissions at the beginning and Grant permissions at the end for optimal performance
                foreach (string role in roles.Split(';'))
                {
                    if (!string.IsNullOrEmpty(role))
                    {
                        // Deny permission
                        if (role.StartsWith("!", StringComparison.Ordinal))
                        {
                            // Portal Admin cannot be denied from his/her portal (so ignore deny permissions if user is portal admin)
                            if (settings != null && !(settings.PortalId == objUserInfo.PortalID && objUserInfo.IsInRole(settings.AdministratorRoleName)))
                            {
                                string denyRole = role.Replace("!", string.Empty);
                                if (denyRole == Globals.glbRoleAllUsersName || objUserInfo.IsInRole(denyRole))
                                {
                                    isDenied = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return isDenied;
        }

        /// <summary>Determines whether the current user belongs to the specified role.</summary>
        /// <param name="role">The role name.</param>
        /// <returns><see langword="true"/> if user belongs to the specified role; otherwise, <see langword="false"/>.</returns>
        public static bool IsInRole(string role)
        {
            if (!string.IsNullOrEmpty(role) && role == Globals.glbRoleUnauthUserName &&
                HttpContextSource.Current?.User?.Identity?.IsAuthenticated != true)
            {
                return true;
            }

            return IsInRoles(UserController.Instance.GetCurrentUserInfo(), PortalController.Instance.GetCurrentSettings(), role);
        }

        /// <summary>Determines whether the current user belongs to the specified roles.</summary>
        /// <param name="roles">The semicolon separated list of roles.</param>
        /// <returns><see langword="true"/> if user belongs to the specified roles; otherwise, <see langword="false"/>.</returns>
        public static bool IsInRoles(string roles)
        {
            var objUserInfo = UserController.Instance.GetCurrentUserInfo();
            var settings = PortalController.Instance.GetCurrentSettings();
            return IsInRoles(objUserInfo, settings, roles);
        }

        /// <summary>Determines whether the provided user belongs to the specified roles.</summary>
        /// <param name="objUserInfo">The user information.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="roles">The semicolon separated list of roles.</param>
        /// <returns><see langword="true"/> if the provided user belongs to the specific roles; otherwise, <see langword="false"/>.</returns>
        [DnnDeprecated(10, 0, 2, "Use overload taking IPortalSettings")]
        public static partial bool IsInRoles(UserInfo objUserInfo, PortalSettings settings, string roles)
            => IsInRoles(objUserInfo, (IPortalSettings)settings, roles);

        /// <summary>Determines whether the provided user belongs to the specified roles.</summary>
        /// <param name="objUserInfo">The user information.</param>
        /// <param name="settings">The settings.</param>
        /// <param name="roles">The semicolon separated list of roles.</param>
        /// <returns><see langword="true"/> if the provided user belongs to the specific roles; otherwise, <see langword="false"/>.</returns>
        public static bool IsInRoles(UserInfo objUserInfo, IPortalSettings settings, string roles)
        {
            if (objUserInfo.IsSuperUser)
            {
                return true;
            }

            if (roles == null)
            {
                return false;
            }

            foreach (var role in roles.Split(';'))
            {
                ProcessRole(objUserInfo, settings, role, out var roleAllowed);
                if (roleAllowed.HasValue)
                {
                    return roleAllowed.Value;
                }
            }

            return false;
        }

        /// <summary>Determines whether the specified user is a friend of the current user.</summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns><see langword="true"/> if the specified user is a friend of the current user; otherwise, <see langword="false"/>.</returns>
        public static bool IsFriend(int userId)
        {
            var objUserInfo = UserController.Instance.GetCurrentUserInfo();
            var settings = PortalController.Instance.GetCurrentSettings();
            return IsInRoles(objUserInfo, settings, RoleFriendPrefix + userId);
        }

        /// <summary>Determines whether the specified user is a follower of the current user.</summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns><see langword="true"/> if the specified user is a follower of the current user; otherwise, <see langword="false"/>.</returns>
        public static bool IsFollower(int userId)
        {
            var objUserInfo = UserController.Instance.GetCurrentUserInfo();
            var settings = PortalController.Instance.GetCurrentSettings();
            return IsInRoles(objUserInfo, settings, RoleFollowerPrefix + userId);
        }

        /// <summary>Determines whether the specified user is an owner.</summary>
        /// <param name="userId">The user identifier.</param>
        /// <returns><see langword="true"/> if the specified user is an owner; otherwise, <see langword="false"/>.</returns>
        public static bool IsOwner(int userId)
        {
            var objUserInfo = UserController.Instance.GetCurrentUserInfo();
            var settings = PortalController.Instance.GetCurrentSettings();
            return IsInRoles(objUserInfo, settings, RoleOwnerPrefix + userId);
        }

        /// <summary>This function creates a random key.</summary>
        /// <param name="numBytes">This is the number of bytes for the key.</param>
        /// <returns>A random string.</returns>
        /// <remarks>This is a public function used for generating SHA1 keys.</remarks>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string CreateKey(int numBytes)
        {
            var buff = new byte[numBytes];
            RandomNumberGenerator.Fill(buff);
            return BytesToHexString(buff);
        }

        /// <summary>Decrypts the provided string data using a supplied key.</summary>
        /// <param name="strKey">The encryption key.</param>
        /// <param name="strData">The encrypted data.</param>
        /// <returns>The decrypted string.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        [DnnDeprecated(10, 2, 2, "Use DotNetNuke.Abstractions.Security.ICryptographyProvider")]
        public partial string Decrypt(string strKey, string strData)
        {
            return this.cryptographyProvider.DecryptParameter(strData, strKey, CryptographyProvider.Instance().EncryptParameterAlgorithmName);
        }

        /// <summary>Decrypts a string using a provided passphrase.</summary>
        /// <param name="message">The encrypted message.</param>
        /// <param name="passphrase">The passphrase.</param>
        /// <returns>The decrypted string.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        [DnnDeprecated(10, 2, 2, "Use DotNetNuke.Abstractions.Security.ICryptographyProvider")]
        public partial string DecryptString(string message, string passphrase)
        {
            if (!message.Contains(EncryptedStringAlgorithmSeparator, StringComparison.Ordinal))
            {
                return this.cryptographyProvider.DecryptString(
                    message,
                    passphrase,
                    CryptographyProvider.Instance().EncryptStringAlgorithmName,
                    null);
            }

            var endOfMessage = message.IndexOf(EncryptedStringAlgorithmSeparator, StringComparison.Ordinal);
            var encryptedMessage = message.Substring(0, endOfMessage);
            var startOfAlgorithm = endOfMessage + EncryptedStringAlgorithmSeparator.Length;
            var endOfAlgorithm = message.IndexOf(EncryptedStringInitializationVectorSeparator, startOfAlgorithm, StringComparison.Ordinal);
            var algorithmName = message.Substring(startOfAlgorithm, endOfAlgorithm - startOfAlgorithm);
            var startOfInitializationVector = endOfAlgorithm + EncryptedStringInitializationVectorSeparator.Length;
            var initializationVector = message.Substring(startOfInitializationVector);
            return this.cryptographyProvider.DecryptString(encryptedMessage, passphrase, algorithmName, initializationVector);
        }

        /// <summary>Encrypts the specified key.</summary>
        /// <param name="key">The key.</param>
        /// <param name="data">The data.</param>
        /// <returns>The encrypted string.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        [DnnDeprecated(10, 2, 2, "Use DotNetNuke.Abstractions.Security.ICryptographyProvider")]
        public partial string Encrypt(string key, string data)
        {
            return this.cryptographyProvider.EncryptParameter(data, key).EncryptedMessage;
        }

        /// <summary>Encrypts a string using a provided passphrase.</summary>
        /// <param name="message">The message.</param>
        /// <param name="passphrase">The passphrase.</param>
        /// <returns>The encrypted string.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        [DnnDeprecated(10, 2, 2, "Use DotNetNuke.Abstractions.Security.ICryptographyProvider")]
        public partial string EncryptString(string message, string passphrase)
        {
            var (encryptedMessage, algorithm, initializationVector) = this.cryptographyProvider.EncryptString(message, passphrase);
            return $"{encryptedMessage}{EncryptedStringAlgorithmSeparator}{algorithm}{EncryptedStringInitializationVectorSeparator}{initializationVector}";
        }

        /// <summary>This function applies security filtering to the UserInput string.</summary>
        /// <param name="userInput">This is the string to be filtered.</param>
        /// <param name="filterType">Flags which designate the filters to be applied.</param>
        /// <returns>Filtered UserInput.</returns>
        public string InputFilter(string userInput, FilterFlag filterType)
        {
            if (userInput == null)
            {
                return string.Empty;
            }

            var tempInput = userInput;
#pragma warning disable CS0618 // Type or member is obsolete
            if ((filterType & FilterFlag.NoAngleBrackets) == FilterFlag.NoAngleBrackets)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                var removeAngleBrackets = Config.GetSetting("RemoveAngleBrackets") != null && bool.Parse(Config.GetSetting("RemoveAngleBrackets"));
                if (removeAngleBrackets)
                {
                    tempInput = FormatAngleBrackets(tempInput);
                }
            }

#pragma warning disable CS0618 // Type or member is obsolete
            if ((filterType & FilterFlag.NoSQL) == FilterFlag.NoSQL)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                tempInput = FormatRemoveSQL(tempInput);
            }

#pragma warning disable CS0618 // Type or member is obsolete
            if ((filterType & FilterFlag.NoMarkup) == FilterFlag.NoMarkup && IncludesMarkup(tempInput))
#pragma warning restore CS0618 // Type or member is obsolete
            {
                tempInput = WebUtility.HtmlEncode(tempInput);
            }

#pragma warning disable CS0618 // Type or member is obsolete
            if ((filterType & FilterFlag.NoScripting) == FilterFlag.NoScripting)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                tempInput = FilterStrings(tempInput);
            }

            if ((filterType & FilterFlag.MultiLine) == FilterFlag.MultiLine)
            {
                tempInput = FormatMultiLine(tempInput);
            }

            if ((filterType & FilterFlag.NoProfanity) == FilterFlag.NoProfanity)
            {
                tempInput = this.Replace(tempInput, ConfigType.ListController, "ProfanityFilter", FilterScope.SystemAndPortalList);
            }

            if ((filterType & FilterFlag.NoControlCharacters) == FilterFlag.NoControlCharacters)
            {
                tempInput = FormatRemoveControlCharacters(tempInput);
            }

            return tempInput;
        }

        /// <summary>Replaces profanity words with other words in the provided input string.</summary>
        /// <remarks>
        /// The correspondence between the words to search and the words to replace could be specified in two different places:
        /// 1) In an external file. (NOT IMPLEMENTED)
        /// 2) In System/Site lists.
        /// The name of the System List is "ProfanityFilter". The name of the list in each portal is composed using the following rule:
        /// "ProfanityFilter-" + PortalID.
        /// </remarks>
        /// <param name="inputString">The string to search the words in.</param>
        /// <param name="configType">The type of configuration.</param>
        /// <param name="configSource">The external file to search the words. Ignored when configType is ListController.</param>
        /// <param name="filterScope">When using ListController configType, this parameter indicates which list(s) to use.</param>
        /// <returns>The original text with the profanity words replaced.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string Replace(string inputString, ConfigType configType, string configSource, FilterScope filterScope)
        {
            switch (configType)
            {
                case ConfigType.ListController:
                    const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Singleline;
                    const string listName = "ProfanityFilter";

                    IPortalSettings settings;

                    IEnumerable<ListEntryInfo> listEntryHostInfos;
                    IEnumerable<ListEntryInfo> listEntryPortalInfos;

                    switch (filterScope)
                    {
                        case FilterScope.SystemList:
                            listEntryHostInfos = this.listController.GetListEntryInfoItems(listName, string.Empty, Null.NullInteger);
                            inputString = listEntryHostInfos.Aggregate(inputString, (current, removeItem) => Regex.Replace(current, @"\b" + Regex.Escape(removeItem.Text) + @"\b", removeItem.Value, options));
                            break;
                        case FilterScope.SystemAndPortalList:
                            settings = PortalController.Instance.GetCurrentSettings();
                            listEntryHostInfos = this.listController.GetListEntryInfoItems(listName, string.Empty, Null.NullInteger);
                            listEntryPortalInfos = this.listController.GetListEntryInfoItems(listName + "-" + settings.PortalId, string.Empty, settings.PortalId);
                            inputString = listEntryHostInfos.Aggregate(inputString, (current, removeItem) => Regex.Replace(current, @"\b" + Regex.Escape(removeItem.Text) + @"\b", removeItem.Value, options));
                            inputString = listEntryPortalInfos.Aggregate(inputString, (current, removeItem) => Regex.Replace(current, @"\b" + Regex.Escape(removeItem.Text) + @"\b", removeItem.Value, options));
                            break;
                        case FilterScope.PortalList:
                            settings = PortalController.Instance.GetCurrentSettings();
                            listEntryPortalInfos = this.listController.GetListEntryInfoItems(listName + "-" + settings.PortalId, string.Empty, settings.PortalId);
                            inputString = listEntryPortalInfos.Aggregate(inputString, (current, removeItem) => Regex.Replace(current, @"\b" + Regex.Escape(removeItem.Text) + @"\b", removeItem.Value, options));
                            break;
                    }

                    break;
                case ConfigType.ExternalFile:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException(nameof(configType));
            }

            return inputString;
        }

        /// <summary>Removes profanity words in the provided input string.</summary>
        /// <remarks>
        /// The words to search could be defined in two different places:
        /// 1) In an external file. (NOT IMPLEMENTED)
        /// 2) In System/Site lists.
        /// The name of the System List is "ProfanityFilter". The name of the list in each portal is composed using the following rule:
        /// "ProfanityFilter-" + PortalID.
        /// </remarks>
        /// <param name="inputString">The string to search the words in.</param>
        /// <param name="configType">The type of configuration.</param>
        /// <param name="configSource">The external file to search the words. Ignored when configType is ListController.</param>
        /// <param name="filterScope">When using ListController configType, this parameter indicates which list(s) to use.</param>
        /// <returns>The original text with the profanity words removed.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public string Remove(string inputString, ConfigType configType, string configSource, FilterScope filterScope)
        {
            switch (configType)
            {
                case ConfigType.ListController:
                    const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Singleline;
                    const string listName = "ProfanityFilter";

                    IPortalSettings settings;

                    IEnumerable<ListEntryInfo> listEntryHostInfos;
                    IEnumerable<ListEntryInfo> listEntryPortalInfos;

                    switch (filterScope)
                    {
                        case FilterScope.SystemList:
                            listEntryHostInfos = this.listController.GetListEntryInfoItems(listName, string.Empty, Null.NullInteger);
                            inputString = listEntryHostInfos.Aggregate(inputString, (current, removeItem) => Regex.Replace(current, @"\b" + Regex.Escape(removeItem.Text) + @"\b", string.Empty, options));
                            break;
                        case FilterScope.SystemAndPortalList:
                            settings = PortalController.Instance.GetCurrentSettings();
                            listEntryHostInfos = this.listController.GetListEntryInfoItems(listName, string.Empty, Null.NullInteger);
                            listEntryPortalInfos = this.listController.GetListEntryInfoItems(listName + "-" + settings.PortalId, string.Empty, settings.PortalId);
                            inputString = listEntryHostInfos.Aggregate(inputString, (current, removeItem) => Regex.Replace(current, @"\b" + Regex.Escape(removeItem.Text) + @"\b", string.Empty, options));
                            inputString = listEntryPortalInfos.Aggregate(inputString, (current, removeItem) => Regex.Replace(current, @"\b" + Regex.Escape(removeItem.Text) + @"\b", string.Empty, options));
                            break;
                        case FilterScope.PortalList:
                            settings = PortalController.Instance.GetCurrentSettings();
                            listEntryPortalInfos = this.listController.GetListEntryInfoItems(listName + "-" + settings.PortalId, string.Empty, settings.PortalId);
                            inputString = listEntryPortalInfos.Aggregate(inputString, (current, removeItem) => Regex.Replace(current, @"\b" + Regex.Escape(removeItem.Text) + @"\b", string.Empty, options));
                            break;
                    }

                    break;
                case ConfigType.ExternalFile:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException(nameof(configType));
            }

            return inputString;
        }

        /// <summary>
        /// Signs the provided user in and sets a persistent login cookie if needed.
        /// </summary>
        /// <param name="user">The user info.</param>
        /// <param name="createPersistentCookie">if set to <see langword="true"/> [create persistent cookie].</param>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public void SignIn(UserInfo user, bool createPersistentCookie)
        {
            this.SignInAsync(user, createPersistentCookie).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Signs the provided user in and sets a persistent login cookie if needed.
        /// </summary>
        /// <param name="user">The user info.</param>
        /// <param name="createPersistentCookie">if set to <see langword="true"/> [create persistent cookie].</param>
        /// <returns>A task that completes once the authentication cookie has been issued.</returns>
        /// <remarks>
        /// <para>
        /// Forms authentication no longer exists: the user is signed in with cookie
        /// authentication and a claims principal (name, user id, portal id and one role claim
        /// per DNN role), so <c>HttpContext.User</c>, <c>[Authorize(Roles = …)]</c> and
        /// <c>User.IsInRole</c> all keep working.
        /// </para>
        /// <para>
        /// DNN's server-side record of every issued cookie (the <c>AuthCookie</c> table, which
        /// lets a sign-out invalidate a cookie that is still in a browser) is preserved: a
        /// one-time token is registered with <see cref="AuthCookieController"/> and travels in
        /// the cookie as the <see cref="DnnAuthenticationDefaults.AuthTokenClaimType"/> claim.
        /// </para>
        /// </remarks>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public async Task SignInAsync(UserInfo user, bool createPersistentCookie)
        {
            if (user is null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var context = HttpContextSource.CurrentOrThrow;
            var isPortalGroupMember = PortalController.IsMemberOfPortalGroup(this.portalController, user.PortalID);

            var timeoutMinutes = createPersistentCookie
                ? Config.GetPersistentCookieTimeout(this.appStatus)
                : Config.GetAuthCookieTimeout(this.appStatus);
            var expiration = DateTime.Now.AddMinutes(timeoutMinutes);

            var cookieDomain = isPortalGroupMember
                ? GetCookieDomain(this.portalController, this.appStatus, this.portalGroupController, user.PortalID)
                : null;

            // Register the cookie server-side so that it can be invalidated later.
            var authToken = Guid.NewGuid().ToString("N");
            AuthCookieController.Instance.Update(authToken, expiration.ToUniversalTime(), user.UserID);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString(CultureInfo.InvariantCulture)),
                new Claim(DnnAuthenticationDefaults.PortalIdClaimType, user.PortalID.ToString(CultureInfo.InvariantCulture)),
                new Claim(DnnAuthenticationDefaults.AuthTokenClaimType, authToken),
            };

            if (!string.IsNullOrEmpty(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }

            if (!string.IsNullOrEmpty(user.DisplayName))
            {
                claims.Add(new Claim(DnnAuthenticationDefaults.DisplayNameClaimType, user.DisplayName));
            }

            if (user.IsSuperUser)
            {
                claims.Add(new Claim(DnnAuthenticationDefaults.SuperUserClaimType, bool.TrueString));
            }

            foreach (var role in user.Roles ?? Array.Empty<string>())
            {
                if (!string.IsNullOrEmpty(role))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            await this.authenticationCookieService.SignInAsync(context, user.Username, createPersistentCookie, claims, cookieDomain);

            if (isPortalGroupMember && !string.IsNullOrEmpty(cookieDomain))
            {
                // The portal group's shared-domain marker, read on sign-out.
                context.Response.Cookies.Append(
                    DnnAuthenticationDefaults.SiteGroupCookieName,
                    cookieDomain,
                    new CookieOptions
                    {
                        Expires = expiration,
                        Domain = cookieDomain,
                        Path = this.authenticationCookieService.CookiePath,
                        Secure = this.authenticationCookieService.RequireSsl,
                        HttpOnly = true,
                        SameSite = SameSiteMode.Lax,
                    });
            }

            if (user.IsSuperUser)
            {
                // save userinfo object in context to ensure Personalization is saved correctly
                context.Items[DnnAuthenticationDefaults.UserInfoContextKey] = user;
                DotNetNuke.Common.Globals.GetCurrentServiceProvider().GetRequiredService<LogRequestContext>()?.AddToLogContext("UserId", user.UserID);
            }

            // Identity the Login is processed by system.
            context.Items[DnnAuthenticationDefaults.UserSignInContextKey] = true;
        }

        /// <summary>
        /// Signs the current user out.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public void SignOut()
        {
            this.SignOutAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Signs the current user out.
        /// </summary>
        /// <returns>A task that completes once the authentication cookie has been expired.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public async Task SignOutAsync()
        {
            var context = HttpContextSource.CurrentOrThrow;

            InvalidateSession(context);

            // This will prevent next requests from being authenticated if using the same cookie.
            var authToken = context.User?.FindFirst(DnnAuthenticationDefaults.AuthTokenClaimType)?.Value;
            if (!string.IsNullOrEmpty(authToken))
            {
                AuthCookieController.Instance.Update(authToken, OldExpiryTime, Null.NullInteger);
            }

            // Log the user off from the cookie authentication system.
            await this.authenticationCookieService.SignOutAsync(context);

            // When the portal belongs to a portal group the cookies were issued on the shared
            // authentication domain, so they have to be expired on that same domain.
            var domain = context.Request.Cookies[DnnAuthenticationDefaults.SiteGroupCookieName];
            if (!string.IsNullOrEmpty(domain))
            {
                var sharedDomainOptions = new CookieOptions
                {
                    Expires = OldExpiryTime,
                    Domain = domain,
                    Path = this.authenticationCookieService.CookiePath,
                    Secure = this.authenticationCookieService.RequireSsl,
                };

                context.Response.Cookies.Delete(this.authenticationCookieService.CookieName, sharedDomainOptions);
                context.Response.Cookies.Delete(DnnAuthenticationDefaults.SiteGroupCookieName, sharedDomainOptions);
            }

            // Remove current userinfo from context items
            context.Items.Remove(DnnAuthenticationDefaults.UserInfoContextKey);
            DotNetNuke.Common.Globals.GetCurrentServiceProvider().GetRequiredService<LogRequestContext>()?.AddToLogContext("UserId", Null.NullInteger);

            // remove language cookie and authentication type cookie
            context.Response.Cookies.Delete("language");
            context.Response.Cookies.Delete("authentication");

            // expire cookies
            var applicationPath = !string.IsNullOrEmpty(Globals.ApplicationPath) ? Globals.ApplicationPath : "/";
            var expiredOptions = new CookieOptions { Path = applicationPath, Expires = DateTime.Now.AddYears(-30) };
            context.Response.Cookies.Delete("portalaliasid", expiredOptions);
            context.Response.Cookies.Delete("portalroles", expiredOptions);

            // clear any authentication provider tokens that match *UserToken convention e.g. FacebookUserToken ,TwitterUserToken, LiveUserToken and GoogleUserToken
            foreach (var authCookie in context.Request.Cookies.Keys)
            {
                if (authCookie.EndsWith("UserToken", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Cookies.Delete(authCookie, expiredOptions);
                }
            }
        }

        /// <summary>This function applies security filtering to the UserInput string, and reports whether the input string is valid.</summary>
        /// <param name="userInput">This is the string to be filtered.</param>
        /// <param name="filterType">Flags which designate the filters to be applied.</param>
        /// <returns><see langword="true"/> if the input is valid, otherwise <see langword="false"/>.</returns>
        public bool ValidateInput(string userInput, FilterFlag filterType)
        {
            string filteredInput = this.InputFilter(userInput, filterType);

            return userInput == filteredInput;
        }

        /// <summary>
        /// This function loops through every portal that has set its own AllowedExtensionWhitelist
        /// and checks that there are no extensions there that are restricted by the host
        ///
        /// The only time we should call this is if the host allowed extensions list has changed.
        /// </summary>
        /// <param name="newMasterList">Comma separated list of extensions that govern all users on this installation.</param>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public void CheckAllPortalFileExtensionWhitelists(string newMasterList)
        {
            var masterList = new FileExtensionWhitelist(newMasterList);
            var portalSettings = Data.DataProvider.Instance().GetPortalSettingsBySetting("AllowedExtensionsWhitelist", null);
            foreach (var portalId in portalSettings.Keys)
            {
                if (!string.IsNullOrEmpty(portalSettings[portalId]))
                {
                    var portalExts = new FileExtensionWhitelist(portalSettings[portalId]);
                    var newValue = portalExts.RestrictBy(masterList).ToStorageString();
                    if (newValue != portalSettings[portalId])
                    {
                        PortalController.UpdatePortalSetting(this.portalController, portalId, "AllowedExtensionsWhitelist", newValue, false);
                    }
                }
            }
        }

        private static void ProcessRole(UserInfo user, IPortalSettings settings, string roleName, out bool? roleAllowed)
        {
            var roleType = GetRoleType(roleName);
            switch (roleType)
            {
                case RoleType.Friend:
                    ProcessFriendRole(user, roleName, out roleAllowed);
                    break;
                case RoleType.Follower:
                    ProcessFollowerRole(user, roleName, out roleAllowed);
                    break;
                case RoleType.Owner:
                    ProcessOwnerRole(user, roleName, out roleAllowed);
                    break;
                default:
                    ProcessSecurityRole(user, settings, roleName, out roleAllowed);
                    break;
            }
        }

        private static void ProcessFriendRole(UserInfo user, string roleName, out bool? roleAllowed)
        {
            roleAllowed = null;
            var targetUser = UserController.Instance.GetUserById(user.PortalID, GetEntityFromRoleName(roleName));
            var relationShip = RelationshipController.Instance.GetFriendRelationship(user, targetUser);
            if (relationShip != null && relationShip.Status == RelationshipStatus.Accepted)
            {
                roleAllowed = true;
            }
        }

        private static void ProcessFollowerRole(UserInfo user, string roleName, out bool? roleAllowed)
        {
            roleAllowed = null;
            var targetUser = UserController.Instance.GetUserById(user.PortalID, GetEntityFromRoleName(roleName));
            var relationShip = RelationshipController.Instance.GetFollowerRelationship(user, targetUser);
            if (relationShip != null && relationShip.Status == RelationshipStatus.Accepted)
            {
                roleAllowed = true;
            }
        }

        private static void ProcessOwnerRole(UserInfo user, string roleName, out bool? roleAllowed)
        {
            roleAllowed = null;
            var entityId = GetEntityFromRoleName(roleName);
            if (entityId == user.UserID)
            {
                roleAllowed = true;
            }
        }

        private static int GetEntityFromRoleName(string roleName)
        {
            var roleParts = roleName.Split(':');
            int result;
            if (roleParts.Length > 1 && int.TryParse(roleParts[1], out result))
            {
                return result;
            }

            return Null.NullInteger;
        }

        private static void ProcessSecurityRole(UserInfo user, IPortalSettings settings, string roleName, out bool? roleAllowed)
        {
            roleAllowed = null;

            // permissions strings are encoded with Deny permissions at the beginning and Grant permissions at the end for optimal performance
            if (!string.IsNullOrEmpty(roleName))
            {
                // Deny permission
                if (roleName.StartsWith("!", StringComparison.Ordinal))
                {
                    // Portal Admin cannot be denied from his/her portal (so ignore deny permissions if user is portal admin)
                    if (settings != null && !(settings.PortalId == user.PortalID && user.IsInRole(settings.AdministratorRoleName)))
                    {
                        string denyRole = roleName.Replace("!", string.Empty);
                        if (denyRole == Globals.glbRoleAllUsersName || user.IsInRole(denyRole))
                        {
                            roleAllowed = false;
                        }
                    }
                }

                // Grant permission
                else
                {
                    if (roleName == Globals.glbRoleAllUsersName || user.IsInRole(roleName))
                    {
                        roleAllowed = true;
                    }
                }
            }
        }

        private static RoleType GetRoleType(string roleName)
        {
            if (roleName.StartsWith(RoleFriendPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                return RoleType.Friend;
            }

            if (roleName.StartsWith(RoleFollowerPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                return RoleType.Follower;
            }

            if (roleName.StartsWith(RoleOwnerPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                return RoleType.Owner;
            }

            return RoleType.Security;
        }

        private static string BytesToHexString(IEnumerable<byte> bytes)
        {
            var hexString = new StringBuilder();
            foreach (var b in bytes)
            {
                hexString.Append(string.Format(CultureInfo.InvariantCulture, "{0:X2}", b));
            }

            return hexString.ToString();
        }

        /// <summary>
        /// This function uses Regex search strings to remove HTML tags which are
        /// targeted in Cross-site scripting (XSS) attacks.  This function will evolve
        /// to provide more robust checking as additional holes are found.
        /// </summary>
        /// <param name="strInput">This is the string to be filtered.</param>
        /// <returns>Filtered UserInput.</returns>
        /// <remarks>
        /// This is a private function that is used internally by the <see cref="InputFilter"/> function.
        /// </remarks>
        private static string FilterStrings(string strInput)
        {
            // setup up list of search terms as items may be used twice
            var tempInput = strInput;
            if (string.IsNullOrWhiteSpace(tempInput))
            {
                return tempInput;
            }

            const string replacement = " ";

            // remove the js event from html tags
            var tagMatches = DangerElementsRegex.Matches(tempInput);
            foreach (Match match in tagMatches)
            {
                var tagContent = match.Value;
                var cleanTagContent = DangerElementContentRegex.Replace(tagContent, string.Empty);
                tempInput = tempInput.Replace(tagContent, cleanTagContent);
            }

            // check if text contains encoded angle brackets, if it does it we decode it to check the plain text
            if (tempInput.Contains("&gt;") || tempInput.Contains("&lt;") || tempInput.Contains("&#"))
            {
                // text is encoded, so decode and try again
                tempInput = WebUtility.HtmlDecode(tempInput);
                tempInput = RxListStrings.Aggregate(tempInput, (current, s) => s.Replace(current, replacement));

                // Re-encode
                tempInput = WebUtility.HtmlEncode(tempInput);
            }
            else
            {
                tempInput = RxListStrings.Aggregate(tempInput, (current, s) => s.Replace(current, replacement));
            }

            // Final pass: when the input actually carries markup, hand it to the platform's
            // HTML sanitizer (HtmlSanitizer 9.x, via HtmlUtils) so that script elements,
            // inline event handlers and javascript: URLs are removed by a real HTML parser
            // rather than by pattern matching alone. Plain text is left untouched, which keeps
            // the callers that compare filtered input against the original (user names, for
            // instance) behaving exactly as before.
            if (IncludesMarkup(tempInput))
            {
                tempInput = HtmlUtils.CleanOutOfJavascript(tempInput);
            }

            return tempInput;
        }

        /// <summary>This filter removes angle brackets i.e.</summary>
        /// <param name="strInput">This is the string to be filtered.</param>
        /// <returns>Filtered UserInput.</returns>
        /// <remarks>This is a private function that is used internally by the <see cref="InputFilter"/> function.</remarks>
        private static string FormatAngleBrackets(string strInput)
        {
            var tempInput = new StringBuilder(strInput).Replace("<", string.Empty).Replace(">", string.Empty);
            return tempInput.ToString();
        }

        /// <summary>This filter removes CrLf characters and inserts br.</summary>
        /// <param name="strInput">This is the string to be filtered.</param>
        /// <returns>Filtered UserInput.</returns>
        /// <remarks>This is a private function that is used internally by the <see cref="InputFilter"/> function.</remarks>
        private static string FormatMultiLine(string strInput)
        {
            const string lbreak = "<br />";
            var tempInput = new StringBuilder(strInput).Replace("\r\n", lbreak).Replace("\n", lbreak).Replace("\r", lbreak);
            return tempInput.ToString();
        }

        /// <summary>This function verifies raw SQL statements to prevent SQL injection attacks and replaces a similar function (PreventSQLInjection) from the Common.Globals.vb module.</summary>
        /// <param name="strSQL">This is the string to be filtered.</param>
        /// <returns>Filtered UserInput.</returns>
        /// <remarks>This is a private function that is used internally by the <see cref="InputFilter"/> function.</remarks>
        private static string FormatRemoveSQL(string strSQL)
        {
            // Check for forbidden T-SQL commands. Use word boundaries to filter only real statements.
            return BadStatementRegex.Replace(strSQL, " ").Replace("'", "''");
        }

        /// <summary>This function removes control characters from the string.</summary>
        /// <param name="str">This is the string to be filtered.</param>
        /// <returns>Filtered UserInput.</returns>
        /// <remarks>This is a private function that is used internally by the <see cref="InputFilter"/> function.</remarks>
        private static string FormatRemoveControlCharacters(string str)
        {
            return ControlCharactersRegex.Replace(ControlCharacterToWhitespaceRegex.Replace(str, " "), string.Empty);
        }

        /// <summary>This function determines if the Input string contains any markup.</summary>
        /// <param name="strInput">This is the string to be checked.</param>
        /// <returns>True if string contains Markup tag(s).</returns>
        /// <remarks>This is a private function that is used internally by the <see cref="InputFilter"/> function.</remarks>
        private static bool IncludesMarkup(string strInput)
        {
            return StripTagsRegex.IsMatch(strInput);
        }

        /// <summary>Clears the current session so that a new one is issued.</summary>
        /// <param name="context">The current request.</param>
        /// <remarks>
        /// ASP.NET Core sessions have no <c>Abandon</c>: clearing the session and dropping the
        /// session cookie is the equivalent, and it is a no-op when the host has not enabled
        /// session state at all.
        /// </remarks>
        private static void InvalidateSession(HttpContext context)
        {
            if (context.Features.Get<ISessionFeature>() is null)
            {
                // Session state is not enabled for this application.
                return;
            }

            var session = context.Session;
            if (session is null || !session.IsAvailable)
            {
                return;
            }

            session.Clear();
        }
    }
}
