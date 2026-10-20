// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Security
{
    /// <summary>The exception that is thrown when a password cannot be retrieved from the password store.</summary>
    [Serializable]
    public class MembershipPasswordException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="MembershipPasswordException"/> class.</summary>
        public MembershipPasswordException()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MembershipPasswordException"/> class.</summary>
        /// <param name="message">The message.</param>
        public MembershipPasswordException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MembershipPasswordException"/> class.</summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public MembershipPasswordException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
