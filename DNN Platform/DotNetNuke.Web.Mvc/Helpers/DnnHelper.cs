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
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.DependencyInjection;

    using FileOrder = DotNetNuke.Web.Client.FileOrder;

    /// <summary>A DNN helper exposing DNN-specific view helpers (client resources, JavaScript libraries, localization).</summary>
    public class DnnHelper
    {
        private readonly IClientResourceController clientResourceController;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IEventLogger eventLogger;

        /// <summary>Initializes a new instance of the <see cref="DnnHelper"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="htmlHelper">The HTML helper to wrap.</param>
        /// <exception cref="InvalidOperationException">The DnnHelper class can only be used in Views that inherit from DnnWebViewPage.</exception>
        protected DnnHelper(IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, IHtmlHelper htmlHelper)
        {
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger = eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            this.HtmlHelper = htmlHelper;

            if (htmlHelper.ViewContext.HttpContext.Items.TryGetValue(typeof(DnnModuleState), out var stateObj) && stateObj is DnnModuleState state)
            {
                this.ModuleContext = state.ModuleContext;
                this.LocalResourceFile = state.LocalResourceFile;
            }
            else
            {
                throw new InvalidOperationException("The DnnHelper class can only be used in Views that inherit from DnnWebViewPage");
            }
        }

        /// <summary>Gets the active module.</summary>
        public ModuleInfo ActiveModule => this.ModuleContext?.Configuration;

        /// <summary>Gets the active page.</summary>
        public TabInfo ActivePage => this.PortalSettings?.ActiveTab;

        /// <summary>Gets the portal settings.</summary>
        public PortalSettings PortalSettings => this.ModuleContext?.PortalSettings;

        /// <summary>Gets the current user.</summary>
        public UserInfo User => this.PortalSettings?.UserInfo;

        /// <summary>Gets the view bag.</summary>
        public dynamic ViewBag => this.HtmlHelper.ViewBag;

        /// <summary>Gets the view context.</summary>
        public ViewContext ViewContext => this.HtmlHelper.ViewContext;

        /// <summary>Gets the view data.</summary>
        public ViewDataDictionary ViewData => this.HtmlHelper.ViewData;

        /// <summary>Gets or sets the local resource file.</summary>
        public string LocalResourceFile { get; set; }

        /// <summary>Gets or sets the module instance context.</summary>
        public ModuleInstanceContext ModuleContext { get; set; }

        /// <summary>Gets the wrapped HTML helper.</summary>
        internal IHtmlHelper HtmlHelper { get; }

        /// <summary>Localizes the given key against the local resource file.</summary>
        /// <param name="key">The resource key.</param>
        /// <returns>The localized string.</returns>
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

        /// <summary>Requests that a CSS file be registered on the client browser.</summary>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        public void DnnCssInclude(string filePath, int priority)
        {
            var resource = this.clientResourceController.CreateStylesheet(filePath);
            resource.Priority = priority;
            resource.Register();
        }

        /// <summary>Requests that a CSS file be registered on the client browser.</summary>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        public void DnnCssInclude(string filePath, FileOrder.Css priority)
        {
            this.DnnCssInclude(filePath, (int)priority);
        }

        /// <summary>Requests that a CSS file be registered on the client browser.</summary>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The provider name to be used to render the CSS file on the page.</param>
        public void DnnCssInclude(string filePath, int priority, string provider)
        {
            var resource = this.clientResourceController.CreateStylesheet(filePath);
            resource.Priority = priority;
            resource.Provider = provider;
            resource.Register();
        }

        /// <summary>Requests that a CSS file be registered on the client browser.</summary>
        /// <param name="filePath">The relative file path to the CSS resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The provider name to be used to render the CSS file on the page.</param>
        /// <param name="name">Name of framework like Bootstrap, Angular, etc.</param>
        /// <param name="version">Version nr of framework.</param>
        public void DnnCssInclude(string filePath, int priority, string provider, string name, string version)
        {
            var resource = this.clientResourceController.CreateStylesheet(filePath);
            resource.Priority = priority;
            resource.Provider = provider;
            resource.Name = name;
            resource.Version = version;
            resource.Register();
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
            var resource = this.clientResourceController.CreateScript(filePath);
            resource.Priority = priority;
            resource.Register();
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
            var resource = this.clientResourceController.CreateScript(filePath);
            resource.Priority = priority;
            resource.Provider = provider;
            resource.Register();
        }

        /// <summary>Requests that a JavaScript file be registered on the client browser.</summary>
        /// <param name="filePath">The relative file path to the JavaScript resource.</param>
        /// <param name="priority">The relative priority in which the file should be loaded.</param>
        /// <param name="provider">The name of the provider responsible for rendering the script output.</param>
        /// <param name="name">Name of framework like Bootstrap, Angular, etc.</param>
        /// <param name="version">Version nr of framework.</param>
        public void DnnJsInclude(string filePath, int priority, string provider, string name, string version)
        {
            var resource = this.clientResourceController.CreateScript(filePath);
            resource.Priority = priority;
            resource.Provider = provider;
            resource.Name = name;
            resource.Version = version;
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
        /// <param name="specificVersion">How much of the <paramref name="version"/> to pay attention to.</param>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        public void JavaScriptLibraryInclude(string name, Version version, SpecificVersion specificVersion)
        {
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, name, version, specificVersion);
        }
    }
}
