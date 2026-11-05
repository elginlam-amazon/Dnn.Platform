// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Security.Membership
{
    using System;

    /// <summary>
    /// Thrown when the membership store rejects an operation. Replaces
    /// <c>System.Configuration.Provider.ProviderException</c>, which the ASP.NET membership
    /// provider used to raise and which is not available on modern .NET.
    /// </summary>
    public class MembershipStoreException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="MembershipStoreException"/> class.</summary>
        public MembershipStoreException()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MembershipStoreException"/> class.</summary>
        /// <param name="message">The error message.</param>
        public MembershipStoreException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MembershipStoreException"/> class.</summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The underlying error.</param>
        public MembershipStoreException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
