// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Security
{
    /// <summary>Describes the result of a <see cref="Membership.CreateUser(string, string)"/> operation.</summary>
    public enum MembershipCreateStatus
    {
        /// <summary>The user was successfully created.</summary>
        Success = 0,

        /// <summary>The user name was not found in the database.</summary>
        InvalidUserName = 1,

        /// <summary>The password is not formatted correctly.</summary>
        InvalidPassword = 2,

        /// <summary>The password question is not formatted correctly.</summary>
        InvalidQuestion = 3,

        /// <summary>The password answer is not formatted correctly.</summary>
        InvalidAnswer = 4,

        /// <summary>The email address is not formatted correctly.</summary>
        InvalidEmail = 5,

        /// <summary>The user name already exists in the database for the application.</summary>
        DuplicateUserName = 6,

        /// <summary>The email address already exists in the database for the application.</summary>
        DuplicateEmail = 7,

        /// <summary>The user was not created, for a reason defined by the provider.</summary>
        UserRejected = 8,

        /// <summary>The provider user key is of an invalid type or format.</summary>
        InvalidProviderUserKey = 9,

        /// <summary>The provider user key already exists in the database for the application.</summary>
        DuplicateProviderUserKey = 10,

        /// <summary>The provider returned an error that is not described by other values.</summary>
        ProviderError = 11,
    }
}
