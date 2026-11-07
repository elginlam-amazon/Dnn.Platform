// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api
{
    using System;

    /// <summary>Provides Dnn specific details for authorization filter.</summary>
    /// <remarks>
    /// The implementation moved to <see cref="DnnAuthorizeBaseAttribute"/> (the name used by the
    /// <c>DotNetNuke.Web.Mvc</c> host surface). This type is kept, deriving from it, because it is part of
    /// the public contract: third-party modules declare their authorization attributes as
    /// <c>: AuthorizeAttributeBase</c> and override <see cref="DnnAuthorizeBaseAttribute.IsAuthorized"/>.
    /// Those subclasses keep compiling and keep behaving identically.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public abstract class AuthorizeAttributeBase : DnnAuthorizeBaseAttribute
    {
    }
}
