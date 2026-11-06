// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.DDRMenu
{
    using DotNetNuke.DependencyInjection;
    using DotNetNuke.UI;
    using DotNetNuke.Web.DDRMenu.Localisation;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    /// <summary>Registers DDR Menu services.</summary>
    public class Startup : IDnnStartup
    {
        /// <inheritdoc />
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<ILocaliser, Localiser>();
            services.TryAddEnumerable(ServiceDescriptor.Scoped<ILocalisation, Generic>());

            // The module's controls are Blazor components now, but the control sources stored in the
            // database (and in installed third party skins) still name the historical markup files,
            // so the mapping from those paths to the component types is registered explicitly. The
            // two .razor paths would also be matched by the resolver's file-name convention; they
            // are registered so that a rename cannot silently break the module's routing.
            var controlTypes = DnnControlTypeResolver.Instance;
            controlTypes.Register("DesktopModules/DDRMenu/Menu.ascx", typeof(SkinObject));
            controlTypes.Register("DesktopModules/DDRMenu/Actions.ascx", typeof(Actions));
            controlTypes.Register("DesktopModules/DDRMenu/MenuView.razor", typeof(MenuView));
            controlTypes.Register("DesktopModules/DDRMenu/MenuSettings.razor", typeof(MenuSettings));

            services.TryAddSingleton<IDnnControlTypeResolver>(controlTypes);

            // DotNetNuke.Library registers the default resolver with TryAddSingleton of a *new*
            // DnnControlTypeResolver, which would not carry the registrations above. Pointing the
            // service at the shared registry keeps them visible whichever startup runs first; it is
            // also the instance ControlUtilities falls back to when nothing is registered at all.
            services.AddSingleton<IDnnControlTypeResolver>(_ => DnnControlTypeResolver.Instance);
        }
    }
}
