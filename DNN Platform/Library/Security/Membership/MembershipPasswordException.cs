// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Security.Membership
{
    using System;

    /// <summary>
    /// Thrown when a password operation fails because the supplied password answer is incorrect.
    /// Replaces <c>System.Web.Security.MembershipPasswordException</c>, which the ASP.NET membership
    /// provider used to raise from <c>GetPassword</c> and <c>ResetPassword</c> and which is not
    /// available on modern .NET.
    /// </summary>
    /// <remarks>
    /// This derives from <see cref="MembershipStoreException"/> so that callers which already catch
    /// the broader store exception keep working unchanged, while the password-reset and
    /// change-password UIs can continue to catch the narrow case and show their
    /// "invalid password answer" message rather than a generic failure. Keeping the two distinct is
    /// a behavioural requirement, not a tidiness one: an incorrect password answer must not be
    /// reported the same way as a missing user or a locked-out account.
    /// </remarks>
    public class MembershipPasswordException : MembershipStoreException
    {
        /// <summary>Initializes a new instance of the <see cref="MembershipPasswordException"/> class.</summary>
        public MembershipPasswordException()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MembershipPasswordException"/> class.</summary>
        /// <param name="message">The error message.</param>
        public MembershipPasswordException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MembershipPasswordException"/> class.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The underlying error.</param>
        public MembershipPasswordException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
