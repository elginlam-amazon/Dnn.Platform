// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DNNConnect.CKEditorProvider.Services
{
    using System.Net;
    using System.Net.Http;

    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>WebAPI controller to enable the CKE cloudservices plugin.</summary>
    public class CloudServicesController : DnnApiController
    {
        /// <summary>Provides a fake token the cloudservices plugin needs.</summary>
        /// <returns>A fake token.</returns>
        [AllowAnonymous]
        [HttpGet]
        public HttpResponseMessage GetToken()
        {
            return this.Request.CreateResponse(HttpStatusCode.OK, "faketoken");
        }
    }
}
