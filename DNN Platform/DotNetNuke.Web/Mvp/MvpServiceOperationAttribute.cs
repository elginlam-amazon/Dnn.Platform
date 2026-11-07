// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    using DotNetNuke.Framework.Reflections;
    using DotNetNuke.Internal.SourceGenerators;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Marks a method of a <see cref="WebServiceViewBase"/> as a callable operation.</summary>
    /// <remarks>
    /// Replaces <c>System.Web.Services.WebMethodAttribute</c>. The method is reachable at
    /// <c>{route}/{name}</c> and its return value is written as the JSON response.
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public partial class MvpServiceOperationAttribute : Attribute
    {
        /// <summary>Gets or sets the name of the operation, which defaults to the method's name.</summary>
        public string Name { get; set; }
    }
}
