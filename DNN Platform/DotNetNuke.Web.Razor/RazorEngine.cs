// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Razor
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text.Encodings.Web;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Razor.Helpers;

    using Microsoft.AspNetCore.Diagnostics;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.Razor;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    // Microsoft.AspNetCore.Mvc.ViewFeatures (needed for ViewDataDictionary,
    // ITempDataDictionaryFactory and HtmlHelperOptions) also declares an HtmlHelper; the helper
    // this engine assigns to a page is DNN's own.
    using HtmlHelper = DotNetNuke.Web.Razor.Helpers.HtmlHelper;

    /// <summary>Renders a razor file.</summary>
    /// <remarks>
    /// The net48 implementation sat on <c>System.Web.WebPages</c>: it compiled the script with
    /// <c>System.Web.Compilation.BuildManager.GetCompiledType</c> and rendered it with
    /// <c>WebPageBase.ExecutePageHierarchy</c>. Neither exists on ASP.NET Core, so compilation is
    /// now done by <see cref="IRazorPageFactoryProvider"/> (backed by Razor runtime compilation, so
    /// module scripts installed into the site are still compiled on first use) and the page
    /// hierarchy is executed by <see cref="RazorView"/>, which runs the page together with any
    /// layout it declares.
    /// </remarks>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public partial class RazorEngine
    {
        /// <summary>Initializes a new instance of the <see cref="RazorEngine"/> class.</summary>
        /// <param name="razorScriptFile">The virtual path of the razor script, e.g. <c>~/DesktopModules/Foo/_Foo.cshtml</c>.</param>
        /// <param name="moduleContext">The module context the script renders for.</param>
        /// <param name="localResourceFile">The localization resource file, or <see langword="null"/> to use the default for <paramref name="razorScriptFile"/>.</param>
        public RazorEngine(string razorScriptFile, ModuleInstanceContext moduleContext, string localResourceFile)
        {
            this.RazorScriptFile = razorScriptFile;
            this.ModuleContext = moduleContext;
            this.LocalResourceFile = localResourceFile ?? Path.Combine(Path.GetDirectoryName(razorScriptFile), Localization.LocalResourceDirectory, Path.GetFileName(razorScriptFile) + ".resx");

            try
            {
                this.InitWebpage();
            }
            catch (Exception exc) when (exc is ICompilationException)
            {
                // Razor parse and compile failures must surface to the caller, exactly as the
                // System.Web HttpParseException / HttpCompileException pair did on net48. Runtime
                // compilation reports both of them through ICompilationException.
                throw;
            }
            catch (Exception exc)
            {
                Exceptions.LogException(exc);
            }
        }

        /// <summary>Gets or sets the compiled razor page.</summary>
        public DotNetNukeWebPage Webpage { get; set; }

        /// <summary>Gets the context of the current request.</summary>
        /// <remarks>
        /// Replaces the net48 <c>new HttpContextWrapper(System.Web.HttpContext.Current)</c>. The
        /// context comes from the registered <see cref="IHttpContextAccessor"/>.
        /// </remarks>
        protected HttpContext HttpContext
        {
            get { return HttpContextSource.Current; }
        }

        /// <summary>Gets or sets the virtual path of the razor script.</summary>
        protected string RazorScriptFile { get; set; }

        /// <summary>Gets or sets the module context the script renders for.</summary>
        protected ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets or sets the localization resource file of the script.</summary>
        protected string LocalResourceFile { get; set; }

        /// <summary>Get the type of the model.</summary>
        /// <returns>A type or <see langword="null"/>.</returns>
        [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
        public partial Type RequestedModelType()
        {
            if (this.Webpage != null)
            {
                var webpageType = this.Webpage.GetType();
                if (webpageType.BaseType != null && webpageType.BaseType.IsGenericType)
                {
                    return webpageType.BaseType.GetGenericArguments()[0];
                }
            }

            return null;
        }

        /// <summary>Renders a razor file into a <see cref="TextWriter"/>.</summary>
        /// <param name="writer">The writer into which to render the HTML.</param>
        /// <param name="model">The model.</param>
        /// <typeparam name="T">The type of the model.</typeparam>
        [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
        public partial void Render<T>(TextWriter writer, T model)
        {
            try
            {
                var context = new RazorPageContext(this.HttpContext, this.Webpage, model);
                ExecuteSynchronously(() => this.RenderAsync(writer, context));
            }
            catch (Exception exc)
            {
                Exceptions.LogException(exc);
            }
        }

        /// <summary>Renders a razor file into a <see cref="TextWriter"/>.</summary>
        /// <param name="writer">The writer into which to render the HTML.</param>
        [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
        public partial void Render(TextWriter writer)
        {
            try
            {
                var context = new RazorPageContext(this.HttpContext, this.Webpage, null);
                ExecuteSynchronously(() => this.RenderAsync(writer, context));
            }
            catch (Exception exc)
            {
                Exceptions.LogException(exc);
            }
        }

        /// <summary>Renders a razor file into a <see cref="TextWriter"/>.</summary>
        /// <param name="writer">The writer into which to render the HTML.</param>
        /// <param name="context">The razor page context.</param>
        /// <remarks>
        /// The net48 overload took a <c>System.Web.WebPages.WebPageContext</c>; it now takes the
        /// DNN-owned <see cref="RazorPageContext"/>, which has no System.Web dependency.
        /// </remarks>
        [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
        public partial void Render(TextWriter writer, RazorPageContext context)
        {
            try
            {
                ExecuteSynchronously(() => this.RenderAsync(writer, context));
            }
            catch (Exception exc)
            {
                Exceptions.LogException(exc);
            }
        }

        /// <summary>Gets the service provider the rendering pipeline is resolved from.</summary>
        /// <returns>A service provider.</returns>
        private static IServiceProvider GetServiceProvider()
        {
            return HttpContextSource.Current?.RequestServices ?? Globals.GetCurrentServiceProvider();
        }

        /// <summary>
        /// Runs an asynchronous rendering operation from a synchronous caller.
        /// </summary>
        /// <param name="operation">The operation to run.</param>
        /// <remarks>
        /// <see cref="Render(TextWriter)"/> and its overloads are called from the synchronous
        /// <c>OnPreRender</c> of the module control surface, so the asynchronous Razor rendering
        /// pipeline has to be bridged. The work is pushed onto the thread pool rather than blocked
        /// on inline so that an awaited continuation can never queue behind the blocked thread.
        /// </remarks>
        private static void ExecuteSynchronously(Func<Task> operation)
        {
            Task.Run(operation).GetAwaiter().GetResult();
        }

        /// <summary>Normalizes a DNN virtual path into the application-relative path the Razor view engine expects.</summary>
        /// <param name="virtualPath">The path to normalize, e.g. <c>~/DesktopModules/Foo/_Foo.cshtml</c>.</param>
        /// <returns>The path rooted at the application, e.g. <c>/DesktopModules/Foo/_Foo.cshtml</c>.</returns>
        private static string NormalizeViewPath(string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath))
            {
                return virtualPath;
            }

            var path = virtualPath.Replace('\\', '/');
            if (path.StartsWith("~/", StringComparison.Ordinal))
            {
                path = path.Substring(1);
            }
            else if (path == "~")
            {
                path = "/";
            }

            return path.StartsWith('/') ? path : "/" + path;
        }

        /// <summary>Gets the virtual directory of a razor script, replacing <c>VirtualPathUtility.GetDirectory</c>.</summary>
        /// <param name="virtualPath">The path of the script.</param>
        /// <returns>The virtual directory, with a trailing slash.</returns>
        private static string GetVirtualDirectory(string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath))
            {
                return virtualPath;
            }

            var path = virtualPath.Replace('\\', '/');
            var lastSlash = path.LastIndexOf('/');
            return lastSlash < 0 ? string.Empty : path.Substring(0, lastSlash + 1);
        }

        /// <summary>Compiles the razor script and creates an instance of the compiled page.</summary>
        /// <returns>The page instance, or <see langword="null"/> when the script could not be found.</returns>
        /// <remarks>
        /// Replaces <c>BuildManager.GetCompiledType(file)</c> + <c>Activator.CreateInstance</c>:
        /// <see cref="IRazorPageFactoryProvider.CreateFactory(string)"/> compiles the script (with
        /// Razor runtime compilation registered by <see cref="Startup"/>) and hands back the
        /// factory that creates the compiled page.
        /// </remarks>
        private object CreateWebPageInstance()
        {
            var services = GetServiceProvider();
            var pageFactoryProvider = services.GetRequiredService<IRazorPageFactoryProvider>();
            var result = pageFactoryProvider.CreateFactory(NormalizeViewPath(this.RazorScriptFile));
            object objectValue = null;
            if (result.Success && result.RazorPageFactory != null)
            {
                objectValue = RuntimeHelpers.GetObjectValue(result.RazorPageFactory());
            }

            return objectValue;
        }

        private void InitHelpers(DotNetNukeWebPage webPage)
        {
            webPage.Dnn = new DnnHelper(this.ModuleContext);
            webPage.Html = new HtmlHelper(this.ModuleContext, this.LocalResourceFile);
            webPage.Url = new UrlHelper(this.ModuleContext);
        }

        private void InitWebpage()
        {
            if (!string.IsNullOrEmpty(this.RazorScriptFile))
            {
                var objectValue = RuntimeHelpers.GetObjectValue(this.CreateWebPageInstance());
                if (objectValue == null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "The webpage found at '{0}' was not created.", new object[] { this.RazorScriptFile }));
                }

                this.Webpage = objectValue as DotNetNukeWebPage;
                if (this.Webpage == null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "The webpage at '{0}' must derive from DotNetNukeWebPage.", new object[] { this.RazorScriptFile }));
                }

                this.Webpage.Path = NormalizeViewPath(this.RazorScriptFile);
                this.Webpage.PageContext = new RazorPageContext(this.HttpContext, this.Webpage, null);
                this.Webpage.VirtualPath = GetVirtualDirectory(this.RazorScriptFile);
                this.InitHelpers(this.Webpage);
            }
        }

        /// <summary>Executes the page hierarchy of the compiled page into <paramref name="writer"/>.</summary>
        /// <param name="writer">The writer into which to render the HTML.</param>
        /// <param name="context">The razor page context.</param>
        /// <returns>A task representing the rendering.</returns>
        /// <remarks>Replaces <c>WebPageBase.ExecutePageHierarchy(WebPageContext, TextWriter, WebPageBase)</c>.</remarks>
        private async Task RenderAsync(TextWriter writer, RazorPageContext context)
        {
            if (this.Webpage == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "The webpage found at '{0}' was not created.", new object[] { this.RazorScriptFile }));
            }

            this.Webpage.PageContext = context;
            this.Webpage.Model = context?.Model;

            var services = GetServiceProvider();
            var httpContext = context?.HttpContext ?? this.HttpContext
                ?? throw new InvalidOperationException(
                    $"The razor script '{this.RazorScriptFile}' cannot be rendered outside of a request: no HttpContext is available.");

            var routeData = httpContext.GetRouteData() ?? new RouteData();
            var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());

            var viewData = new ViewDataDictionary<object>(
                services.GetRequiredService<IModelMetadataProvider>(),
                actionContext.ModelState)
            {
                Model = context?.Model,
            };

            var tempData = services.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(httpContext);
            var htmlEncoder = services.GetService<HtmlEncoder>() ?? HtmlEncoder.Default;
            var diagnosticListener = services.GetService<DiagnosticListener>() ?? new DiagnosticListener("DotNetNuke.Web.Razor");
            var htmlHelperOptions = services.GetService<IOptions<MvcViewOptions>>()?.Value.HtmlHelperOptions ?? new HtmlHelperOptions();

            var view = new RazorView(
                services.GetRequiredService<IRazorViewEngine>(),
                services.GetRequiredService<IRazorPageActivator>(),
                Array.Empty<IRazorPage>(),
                this.Webpage,
                htmlEncoder,
                diagnosticListener);

            var viewContext = new ViewContext(actionContext, view, viewData, tempData, writer, htmlHelperOptions)
            {
                ExecutingFilePath = this.Webpage.Path,
            };

            await view.RenderAsync(viewContext).ConfigureAwait(false);
        }
    }
}
