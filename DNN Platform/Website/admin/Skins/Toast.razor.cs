// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Skins.Controls
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Xml;

    using DotNetNuke.Abstractions;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Website;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>A skin/theme object which allows the display of toast messages.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>Toast.ascx</c> + <c>Toast.ascx.cs</c> + <c>Toast.ascx.designer.cs</c>; the markup now
    /// lives in <c>Toast.razor</c>. The single designer field <c>addtionalScripts</c> (an
    /// <c>asp:Literal</c>) becomes <see cref="AdditionalScripts"/> +
    /// <see cref="AdditionalScriptsVisible"/>.
    /// </para>
    /// <para>
    /// The inline document-ready script of the <c>.ascx</c> is built by
    /// <see cref="BuildToastScript"/> from the same server side values and emitted through
    /// <see cref="DotNetNuke.UI.Utilities.IClientApiPage.RegisterStartupScript"/>, which is DNN's
    /// keyed per-page script seam (a Razor component may not declare a <c>script</c> element).
    /// </para>
    /// <para>
    /// <c>this.Server.MapPath(...)</c> is gone: the <c>Toast.config</c> path is resolved with
    /// <see cref="HttpContextSource.MapPath"/>, which is content-root correct and needs no
    /// request. When the component was not loaded from a markup source (so
    /// <c>TemplateSourceDirectory</c> is empty) the control's own shipped folder is used, which is
    /// where the config file lived.
    /// </para>
    /// </remarks>
    public partial class Toast
    {
        private const string MyFileName = "Toast.ascx";

        /// <summary>The folder <c>Toast.config</c> ships in, used when no markup source is known.</summary>
        private const string DefaultConfigDirectory = "~/admin/Skins";

        private static readonly ILogger Logger = DnnLoggingController.GetLogger<Toast>();
        private static readonly string ToastCacheKey = "DNN_Toast_Config";
        private readonly INavigationManager navigationManager;
        private readonly IJavaScriptLibraryHelper javaScript;
        private readonly IClientResourceController clientResourceController;
        private readonly IServicesFramework servicesFramework;

        /// <summary>Initializes a new instance of the <see cref="Toast"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public Toast()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Toast"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="javaScript">The JavaScript library helper.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IServicesFramework. Scheduled removal in v12.0.0.")]
        public Toast(INavigationManager navigationManager, IJavaScriptLibraryHelper javaScript, IClientResourceController clientResourceController)
            : this(navigationManager, javaScript, clientResourceController, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Toast"/> class.</summary>
        /// <param name="navigationManager">The navigation manager.</param>
        /// <param name="javaScript">The JavaScript library helper.</param>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="servicesFramework">The web API services framework.</param>
        public Toast(INavigationManager navigationManager, IJavaScriptLibraryHelper javaScript, IClientResourceController clientResourceController, IServicesFramework servicesFramework)
        {
            this.navigationManager = navigationManager ?? Globals.GetCurrentServiceProvider().GetRequiredService<INavigationManager>();
            this.javaScript = javaScript ?? Globals.GetCurrentServiceProvider().GetRequiredService<IJavaScriptLibraryHelper>();
            this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
            this.servicesFramework = servicesFramework ?? Globals.GetCurrentServiceProvider().GetRequiredService<IServicesFramework>();
        }

        protected string ServiceModuleName { get; private set; }

        protected string ServiceAction { get; private set; }

        /// <summary>Gets the operator supplied markup of the <c>scripts</c> node of <c>Toast.config</c>.</summary>
        /// <remarks>Replaces <c>addtionalScripts.Text</c>.</remarks>
        protected string AdditionalScripts { get; private set; }

        /// <summary>Gets a value indicating whether <see cref="AdditionalScripts"/> renders.</summary>
        /// <remarks>Replaces <c>addtionalScripts.Visible</c>, which defaulted to <see langword="false"/>.</remarks>
        protected bool AdditionalScriptsVisible { get; private set; }

        /// <summary>Determines whether there is an authenticated user to show toasts for.</summary>
        /// <returns><see langword="true"/> when a user is signed in.</returns>
        public bool IsOnline()
        {
            var userInfo = UserController.Instance.GetCurrentUserInfo();

            // SENTINEL: -1 (Null.NullInteger) is "anonymous"; a real UserID of 0 must not be rejected.
            return userInfo.UserID != -1;
        }

        /// <summary>Gets the URL of the notifications view of the message center.</summary>
        /// <returns>The URL.</returns>
        public string GetNotificationLink()
        {
            return this.GetMessageLink() + "?view=notifications&action=notifications";
        }

        /// <summary>Gets the URL of the message center.</summary>
        /// <returns>The URL.</returns>
        public string GetMessageLink()
        {
            return this.navigationManager.NavigateURL(this.GetMessageTab(), string.Empty, string.Format("userId={0}", this.PortalSettings.UserId));
        }

        /// <summary>Gets the localized "see all messages" text.</summary>
        /// <returns>The localized text.</returns>
        public string GetMessageLabel()
        {
            return Localization.GetString("SeeAllMessage", Localization.GetResourceFile(this, MyFileName));
        }

        /// <summary>Gets the localized "see all notifications" text.</summary>
        /// <returns>The localized text.</returns>
        public string GetNotificationLabel()
        {
            return Localization.GetString("SeeAllNotification", Localization.GetResourceFile(this, MyFileName));
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            this.javaScript.RequestRegistration(CommonJs.jQueryUI);
            this.servicesFramework.RequestAjaxAntiForgerySupport();

            this.clientResourceController.RegisterScript("~/resources/Shared/components/Toast/jquery.toastmessage.js", FileOrder.Js.jQuery);
            this.clientResourceController.RegisterStylesheet("~/resources/Shared/components/Toast/jquery.toastmessage.css", FileOrder.Css.DefaultCss);

            this.InitializeConfig();

            this.RegisterToastScript();
        }

        /// <summary>Emits the document-ready glue the <c>.ascx</c> declared inline.</summary>
        private void RegisterToastScript()
        {
            var page = this.Page;
            if (page == null)
            {
                Logger.LogWarning(
                    "The Toast skin object could not register its client script because no IClientApiPage is available; toast notifications will not poll.");
                return;
            }

            if (page.IsClientScriptBlockRegistered(typeof(Toast), "dnn.toast"))
            {
                return;
            }

            page.RegisterStartupScript(typeof(Toast), "dnn.toast", this.BuildToastScript(), true);
        }

        /// <summary>Builds the toast client script, preserving the <c>.ascx</c> script verbatim.</summary>
        /// <returns>The script to run on document ready.</returns>
        private string BuildToastScript()
        {
            var serviceModuleName = Localization.GetSafeJSString(this.ServiceModuleName);
            var serviceAction = Localization.GetSafeJSString(this.ServiceAction);
            var notificationLink = Localization.GetSafeJSString(this.GetNotificationLink());
            var notificationLabel = Localization.GetSafeJSString(this.GetNotificationLabel());
            var isOnline = this.IsOnline() ? "true" : "false";

            return $@"
    $(document).ready(function () {{

        if (typeof dnn == 'undefined') dnn = {{}};
        if (typeof dnn.toast == 'undefined') dnn.toast = {{}};
        var sf = $.ServicesFramework();

        dnn.toast.refreshUser = function () {{
            $.ajax({{
                type: ""GET"",
                url: sf.getServiceRoot('{serviceModuleName}') + '{serviceAction}',
                contentType: ""application/json"",
                dataType: ""json"",
                cache: false,
                success: function (data) {{
                    if (typeof dnn.toast.toastTimer !== 'undefined') {{
                        // Cancel the periodic update.
                        clearTimeout(dnn.toast.toastTimer);
                        delete dnn.toast.toastTimer;
                    }}

                    if (!data || !data.Success) {{
                        return;
                    }}

                    $(document).trigger('dnn.toastupdate', data);

                    var toastMessages = [];

                    for (var i = 0; i < data.Toasts.length; i++) {{
                        var toast = {{
                            subject: data.Toasts[i].Subject,
                            body: data.Toasts[i].Body
                        }};

                        toastMessages.push(toast);
                    }}

                    var message = {{
                        messages: toastMessages,
                        seeMoreLink: '{notificationLink}', seeMoreText: '{notificationLabel}'
                    }};

                    $().dnnToastMessage('showAllToasts', message);

                    dnn.toast.toastTimer = setTimeout(dnn.toast.refreshUser, 30000);
                }},
                error: function (xhr, status, error) {{
                    if (typeof dnn.toast.toastTimer !== 'undefined') {{
                        // Cancel the periodic update.
                        clearTimeout(dnn.toast.toastTimer);
                        delete dnn.toast.toastTimer;
                    }}
                }}
            }});
        }};

        function checkLogin() {{
            return {isOnline};
        }};

        // initial setup for toast
        var pageUnloaded = window.dnnModal && window.dnnModal.pageUnloaded;
        if (checkLogin() && !pageUnloaded) {{
            dnn.toast.toastTimer = setTimeout(dnn.toast.refreshUser, 4000);
        }}
    }});
";
        }

        // This method is copied from user skin object
        private int GetMessageTab()
        {
            var cacheKey = string.Format("MessageCenterTab:{0}:{1}", this.PortalSettings.PortalId, this.PortalSettings.CultureCode);
            var messageTabId = DataCache.GetCache<int>(cacheKey);
            if (messageTabId > 0)
            {
                return messageTabId;
            }

            // Find the Message Tab
            messageTabId = this.FindMessageTab();

            // save in cache
            // NOTE - This cache is not being cleared. There is no easy way to clear this, except Tools->Clear Cache
            DataCache.SetCache(cacheKey, messageTabId, TimeSpan.FromMinutes(20));

            return messageTabId;
        }

        // This method is copied from user skin object
        private int FindMessageTab()
        {
            // On brand new install the new Message Center Module is on the child page of User Profile Page
            // On Upgrade to 6.2.0, the Message Center module is on the User Profile Page
            var profileTab = TabController.Instance.GetTab(this.PortalSettings.UserTabId, this.PortalSettings.PortalId, false);
            if (profileTab != null)
            {
                var childTabs = TabController.Instance.GetTabsByPortal(profileTab.PortalID).DescendentsOf(profileTab.TabID);
                foreach (TabInfo tab in childTabs)
                {
                    foreach (KeyValuePair<int, ModuleInfo> kvp in ModuleController.Instance.GetTabModules(tab.TabID))
                    {
                        var module = kvp.Value;
                        if (module.DesktopModule.FriendlyName == "Message Center")
                        {
                            return tab.TabID;
                        }
                    }
                }
            }

            // default to User Profile Page
            return this.PortalSettings.UserTabId;
        }

        private void InitializeConfig()
        {
            this.ServiceModuleName = "InternalServices";
            this.ServiceAction = "NotificationsService/GetToasts";

            try
            {
                var toastConfig = DataCache.GetCache<IDictionary<string, string>>(ToastCacheKey);
                if (toastConfig == null)
                {
                    var sourceDirectory = string.IsNullOrEmpty(this.TemplateSourceDirectory)
                        ? DefaultConfigDirectory
                        : this.TemplateSourceDirectory;
                    var configFile = HttpContextSource.MapPath(Path.Combine(sourceDirectory, "Toast.config"));

                    if (File.Exists(configFile))
                    {
                        var xmlDocument = new XmlDocument { XmlResolver = null };
                        using (var configReader = XmlReader.Create(configFile, new XmlReaderSettings { XmlResolver = null, }))
                        {
                            xmlDocument.Load(configReader);
                        }

                        var moduleNameNode = xmlDocument.DocumentElement?.SelectSingleNode("moduleName");
                        var actionNode = xmlDocument.DocumentElement?.SelectSingleNode("action");
                        var scriptsNode = xmlDocument.DocumentElement?.SelectSingleNode("scripts");

                        if (moduleNameNode != null && !string.IsNullOrEmpty(moduleNameNode.InnerText))
                        {
                            this.ServiceModuleName = moduleNameNode.InnerText;
                        }

                        if (actionNode != null && !string.IsNullOrEmpty(actionNode.InnerText))
                        {
                            this.ServiceAction = actionNode.InnerText;
                        }

                        if (scriptsNode != null && !string.IsNullOrEmpty(scriptsNode.InnerText))
                        {
                            this.AdditionalScripts = scriptsNode.InnerText;
                            this.AdditionalScriptsVisible = true;
                        }
                    }

                    var config = new Dictionary<string, string>()
                    {
                        { "ServiceModuleName", this.ServiceModuleName },
                        { "ServiceAction", this.ServiceAction },
                        { "AddtionalScripts", this.AdditionalScripts },
                    };
                    DataCache.SetCache(ToastCacheKey, config);
                }
                else
                {
                    if (!string.IsNullOrEmpty(toastConfig["ServiceModuleName"]))
                    {
                        this.ServiceModuleName = toastConfig["ServiceModuleName"];
                    }

                    if (!string.IsNullOrEmpty(toastConfig["ServiceAction"]))
                    {
                        this.ServiceAction = toastConfig["ServiceAction"];
                    }

                    if (!string.IsNullOrEmpty(toastConfig["AddtionalScripts"]))
                    {
                        this.AdditionalScripts = toastConfig["AddtionalScripts"];
                        this.AdditionalScriptsVisible = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ToastInitializeConfigException(ex);
            }
        }
    }
}
