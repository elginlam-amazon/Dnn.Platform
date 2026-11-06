// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    /// <summary>
    /// Implementing this interface on an Auth filter will allow the filter to override the default
    /// Host level auth provided by DnnController.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mere presence of this interface acts as a flag. It is honored by
    /// <c>DotNetNuke.Web.Api.Internal.DnnActionFilterProvider</c>, the ASP.NET Core
    /// <c>IFilterProvider</c> which replaced the Web API 2 filter provider: unless one of the filters
    /// contributed for the action implements this interface, the provider appends a
    /// <see cref="RequireHostAttribute"/>. That is the services framework's default-deny, and it means
    /// an endpoint with no explicit authorization attribute is reachable by super-users only.
    /// </para>
    /// <para>
    /// The implementors are <see cref="DnnAuthorizeAttribute"/>,
    /// <see cref="DnnModuleAuthorizeAttribute"/>, <see cref="ApiTokenAuthorizeAttribute"/>,
    /// <c>Internal.DnnPageEditorAttribute</c> and <c>Internal.DnnPagePermissionAttribute</c>.
    /// </para>
    /// </remarks>
    public interface IOverrideDefaultAuthLevel
    {
        // no need for methods the mere presence of this interface acts as a flag to OverridableHostAuthFilter
    }
}
