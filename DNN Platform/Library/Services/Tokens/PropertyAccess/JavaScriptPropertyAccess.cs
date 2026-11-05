// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable once CheckNamespace
namespace DotNetNuke.Services.Tokens
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Web.Client;
    using DotNetNuke.Web.Client.ClientResourceManagement;
    using Microsoft.AspNetCore.Http;

    /// <summary>Property Access implementation for javascript registration.</summary>
    public class JavaScriptPropertyAccess : JsonPropertyAccess<JavaScriptDto>
    {
        private readonly HttpContext context;

        /// <summary>Initializes a new instance of the <see cref="JavaScriptPropertyAccess"/> class.</summary>
        /// <param name="context">The current request.</param>
        /// <remarks>Was <c>JavaScriptPropertyAccess(System.Web.UI.Page)</c>; client resources are registered per request now.</remarks>
        public JavaScriptPropertyAccess(HttpContext context)
        {
            this.context = context;
        }

        /// <inheritdoc />
        protected override string ProcessToken(JavaScriptDto model, UserInfo accessingUser, Scope accessLevel)
        {
            if (string.IsNullOrEmpty(model.JsName) && string.IsNullOrEmpty(model.Path))
            {
                throw new ArgumentException("If the jsname property is not specified then the JavaScript token must specify a path.");
            }

            if (model.Priority == 0)
            {
                model.Priority = (int)FileOrder.Js.DefaultPriority;
            }

            if (string.IsNullOrEmpty(model.Path))
            {
                RegisterInstalledLibrary(model);
            }
            else
            {
                ClientResourceManager.RegisterScript(
                    this.context,
                    model.Path,
                    model.Priority,
                    model.Provider ?? string.Empty,
                    model.JsName ?? string.Empty,
                    model.Version ?? string.Empty,
                    model.HtmlAttributes);
            }

            return string.Empty;
        }

        private static void RegisterInstalledLibrary(JavaScriptDto model)
        {
            Version version = null;
            var specific = SpecificVersion.Latest;
            if (!string.IsNullOrEmpty(model.Version))
            {
                version = new Version(model.Version);

                if (!string.IsNullOrEmpty(model.Specific))
                {
                    switch (model.Specific)
                    {
                        case "Exact":
                            specific = SpecificVersion.Exact;
                            break;
                        case "LatestMajor":
                            specific = SpecificVersion.LatestMajor;
                            break;
                        case "LatestMinor":
                            specific = SpecificVersion.LatestMinor;
                            break;
                        default:
                            specific = SpecificVersion.Latest;
                            break;
                    }
                }
            }

            JavaScript.RequestRegistration(model.JsName, version, specific);
        }
    }
}
