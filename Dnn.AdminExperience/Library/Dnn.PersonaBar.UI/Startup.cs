// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.UI;

using Dnn.PersonaBar.UI.Components.Controllers;
using Dnn.PersonaBar.UI.HttpModules;
using Dnn.PersonaBar.UI.UserControls;
using DotNetNuke.DependencyInjection;
using DotNetNuke.Services.FileSystem.Internal;
using DotNetNuke.UI;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>The <see cref="IDnnStartup"/> instance for the Persona Bar UI project.</summary>
public class Startup : IDnnStartup
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        services.TryAddTransient<IDirectory, DirectoryWrapper>();
        services.AddTransient<IAdminMenuController, AdminMenuController>();

        // Replaces the <system.webServer><modules> registration of PersonaBarModule the
        // extension's .dnn manifest installed: the IHttpModule is now an IHostedService, so the
        // application-start/application-end sequence is owned by the host's lifetime.
        services.AddSingleton<IHostedService, PersonaBarModule>();

        // The "ControlPanel" host setting stores the historical markup path of the control panel
        // (BusinessController.UpdateControlPanel writes it, and Dnn.EditBar.UI compares against
        // that exact string), so the stored value is unchanged and the path is mapped here to the
        // Blazor component that replaced PersonaBarContainer.ascx. Registering it explicitly - the
        // convention based fallback of DnnControlTypeResolver would also find PersonaBarContainer -
        // keeps the resolution deterministic even if another assembly ships a component with the
        // same type name.
        var controlTypes = DnnControlTypeResolver.Instance;
        controlTypes.Register(
            "DesktopModules/admin/Dnn.PersonaBar/UserControls/PersonaBarContainer.ascx",
            typeof(PersonaBarContainer));

        // The replacement component's own source path is registered alongside the historical one so
        // that a future manifest update to the .razor path keeps resolving.
        controlTypes.Register(
            "DesktopModules/admin/Dnn.PersonaBar/UserControls/PersonaBarContainer.razor",
            typeof(PersonaBarContainer));

        // DotNetNuke.Library registers the default resolver with TryAddSingleton of a *new*
        // DnnControlTypeResolver, which would not carry the registrations above - and
        // ControlUtilities.ResolveControlType resolves IDnnControlTypeResolver from DI first.
        // Pointing the service at the shared registry keeps them visible whichever startup runs
        // first (the DDRMenu precedent).
        services.AddSingleton<IDnnControlTypeResolver>(_ => controlTypes);
    }
}
