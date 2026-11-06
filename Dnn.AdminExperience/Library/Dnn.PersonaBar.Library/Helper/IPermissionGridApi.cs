// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.PersonaBar.Library.Helper
{
    using Microsoft.AspNetCore.Mvc;

    /// <summary>Contract for the permission grid service endpoints.</summary>
    /// <remarks>
    /// The members returned a Web API 2 <c>HttpResponseMessage</c>, which is a client type on
    /// ASP.NET Core; they now return <see cref="IActionResult"/>, matching the migrated
    /// <c>DnnApiController</c> surface. The member names are unchanged.
    /// </remarks>
    public interface IPermissionGridApi
    {
        // TODO: ? IActionResult GetPermissionsData();

        /// <summary>Returns all roles/role groups info.</summary>
        /// <returns>An <see cref="IActionResult"/> with the roles.</returns>
        IActionResult GetRoles();
    }
}
