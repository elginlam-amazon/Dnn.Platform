// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Pages;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Internal;
    using DotNetNuke.Entities.Controllers;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.UI.Utilities;

    using Microsoft.Extensions.DependencyInjection;

    using Globals = DotNetNuke.Common.Globals;
    using UrlEncoder = System.Text.Encodings.Web.UrlEncoder;

    /// <summary>The DNN default page: the layout component that hosts a portal page's content.</summary>
    /// <remarks>
    /// <para>
    /// This was the code-behind of <c>Default.aspx</c> (a <c>System.Web.UI.Page</c>). Its Blazor
    /// equivalent is the host page's layout component: it derives from <see cref="PageBase"/> (a
    /// <see cref="Microsoft.AspNetCore.Components.LayoutComponentBase"/>), so a DNN skin renders
    /// <c>@Body</c> inside it. It must NOT emit <c>&lt;html&gt;</c>/<c>&lt;head&gt;</c>/<c>&lt;body&gt;</c>
    /// or a <c>HeadOutlet</c> — the host application's <c>App.razor</c> owns those.
    /// </para>
    /// <para>
    /// The page metadata fields (<see cref="Title"/>, <see cref="Description"/>,
    /// <see cref="KeyWords"/>, <see cref="Author"/>, <see cref="Copyright"/>,
    /// <see cref="Generator"/>, <see cref="Comment"/>) were assigned by the skin and then written into
    /// the page's <c>&lt;head&gt;</c> during render. They keep their names and types, but they are now
    /// backed by <see cref="IPageService"/>, the scoped service that collects a request's head content
    /// and that the layout renders — so assigning one actually emits the tag instead of only being
    /// remembered on the page object. <see cref="ApplyPageMetadata"/> flushes the author/copyright/
    /// generator/comment values, which have no direct <see cref="IPageService"/> setter, as meta tags.
    /// </para>
    /// <para>
    /// The WebForms class also spoofed <c>HttpContext.Handler</c> so that the ASP.NET page handler
    /// looked like a DNN page to downstream modules. ASP.NET Core has no handler to spoof; the
    /// equivalent information (the resolved <c>PortalSettings</c>) is published by the DNN URL rewriter
    /// middleware into <c>HttpContext.Items</c>, which is what <see cref="PortalSettings.Current"/>
    /// reads. Nothing replaces the spoof and nothing needs to.
    /// </para>
    /// </remarks>
    public class CDefault : PageBase
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public string Author = string.Empty;

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public string Comment = string.Empty;

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public string Copyright = string.Empty;

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public string Description = string.Empty;

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public string Generator = string.Empty;

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public string KeyWords = string.Empty;

        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public string Title = string.Empty;

        private static readonly object InstallerFilesRemovedLock = new object();

        private readonly IHostSettingsService injectedHostSettingsService;

        private IHostSettingsService resolvedHostSettingsService;

        /// <summary>Initializes a new instance of the <see cref="CDefault"/> class.</summary>
        /// <remarks>This is the constructor the Blazor renderer uses; services are resolved on first use.</remarks>
        [Obsolete("Deprecated in DotNetNuke 10.0.2. Please use overload with IPortalController. Scheduled removal in v12.0.0.")]
        public CDefault()
            : this(null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="CDefault"/> class.</summary>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="hostSettings">The host settings.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IUserController. Scheduled removal in v12.0.0.")]
        public CDefault(IPortalController portalController, IApplicationStatusInfo appStatus, IHostSettings hostSettings)
            : this(portalController, appStatus, hostSettings, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="CDefault"/> class.</summary>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="userController">The user controller.</param>
        /// <param name="hostSettingsService">The host settings service.</param>
        public CDefault(IPortalController portalController, IApplicationStatusInfo appStatus, IHostSettings hostSettings, IUserController userController, IHostSettingsService hostSettingsService)
            : base(portalController, appStatus, hostSettings, userController)
        {
            this.injectedHostSettingsService = hostSettingsService;
        }

        /// <summary>Gets the host settings service.</summary>
        protected IHostSettingsService HostSettingsService
            => this.resolvedHostSettingsService ??= this.injectedHostSettingsService
               ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettingsService>();

        protected string AdvancedSettingsPageUrl
        {
            get
            {
                string result;
                var tab = TabController.Instance.GetTabByName("Advanced Settings", this.PortalSettings.PortalId);
                var modules = ModuleController.Instance.GetTabModules(tab.TabID).Values;

                if (modules.Count > 0)
                {
                    var pmb = new PortalModuleBase();

                    // Was Server.UrlEncode, which does not exist on ASP.NET Core.
                    var returnUrl = UrlEncoder.Default.Encode(TestableGlobals.Instance.NavigateURL());
                    result = pmb.EditUrl(tab.TabID, string.Empty, false, string.Concat("mid=", modules.ElementAt(0).ModuleID), "popUp=true", string.Concat("ReturnUrl=", returnUrl));
                }
                else
                {
                    result = TestableGlobals.Instance.NavigateURL(tab.TabID);
                }

                return result;
            }
        }

        /// <summary>Allows the scroll position on the page to be moved to the top of the passed in control.</summary>
        /// <param name="objControl">Control to scroll to.</param>
        /// <remarks>The <c>objControl</c> parameter was a <c>System.Web.UI.Control</c>; it is now the Client API's control abstraction.</remarks>
        public void ScrollToControl(IClientApiControl objControl)
        {
            if (!ClientAPI.BrowserSupportsFunctionality(ClientAPI.ClientFunctionality.Positioning))
            {
                return;
            }

            var context = HttpContextSource.Current;
            JavaScript.RegisterClientReference(context, ClientAPI.ClientNamespaceReferences.dnn_dom_positioning);

            var clientApiPage = ClientApiPageAccessor.GetPage(context, nameof(this.ScrollToControl));
            if (clientApiPage == null)
            {
                return;
            }

            ClientAPI.RegisterClientVariable(clientApiPage, "ScrollToControl", objControl?.ClientId, true);
            DNNClientAPI.SetScrollTop(clientApiPage);
        }

        /// <summary>Writes this page's metadata fields into the page's head.</summary>
        /// <remarks>
        /// Replaces the WebForms render-time assignment of the <c>&lt;title&gt;</c> and the
        /// <c>description</c>/<c>keywords</c>/<c>author</c>/<c>copyright</c>/<c>generator</c> meta tags
        /// (and the HTML comment) from the public fields. A skin calls this after setting them; it is
        /// also called from <see cref="OnPreRender"/> so a skin that only assigns the fields still gets
        /// its head content.
        /// </remarks>
        public void ApplyPageMetadata()
        {
            var pageService = this.PageService;

            if (!string.IsNullOrEmpty(this.Title))
            {
                pageService.SetTitle(this.Title);
            }

            if (!string.IsNullOrEmpty(this.Description))
            {
                pageService.SetDescription(this.Description);
            }

            if (!string.IsNullOrEmpty(this.KeyWords))
            {
                pageService.SetKeyWords(this.KeyWords);
            }

            AddMeta(pageService, "author", this.Author);
            AddMeta(pageService, "copyright", this.Copyright);
            AddMeta(pageService, "generator", this.Generator);

            if (!string.IsNullOrEmpty(this.Comment))
            {
                pageService.AddToHead(new PageTag($"<!-- {this.Comment} -->", PagePriority.Default));
            }
        }

        /// <inheritdoc />
        protected override void RegisterAjaxScript()
        {
            // Was guarded by `if (this.Page.Form != null)`: a page without a server form could not host
            // the script. Every Blazor page is rendered inside the host's document, so the equivalent
            // precondition is simply that there is a request to register against.
            if (HttpContextSource.Current != null && ServicesFrameworkInternal.Instance.IsAjaxScriptSupportRequired)
            {
                ServicesFrameworkInternal.Instance.RegisterAjaxScript(HttpContextSource.Current);
            }
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            this.ApplyPageMetadata();
        }

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        protected void ManageInstallerFiles()
        {
            if (this.HostSettingsService.GetBoolean("InstallerFilesRemoved"))
            {
                return;
            }

            lock (InstallerFilesRemovedLock)
            {
                if (!this.HostSettingsService.GetBoolean("InstallerFilesRemoved"))
                {
                    Services.Upgrade.Upgrade.DeleteInstallerFiles();
                    this.HostSettingsService.Update("InstallerFilesRemoved", "True", true);
                }
            }
        }

        private static void AddMeta(IPageService pageService, string name, string content)
        {
            if (!string.IsNullOrEmpty(content))
            {
                pageService.AddMeta(new PageMeta(name, content, PagePriority.Default));
            }
        }
    }
}
