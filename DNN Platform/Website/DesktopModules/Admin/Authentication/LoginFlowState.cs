// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Modules.Admin.Authentication
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Security.Membership;

    using Microsoft.AspNetCore.Http;

    /// <summary>
    /// The state the login flow carries from one request to the next: which "panel" is showing and
    /// everything an external authentication provider told us about the user before a DNN account
    /// existed for them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the replacement for the <c>ViewState</c> slots of <c>Login.ascx.cs</c>
    /// (<c>PageNo</c>, <c>AuthenticationType</c>, <c>AutoRegister</c>, <c>ProfileProperties</c>,
    /// <c>RememberMe</c>, <c>LoginStatus</c>, <c>UserToken</c>, <c>UserName</c>). ViewState does
    /// not exist in Blazor, and a sign-in has to happen on a real HTTP request, so the login
    /// control round-trips through form posts exactly as the WebForms control round-tripped
    /// through postbacks — which means this state has to survive a request boundary.
    /// </para>
    /// <para>
    /// It is kept in the <b>server-side session</b>, not in hidden fields. WebForms ViewState was
    /// MAC-protected; a plain hidden field is not, and <see cref="UserToken"/> is the value
    /// <c>AuthenticationController.AddUserAuthentication</c> uses to bind an external identity to a
    /// DNN account. Letting a browser choose it would let anyone claim another provider's identity,
    /// so it never leaves the server.
    /// </para>
    /// <para>
    /// The state is scoped by module id, so two login modules on one page do not share it (the
    /// per-control isolation ViewState gave).
    /// </para>
    /// </remarks>
    public sealed class LoginFlowState
    {
        private const string SessionKeyPrefix = "DNN_LoginFlow_";

        /// <summary>Gets or sets the current page ("panel") number: 0 login, 1 register/associate, 2 password, 3 profile, 4 data consent.</summary>
        public int PageNo { get; set; }

        /// <summary>Gets or sets the current authentication type.</summary>
        public string AuthenticationType { get; set; } = Null.NullString;

        /// <summary>Gets or sets a value indicating whether the user should be automatically registered.</summary>
        public bool AutoRegister { get; set; } = Null.NullBoolean;

        /// <summary>Gets or sets a value indicating whether a permanent auth cookie should be created.</summary>
        public bool RememberMe { get; set; } = Null.NullBoolean;

        /// <summary>Gets or sets the login status.</summary>
        public UserLoginStatus LoginStatus { get; set; } = UserLoginStatus.LOGIN_FAILURE;

        /// <summary>Gets or sets the current user token.</summary>
        public string UserToken { get; set; } = string.Empty;

        /// <summary>Gets or sets the current user name.</summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>Gets or sets the user id the flow is working on, or <see cref="Null.NullInteger"/> (-1) when there is none.</summary>
        /// <remarks>
        /// -1 (<see cref="Null.NullInteger"/>) is the "not set" sentinel the WebForms control used;
        /// it is preserved exactly, because 0 is a legal user id.
        /// </remarks>
        public int UserId { get; set; } = Null.NullInteger;

        /// <summary>Gets or sets the profile properties an authentication provider returned.</summary>
        public NameValueCollection ProfileProperties { get; set; } = new NameValueCollection();

        /// <summary>
        /// Gets or sets a value indicating whether the flow must re-run <c>ValidateUser</c> on the
        /// next request.
        /// </summary>
        /// <remarks>
        /// The WebForms control continued the login inline when a sub-panel (change password,
        /// update profile, data consent) completed, because a postback and the sign-in it led to
        /// were the same request. In Blazor those panels complete on an interactive circuit, where
        /// HttpContext is <see langword="null"/> and the authentication cookie therefore cannot be
        /// written. The continuation is deferred by one request instead: the panel sets this flag
        /// and forces a navigation, and the resulting request — which has a live HttpContext and an
        /// unstarted response — runs the same <c>ValidateUser</c> call with the same arguments.
        /// Nothing about the decision changes, only where it executes.
        /// </remarks>
        public bool PendingValidate { get; set; }

        /// <summary>Gets or sets a value indicating whether the pending <c>ValidateUser</c> must pass <c>ignoreExpiring</c>.</summary>
        public bool PendingValidateIgnoreExpiring { get; set; }

        /// <summary>Gets or sets the module messages that were raised just before a deferred continuation.</summary>
        /// <remarks>
        /// A module message added to the skin is lost when the request ends in a redirect, so the
        /// messages the WebForms control showed alongside its inline continuation travel here and
        /// are re-raised on the next request.
        /// </remarks>
        public List<PendingMessage> PendingMessages { get; set; } = new List<PendingMessage>();

        /// <summary>Loads the state for the given module from the session.</summary>
        /// <param name="moduleId">The module id the state belongs to.</param>
        /// <returns>The stored state, or a new instance with the WebForms defaults when there is none.</returns>
        public static LoginFlowState Load(int moduleId)
        {
            var session = GetSession();
            if (session == null)
            {
                // No request (an interactive render): the caller gets the defaults, which is the
                // same thing a page with no ViewState for these keys got.
                return new LoginFlowState();
            }

            var json = session.GetString(Key(moduleId));
            if (string.IsNullOrEmpty(json))
            {
                return new LoginFlowState();
            }

            try
            {
                var dto = JsonSerializer.Deserialize<LoginFlowStateDto>(json);
                if (dto == null)
                {
                    return new LoginFlowState();
                }

                var state = new LoginFlowState
                {
                    PageNo = dto.PageNo,
                    AuthenticationType = dto.AuthenticationType ?? Null.NullString,
                    AutoRegister = dto.AutoRegister,
                    RememberMe = dto.RememberMe,
                    LoginStatus = dto.LoginStatus,
                    UserToken = dto.UserToken ?? string.Empty,
                    UserName = dto.UserName ?? string.Empty,
                    UserId = dto.UserId,
                    PendingValidate = dto.PendingValidate,
                    PendingValidateIgnoreExpiring = dto.PendingValidateIgnoreExpiring,
                    PendingMessages = dto.PendingMessages ?? new List<PendingMessage>(),
                };

                if (dto.ProfileProperties != null)
                {
                    foreach (var pair in dto.ProfileProperties)
                    {
                        state.ProfileProperties[pair.Key] = pair.Value;
                    }
                }

                return state;
            }
            catch (JsonException)
            {
                // A session value we cannot read is treated as absent rather than fatal, exactly as
                // an unreadable ViewState slot fell back to the default.
                return new LoginFlowState();
            }
        }

        /// <summary>Removes the state for the given module from the session.</summary>
        /// <param name="moduleId">The module id the state belongs to.</param>
        public static void Clear(int moduleId)
        {
            GetSession()?.Remove(Key(moduleId));
        }

        /// <summary>Stores this state for the given module in the session.</summary>
        /// <param name="moduleId">The module id the state belongs to.</param>
        public void Save(int moduleId)
        {
            var session = GetSession();
            if (session == null)
            {
                return;
            }

            var dto = new LoginFlowStateDto
            {
                PageNo = this.PageNo,
                AuthenticationType = this.AuthenticationType,
                AutoRegister = this.AutoRegister,
                RememberMe = this.RememberMe,
                LoginStatus = this.LoginStatus,
                UserToken = this.UserToken,
                UserName = this.UserName,
                UserId = this.UserId,
                PendingValidate = this.PendingValidate,
                PendingValidateIgnoreExpiring = this.PendingValidateIgnoreExpiring,
                PendingMessages = this.PendingMessages,
                ProfileProperties = this.ProfileProperties.AllKeys
                    .Where(key => key != null)
                    .ToDictionary(key => key, key => this.ProfileProperties[key]),
            };

            session.SetString(Key(moduleId), JsonSerializer.Serialize(dto));
        }

        private static string Key(int moduleId)
            => SessionKeyPrefix + moduleId.ToString(CultureInfo.InvariantCulture);

        private static ISession GetSession()
        {
            var context = HttpContextSource.Current;
            if (context == null)
            {
                return null;
            }

            try
            {
                return context.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>() == null
                    ? null
                    : context.Session;
            }
            catch (InvalidOperationException)
            {
                // Session was not configured for this request.
                return null;
            }
        }

        /// <summary>A module message that has to survive a deferred continuation.</summary>
        public sealed class PendingMessage
        {
            /// <summary>Gets or sets the message text, or the resource key when <see cref="Localized"/> is <see langword="false"/>.</summary>
            public string Message { get; set; }

            /// <summary>Gets or sets the message type, as the integer value of <c>ModuleMessage.ModuleMessageType</c>.</summary>
            public int MessageType { get; set; }

            /// <summary>Gets or sets a value indicating whether <see cref="Message"/> is already localized text.</summary>
            public bool Localized { get; set; }
        }

        /// <summary>The serialization shape of <see cref="LoginFlowState"/>.</summary>
        private sealed class LoginFlowStateDto
        {
            public int PageNo { get; set; }

            public string AuthenticationType { get; set; }

            public bool AutoRegister { get; set; }

            public bool RememberMe { get; set; }

            public UserLoginStatus LoginStatus { get; set; }

            public string UserToken { get; set; }

            public string UserName { get; set; }

            public int UserId { get; set; }

            public bool PendingValidate { get; set; }

            public bool PendingValidateIgnoreExpiring { get; set; }

            public List<PendingMessage> PendingMessages { get; set; }

            public Dictionary<string, string> ProfileProperties { get; set; }
        }
    }
}
