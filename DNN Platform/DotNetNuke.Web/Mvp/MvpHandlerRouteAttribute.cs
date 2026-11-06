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

    /// <summary>Declares the path a Model-View-Presenter handler or web service answers on.</summary>
    /// <remarks>
    /// <para>
    /// Replaces the <c>&lt;httpHandlers&gt;</c>/<c>&lt;handlers&gt;</c> registration of an
    /// <c>.ashx</c> and the physical presence of an <c>.asmx</c> file. Declare the legacy path so
    /// existing callers keep working:
    /// </para>
    /// <code>
    /// [MvpHandlerRoute("~/DesktopModules/MyModule/Thumbnail.ashx")]
    /// public class ThumbnailHandler : HttpHandlerView { }
    /// </code>
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public partial class MvpHandlerRouteAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="MvpHandlerRouteAttribute"/> class.</summary>
        /// <param name="path">
        /// The request path, which may be written as an application relative virtual path
        /// (<c>~/DesktopModules/…</c>) or as an absolute path (<c>/DesktopModules/…</c>).
        /// </param>
        public MvpHandlerRouteAttribute(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.Path = path;
        }

        /// <summary>Gets the declared request path.</summary>
        public string Path { get; }

        /// <summary>
        /// Gets or sets the HTTP methods this route accepts, e.g. <c>"GET,POST"</c>. All methods are
        /// accepted when it is not set, which is what an HTTP handler did.
        /// </summary>
        public string Methods { get; set; }

        /// <summary>Gets the route pattern for this path.</summary>
        /// <returns>The absolute path, without the application relative <c>~</c>.</returns>
        public string GetRoutePattern()
        {
            var pattern = this.Path.Trim();
            if (pattern.StartsWith('~'))
            {
                pattern = pattern.Substring(1);
            }

            return pattern.StartsWith('/') ? pattern : "/" + pattern;
        }

        /// <summary>Gets the HTTP methods this route accepts.</summary>
        /// <returns>The methods, or an empty array when every method is accepted.</returns>
        public string[] GetMethods()
        {
            return string.IsNullOrWhiteSpace(this.Methods)
                ? Array.Empty<string>()
                : this.Methods.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
