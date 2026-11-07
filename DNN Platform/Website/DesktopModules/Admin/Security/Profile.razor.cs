// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DesktopModules.Admin.Security
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Lists;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Profile;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Services.ClientDependency;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Skins.Controls;
    using DotNetNuke.UI.WebControls;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    using MembershipProvider = DotNetNuke.Security.Membership.MembershipProvider;

    /// <summary>The Profile UserModuleBase is used to register Users.</summary>
    /// <remarks>
    /// <para>
    /// Was <c>Profile.ascx</c> + <c>Profile.ascx.cs</c> + <c>Profile.ascx.designer.cs</c>; the markup
    /// lives in <c>Profile.razor</c>. The class was named <c>DNNProfile</c>; the Razor compiler names
    /// a component after its file, so it is <c>Profile</c> now.
    /// </para>
    /// <para>
    /// <b>Per-property visibility is an authorization control and is ported unchanged.</b> The rules,
    /// in the order <see cref="DataBind"/> applies them:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// An administrator who is <i>not</i> looking at their own profile (<c>IsAdmin &amp;&amp;
    /// !IsProfile</c>) has every property forced visible - this is the only place the per-property
    /// <c>Visible</c> flag is overridden, and it is still gated on both halves of that condition.
    /// </description></item>
    /// <item><description>
    /// A property is handed to the editor only when it is not <c>Deleted</c> <b>and</b> either the
    /// request is authenticated or the property is not an <c>Image</c>-typed property. Anonymous
    /// visitors therefore still never receive image properties. The image data type is still
    /// resolved through the list controller (<c>DataType</c>/<c>Image</c>) and compared on
    /// <c>EntryID</c>, not on a hard-coded number.
    /// </description></item>
    /// <item><description>
    /// The per-property visibility <i>selectors</i> are offered only when the portal setting
    /// <c>Profile_DisplayVisibility</c> is on <b>and</b> the viewer is the profile owner
    /// (<see cref="ShowVisibility"/>). An administrator editing somebody else cannot change that
    /// user's visibility choices.
    /// </description></item>
    /// <item><description>
    /// Read-only properties stay read-only unless the viewer is in the Administrator role; that rule
    /// lives in <see cref="ProfileEditorControl"/> and is untouched.
    /// </description></item>
    /// </list>
    /// <para>
    /// The update guard is reproduced character for character, including its original (and
    /// deliberately not "tidied") shape <c>IsUserOrAdmin == false &amp;&amp; UserId ==
    /// Null.NullInteger</c>, and the <see cref="Null.NullInteger"/> sentinel is kept rather than a
    /// <c>&lt;= 0</c> test, because user ID 0 is a real ID.
    /// </para>
    /// <para>
    /// <c>this.ID = "Profile.ascx"</c> existed purely so that <c>LocalResourceFile</c> resolved to
    /// <c>App_LocalResources/Profile.ascx</c>. Components have no <c>ID</c>, and the type is no
    /// longer called <c>Profile.ascx</c> either, so <see cref="OnInit"/> sets
    /// <c>LocalResourceFile</c> to that same path explicitly - otherwise every prompt on the page
    /// would silently fall back to a resource file that does not exist.
    /// <c>Localization.GetString</c> is kept (through <c>LocalizeText</c> and the editor's own
    /// lookups) rather than <c>IStringLocalizer</c>, because profile prompts are commonly customised
    /// per portal in the DNN Language Editor.
    /// </para>
    /// </remarks>
    public partial class Profile : ProfileUserControlBase
    {
        /// <summary>The script that replaces the control's inline &lt;script&gt; block.</summary>
        private const string SetUpScriptPath = "~/desktopmodules/Admin/Security/Scripts/dnn.Profile.js";

        private readonly ListController listController;
        private readonly IPortalController portalController;
        private readonly IApplicationStatusInfo appStatus;
        private readonly IPortalGroupController portalGroupController;
        private readonly IEventLogger eventLogger;
        private readonly IClientResourceController clientResourceController;

        /// <summary>Replaces <c>ProfileProperties.EditMode</c> as the backing store for <see cref="EditorMode"/>.</summary>
        private PropertyEditorMode editorMode;

        /// <summary>Replaces <c>ProfileProperties.LabelMode</c>, assigned from the page culture in <see cref="OnInit"/>.</summary>
        private LabelMode labelMode;

        /// <summary>Replaces <c>actionsRow.Visible</c> as the backing store for <see cref="ShowUpdate"/>.</summary>
        private bool showUpdate = true;

        /// <summary>The filtered property collection handed to the editor. Replaces <c>ProfileProperties.DataSource</c>.</summary>
        private ProfilePropertyDefinitionCollection profileDataSource;

        /// <summary>
        /// Whether the editor still has to be re-bound after the render that carries the latest
        /// <c>User</c>/<c>ShowVisibility</c>/<c>DataSource</c> parameter values into it.
        /// </summary>
        private bool rebindPending;

        /// <summary>Initializes a new instance of the <see cref="Profile"/> class.</summary>
        public Profile()
            : this(null, null, null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Profile"/> class.</summary>
        /// <param name="listController">The list controller.</param>
        /// <param name="portalController">The portal controller.</param>
        /// <param name="appStatus">The application status.</param>
        /// <param name="portalGroupController">The portal group controller.</param>
        /// <param name="eventLogger">The event logger.</param>
        public Profile(ListController listController, IPortalController portalController, IApplicationStatusInfo appStatus, IPortalGroupController portalGroupController, IEventLogger eventLogger)
        {
            var services = Globals.GetCurrentServiceProvider();
            this.listController = listController ?? services.GetRequiredService<ListController>();
            this.portalController = portalController ?? services.GetRequiredService<IPortalController>();
            this.appStatus = appStatus ?? services.GetRequiredService<IApplicationStatusInfo>();
            this.portalGroupController = portalGroupController ?? services.GetRequiredService<IPortalGroupController>();
            this.eventLogger = eventLogger ?? services.GetRequiredService<IEventLogger>();
            this.clientResourceController = services.GetRequiredService<IClientResourceController>();
        }

        /// <summary>Gets a value indicating whether the User is valid.</summary>
        public bool IsValid => this.ProfileProperties.IsValid || this.IsAdmin;

        /// <summary>Gets the UserProfile associated with this control.</summary>
        public UserProfile UserProfile
        {
            get
            {
                UserProfile profile = null;
                if (this.User != null)
                {
                    profile = this.User.Profile;
                }

                return profile;
            }
        }

        /// <summary>Gets or sets the EditorMode.</summary>
        public PropertyEditorMode EditorMode
        {
            get => this.editorMode;
            set => this.editorMode = value;
        }

        /// <summary>Gets or sets a value indicating whether the Update button.</summary>
        public bool ShowUpdate
        {
            get => this.showUpdate;
            set => this.showUpdate = value;
        }

        /// <summary>Gets or sets the profile editor. Replaces the designer field of the same name.</summary>
        protected ProfileEditorControl ProfileProperties { get; set; }

        /// <summary>Gets or sets the JavaScript runtime used to run the control's set-up script.</summary>
        [Microsoft.AspNetCore.Components.Inject]
        protected IJSRuntime JsRuntime { get; set; }

        /// <summary>Gets a value indicating whether to display the Visibility controls.</summary>
        protected bool ShowVisibility
        {
            get
            {
                object setting = GetSetting(this.PortalId, "Profile_DisplayVisibility");
                return Convert.ToBoolean(setting) && this.IsUser;
            }
        }

        /// <summary>DataBind binds the data to the controls.</summary>
        public void DataBind()
        {
            // Before we bind the Profile to the editor we need to "update" the visible data
            var properties = new ProfilePropertyDefinitionCollection();
            var imageType = this.listController.GetListEntryInfo("DataType", "Image");
            foreach (ProfilePropertyDefinition profProperty in this.UserProfile.ProfileProperties)
            {
                if (this.IsAdmin && !this.IsProfile)
                {
                    profProperty.Visible = true;
                }

                if (!profProperty.Deleted && (this.Request.IsAuthenticated() || profProperty.DataType != imageType.EntryID))
                {
                    properties.Add(profProperty);
                }
            }

            this.profileDataSource = properties;

            // The User / ShowVisibility / DataSource values reach the editor through the component
            // parameters declared in Profile.razor, which read this.User, this.ShowVisibility and
            // this.profileDataSource. Writing them on the @ref'd instance here would bypass
            // SetParametersAsync (BL0005) and be overwritten by the next render of this page, so
            // the (re)bind is deferred until after the render that carries the new parameters.
            if (this.ProfileProperties is not null)
            {
                this.rebindPending = true;
            }

            this.StateHasChanged();
        }

        /// <summary>Page_Init runs when the control is initialized.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            // `this.ID = "Profile.ascx"` only ever served to point LocalResourceFile at
            // App_LocalResources/Profile.ascx; set that destination directly.
            this.LocalResourceFile = Path.Combine(this.ControlPath, Localization.LocalResourceDirectory + "/Profile.ascx");

            // Check if culture is RTL. PageBase.PageCulture is Localization.GetPageLocale of the
            // current portal settings, and there is no `this.Page` to reach it through any more.
            var pageCulture = Localization.GetPageLocale(this.portalController.GetCurrentSettings());
            this.labelMode = pageCulture.TextInfo.IsRightToLeft ? LabelMode.Right : LabelMode.Left;
        }

        /// <inheritdoc />
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            this.clientResourceController.RegisterScript(SetUpScriptPath);
        }

        /// <summary>Runs the control's set-up script, which used to be an inline &lt;script&gt; block.</summary>
        /// <param name="firstRender">Whether this is the first render.</param>
        /// <returns>A task.</returns>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            // DataBind() recreates the editor from the parameters the renderer has just applied,
            // so it has to run after this render rather than from DataBind() itself.
            if (this.rebindPending && this.ProfileProperties is not null)
            {
                this.rebindPending = false;
                this.ProfileProperties.DataBind();
                this.StateHasChanged();
            }

            // The original ran setUpProfile() on document.ready and again from
            // PageRequestManager.add_endRequest after every partial postback, i.e. after every
            // update of this markup. After every render is the same contract.
            var clientId = this.ProfileProperties?.ClientID;
            if (!string.IsNullOrEmpty(clientId))
            {
                await this.JsRuntime.InvokeVoidAsync("dnn.setUpProfile", clientId);
            }
        }

        /// <summary>cmdUpdate_Click runs when the Update Button is clicked.</summary>
        private void CmdUpdateClick()
        {
            if (this.IsUserOrAdmin == false && this.UserId == Null.NullInteger)
            {
                return;
            }

            if (this.IsValid)
            {
                if (this.User.UserID == this.PortalSettings.AdministratorId)
                {
                    // Clear the Portal Cache
                    DataCache.ClearPortalCache(this.UserPortalID, true);
                }

                // Update DisplayName to conform to Format
                this.UpdateDisplayName();

                if (this.PortalSettings.Registration.RequireUniqueDisplayName)
                {
                    var usersWithSameDisplayName = (List<UserInfo>)MembershipProvider.Instance().GetUsersBasicSearch(this.PortalId, 0, 2, "DisplayName", true, "DisplayName", this.User.DisplayName);
                    if (usersWithSameDisplayName.Any(user => user.UserID != this.User.UserID))
                    {
                        this.AddModuleMessage("DisplayNameNotUnique", ModuleMessage.ModuleMessageType.RedError, true);
                        return;
                    }
                }

                var properties = (ProfilePropertyDefinitionCollection)this.ProfileProperties.DataSource;

                // Update User's profile
                this.User = ProfileController.UpdateUserProfile(this.portalController, this.appStatus, this.portalGroupController, this.eventLogger, this.User, properties);

                this.OnProfileUpdated(EventArgs.Empty);
                this.OnProfileUpdateCompleted(EventArgs.Empty);
            }
        }

        private void UpdateDisplayName()
        {
            if (!string.IsNullOrEmpty(this.PortalSettings.Registration.DisplayNameFormat))
            {
                this.User.UpdateDisplayName(this.PortalSettings.Registration.DisplayNameFormat);
            }
        }
    }
}
