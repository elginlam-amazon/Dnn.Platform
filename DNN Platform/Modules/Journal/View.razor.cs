// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.Journal
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text.Encodings.Web;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Modules.Journal.Components;
    using DotNetNuke.Modules.Journal.Controls;
    using DotNetNuke.Security.Roles;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Web.Client.ResourceManager;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Html;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>The ViewJournal class displays the content.</summary>
    /// <remarks>
    /// Was <c>View.ascx</c> + <c>View.ascx.cs</c> + <c>View.ascx.designer.cs</c>; the markup is
    /// <c>View.razor</c>. The designer-generated <c>ctlJournalList</c> field is created here
    /// (keeping its <c>ctlJournalList</c> ID) because <c>&lt;dnnj:JournalListControl&gt;</c> has no
    /// Blazor tag equivalent. The view renders a &lt;JournalListControl&gt; child component and
    /// passes this instance's values through as parameters, so the values assigned below are the
    /// rendered ones and the child runs the full component lifecycle.
    /// </remarks>
    public partial class View : JournalModuleBase
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public int PageSize = 20;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public bool AllowPhotos = true;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public bool AllowFiles = true;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public int MaxMessageLength = 250;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public bool CanRender = true;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public bool ShowEditor = true;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public bool CanComment = true;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public bool IsGroup;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public string BaseUrl;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public string ProfilePage;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public int Gid = -1;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public int Pid = -1;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public long MaxUploadSize;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        public bool IsPublicGroup;

        /// <summary>The query string parameters of the user profile page link.</summary>
        private static readonly string[] ProfilePageParams = { "userId=xxx" };

        /// <summary>
        /// The journal list this view renders, which was the <c>ctlJournalList</c> field the
        /// <c>.ascx.designer.cs</c> generated for <c>&lt;dnnj:JournalListControl ID="ctlJournalList"&gt;</c>.
        /// </summary>
        private readonly JournalListControl ctlJournalList = new JournalListControl { ID = "ctlJournalList" };

        private INavigationManager navigationManager;
        private IClientResourceController clientResourceController;
        private IApplicationStatusInfo appStatus;
        private IEventLogger eventLogger;
        private IServicesFramework servicesFramework;
        private IHostSettings hostSettings;

        /// <summary>Initializes a new instance of the <see cref="View"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with INavigationManager. Scheduled removal in v12.0.0.")]
        public View()
            : this(null, null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="View"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.4. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public View(INavigationManager navigationManager, IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, IServicesFramework servicesFramework)
            : this(navigationManager, clientResourceController, appStatus, eventLogger, servicesFramework, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="View"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="servicesFramework">The web API service framework.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <remarks>
        /// <para>
        /// The Blazor renderer creates components through their parameterless constructor, so the
        /// overloads above are what the DI-aware call sites and any existing derived control use;
        /// every service they do not supply is resolved from <see cref="PortalModuleBase.DependencyProvider"/>,
        /// exactly as the WebForms control did. The <c>[Obsolete]</c> markers are kept so the
        /// deprecation schedule is unchanged.
        /// </para>
        /// <para>
        /// The supplied services are only stored here. Resolving the unsupplied ones is deferred
        /// to <see cref="EnsureServices"/>, called from <see cref="OnInit"/>: the renderer, not
        /// DI, constructs the component, and <see cref="PortalModuleBase.DependencyProvider"/> is
        /// backed by a request-scoped lazy scope, so resolving at construction time would run
        /// against whatever scope happened to be ambient. <c>MaxUploadSize</c> is computed there
        /// too, since it needs the resolved <c>appStatus</c>.
        /// </para>
        /// </remarks>
        public View(INavigationManager navigationManager, IClientResourceController clientResourceController, IApplicationStatusInfo appStatus, IEventLogger eventLogger, IServicesFramework servicesFramework, IHostSettings hostSettings)
        {
            this.navigationManager = navigationManager;
            this.clientResourceController = clientResourceController;
            this.appStatus = appStatus;
            this.eventLogger = eventLogger;
            this.servicesFramework = servicesFramework;
            this.hostSettings = hostSettings;
        }

        /// <summary>Gets the image file types the journal uploader accepts, encoded for a script literal.</summary>
        protected MarkupString ImageFileTypesJs => this.JsEncode(Globals.ImageFileTypes);

        /// <summary>Gets the URL of the user file manager templates folder, encoded for a script literal.</summary>
        /// <remarks>Replaces <c>Page.ResolveUrl(...)</c>, which has no component equivalent.</remarks>
        protected MarkupString UserFileManagerTemplatePathJs =>
            this.JsEncode(Globals.ResolveUrl("~/Resources/Shared/Components/UserFileManager/Templates/"));

        /// <summary>Gets the base URL of this module's client resources, encoded for a script literal.</summary>
        protected MarkupString BaseUrlJs => this.JsEncode(this.BaseUrl);

        /// <inheritdoc />
        /// <remarks>
        /// This is the ported <c>OnInit</c>, verbatim. The trailing
        /// <c>InitializeComponent()</c> call is gone with the <c>this.Load += this.Page_Load</c>
        /// wiring it existed for; the handler body is <see cref="OnLoad"/> below.
        /// </remarks>
        protected override void OnInit(EventArgs e)
        {
            this.EnsureServices();

            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, CommonJs.DnnPlugins);
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, CommonJs.jQueryFileUpload);
            this.servicesFramework.RequestAjaxAntiForgerySupport();
            JavaScript.RequestRegistration(this.appStatus, this.eventLogger, this.PortalSettings, CommonJs.Knockout);

            this.clientResourceController.RegisterScript("~/DesktopModules/Journal/Scripts/journal.js");
            this.clientResourceController.RegisterScript("~/DesktopModules/Journal/Scripts/journalcomments.js");
            this.clientResourceController.RegisterScript("~/DesktopModules/Journal/Scripts/mentionsInput.js");

            // These three were the <dnn:DnnJsInclude>/<dnn:DnnCssInclude> tags of View.ascx. A
            // Razor component cannot host a tag helper, so they are registered here with the same
            // FilePath and Priority; AddTag="false" meant "register only, emit no CDF comment",
            // which is what a registration without a tag does.
            this.clientResourceController
                .CreateScript("~/Resources/Shared/Components/UserFileManager/jquery.dnnUserFileUpload.js")
                .SetPriority(102)
                .Register();
            this.clientResourceController
                .CreateScript("~/Resources/Shared/Components/UserFileManager/UserFileManager.js")
                .SetPriority(105)
                .Register();
            this.clientResourceController
                .CreateStylesheet("~/Resources/Shared/Components/UserFileManager/UserFileManager.css")
                .Register();

            var isAdmin = this.UserInfo.IsInRole(RoleController.Instance.GetRoleById(this.PortalId, this.PortalSettings.AdministratorRoleId).RoleName);
            if (this.Request?.HttpContext?.User?.Identity?.IsAuthenticated != true || (!this.UserInfo.IsSuperUser && !isAdmin && this.UserInfo.IsInRole("Unverified Users")))
            {
                this.ShowEditor = false;
            }
            else
            {
                this.ShowEditor = this.EditorEnabled;
            }

            if (this.Settings.ContainsKey(Constants.DefaultPageSize))
            {
                this.PageSize = Convert.ToInt16(this.Settings[Constants.DefaultPageSize]);
            }

            if (this.Settings.ContainsKey(Constants.MaxCharacters))
            {
                this.MaxMessageLength = Convert.ToInt16(this.Settings[Constants.MaxCharacters]);
            }

            if (this.Settings.ContainsKey(Constants.AllowPhotos))
            {
                this.AllowPhotos = Convert.ToBoolean(this.Settings[Constants.AllowPhotos]);
            }

            if (this.Settings.ContainsKey(Constants.AllowFiles))
            {
                this.AllowFiles = Convert.ToBoolean(this.Settings[Constants.AllowFiles]);
            }

            this.ctlJournalList.Enabled = true;
            this.ctlJournalList.ProfileId = -1;
            this.ctlJournalList.PageSize = this.PageSize;
            this.ctlJournalList.ModuleId = this.ModuleId;

            ModuleInfo moduleInfo = this.ModuleContext.Configuration;

            foreach (var module in ModuleController.Instance.GetTabModules(this.TabId).Values)
            {
                if (module.ModuleDefinition.FriendlyName == "Social Groups")
                {
                    if (this.GroupId == -1 && this.FilterMode == JournalMode.Auto)
                    {
                        this.ShowEditor = false;
                        this.ctlJournalList.Enabled = false;
                    }

                    if (this.GroupId > 0)
                    {
                        RoleInfo roleInfo = RoleController.Instance.GetRoleById(moduleInfo.OwnerPortalID, this.GroupId);
                        if (roleInfo != null)
                        {
                            if (this.UserInfo.IsInRole(roleInfo.RoleName))
                            {
                                this.ShowEditor = true;
                                this.CanComment = true;
                                this.IsGroup = true;
                            }
                            else
                            {
                                this.ShowEditor = false;
                                this.CanComment = false;
                            }

                            if (!roleInfo.IsPublic && !this.ShowEditor)
                            {
                                this.ctlJournalList.Enabled = false;
                            }

                            if (roleInfo.IsPublic && !this.ShowEditor)
                            {
                                this.ctlJournalList.Enabled = true;
                            }

                            if (roleInfo.IsPublic && this.ShowEditor)
                            {
                                this.ctlJournalList.Enabled = true;
                            }

                            if (roleInfo.IsPublic)
                            {
                                this.IsPublicGroup = true;
                            }
                        }
                        else
                        {
                            this.ShowEditor = false;
                            this.ctlJournalList.Enabled = false;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(this.QueryValue("userId")))
            {
                this.ctlJournalList.ProfileId = Convert.ToInt32(this.QueryValue("userId"));
                if (!this.UserInfo.IsSuperUser && !isAdmin && this.ctlJournalList.ProfileId != this.UserId)
                {
                    this.ShowEditor = this.ShowEditor && Utilities.AreFriends(UserController.GetUserById(this.hostSettings, this.PortalId, this.ctlJournalList.ProfileId), this.UserInfo);
                }
            }
            else if (this.GroupId > 0)
            {
                this.ctlJournalList.SocialGroupId = Convert.ToInt32(this.QueryValue("groupId"));
            }

            base.OnInit(e);
        }

        /// <inheritdoc />
        /// <remarks>This is the ported <c>Page_Load</c>, which ran in the WebForms <c>Load</c> phase.</remarks>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                this.BaseUrl = Globals.ApplicationPath;
                this.BaseUrl = this.BaseUrl.EndsWith("/", StringComparison.Ordinal) ? this.BaseUrl : this.BaseUrl + "/";
                this.BaseUrl += "DesktopModules/Journal/";

                this.ProfilePage = this.navigationManager.NavigateURL(this.PortalSettings.UserTabId, string.Empty, ProfilePageParams);

                if (!string.IsNullOrEmpty(this.QueryValue("userId")))
                {
                    this.Pid = Convert.ToInt32(this.QueryValue("userId"));
                    this.ctlJournalList.ProfileId = this.Pid;
                }
                else if (this.GroupId > 0)
                {
                    this.Gid = this.GroupId;
                    this.ctlJournalList.SocialGroupId = this.GroupId;
                }

                this.ctlJournalList.PageSize = this.PageSize;
            }
            catch (Exception exc)
            {
                Exceptions.ProcessModuleLoadException(this, exc);
            }
        }

        /// <summary>Gets the localized value of <paramref name="key"/>, encoded for a script literal.</summary>
        /// <param name="key">The resource key.</param>
        /// <returns>The encoded value, emitted verbatim.</returns>
        /// <remarks>
        /// <see cref="PortalModuleBase.LocalizeJsString"/> returns an <see cref="IHtmlContent"/>
        /// which <c>&lt;%: … %&gt;</c> wrote without HTML-encoding it; Blazor HTML-encodes anything
        /// that is not a <see cref="MarkupString"/>, which would corrupt the script literal, so the
        /// content is materialized here instead.
        /// </remarks>
        protected MarkupString JsString(string key) => ToMarkup(this.LocalizeJsString(key));

        /// <summary>JavaScript-encodes <paramref name="value"/> for a script literal.</summary>
        /// <param name="value">The value to encode.</param>
        /// <returns>The encoded value, emitted verbatim.</returns>
        /// <remarks>Replaces <c>&lt;%: JavaScriptStringEncode(…) %&gt;</c>.</remarks>
        protected MarkupString JsEncode(string value) => ToMarkup(HtmlUtils.JavaScriptStringEncode(value));

        private static MarkupString ToMarkup(IHtmlContent content)
        {
            using (var writer = new StringWriter())
            {
                content.WriteTo(writer, HtmlEncoder.Default);
                return (MarkupString)writer.ToString();
            }
        }

        /// <summary>Gets the value of the given query string parameter.</summary>
        /// <param name="key">The parameter name.</param>
        /// <returns>The value, or an empty string when it was not supplied.</returns>
        /// <remarks>Replaces <c>Request.QueryString[key]</c>.</remarks>
        private string QueryValue(string key) => this.Request?.Query[key].ToString() ?? string.Empty;

        /// <summary>Resolves any service a constructor did not supply, and derives MaxUploadSize.</summary>
        /// <remarks>
        /// Runs in the initialization phase rather than in the constructor: the renderer owns
        /// construction, so <see cref="PortalModuleBase.DependencyProvider"/> is only reliably
        /// usable once the component is initializing against its request scope. Services the
        /// DI-aware constructors did supply are left as they are.
        /// </remarks>
        private void EnsureServices()
        {
            this.navigationManager ??= this.DependencyProvider.GetRequiredService<INavigationManager>();
            this.clientResourceController ??= this.DependencyProvider.GetRequiredService<IClientResourceController>();
            this.appStatus ??= this.DependencyProvider.GetRequiredService<IApplicationStatusInfo>();
            this.eventLogger ??= this.DependencyProvider.GetRequiredService<IEventLogger>();
            this.servicesFramework ??= this.DependencyProvider.GetRequiredService<IServicesFramework>();
            this.hostSettings ??= this.DependencyProvider.GetRequiredService<IHostSettings>();
            this.MaxUploadSize = Config.GetMaxUploadSize(this.appStatus);
        }
    }
}
