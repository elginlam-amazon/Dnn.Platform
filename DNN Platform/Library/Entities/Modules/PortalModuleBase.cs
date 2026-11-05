// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Entities.Modules
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Threading;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Html;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The PortalModuleBase class defines a custom base class inherited by all
    /// desktop portal modules within the Portal.
    ///
    /// The PortalModuleBase class defines portal specific properties
    /// that are used by the portal framework to correctly display portal modules.
    /// </summary>
    /// <remarks>
    /// This was a <c>System.Web.UI.UserControl</c>. It is now a Blazor component base (through
    /// <see cref="UserControlBase"/>, which derives from <c>ComponentBase</c>). Every public
    /// member is preserved; the WebForms-only concepts map as follows:
    /// <list type="bullet">
    /// <item><description><c>Control</c>/<c>ContainerControl</c> are now <c>IComponent</c>. The container is supplied by the enclosing container component as a cascading value named <c>ModuleContainer</c>.</description></item>
    /// <item><description><c>OnInit</c>/<c>OnLoad</c> are still the overridable hooks for derived module controls, but they are now invoked from <c>OnInitialized</c>/<c>OnParametersSet</c>.</description></item>
    /// <item><description><c>TemplateSourceDirectory</c>/<c>ID</c> no longer exist; <see cref="ControlPath"/> is derived from the module's registered control source and <see cref="ControlName"/>/<see cref="LocalResourceFile"/> from the component type name.</description></item>
    /// <item><description><c>IHtmlString</c> results are now <c>IHtmlContent</c>.</description></item>
    /// </list>
    /// HttpContext is <see langword="null"/> during interactive (SignalR) rendering, so anything
    /// derived from the request must be read in <c>OnInitialized</c> and captured in a field —
    /// never from an <c>@onclick</c> handler.
    /// </remarks>
    public partial class PortalModuleBase : UserControlBase, IModuleControl, IDisposable
    {
        protected static readonly Regex FileInfoRegex = new Regex(
            @"\.([a-z]{2,3}\-[0-9A-Z]{2,4}(-[A-Z]{2})?)(\.(Host|Portal-\d+))?\.resx$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            TimeSpan.FromSeconds(1));

        private readonly ILogger tracelLogger = DnnLoggingController.GetLogger("DNN.Trace");
        private readonly Lazy<ServiceScopeContainer> serviceScopeContainer = new Lazy<ServiceScopeContainer>(ServiceScopeContainer.GetRequestOrCreateScope);
        private string localResourceFile;
        private ModuleInstanceContext moduleContext;

        /// <summary>
        /// Gets or sets the container component this module is rendered inside.
        /// </summary>
        /// <remarks>
        /// The legacy implementation walked the WebForms control tree looking for the
        /// <c>"ctr" + ModuleId</c> control. Blazor has no server-side control tree to walk, so the
        /// container component cascades itself under the name <c>ModuleContainer</c> and this
        /// property receives it.
        /// </remarks>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [CascadingParameter(Name = "ModuleContainer")]
        public Microsoft.AspNetCore.Components.IComponent ContainerControl { get; set; }

        /// <summary>Gets a value indicating whether the user is in the Administrator role.</summary>
        public bool EditMode => this.ModuleContext.EditMode;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsEditable => this.ModuleContext.IsEditable;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int PortalId => this.ModuleContext.PortalId;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int TabId => this.ModuleContext.TabId;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public UserInfo UserInfo => this.PortalSettings.UserInfo;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int UserId => this.PortalSettings.UserId;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public PortalAliasInfo PortalAlias => this.PortalSettings.PortalAlias;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Hashtable Settings => this.ModuleContext.Settings;

        /// <summary>Gets the underlying base control for this ModuleControl.</summary>
        /// <returns>This component.</returns>
        public Microsoft.AspNetCore.Components.IComponent Control => this;

        /// <summary>Gets the Path for this control (used primarily for UserControls).</summary>
        /// <returns>A String.</returns>
        /// <remarks>
        /// Replaces <c>Control.TemplateSourceDirectory</c>, which was the virtual directory of the
        /// <c>.ascx</c> file. The equivalent information on ASP.NET Core is the module control
        /// source registered for this module, falling back to the module's DesktopModule folder.
        /// </remarks>
        public string ControlPath
        {
            get
            {
                var controlSource = this.ModuleContext?.Configuration?.ModuleControl?.ControlSrc;
                if (!string.IsNullOrEmpty(controlSource))
                {
                    var normalized = controlSource.Replace('\\', '/');
                    var lastSlash = normalized.LastIndexOf('/');
                    if (lastSlash > 0)
                    {
                        return normalized.Substring(0, lastSlash + 1);
                    }
                }

                var folderName = this.ModuleContext?.Configuration?.DesktopModule?.FolderName;
                return string.IsNullOrEmpty(folderName) ? "/" : $"~/DesktopModules/{folderName}/";
            }
        }

        /// <summary>Gets the Name for this control.</summary>
        /// <returns>A String.</returns>
        public string ControlName => this.GetType().Name.Replace("_", ".");

        /// <summary>Gets the Module Context for this control.</summary>
        /// <returns>A ModuleInstanceContext.</returns>
        public ModuleInstanceContext ModuleContext
        {
            get
            {
                if (this.moduleContext == null)
                {
                    this.moduleContext = new ModuleInstanceContext(this);
                }

                return this.moduleContext;
            }
        }

        // CONVERSION: Remove obsoleted methods (FYI some core modules use these, such as Links)

        /// <summary>
        ///   Gets the CacheDirectory property is used to return the location of the "Cache"
        ///   Directory for the Module.
        /// </summary>
        [Obsolete("Deprecated in DotNetNuke 7.0.0. Please use ModuleController.CacheDirectory(). Scheduled removal in v11.0.0.")]
        public string CacheDirectory
        {
            get
            {
                return PortalController.Instance.GetCurrentSettings().HomeDirectoryMapPath + "Cache";
            }
        }

        /// <summary>
        ///   Gets the CacheFileName property is used to store the FileName for this Module's
        ///   Cache.
        /// </summary>
        [Obsolete("Deprecated in DotNetNuke 7.0.0. Please use ModuleController.CacheFileName(TabModuleID). Scheduled removal in v11.0.0.")]
        public string CacheFileName
        {
            get
            {
                string strCacheKey = "TabModule:";
                strCacheKey += this.TabModuleId + ":";
                strCacheKey += Thread.CurrentThread.CurrentUICulture.ToString();
                return PortalController.Instance.GetCurrentSettings().HomeDirectoryMapPath + "Cache" + @"\" + Globals.CleanFileName(strCacheKey) + ".resources";
            }
        }

        [Obsolete("Deprecated in DotNetNuke 7.0.0. Please use ModuleController.CacheKey(TabModuleID). Scheduled removal in v11.0.0.")]
        public string CacheKey
        {
            get
            {
                string strCacheKey = "TabModule:";
                strCacheKey += this.TabModuleId + ":";
                strCacheKey += Thread.CurrentThread.CurrentUICulture.ToString();
                return strCacheKey;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ModuleActionCollection Actions
        {
            get => this.ModuleContext.Actions;
            set => this.ModuleContext.Actions = value;
        }

        public string HelpURL
        {
            get => this.ModuleContext.HelpURL;
            set => this.ModuleContext.HelpURL = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ModuleInfo ModuleConfiguration
        {
            get => this.ModuleContext.Configuration;
            set => this.ModuleContext.Configuration = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int TabModuleId
        {
            get => this.ModuleContext.TabModuleId;
            set => this.ModuleContext.TabModuleId = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ModuleId
        {
            get => this.ModuleContext.ModuleId;
            set => this.ModuleContext.ModuleId = value;
        }

        /// <summary>Gets or sets the local resource file for this control.</summary>
        /// <returns>A String.</returns>
        public string LocalResourceFile
        {
            get
            {
                string fileRoot;
                if (string.IsNullOrEmpty(this.localResourceFile))
                {
                    // Control.ID has no Blazor equivalent; the component type name is what the
                    // .ascx file (and therefore its .resx) was named after.
                    fileRoot = Path.Combine(this.ControlPath, Localization.LocalResourceDirectory + "/" + this.GetType().Name);
                }
                else
                {
                    fileRoot = this.localResourceFile;
                }

                return fileRoot;
            }

            set
            {
                this.localResourceFile = value;
            }
        }

        /// <summary>
        /// Gets the Dependency Provider to resolve registered
        /// services with the container.
        /// </summary>
        /// <value>
        /// The Dependency Service.
        /// </value>
        protected IServiceProvider DependencyProvider => this.serviceScopeContainer.Value.ServiceScope.ServiceProvider;

        /// <summary>
        /// Gets the ID of the active tab, or <see cref="Null.NullInteger"/> when there is no
        /// resolved portal context (for example during interactive Blazor rendering, where
        /// <c>HttpContext</c> is <see langword="null"/>).
        /// </summary>
        private int ActiveTabId => this.PortalSettings?.ActiveTab?.TabID ?? Null.NullInteger;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string EditUrl()
        {
            return this.ModuleContext.EditUrl();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string EditUrl(string controlKey)
        {
            return this.ModuleContext.EditUrl(controlKey);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string EditUrl(string keyName, string keyValue)
        {
            return this.ModuleContext.EditUrl(keyName, keyValue);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string EditUrl(string keyName, string keyValue, string controlKey)
        {
            return this.ModuleContext.EditUrl(keyName, keyValue, controlKey);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string EditUrl(string keyName, string keyValue, string controlKey, params string[] additionalParameters)
        {
            return this.ModuleContext.EditUrl(keyName, keyValue, controlKey, additionalParameters);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string EditUrl(int tabID, string controlKey, bool pageRedirect, params string[] additionalParameters)
        {
            return this.ModuleContext.NavigateUrl(tabID, controlKey, pageRedirect, additionalParameters);
        }

        public int GetNextActionID()
        {
            return this.ModuleContext.GetNextActionID();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Gets the file name for the module cache.</summary>
        /// <param name="tabModuleId">The tab-module ID.</param>
        /// <returns>The absolute file path.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        [DnnDeprecated(7, 0, 0, "Please use ModuleController.CacheFileName(TabModuleID)", RemovalVersion = 11)]
        public partial string GetCacheFileName(int tabModuleId)
        {
            string strCacheKey = "TabModule:";
            strCacheKey += tabModuleId + ":";
            strCacheKey += Thread.CurrentThread.CurrentUICulture.ToString();
            return PortalController.Instance.GetCurrentSettings().HomeDirectoryMapPath + "Cache" + @"\" + Globals.CleanFileName(strCacheKey) + ".resources";
        }

        /// <summary>Gets the cache key for the module.</summary>
        /// <param name="tabModuleId">The tab-module ID.</param>
        /// <returns>The cache key.</returns>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        [DnnDeprecated(7, 0, 0, "Please use ModuleController.CacheKey(TabModuleID)", RemovalVersion = 11)]
        public partial string GetCacheKey(int tabModuleId)
        {
            string strCacheKey = "TabModule:";
            strCacheKey += tabModuleId + ":";
            strCacheKey += Thread.CurrentThread.CurrentUICulture.ToString();
            return strCacheKey;
        }

        /// <inheritdoc cref="ModuleController.SynchronizeModule" />
        [DnnDeprecated(7, 0, 0, "Please use ModuleController.SynchronizeModule(ModuleId)", RemovalVersion = 11)]
        public partial void SynchronizeModule()
        {
            ModuleController.SynchronizeModule(this.ModuleId);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.serviceScopeContainer.IsValueCreated)
                {
                    this.serviceScopeContainer.Value.Dispose();
                }
            }
        }

        /// <inheritdoc />
        /// <remarks>Replaces the WebForms <c>Init</c> phase; <see cref="UserControlBase.OnInit(EventArgs)"/> is raised by the base class.</remarks>
        protected override void OnInitialized()
        {
            var activeTabId = this.ActiveTabId;
            this.tracelLogger.PortalModuleBaseOnInitStart(activeTabId, this.ModuleId, this.GetType());
            base.OnInitialized();
            this.tracelLogger.PortalModuleBaseOnInitEnd(activeTabId, this.ModuleId, this.GetType());
        }

        /// <inheritdoc />
        /// <remarks>Replaces the WebForms <c>Load</c> phase; <see cref="UserControlBase.OnLoad(EventArgs)"/> is raised by the base class.</remarks>
        protected override void OnParametersSet()
        {
            var activeTabId = this.ActiveTabId;
            this.tracelLogger.PortalModuleBaseOnLoadStart(activeTabId, this.ModuleId, this.GetType());
            base.OnParametersSet();
            this.tracelLogger.PortalModuleBaseOnLoadEnd(activeTabId, this.ModuleId, this.GetType());
        }

        /// <summary>Helper method that can be used to add an ActionEventHandler to the Skin for this Module Control.</summary>
        /// <param name="e">The event arguments.</param>
        protected void AddActionHandler(ActionEventHandler e)
        {
            UI.Skins.Skin parentSkin = UI.Skins.Skin.GetParentSkin(this);
            parentSkin?.RegisterModuleActionEvent(this.ModuleId, e);
        }

        /// <inheritdoc cref="Localization.GetString(string,string)"/>
        [DnnDeprecated(10, 0, 2, "Use LocalizeText or LocalizeHtml")]
        protected partial string LocalizeString(string key)
        {
            return Localization.GetString(key, this.LocalResourceFile);
        }

        /// <inheritdoc cref="Localization.GetSafeJSString(string,string)"/>
        [DnnDeprecated(10, 0, 2, "Use LocalizeJsString")]
        protected partial string LocalizeSafeJsString(string key)
        {
            return Localization.GetSafeJSString(key, this.LocalResourceFile);
        }

        /// <summary>Gets the text associated with the <paramref name="key"/> in this control's <see cref="LocalResourceFile"/>.</summary>
        /// <param name="key">The resource key.</param>
        /// <returns>The localized text.</returns>
        protected string LocalizeText(string key)
            => Localization.GetString(key, this.LocalResourceFile);

        /// <summary>Gets the HTML associated with the <paramref name="key"/> in this control's <see cref="LocalResourceFile"/>.</summary>
        /// <param name="key">The resource key.</param>
        /// <returns>The localized text as HTML.</returns>
        protected IHtmlContent LocalizeHtml(string key)
            => new HtmlString(Localization.GetString(key, this.LocalResourceFile));

        /// <summary>Gets the text associated with the <paramref name="key"/> in this control's <see cref="LocalResourceFile"/>.</summary>
        /// <param name="key">The resource key.</param>
        /// <param name="addDoubleQuotes">A value that indicates whether double quotation marks will be included around the encoded string.</param>
        /// <returns>The localized text encoded as a JavaScript string.</returns>
        protected IHtmlContent LocalizeJsString(string key, bool addDoubleQuotes = false)
            => HtmlUtils.JavaScriptStringEncode(Localization.GetString(key, this.LocalResourceFile), addDoubleQuotes);
    }
}
