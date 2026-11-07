// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.DDRMenu
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.UI;
    using DotNetNuke.Web.DDRMenu.DNNCommon;
    using DotNetNuke.Web.DDRMenu.Localisation;

    using Microsoft.AspNetCore.Components.Rendering;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.JSInterop;

    /// <summary>DDR Menu component.</summary>
    /// <remarks>
    /// Was a <c>System.Web.UI.WebControls.WebControl</c> implementing <c>IPostBackEventHandler</c>.
    /// It is now a DNN skinning component: <see cref="RaisePostBackEvent"/> is kept (its argument
    /// format is the menu node id, which is part of the contract the rendered templates use) and it
    /// is wired to the rendered markup through <c>EventCallback.Factory.Create</c>, the same way
    /// <c>DotNetNuke.UI.Skins.Controls.SkinsEditControl</c> does.
    /// </remarks>
    internal sealed class DDRMenuControl : DnnComponentBase
    {
        private readonly ILocaliser localiser;
        private readonly IHostSettings hostSettings;
        private readonly ITabController tabController;
        private DotNetObjectReference<DDRMenuControl> selfReference;
        private string menuHtml;
        private MenuBase menu;

        /// <summary>Initializes a new instance of the <see cref="DDRMenuControl"/> class.</summary>
        /// <param name="localiser">The tab localizer.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public DDRMenuControl(ILocaliser localiser)
            : this(localiser, null, null)
        {
            this.localiser = localiser;
        }

        /// <summary>Initializes a new instance of the <see cref="DDRMenuControl"/> class.</summary>
        /// <param name="localiser">The tab localizer.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="tabController">The tab controller.</param>
        public DDRMenuControl(ILocaliser localiser, IHostSettings hostSettings, ITabController tabController)
        {
            this.localiser = localiser ?? Globals.GetCurrentServiceProvider().GetRequiredService<ILocaliser>();
            this.hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            this.tabController = tabController ?? Globals.GetCurrentServiceProvider().GetRequiredService<ITabController>();
        }

        /// <summary>Handles a click on the menu.</summary>
        /// <param name="id">The id of the menu item being clicked.</param>
        public delegate void MenuClickEventHandler(string id);

        /// <summary>Handles a click on a node of the menu.</summary>
        public event MenuClickEventHandler NodeClick;

        /// <summary>Gets or sets a value indicating whether view state is enabled.</summary>
        /// <remarks>
        /// There is no view state on ASP.NET Core, but the property is kept as a no-op because
        /// <see cref="DDRMenuNavigationProvider.Initialize"/> (and third party callers) still assign
        /// it; it always reads <see langword="false"/>, exactly as the WebForms override did.
        /// </remarks>
        public bool EnableViewState
        {
            get { return false; }
            set { }
        }

        /// <summary>Gets or sets the menu root node.</summary>
        internal MenuNode RootNode { get; set; }

        /// <summary>Gets or sets a value indicating whether to skip the localization.</summary>
        internal bool SkipLocalisation { get; set; }

        /// <summary>Gets or sets the menu settings.</summary>
        internal Settings MenuSettings { get; set; }

        /// <summary>Raises the <see cref="NodeClick"/> event for the clicked menu node.</summary>
        /// <param name="eventArgument">The id of the clicked node.</param>
        /// <remarks>
        /// Was <c>IPostBackEventHandler.RaisePostBackEvent</c>; the interface does not exist on
        /// ASP.NET Core, but the method is preserved because the navigation provider and the
        /// rendered templates address it by this name and argument format.
        /// </remarks>
        public void RaisePostBackEvent(string eventArgument)
        {
            using (new DNNContext(this))
            {
                if (this.NodeClick != null)
                {
                    this.NodeClick(eventArgument);
                }
            }
        }

        /// <summary>Raises the <see cref="NodeClick"/> event for the clicked menu node.</summary>
        /// <param name="nodeId">The id of the clicked node.</param>
        /// <remarks>
        /// The JavaScript entry point of the post back pipeline, called by
        /// <c>DDR.Menu.postBack</c> with the argument the clicked node's own script carries. It
        /// replaces the WebForms <c>__doPostBack</c>/<c>__EVENTARGUMENT</c> round trip, which does
        /// not exist when the menu renders interactively; see
        /// <c>MenuNode.ApplyContext</c> for the node side of the contract.
        /// </remarks>
        [JSInvokable]
        public void RaiseNodeClick(string nodeId)
        {
            this.RaisePostBackEvent(nodeId);
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender).ConfigureAwait(true);

            if (!firstRender || string.IsNullOrEmpty(this.ClientId))
            {
                return;
            }

            // Registers this control as the post back target for the node scripts rendered by
            // MenuNode.ApplyContext. Keyed by client id, exactly as __doPostBack identified its
            // target control by id.
            var jsRuntime = Globals.GetCurrentServiceProvider().GetService<IJSRuntime>();
            if (jsRuntime == null)
            {
                return;
            }

            this.selfReference ??= DotNetObjectReference.Create(this);
            await jsRuntime.InvokeVoidAsync("DDR.Menu.registerPostBack", this.ClientId, this.selfReference).ConfigureAwait(true);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing && this.selfReference != null)
            {
                this.selfReference.Dispose();
                this.selfReference = null;
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        /// <remarks>
        /// The WebForms <c>PreRender</c> phase ran before the control rendered; the component
        /// equivalent (<see cref="DnnComponentBase.OnPreRender"/>) runs after the first render, so
        /// the menu is also built on demand from <see cref="RenderContents"/> and this override only
        /// covers the case where the settings arrive later (the navigation provider binds the menu
        /// from its consumer's own pre-render phase).
        /// </remarks>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            if (this.menu == null && this.MenuSettings != null)
            {
                this.PrepareMenu();
                this.NotifyTreeChanged();
            }
        }

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (this.menu == null && this.MenuSettings != null)
            {
                this.PrepareMenu();
            }

            if (this.menu == null)
            {
                return;
            }

            // The menu HTML is produced once, by PrepareMenu, during the load/pre-render phase
            // while the request is still live — the template engines (Razor especially) read the
            // request. Re-rendering it from here on every interactive render would run them with
            // no HttpContext.
            var html = this.menuHtml;
            if (html == null)
            {
                return;
            }

            // the WebControl rendered its content inside a <span id="..."> wrapper; the wrapper is
            // kept so the client scripts and the menu CSS still find the control's element.
            builder.OpenElement(sequence++, "span");
            if (!string.IsNullOrEmpty(this.ID))
            {
                builder.AddAttribute(sequence++, "id", this.ClientId);
            }

            builder.AddMarkupContent(sequence++, html);

            base.RenderContents(builder, ref sequence);

            builder.CloseElement();
        }

        private void PrepareMenu()
        {
            using (new DNNContext(this))
            {
                this.MenuSettings.MenuStyle ??= "DNNMenu";
                this.menu = MenuBase.Instantiate(this.localiser, this.hostSettings, this.tabController, this.MenuSettings.MenuStyle);
                this.menu.RootNode = this.RootNode ?? new MenuNode();
                this.menu.SkipLocalisation = this.SkipLocalisation;
                this.menu.ApplySettings(this.MenuSettings);

                this.menu.PreRender();

                // Run the template engine here, inside the DNN context and while the request is
                // live; RenderContents only emits the result.
                using (var writer = new StringWriter())
                {
                    this.menu.Render(writer);
                    this.menuHtml = writer.ToString();
                }
            }
        }
    }
}
