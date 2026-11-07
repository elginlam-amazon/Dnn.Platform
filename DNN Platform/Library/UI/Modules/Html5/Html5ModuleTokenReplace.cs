// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Modules.Html5
{
    using DotNetNuke.Abstractions.Modules;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Modules.Actions;
    using DotNetNuke.Services.Personalization;
    using DotNetNuke.Services.Tokens;
    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A <see cref="TokenReplace"/> for HTML modules.</summary>
    public class Html5ModuleTokenReplace : HtmlTokenReplace
    {
        /// <summary>Initializes a new instance of the <see cref="Html5ModuleTokenReplace"/> class.</summary>
        /// <param name="page">The component on which the module is rendering.</param>
        /// <param name="html5File">The path to the module's HTML file.</param>
        /// <param name="moduleContext">The module context.</param>
        /// <param name="moduleActions">The module actions collection.</param>
        /// <remarks>The <c>page</c> parameter was a <c>System.Web.UI.Page</c>; the hosting page is a Blazor component now.</remarks>
        public Html5ModuleTokenReplace(
            IComponent page,
            string html5File,
            ModuleInstanceContext moduleContext,
            ModuleActionCollection moduleActions)
            : this(page, null, html5File, moduleContext, moduleActions)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="Html5ModuleTokenReplace"/> class.</summary>
        /// <param name="page">The page on which the module is rendering.</param>
        /// <param name="businessControllerProvider">The business controller provider.</param>
        /// <param name="html5File">The path to the module's HTML file.</param>
        /// <param name="moduleContext">The module context.</param>
        /// <param name="moduleActions">The module actions collection.</param>
        public Html5ModuleTokenReplace(
            IComponent page,
            IBusinessControllerProvider businessControllerProvider,
            string html5File,
            ModuleInstanceContext moduleContext,
            ModuleActionCollection moduleActions)
            : base(HttpContextSource.Current)
        {
            this.AccessingUser = moduleContext.PortalSettings.UserInfo;
            this.DebugMessages = Personalization.GetUserMode() != Entities.Portals.PortalSettings.Mode.View;
            this.ModuleId = moduleContext.ModuleId;
            this.PortalSettings = moduleContext.PortalSettings;

            this.AddPropertySource("moduleaction", new ModuleActionsPropertyAccess(moduleContext, moduleActions));
            this.AddPropertySource("resx", new ModuleLocalizationPropertyAccess(moduleContext, html5File));
            this.AddPropertySource("modulecontext", new ModuleContextPropertyAccess(moduleContext));
            this.AddPropertySource("request", new RequestPropertyAccess(HttpContextSource.Current?.Request));

            // DNN-7750
            businessControllerProvider ??= Globals.DependencyProvider.GetRequiredService<IBusinessControllerProvider>();
            var customTokenProvider = businessControllerProvider.GetInstance<ICustomTokenProvider>(moduleContext);
            if (customTokenProvider != null)
            {
                var tokens = customTokenProvider.GetTokens(page, moduleContext);
                foreach (var token in tokens)
                {
                    this.AddPropertySource(token.Key, token.Value);
                }
            }
        }
    }
}
