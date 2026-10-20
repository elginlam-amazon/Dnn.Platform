// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Security
{
    /// <summary>Describes the encryption format for storing passwords for membership users.</summary>
    public enum MembershipPasswordFormat
    {
        /// <summary>Passwords are not encrypted.</summary>
        Clear = 0,

        /// <summary>Passwords are encrypted one-way using a hashing algorithm.</summary>
        Hashed = 1,

        /// <summary>Passwords are encrypted using the machine key.</summary>
        Encrypted = 2,
    }
}
