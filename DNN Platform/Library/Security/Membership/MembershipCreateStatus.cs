// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Security.Membership
{
    /// <summary>
    /// The result of creating a user in the membership store. Replaces
    /// <c>System.Web.Security.MembershipCreateStatus</c>, which does not exist on modern .NET.
    /// The member names and meanings are identical so that existing mapping code is unchanged.
    /// </summary>
    public enum MembershipCreateStatus
    {
        /// <summary>The user was created successfully.</summary>
        Success = 0,

        /// <summary>The user name was invalid.</summary>
        InvalidUserName = 1,

        /// <summary>The password was invalid.</summary>
        InvalidPassword = 2,

        /// <summary>The password question was invalid.</summary>
        InvalidQuestion = 3,

        /// <summary>The password answer was invalid.</summary>
        InvalidAnswer = 4,

        /// <summary>The e-mail address was invalid.</summary>
        InvalidEmail = 5,

        /// <summary>A user with that user name already exists.</summary>
        DuplicateUserName = 6,

        /// <summary>A user with that e-mail address already exists.</summary>
        DuplicateEmail = 7,

        /// <summary>The user was rejected by the store.</summary>
        UserRejected = 8,

        /// <summary>The supplied provider user key was invalid.</summary>
        InvalidProviderUserKey = 9,

        /// <summary>A user with that provider user key already exists.</summary>
        DuplicateProviderUserKey = 10,

        /// <summary>The store reported an unexpected error.</summary>
        ProviderError = 11,
    }
}
