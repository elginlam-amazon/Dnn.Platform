// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable ConvertPropertyToExpressionBody
namespace DotNetNuke.Web.Mvc.Helpers
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Framework.Controllers;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;

    using FileOrder = DotNetNuke.Web.Client.FileOrder;

    public class DnnHelper
    {
        private readonly IClientResourceController clientResourceController;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;

        /// <summary>Initializes a new instance of the <see cref="DnnHelper"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        [ActivatorUtilitiesConstructor]
        public DnnHelper(IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, ViewContext viewContext)
            : this(clientResourceController, appStatus, eventLogger, HtmlHelperFactory.Create(viewContext), null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHelper"/> class.</summary>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public DnnHelper(ViewContext viewContext)
            : this(null, null, null, HtmlHelperFactory.Create(viewContext), null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHelper"/> class.</summary>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        /// <param name="routeCollection">The route collection.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        public DnnHelper(ViewContext viewContext, RouteCollection routeCollection)
            : this(null, null, null, HtmlHelperFactory.Create(viewContext), routeCollection)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHelper"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="viewContext">The view context (carries the view data in ASP.NET Core).</param>
        /// <param name="routeCollection">The route collection.</param>
        public DnnHelper(IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, ViewContext viewContext, RouteCollection routeCollection)
            : this(clientResourceController, appStatus, eventLogger, HtmlHelperFactory.Create(viewContext), routeCollection)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHelper"/> class.</summary>
        /// <param name="htmlHelper">The HTML helper instance to wrap.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IClientResourceController. Scheduled removal in v12.0.0.")]
        protected DnnHelper(IHtmlHelper htmlHelper)
            : this(null, null, null, htmlHelper, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHelper"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="htmlHelper">The HTML helper.</param>
        protected DnnHelper(IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, IHtmlHelper htmlHelper)
            : this(clientResourceController, appStatus, eventLogger, htmlHelper, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnHelper"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="routeCollection">The route collection.</param>
        /// <exception cref="InvalidOperationException">The DnnHelper class can only be used in Views that inherit from DnnWebViewPage.</exception>
        protected DnnHelper(IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, IHtmlHelper htmlHelper, RouteCollection routeCollection)
        {
            Requires.NotNull("htmlHelper", htmlHelper);

            var serviceProvider = htmlHelper.ViewContext?.HttpContext?.RequestServices ?? Globals.GetCurrentServiceProvider();

            this.clientResourceController = clientResourceController ?? serviceProvider.GetRequiredService<IClientResourceController>();
            this.appStatus = appStatus ?? serviceProvider.GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? serviceProvider.GetRequiredService<IEventLogger>();
            this.HtmlHelper = htmlHelper;
            this.RouteCollection = routeCollection;

            if (ModuleApplication.GetCurrentDnnController(htmlHelper.ViewContext?.HttpContext) is not IDnnController controller)
            {
                throw new InvalidOperationException("The DnnHelper class can only be used in Views that inherit from DnnWebViewPage");
            }

            this.DnnPage = controller.DnnPage;

            this.ModuleContext = controller.ModuleContext;
            this.LocalResourceFile = controller.LocalResourceFile;
        }

        public ModuleInfo ActiveModule => this.ModuleContext?.Configuration;

        public TabInfo ActivePage => this.PortalSettings?.ActiveTab;

        public PortalSettings PortalSettings => this.ModuleContext?.PortalSettings;

        /// <summary>Gets the route collection this helper was created with, if any.</summary>
        /// <remarks>ASP.NET Core uses endpoint routing; there is no ambient route table, so this is only
        /// populated when a route collection was passed to the constructor.</remarks>
        public RouteCollection RouteCollection { get; }

        public UserInfo User => this.PortalSettings?.UserInfo;

        public dynamic ViewBag => this.HtmlHelper.ViewBag;

        public ViewContext ViewContext => this.HtmlHelper.ViewContext;

        public ViewDataDictionary ViewData => this.HtmlHelper.ViewData;

        /// <summary>Gets or sets the DNN page (host component) the module is rendered into.</summary>
        public IComponent DnnPage { get; set; }

        public string LocalResourceFile { get; set; }

        public ModuleInstanceContext ModuleContext { get; set; }

        internal IHtmlHelper HtmlHelper { get; set; }

        public string LocalizeString(string key)
        {
            return Localization.GetString(key, this.LocalResourceFile);
        }

        /// <summary>Requests that a CSS file be registered on the client browser.</summary>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        public void DnnCssInclude(string filePath)
        {
            this.clientResourceController.CreateStylesheet(filePath).Register();
        }

        /// <summary>Requests that a CSS file be registered on the client browser. Defaults to rendering in the page header.</summary>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        public void DnnCssInclude(string filePath, int priority)
        {
            var stylesheet = this.clientResourceController.CreateStylesheet(filePath);
            stylesheet.Priority = priority;
            stylesheet.Register();
        }

        /// <summary>Requests that a CSS file be registered on the client browser. Defaults to rendering in the page header.</summary>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        public void DnnCssInclude(string filePath, FileOrder.Css priority)
        {
            this.DnnCssInclude(filePath, (int)priority);
        }

        /// <summary>Requests that a CSS file be registered on the client browser. Allows for overriding the default provider.</summary>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The provider name to be used to render the CSS file on the page.</param>
        public void DnnCssInclude(string filePath, int priority, string provider)
        {
            var stylesheet = this.clientResourceController.CreateStylesheet(filePath);
            stylesheet.Priority = priority;
            stylesheet.Provider = provider;
            stylesheet.Register();
        }

        /// <summary>Requests that a CSS file be registered on the client browser. Allows for overriding the default provider.</summary>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The provider name to be used to render the CSS file on the page.</param>
        /// <param name="name">Name of framework like Bootstrap, Angular, etc.</param>
        /// <param name="version">Version nr of framework.</param>
        public void DnnCssInclude(string filePath, int priority, string provider, string name, string version)
        {
            var stylesheet = this.clientResourceController.CreateStylesheet(filePath);
            stylesheet.Priority = priority;
            stylesheet.Provider = provider;
            stylesheet.Name = name;
            stylesheet.Version = version;
            stylesheet.ForceVersion = false;
            stylesheet.Register();
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        public void DnnJsInclude(string filePath)
        {
            this.clientResourceController.CreateScript(filePath).Register();
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        public void DnnJsInclude(string filePath, int priority)
        {
            var script = this.clientResourceController.CreateScript(filePath);
            script.Priority = priority;
            script.Register();
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        public void DnnJsInclude(string filePath, FileOrder.Js priority)
        {
            this.DnnJsInclude(filePath, (int)priority);
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The name of the provider responsible for rendering the script output.</param>
        public void DnnJsInclude(string filePath, int priority, string provider)
        {
            var script = this.clientResourceController.CreateScript(filePath);
            script.Priority = priority;
            script.Provider = provider;
            script.Register();
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The name of the provider responsible for rendering the script output.</param>
        /// <param name="name">Name of framework like Bootstrap, Angular, etc.</param>
        /// <param name="version">Version nr of framework.</param>
        public void DnnJsInclude(string filePath, int priority, string provider, string name, string version)
        {
            // Note: this overload has always registered a *stylesheet* resource (see the MVC 5
            // implementation, which called CreateStylesheet(filePath).SetPriority(..).SetProvider(..)
            // .SetNameAndVersion(name, version, false).Register()). That looks like a long standing
            // defect for a JavaScript include, but the behavior is preserved here on purpose so the
            // migration does not change what existing modules get rendered; the fluent setters simply
            // became settable properties on IResource.
            var resource = this.clientResourceController.CreateStylesheet(filePath);
            resource.Priority = priority;
            resource.Provider = provider;
            resource.Name = name;
            resource.Version = version;
            resource.ForceVersion = false;
            resource.Register();
        }

        /// <summary>Requests that a script from a JavaScript Library be registered on the client browser.</summary>
        /// <param name="name">The name of the JavaScript Library.</param>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public void JavaScriptLibraryInclude(string name)
        {
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, name);
        }

        /// <summary>Requests that a script from a JavaScript Library be registered on the client browser.</summary>
        /// <param name="name">The name of the JavaScript Library.</param>
        /// <param name="version">The library's version.</param>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public void JavaScriptLibraryInclude(string name, Version version)
        {
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, name, version);
        }

        /// <summary>Requests that a script from a JavaScript Library be registered on the client browser.</summary>
        /// <param name="name">The name of the JavaScript Library.</param>
        /// <param name="version">The library's version.</param>
        /// <param name="specificVersion">
        /// How much of the <paramref name="version"/> to pay attention to.
        /// When <see cref="SpecificVersion.Latest"/> is passed, ignore the <paramref name="version"/>.
        /// When <see cref="SpecificVersion.LatestMajor"/> is passed, match the major version.
        /// When <see cref="SpecificVersion.LatestMinor"/> is passed, match the major and minor versions.
        /// When <see cref="SpecificVersion.Exact"/> is passed, match all parts of the version.
        /// </param>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public void JavaScriptLibraryInclude(string name, Version version, SpecificVersion specificVersion)
        {
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, name, version, specificVersion);
        }
    }
}
