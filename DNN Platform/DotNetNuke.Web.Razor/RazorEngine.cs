// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Razor
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Text.Encodings.Web;

    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Razor.Helpers;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.Razor;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    /// <summary>Renders a razor file.</summary>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public partial class RazorEngine
    {
        /// <summary>Initializes a new instance of the <see cref="RazorEngine"/> class.</summary>
        /// <param name="razorScriptFile">The (app-relative) virtual path to the razor script file.</param>
        /// <param name="moduleContext">The module context.</param>
        /// <param name="localResourceFile">The local resource file, or <see langword="null"/> to use the default.</param>
        public RazorEngine(string razorScriptFile, ModuleInstanceContext moduleContext, string localResourceFile)
        {
            this.RazorScriptFile = razorScriptFile;
            this.ModuleContext = moduleContext;
            this.LocalResourceFile = localResourceFile ?? Path.Combine(Path.GetDirectoryName(razorScriptFile), Localization.LocalResourceDirectory, Path.GetFileName(razorScriptFile) + ".resx");

            try
            {
                this.InitWebpage();
            }
            catch (Exception exc)
            {
                Exceptions.LogException(exc);
            }
        }

        /// <summary>Gets or sets the razor web page instance located for the script file.</summary>
        public DotNetNukeWebPage Webpage { get; set; }

        /// <summary>Gets the current ASP.NET Core <see cref="Microsoft.AspNetCore.Http.HttpContext"/>.</summary>
        protected Microsoft.AspNetCore.Http.HttpContext HttpContext
        {
            get { return System.Web.HttpContext.Current; }
        }

        /// <summary>Gets or sets the (app-relative) virtual path to the razor script file.</summary>
        protected string RazorScriptFile { get; set; }

        /// <summary>Gets or sets the module context.</summary>
        protected ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets or sets the local resource file.</summary>
        protected string LocalResourceFile { get; set; }

        /// <summary>Get the type of the model.</summary>
        /// <returns>A type or <see langword="null"/>.</returns>
        [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
        public partial Type RequestedModelType()
        {
            if (this.Webpage != null)
            {
                var webpageType = this.Webpage.GetType();
                if (webpageType.BaseType.IsGenericType)
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
            this.RenderInternal(writer, model);
        }

        /// <summary>Renders a razor file into a <see cref="TextWriter"/>.</summary>
        /// <param name="writer">The writer into which to render the HTML.</param>
        [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
        public partial void Render(TextWriter writer)
        {
            this.RenderInternal(writer, null);
        }

        private void RenderInternal(TextWriter writer, object model)
        {
            try
            {
                if (this.Webpage == null)
                {
                    return;
                }

                var httpContext = this.HttpContext;
                if (httpContext == null)
                {
                    throw new InvalidOperationException("A razor script can only be rendered during an HTTP request.");
                }

                var services = httpContext.RequestServices;
                var viewEngine = services.GetRequiredService<IRazorViewEngine>();
                var activator = services.GetRequiredService<IRazorPageActivator>();
                var metadataProvider = services.GetRequiredService<IModelMetadataProvider>();
                var tempDataProvider = services.GetRequiredService<ITempDataProvider>();
                var htmlHelperOptions = services.GetRequiredService<IOptions<MvcViewOptions>>().Value.HtmlHelperOptions;
                var diagnosticListener = services.GetRequiredService<DiagnosticListener>();

                var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

                var view = new RazorView(
                    viewEngine,
                    activator,
                    Array.Empty<IRazorPage>(),
                    this.Webpage,
                    HtmlEncoder.Default,
                    diagnosticListener);

                var viewData = new ViewDataDictionary(metadataProvider, actionContext.ModelState)
                {
                    Model = model,
                };

                var tempData = new TempDataDictionary(httpContext, tempDataProvider);

                var viewContext = new ViewContext(actionContext, view, viewData, tempData, writer, htmlHelperOptions);

                view.RenderAsync(viewContext).GetAwaiter().GetResult();
            }
            catch (Exception exc)
            {
                Exceptions.LogException(exc);
            }
        }

        private DotNetNukeWebPage LocateWebPage()
        {
            var httpContext = this.HttpContext;
            if (httpContext == null)
            {
                throw new InvalidOperationException("A razor script can only be located during an HTTP request.");
            }

            var viewEngine = httpContext.RequestServices.GetRequiredService<IRazorViewEngine>();

            var result = viewEngine.GetPage(string.Empty, this.RazorScriptFile);
            var razorPage = result.Page;
            if (razorPage == null)
            {
                var locations = result.SearchedLocations == null ? this.RazorScriptFile : string.Join(", ", result.SearchedLocations);
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "The webpage found at '{0}' was not created. Searched: {1}", this.RazorScriptFile, locations));
            }

            if (razorPage is not DotNetNukeWebPage webpage)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "The webpage at '{0}' must derive from DotNetNukeWebPage.", new object[] { this.RazorScriptFile }));
            }

            return webpage;
        }

        private void InitHelpers(DotNetNukeWebPage webPage)
        {
            webPage.Dnn = new DnnHelper(this.ModuleContext);
            webPage.Html = new Helpers.HtmlHelper(this.ModuleContext, this.LocalResourceFile);
            webPage.Url = new UrlHelper(this.ModuleContext);
        }

        private void InitWebpage()
        {
            if (!string.IsNullOrEmpty(this.RazorScriptFile))
            {
                this.Webpage = this.LocateWebPage();
                this.Webpage.Path = this.RazorScriptFile;
                this.InitHelpers(this.Webpage);
            }
        }
    }
}
