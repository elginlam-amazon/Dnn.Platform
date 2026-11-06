// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Razor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;

    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Web.Razor.Helpers;

    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Razor;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A razor web page model.</summary>
    /// <remarks>
    /// Was a <c>System.Web.WebPages.WebPageBase</c>; it is now an ASP.NET Core Razor page
    /// (<see cref="RazorPage{TModel}"/> of <see langword="dynamic"/>), which is the replacement for
    /// the Web Pages rendering pipeline. Module <c>.cshtml</c>/<c>.vbhtml</c> files that declare
    /// <c>@inherits DotNetNuke.Web.Razor.DotNetNukeWebPage</c> keep compiling against this type.
    /// </remarks>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public abstract partial class DotNetNukeWebPage : RazorPage<dynamic>
    {
        private dynamic model;
        private IDictionary<object, dynamic> pageData;
        private RazorRequestValues requestValues;

        /// <summary>Gets or sets the rendering context of this page.</summary>
        /// <remarks>Replaces <c>WebPageBase.PageContext</c>.</remarks>
        public RazorPageContext PageContext { get; set; }

        /// <summary>Gets or sets the virtual directory of the razor file this page was compiled from.</summary>
        /// <remarks>Replaces <c>WebPageBase.VirtualPath</c>.</remarks>
        public string VirtualPath { get; set; }

        /// <summary>Gets or sets the model of this page.</summary>
        public new dynamic Model
        {
            get { return this.model ?? (this.model = this.PageContext != null ? this.PageContext.Model : this.ViewData?.Model); }
            set { this.model = value; }
        }

        /// <summary>Gets the DNN context helper.</summary>
        protected internal DnnHelper Dnn { get; internal set; }

        /// <summary>Gets the HTML helper.</summary>
        protected internal HtmlHelper Html { get; internal set; }

        /// <summary>Gets the URL helper.</summary>
        protected internal UrlHelper Url { get; internal set; }

        /// <summary>Gets a value indicating whether the current request is a POST.</summary>
        /// <remarks>
        /// Replaces <c>WebPageBase.IsPost</c>. Scripts rendered outside of a request (no
        /// <see cref="ViewContext"/>/<c>HttpContext</c> yet) report <see langword="false"/>, which
        /// is what the net48 property did when there was no current request either.
        /// </remarks>
        protected bool IsPost
        {
            get
            {
                var method = this.ViewContext?.HttpContext?.Request?.Method;
                return method != null && HttpMethods.IsPost(method);
            }
        }

        /// <summary>Gets the data shared between this page and the pages it renders.</summary>
        /// <remarks>
        /// Replaces <c>WebPageBase.PageData</c>. The dictionary is shared with child pages by
        /// <see cref="ConfigurePage"/>, so a layout (or a page rendered through
        /// <see cref="RenderPage"/>) reads what the content page wrote. Reading a key that was
        /// never written returns <see langword="null"/> rather than throwing, as it did on net48 —
        /// see <see cref="RazorPageDataDictionary"/>.
        /// </remarks>
        protected IDictionary<object, dynamic> PageData
        {
            get { return this.pageData ??= new RazorPageDataDictionary(); }
        }

        /// <summary>Gets indexed access to the values of the current request.</summary>
        /// <remarks>
        /// Replaces <c>WebPage.Request</c>, whose <c>Request["key"]</c> indexer has no equivalent
        /// on ASP.NET Core's <see cref="HttpRequest"/>. See <see cref="RazorRequestValues"/>.
        /// </remarks>
        protected RazorRequestValues Request
        {
            get { return this.requestValues ??= new RazorRequestValues(this.ViewContext?.HttpContext); }
        }

        /// <summary>Renders another razor file and returns its markup.</summary>
        /// <param name="path">
        /// The path of the file to render, either relative to this page's own folder
        /// (<c>"Shared/_Header.cshtml"</c>, <c>"_Header2.cshtml"</c>) or application-rooted
        /// (<c>"~/DesktopModules/.../_Header.cshtml"</c>).
        /// </param>
        /// <returns>The rendered markup.</returns>
        /// <remarks>
        /// Replaces <c>WebPageBase.RenderPage(string, params object[])</c>. Razor renders the
        /// returned <see cref="IHtmlContent"/> as-is, so <c>@RenderPage("Shared/_Header.cshtml")</c>
        /// keeps working in a module script.
        /// </remarks>
        protected IHtmlContent RenderPage(string path)
        {
            // Razor templates call @RenderPage(...) from a synchronous expression, so the
            // asynchronous execution of the child page has to be bridged. The work is pushed onto
            // the thread pool (as RazorEngine.ExecuteSynchronously does) so that an awaited
            // continuation can never queue behind the blocked thread.
            return Task.Run(() => this.RenderPageAsync(path)).GetAwaiter().GetResult();
        }

        /// <summary>Renders another razor file and returns its markup.</summary>
        /// <param name="path">The path of the file to render, relative to this page's folder or application-rooted.</param>
        /// <returns>The rendered markup.</returns>
        /// <remarks>
        /// The child page is compiled through <see cref="IRazorPageFactoryProvider"/> exactly as
        /// <c>RazorEngine</c> compiles the root script, is configured from this page through
        /// <see cref="ConfigurePage"/> (so it inherits <see cref="PageContext"/>,
        /// <see cref="ViewContext"/>, <see cref="Dnn"/>, <see cref="Html"/>, <see cref="Url"/> and
        /// <see cref="PageData"/>) and is executed into a buffered writer of its own so that its
        /// output is returned rather than written straight into this page's output.
        /// </remarks>
        protected async Task<HtmlString> RenderPageAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A path is required to render a page.", nameof(path));
            }

            var viewContext = this.ViewContext ?? throw new InvalidOperationException(
                string.Format(CultureInfo.CurrentCulture, "The razor file '{0}' cannot be rendered: this page has no rendering context.", path));

            var resolvedPath = this.ResolveRelativePath(path);
            var services = viewContext.HttpContext?.RequestServices ?? throw new InvalidOperationException(
                string.Format(CultureInfo.CurrentCulture, "The razor file '{0}' cannot be rendered outside of a request.", path));

            var factoryResult = services.GetRequiredService<IRazorPageFactoryProvider>().CreateFactory(resolvedPath);
            if (!factoryResult.Success || factoryResult.RazorPageFactory == null)
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture, "The razor file '{0}' was not found at '{1}'.", path, resolvedPath));
            }

            var childPage = factoryResult.RazorPageFactory();
            using (var writer = new StringWriter())
            {
                var childContext = new ViewContext(viewContext, viewContext.View, viewContext.ViewData, writer)
                {
                    ExecutingFilePath = resolvedPath,
                };

                if (childPage is DotNetNukeWebPage dnnPage)
                {
                    dnnPage.VirtualPath = GetDirectory(resolvedPath);
                    dnnPage.ConfigurePage(this);

                    // ConfigurePage hands down the parent's ViewContext (and therefore the
                    // parent's writer); the child has to write into its own buffer.
                    dnnPage.ViewContext = childContext;
                }
                else
                {
                    childPage.ViewContext = childContext;
                }

                childPage.Path = resolvedPath;
                services.GetRequiredService<IRazorPageActivator>().Activate(childPage, childContext);
                await childPage.ExecuteAsync().ConfigureAwait(false);

                return new HtmlString(writer.ToString());
            }
        }

        /// <summary>Configures this page from the page that renders it.</summary>
        /// <param name="parentPage">The parent page.</param>
        /// <remarks>Replaces <c>WebPageBase.ConfigurePage(WebPageBase)</c>.</remarks>
        protected virtual void ConfigurePage(DotNetNukeWebPage parentPage)
        {
            if (parentPage == null)
            {
                return;
            }

            // Child pages need to get their context from the Parent
            this.PageContext = parentPage.PageContext;
            this.ViewContext = parentPage.ViewContext;
            this.VirtualPath = this.VirtualPath ?? parentPage.VirtualPath;
            this.Dnn = this.Dnn ?? parentPage.Dnn;
            this.Html = this.Html ?? parentPage.Html;
            this.Url = this.Url ?? parentPage.Url;

            // Page data is shared, not copied, so that a layout sees everything the content page
            // wrote into it (and vice versa), exactly as WebPageBase.ConfigurePage did.
            this.pageData = parentPage.PageData;
        }

        /// <summary>Gets the directory of a virtual path, with a trailing slash.</summary>
        /// <param name="virtualPath">The path.</param>
        /// <returns>The directory of <paramref name="virtualPath"/>.</returns>
        private static string GetDirectory(string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath))
            {
                return virtualPath;
            }

            var path = virtualPath.Replace('\\', '/');
            var lastSlash = path.LastIndexOf('/');
            return lastSlash < 0 ? string.Empty : path.Substring(0, lastSlash + 1);
        }

        /// <summary>Resolves a page path against this page's own folder.</summary>
        /// <param name="path">The path to resolve.</param>
        /// <returns>An application-rooted path, e.g. <c>/DesktopModules/Foo/Scripts/Shared/_Header.cshtml</c>.</returns>
        /// <remarks>
        /// Reproduces the net48 behaviour of <c>WebPageBase.NormalizePath</c>: a path that is not
        /// rooted is relative to the folder of the page that renders it, so both
        /// <c>"Shared/_Header.cshtml"</c> (from the script folder) and <c>"_Header2.cshtml"</c>
        /// (from a layout in <c>Shared</c>) resolve the way they did.
        /// </remarks>
        private string ResolveRelativePath(string path)
        {
            var candidate = path.Replace('\\', '/');
            if (candidate.StartsWith("~/", StringComparison.Ordinal))
            {
                candidate = candidate.Substring(1);
            }

            if (candidate.StartsWith('/'))
            {
                return candidate;
            }

            var directory = GetDirectory(this.VirtualPath ?? this.Path);
            if (string.IsNullOrEmpty(directory))
            {
                directory = "/";
            }

            if (directory.StartsWith("~/", StringComparison.Ordinal))
            {
                directory = directory.Substring(1);
            }

            if (!directory.StartsWith('/'))
            {
                directory = "/" + directory;
            }

            if (!directory.EndsWith('/'))
            {
                directory += "/";
            }

            // Collapse any "./" and "../" segments the script may have used.
            var segments = new List<string>();
            foreach (var segment in (directory + candidate).Split('/'))
            {
                if (segment.Length == 0 || segment == ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    if (segments.Count > 0)
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }

                    continue;
                }

                segments.Add(segment);
            }

            return "/" + string.Join("/", segments);
        }
    }
}
