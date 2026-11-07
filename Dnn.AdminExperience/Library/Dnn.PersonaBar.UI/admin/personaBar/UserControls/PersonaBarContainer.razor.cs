// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable once CheckNamespace
namespace Dnn.PersonaBar.UI.UserControls;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Dnn.PersonaBar.Library.Containers;
using Dnn.PersonaBar.Library.Controllers;

using DotNetNuke.Abstractions.Application;
using DotNetNuke.Abstractions.ClientResources;
using DotNetNuke.Abstractions.Logging;
using DotNetNuke.Abstractions.Security.Permissions;
using DotNetNuke.Entities.Users;
using DotNetNuke.Framework;
using DotNetNuke.Framework.JavaScriptLibraries;
using DotNetNuke.Services.ClientDependency;
using DotNetNuke.UI.ControlPanels;
using DotNetNuke.UI.Utilities;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using Newtonsoft.Json;

using Globals = DotNetNuke.Common.Globals;

/// <summary>The control containing the Persona Bar.</summary>
/// <remarks>
/// Was <c>PersonaBarContainer.ascx</c> + <c>PersonaBarContainer.ascx.cs</c> +
/// <c>PersonaBarContainer.ascx.designer.cs</c>; the markup is <c>PersonaBarContainer.razor</c>.
/// <list type="bullet">
/// <item><description>the designer-generated <c>PersonaBarPanel</c> <c>Panel</c> (<c>Visible="False"</c>
/// in markup, assigned from <c>InjectPersonaBar()</c> in <c>OnLoad</c>) is the
/// <see cref="PersonaBarPanelVisible"/> state the markup's wrapper <c>div</c> is guarded by;</description></item>
/// <item><description>the inline <c>&lt;script&gt;</c> block moved to
/// <c>admin/personaBar/scripts/personaBarContainerInterop.js</c>, because a <c>&lt;script&gt;</c>
/// element produced by a render tree is never executed by the browser;</description></item>
/// <item><description><c>RemovedAdminStyleSheet()</c>'s walk of the page's
/// <c>ClientResourceIncludes</c> control collection is now the equivalent
/// <see cref="IClientResourceController.RemoveStylesheetByPath"/> exclusion, since client resources
/// are no longer WebForms child controls.</description></item>
/// </list>
/// This component is not routable: it is loaded through <c>ControlUtilities.LoadControl</c> /
/// <c>IDnnControlTypeResolver</c> from the <c>ControlPanel</c> host setting, which still stores the
/// historical <c>PersonaBarContainer.ascx</c> path.
/// </remarks>
public partial class PersonaBarContainer : ControlPanelBase, IAsyncDisposable
{
    private readonly IPersonaBarContainer personaBarContainer;
    private readonly IPersonaBarController personaBarController;
    private readonly IHostSettings hostSettings;
    private readonly IJavaScriptLibraryHelper javaScript;
    private readonly IClientResourceController clientResourceController;
    private readonly IServicesFramework servicesFramework;

    private IJSObjectReference interopModule;
    private bool loaded;

    /// <summary>Initializes a new instance of the <see cref="PersonaBarContainer"/> class.</summary>
    [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IEventLogger. Scheduled removal in v12.0.0.")]
    public PersonaBarContainer()
        : this(null, null, null, null, null, null, null, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PersonaBarContainer"/> class.</summary>
    /// <param name="personaBarContainer">The Persona Bar container.</param>
    /// <param name="personaBarController">The Persona Bar controller.</param>
    /// <param name="hostSettings">The host settings.</param>
    /// <param name="javaScript">The JavaScript library helper.</param>
    /// <param name="clientResourceController">The client resources controller.</param>
    [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IEventLogger. Scheduled removal in v12.0.0.")]
    public PersonaBarContainer(IPersonaBarContainer personaBarContainer, IPersonaBarController personaBarController, IHostSettings hostSettings, IJavaScriptLibraryHelper javaScript, IClientResourceController clientResourceController)
        : this(null, null, personaBarContainer, personaBarController, hostSettings, javaScript, clientResourceController, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PersonaBarContainer"/> class.</summary>
    /// <param name="eventLogger">The event logger.</param>
    /// <param name="permissionDefinitionService">The permission definition service.</param>
    /// <param name="personaBarContainer">The Persona Bar container.</param>
    /// <param name="personaBarController">The Persona Bar controller.</param>
    /// <param name="hostSettings">The host settings.</param>
    /// <param name="javaScript">The JavaScript library helper.</param>
    /// <param name="clientResourceController">The client resources controller.</param>
    /// <param name="servicesFramework">The web API service framework.</param>
    public PersonaBarContainer(IEventLogger eventLogger, IPermissionDefinitionService permissionDefinitionService, IPersonaBarContainer personaBarContainer, IPersonaBarController personaBarController, IHostSettings hostSettings, IJavaScriptLibraryHelper javaScript, IClientResourceController clientResourceController, IServicesFramework servicesFramework)
        : base(eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>(), permissionDefinitionService ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPermissionDefinitionService>())
    {
        this.personaBarContainer = personaBarContainer ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPersonaBarContainer>();
        this.personaBarController = personaBarController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IPersonaBarController>();
        this.hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
        this.javaScript = javaScript ?? Globals.GetCurrentServiceProvider().GetRequiredService<IJavaScriptLibraryHelper>();
        this.clientResourceController = clientResourceController ?? Globals.GetCurrentServiceProvider().GetRequiredService<IClientResourceController>();
        this.servicesFramework = servicesFramework ?? Globals.GetCurrentServiceProvider().GetRequiredService<IServicesFramework>();
    }

    /// <summary>Gets a JSON representation of <see cref="IPersonaBarContainer.GetConfiguration"/>.</summary>
    public string PersonaBarSettings => JsonConvert.SerializeObject(this.personaBarContainer.GetConfiguration());

    /// <summary>Gets the site's virtual application root path.</summary>
    [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
    public string AppPath => Globals.ApplicationPath;

    /// <summary>Gets the client resource version number.</summary>
    public string BuildNumber => this.hostSettings.CrmVersion.ToString(CultureInfo.InvariantCulture);

    /// <summary>Gets or sets the JavaScript runtime of the circuit this component renders in.</summary>
    /// <remarks>Supplied by the renderer; it is the seam the former inline script runs through.</remarks>
    [Inject]
    protected IJSRuntime JsRuntime { get; set; }

    /// <summary>Gets or sets the logger of this component.</summary>
    /// <remarks>Supplied by the renderer; a failing interop call is logged rather than discarded.</remarks>
    [Inject]
    protected ILogger<PersonaBarContainer> Logger { get; set; }

    /// <summary>Gets a value indicating whether the Persona Bar panel renders.</summary>
    /// <remarks>
    /// Replaces the <c>PersonaBarPanel</c> server control's <c>Visible</c> property, which the
    /// markup defaulted to <c>False</c> and <c>OnLoad</c> assigned from
    /// <see cref="InjectPersonaBar"/>.
    /// </remarks>
    protected bool PersonaBarPanelVisible { get; private set; }

    /// <summary>Gets a value indicating whether the response of the request being rendered is successful.</summary>
    /// <remarks>
    /// Replaces <c>this.Response.StatusCode == (int)HttpStatusCode.OK</c>, which <c>OnPreRender</c>
    /// assigned to <c>Visible</c> so that the control panel is not injected into an error response.
    /// There is no <c>HttpResponse</c> once the Blazor circuit is interactive (SignalR has no
    /// <c>HttpContext</c>), and that is not an error response, so a missing response renders.
    /// </remarks>
    protected bool ResponseIsSuccessful
    {
        get
        {
            var response = this.Response;
            return response == null || response.StatusCode == (int)HttpStatusCode.OK;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this.interopModule != null)
        {
            try
            {
                await this.interopModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // the circuit is already gone; there is nothing left to dispose client side.
            }

            this.interopModule = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void OnInit(EventArgs e)
    {
        base.OnInit(e);

        if (this.personaBarContainer.Visible)
        {
            this.personaBarContainer.Initialize(this);
        }
    }

    /// <inheritdoc />
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // DnnComponentBase raises OnLoad from OnParametersSet, which the renderer can call more
        // than once per request, whereas the WebForms Load event fired exactly once. The body
        // below queries the menu and registers client resources, so it keeps the original
        // once-per-instance semantics.
        if (this.loaded)
        {
            return;
        }

        this.loaded = true;

        this.PersonaBarPanelVisible = this.InjectPersonaBar();

        if (!this.PersonaBarPanelVisible)
        {
            this.RemovedAdminStyleSheet();
        }
    }

    /// <inheritdoc />
    protected override void OnPreRender(EventArgs e)
    {
        this.Visible = this.ResponseIsSuccessful;

        base.OnPreRender(e);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Runs the client side seam the <c>.ascx</c> emitted as an inline <c>&lt;script&gt;</c> inside
    /// the panel: a <c>&lt;script&gt;</c> element produced by a render tree is never executed, so
    /// the block lives in <c>scripts/personaBarContainerInterop.js</c> and is invoked here - the
    /// first point at which JS interop is legal - with the same three values the script
    /// interpolated (<see cref="PersonaBarSettings"/>, <see cref="AppPath"/>,
    /// <see cref="BuildNumber"/>). It only runs when the panel rendered, as the inline script only
    /// reached the browser when the panel was visible.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (!firstRender || !this.PersonaBarPanelVisible || !this.Visible)
        {
            return;
        }

        var jsRuntime = this.JsRuntime;
        if (jsRuntime == null)
        {
            return;
        }

        try
        {
            this.interopModule = await jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                Globals.ResolveUrl("~/DesktopModules/admin/Dnn.PersonaBar/scripts/personaBarContainerInterop.js"));

            await this.interopModule.InvokeVoidAsync(
                "initPersonaBarContainer",
                this.PersonaBarSettings,
                this.AppPath,
                this.BuildNumber);
        }
        catch (JSException ex)
        {
            // the extension's assets are not reachable, so the placeholder stays and the Persona
            // Bar does not load - the same outcome a failed inline script had, but logged, because
            // a silently missing control panel is indistinguishable from one the user may not see.
            this.Logger?.PersonaBarContainerInteropFailed(ex);
        }
        catch (JSDisconnectedException)
        {
            // the circuit went away between the render and the interop call.
        }
    }

    private bool InjectPersonaBar()
    {
        if (!this.personaBarContainer.Visible)
        {
            return false;
        }

        // copied this logic from DotNetNuke.UI.Skins.Skin.InjectControlPanel
        if (this.GetQueryStringValue("dnnprintmode") == "true" || this.GetQueryStringValue("popUp") == "true")
        {
            return false;
        }

        var menuStructure = this.personaBarController.GetMenu(this.PortalSettings, UserController.Instance.GetCurrentUserInfo());
        if (menuStructure.MenuItems == null || !menuStructure.MenuItems.Any())
        {
            return false;
        }

        this.RegisterPersonaBarStyleSheet();

        // Was JavaScript.RegisterClientReference(this.Page, ...), which took the WebForms page; the
        // registration is now per request and keyed off the HttpContext.
        var httpContext = this.Request?.HttpContext;
        if (httpContext != null)
        {
            JavaScript.RegisterClientReference(httpContext, ClientAPI.ClientNamespaceReferences.dnn);
        }

        this.javaScript.RequestRegistration(CommonJs.DnnPlugins); // We need to add the Dnn JQuery plugins because the Edit Bar removes the Control Panel from the page
        this.javaScript.RequestRegistration(CommonJs.KnockoutMapping);

        this.servicesFramework.RequestAjaxAntiForgerySupport();

        this.clientResourceController.RegisterScript("~/Resources/Shared/Components/Tokeninput/jquery.tokeninput.js");
        this.clientResourceController.RegisterStylesheet("~/Resources/Shared/Components/Tokeninput/Themes/token-input-facebook.css");

        return true;
    }

    /// <summary>Gets a query string value by name.</summary>
    /// <param name="name">The name of the value.</param>
    /// <returns>The value, or <see langword="null"/> when the request did not supply it.</returns>
    /// <remarks>Replaces <c>Request.QueryString[name]</c>, which was <see langword="null"/> for an absent key.</remarks>
    private string GetQueryStringValue(string name)
    {
        var request = this.Request;
        return request != null && request.Query.TryGetValue(name, out var value) ? value.ToString() : null;
    }

    private void RegisterPersonaBarStyleSheet()
    {
        this.clientResourceController.RegisterStylesheet("~/DesktopModules/admin/Dnn.PersonaBar/css/personaBarContainer.css");
    }

    /// <summary>Keeps the platform's <c>admin.css</c> out of the rendered client resources.</summary>
    /// <remarks>
    /// The WebForms implementation walked the page's <c>ClientResourceIncludes</c> control
    /// collection and removed the <c>DnnCssInclude</c> whose <c>FilePath</c> was
    /// <c>Globals.HostPath + "admin.css"</c>. Client resources are no longer child controls of the
    /// page: they are registered with <see cref="IClientResourceController"/> and rendered from its
    /// per-request collection, which excludes any path handed to
    /// <see cref="IClientResourceController.RemoveStylesheetByPath"/> - the same net effect, and it
    /// also works when <c>admin.css</c> is registered after this component loads.
    /// </remarks>
    private void RemovedAdminStyleSheet()
    {
        this.clientResourceController.RemoveStylesheetByPath(Globals.HostPath + "admin.css", null);
    }
}
