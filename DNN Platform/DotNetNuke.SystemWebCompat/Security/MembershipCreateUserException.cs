// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Security
{
    /// <summary>The exception that is thrown when a user is not successfully created by a membership provider.</summary>
    [Serializable]
    public class MembershipCreateUserException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="MembershipCreateUserException"/> class.</summary>
        public MembershipCreateUserException()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MembershipCreateUserException"/> class.</summary>
        /// <param name="statusCode">The status code.</param>
        public MembershipCreateUserException(MembershipCreateStatus statusCode)
            : base(statusCode.ToString())
        {
            this.StatusCode = statusCode;
        }

        /// <summary>Initializes a new instance of the <see cref="MembershipCreateUserException"/> class.</summary>
        /// <param name="message">The message.</param>
        public MembershipCreateUserException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MembershipCreateUserException"/> class.</summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public MembershipCreateUserException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>Gets the status of the create operation.</summary>
        public MembershipCreateStatus StatusCode { get; }
    }
}
