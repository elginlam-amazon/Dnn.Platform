// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.DDRMenu
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.UI;
    using DotNetNuke.UI.Skins;
    using DotNetNuke.Web.DDRMenu.DNNCommon;
    using DotNetNuke.Web.DDRMenu.Localisation;
    using DotNetNuke.Web.DDRMenu.TemplateEngine;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    using Microsoft.Extensions.DependencyInjection;

    public class SkinObject : SkinObjectBase
    {
        private readonly IHostSettings hostSettings;
        private readonly ILocaliser localiser;
        private readonly ITabController tabController;
        private MenuBase menu;

        /// <summary>Initializes a new instance of the <see cref="SkinObject"/> class.</summary>
        [Obsolete("Deprecated in DotNetNuke 10.0.0. Please use overload with ILocaliser. Scheduled removal in v12.0.0.")]
        public SkinObject()
            : this(null, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="SkinObject"/> class.</summary>
        /// <param name="localiser">The tab localizer.</param>
        [Obsolete("Deprecated in DotNetNuke 10.2.2. Please use overload with IHostSettings. Scheduled removal in v12.0.0.")]
        public SkinObject(ILocaliser localiser)
            : this(localiser, null, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="SkinObject"/> class.</summary>
        /// <param name="localiser">The tab localizer.</param>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="tabController">The tab controller.</param>
        public SkinObject(ILocaliser localiser, IHostSettings hostSettings, ITabController tabController)
        {
            this.localiser = localiser ?? Globals.GetCurrentServiceProvider().GetRequiredService<ILocaliser>();
            this.hostSettings = hostSettings ?? Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>();
            this.tabController = tabController ?? Globals.GetCurrentServiceProvider().GetRequiredService<ITabController>();
        }

        [Parameter]
        public string MenuStyle { get; set; }

        [Parameter]
        public string NodeXmlPath { get; set; }

        [Parameter]
        public string NodeSelector { get; set; }

        [Parameter]
        public bool IncludeContext { get; set; }

        [Parameter]
        public bool IncludeHidden { get; set; }

        [Parameter]
        public string IncludeNodes { get; set; }

        [Parameter]
        public string ExcludeNodes { get; set; }

        [Parameter]
        public string NodeManipulator { get; set; }

        /// <summary>Gets or sets the client options passed to the menu template.</summary>
        /// <remarks>
        /// Was declared with [DesignerSerializationVisibility(Content)] and
        /// [PersistenceMode(InnerProperty)] so that skin markup could nest the options as inner
        /// elements. Neither attribute has a net10 equivalent (both are WebForms designer/parser
        /// metadata); the property is a component [Parameter] instead, which is how a Blazor
        /// component receives nested values. The name and type are unchanged because skins set it.
        /// </remarks>
        [Parameter]
        public List<ClientOption> ClientOptions { get; set; }

        /// <summary>Gets or sets the template arguments passed to the menu template.</summary>
        /// <remarks>
        /// Same removal of [DesignerSerializationVisibility]/[PersistenceMode] as
        /// <see cref="ClientOptions"/>; exposed as a component [Parameter].
        /// </remarks>
        [Parameter]
        public List<TemplateArgument> TemplateArguments { get; set; }

        /// <inheritdoc />
        /// <remarks>
        /// The WebForms <c>PreRender</c> phase ran before the control rendered its markup; the
        /// component equivalent (<c>OnLoad</c>, raised from <c>OnParametersSet</c>) is the last
        /// phase that still runs before the first render, so the menu is built here and the
        /// rendered HTML is emitted by <see cref="RenderContents"/>.
        /// </remarks>
        protected override void OnLoad(EventArgs e)
        {
            using (new DNNContext(this))
            {
                try
                {
                    base.OnLoad(e);

                    this.menu = MenuBase.Instantiate(this.localiser, this.hostSettings, this.tabController, this.MenuStyle);
                    this.menu.ApplySettings(
                        new Settings
                        {
                            MenuStyle = this.MenuStyle,
                            NodeXmlPath = this.NodeXmlPath,
                            NodeSelector = this.NodeSelector,
                            IncludeContext = this.IncludeContext,
                            IncludeHidden = this.IncludeHidden,
                            IncludeNodes = this.IncludeNodes,
                            ExcludeNodes = this.ExcludeNodes,
                            NodeManipulator = this.NodeManipulator,
                            ClientOptions = this.ClientOptions,
                            TemplateArguments = this.TemplateArguments,
                        });

                    if (string.IsNullOrEmpty(this.NodeXmlPath))
                    {
                        this.menu.RootNode =
                            new MenuNode(
                                this.localiser.LocaliseDNNNodeCollection(
                                    Navigation.GetNavigationNodes(
                                        this.ClientId,
                                        Navigation.ToolTipSource.None,
                                        -1,
                                        -1,
                                        DNNAbstract.GetNavNodeOptions(true))));
                    }

                    this.menu.PreRender();
                }
                catch (Exception exc)
                {
                    Exceptions.ProcessModuleLoadException(this, exc);
                }
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Replaces <c>Render(HtmlTextWriter)</c>: the template engine still writes the menu into a
        /// <see cref="TextWriter"/>, and the accumulated HTML is emitted into the render tree.
        /// </remarks>
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            using (new DNNContext(this))
            {
                try
                {
                    if (this.menu != null)
                    {
                        string html;
                        using (var writer = new StringWriter())
                        {
                            this.menu.Render(writer);
                            html = writer.ToString();
                        }

                        builder.AddMarkupContent(sequence++, html);
                    }

                    base.RenderContents(builder, ref sequence);
                }
                catch (Exception exc)
                {
                    Exceptions.ProcessModuleLoadException(this, exc);
                }
            }
        }
    }
}
