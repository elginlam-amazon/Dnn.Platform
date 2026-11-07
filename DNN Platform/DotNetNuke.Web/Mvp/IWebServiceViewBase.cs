// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvp
{
    using System.Threading.Tasks;

    using DotNetNuke.Internal.SourceGenerators;

    using Microsoft.AspNetCore.Http;

    /// <summary>Represents a class that is a view for a web service in a Model-View-Presenter application.</summary>
    /// <remarks>
    /// Replaces the WebFormsMvp view contract of an <c>.asmx</c> web service. There is no SOAP
    /// consumer of these services inside the solution, so the operations are exposed as REST
    /// (JSON over HTTP) endpoints: one operation per route segment, mapped by
    /// <see cref="MvpEndpointRouteBuilderExtensions.MapDnnMvpEndpoints"/>.
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public partial interface IWebServiceViewBase : IMvpView
    {
        /// <summary>Invokes one of this service's operations.</summary>
        /// <param name="operation">
        /// The name of the operation, which is the name of the method marked with
        /// <see cref="MvpServiceOperationAttribute"/>.
        /// </param>
        /// <param name="context">The context of the request.</param>
        /// <returns>The value the operation returned, which is serialized as the JSON response.</returns>
        /// <remarks>Replaces the SOAP dispatch an <c>.asmx</c> handler performed over <c>[WebMethod]</c> members.</remarks>
        Task<object> InvokeAsync(string operation, HttpContext context);
    }
}
