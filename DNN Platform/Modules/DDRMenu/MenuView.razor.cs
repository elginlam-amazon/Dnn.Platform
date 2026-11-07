// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.DDRMenu
{
    using System;
    using System.IO;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.UI;
    using DotNetNuke.Web.DDRMenu.DNNCommon;
    using DotNetNuke.Web.DDRMenu.Localisation;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A module view to display a menu.</summary>
    /// <remarks>
    /// Was <c>MenuView.ascx</c> + <c>MenuView.ascx.cs</c> + <c>MenuView.ascx.designer.cs</c>; the
    /// markup (and the <c>Render(HtmlTextWriter)</c> override) now lives in <c>MenuView.razor</c>.
    /// </remarks>
    public partial class MenuView : ModuleBase
    {
        private readonly ILocaliser localiser;
        private readonly IHostSettings hostSettings;
        private readonly ITabController tabController;
        private MenuBase menu;
        private string menuHtml;
        private bool renderFailed;

        /// <summary>Initializes a new instance of the <see cref="MenuView"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.0. Please use overload with ILocaliser. Scheduled removal in v12.0.0.")]
        public MenuView()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MenuView"/> class.</summary>
        /// <param name="localiser">The tab localizer.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public MenuView(ILocaliser localiser)
            : this(localiser, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="MenuView"/> class.</summary>
        /// <param name="localiser">The tab localizer.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="tabController">The tab controller.</param>
        public MenuView(ILocaliser localiser, IHostSettings hostSettings, ITabController tabController)
        {
            this.localiser = localiser ?? Globals.GetCurrentServiceProvider().GetRequiredService<ILocaliser>();
            this.hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            this.tabController = tabController ?? Globals.GetCurrentServiceProvider().GetRequiredService<ITabController>();
        }

        /// <summary>
        /// Gets a value indicating whether the "no menu style configured" hint should be rendered.
        /// </summary>
        /// <remarks>
        /// The original <c>Render(HtmlTextWriter)</c> wrote the hint only when <c>menu</c> was null,
        /// i.e. when no menu style was configured. A template that FAILED to render logged through
        /// <c>ProcessModuleLoadException</c> and wrote nothing — it never showed the settings hint.
        /// Keeping the two cases apart preserves that behavior.
        /// </remarks>
        protected bool ShowMenuStyleHint => this.menu == null && !this.renderFailed;

        /// <summary>Gets the menu HTML produced during the load phase.</summary>
        /// <remarks>
        /// The template engine runs ONCE, in <see cref="OnLoad"/>, while the captured request is
        /// still live; the markup only emits the cached string. Re-running the engine from the
        /// markup would re-read the request on every interactive (SignalR) re-render, where
        /// <c>HttpContextSource.Current</c> is null.
        /// </remarks>
        protected MarkupString MenuMarkup => (MarkupString)(this.menuHtml ?? string.Empty);

        /// <inheritdoc />
        /// <remarks>
        /// This is the ported <c>OnPreRender</c>. The WebForms <c>PreRender</c> phase ran before the
        /// control rendered; the component's <c>OnPreRender</c> runs after the first render, so the
        /// menu is built in the <c>Load</c> phase, which is the last one before rendering.
        /// </remarks>
        protected override void OnLoad(EventArgs e)
        {
            using (new DNNContext(this))
            {
                try
                {
                    base.OnLoad(e);

                    var menuStyle = this.GetStringSetting("MenuStyle");
                    if (string.IsNullOrEmpty(menuStyle))
                    {
                        this.menu = null;
                        return;
                    }

                    var menuSettings = new Settings
                    {
                        MenuStyle = this.GetStringSetting("MenuStyle"),
                        NodeXmlPath = this.GetStringSetting("NodeXmlPath"),
                        NodeSelector = this.GetStringSetting("NodeSelector"),
                        IncludeContext = this.GetBoolSetting("IncludeContext"),
                        IncludeHidden = this.GetBoolSetting("IncludeHidden"),
                        IncludeNodes = this.GetStringSetting("IncludeNodes"),
                        ExcludeNodes = this.GetStringSetting("ExcludeNodes"),
                        NodeManipulator = this.GetStringSetting("NodeManipulator"),
                        TemplateArguments =
                                            DDRMenu.Settings.TemplateArgumentsFromSettingString(this.GetStringSetting("TemplateArguments")),
                        ClientOptions =
                                            DDRMenu.Settings.ClientOptionsFromSettingString(this.GetStringSetting("ClientOptions")),
                    };

                    MenuNode rootNode = null;
                    if (string.IsNullOrEmpty(menuSettings.NodeXmlPath))
                    {
                        rootNode =
                            new MenuNode(
                                this.localiser.LocaliseDNNNodeCollection(
                                    Navigation.GetNavigationNodes(
                                        this.ClientId,
                                        Navigation.ToolTipSource.None,
                                        -1,
                                        -1,
                                        DNNAbstract.GetNavNodeOptions(true))));
                    }

                    this.menu = MenuBase.Instantiate(this.localiser, this.hostSettings, this.tabController, menuStyle);
                    this.menu.RootNode = rootNode;
                    this.menu.ApplySettings(menuSettings);

                    this.menu.PreRender();

                    // Run the template engine ONCE, here, while the captured request is still
                    // live. The original control did this from Render(HtmlTextWriter); doing it
                    // from the markup instead would re-enter the engine on every interactive
                    // re-render, when HttpContextSource.Current is null.
                    using (var writer = new StringWriter())
                    {
                        this.menu.Render(writer);
                        this.menuHtml = writer.ToString();
                    }
                }
                catch (Exception exc)
                {
                    // A failed render must NOT fall back to the "specify menu style" hint — the
                    // original wrote nothing in this case and only logged.
                    this.renderFailed = true;
                    Exceptions.ProcessModuleLoadException(this, exc);
                }
            }
        }
    }
}
