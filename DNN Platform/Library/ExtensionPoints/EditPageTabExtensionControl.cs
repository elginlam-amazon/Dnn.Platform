// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.ExtensionPoints
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>Renders the edit-page-tab extension points registered for a module/group.</summary>
    /// <remarks>
    /// <para>
    /// On WebForms this control reached out of itself: it looked up two sibling controls by id
    /// (<see cref="TabControlId"/> and <see cref="PanelControlId"/>) with
    /// <c>Parent.FindControl</c> and pushed the tab <c>&lt;li&gt;</c> elements into one and the tab
    /// panels into the other. Blazor has no mutable, searchable control tree, so the control now
    /// renders both parts itself: the tab list as <c>&lt;ul id="<see cref="TabControlId"/>"&gt;</c>
    /// and the panels inside <c>&lt;div id="<see cref="PanelControlId"/>"&gt;</c>. The ids are kept
    /// as parameters and emitted on those elements so the existing jQuery tab script keeps working.
    /// A host that needs the two parts in different places in its own markup can render
    /// <see cref="Tabs"/> and <see cref="Panels"/> separately instead.
    /// </para>
    /// </remarks>
    public class EditPageTabExtensionControl : DefaultExtensionControl
    {
        private readonly List<IEditPageTabExtensionPoint> extensions = new List<IEditPageTabExtensionPoint>();

        /// <summary>Gets or sets the DOM id of the tab list element.</summary>
        [Parameter]
        [Bindable(true)]
        [DefaultValue("")]
        public string TabControlId { get; set; } = string.Empty;

        /// <summary>Gets or sets the DOM id of the tab panel container element.</summary>
        [Parameter]
        [Bindable(true)]
        [DefaultValue("")]
        public string PanelControlId { get; set; } = string.Empty;

        /// <summary>Gets the tab list fragment (the <c>&lt;ul&gt;</c> of tab headers).</summary>
        public RenderFragment Tabs => this.RenderTabs;

        /// <summary>Gets the tab panels fragment (the content of each tab).</summary>
        public RenderFragment Panels => this.RenderPanels;

        /// <summary>Dispatches the bind action to every loaded extension control.</summary>
        /// <param name="portalId">The portal id.</param>
        /// <param name="tabId">The tab id.</param>
        /// <param name="moduleId">The module id.</param>
        public void BindAction(int portalId, int tabId, int moduleId)
        {
            foreach (var extensionControl in this.Controls)
            {
                var actionsControl = extensionControl as IEditPageTabControlActions;
                if (actionsControl != null)
                {
                    actionsControl.BindAction(portalId, tabId, moduleId);
                }
            }
        }

        /// <summary>Dispatches the save action to every loaded extension control.</summary>
        /// <param name="portalId">The portal id.</param>
        /// <param name="tabId">The tab id.</param>
        /// <param name="moduleId">The module id.</param>
        public void SaveAction(int portalId, int tabId, int moduleId)
        {
            foreach (var extensionControl in this.Controls)
            {
                var actionsControl = extensionControl as IEditPageTabControlActions;
                if (actionsControl != null)
                {
                    actionsControl.SaveAction(portalId, tabId, moduleId);
                }
            }
        }

        /// <summary>Dispatches the cancel action to every loaded extension control.</summary>
        /// <param name="portalId">The portal id.</param>
        /// <param name="tabId">The tab id.</param>
        /// <param name="moduleId">The module id.</param>
        public void CancelAction(int portalId, int tabId, int moduleId)
        {
            foreach (var extensionControl in this.Controls)
            {
                var actionsControl = extensionControl as IEditPageTabControlActions;
                if (actionsControl != null)
                {
                    actionsControl.CancelAction(portalId, tabId, moduleId);
                }
            }
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            var extensionPointManager = new ExtensionPointManager();

            foreach (var extension in extensionPointManager.GetEditPageTabExtensionPoints(this.Module, this.Group))
            {
                if (extension.Visible)
                {
                    this.extensions.Add(extension);
                }
            }
        }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (builder is null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            this.RenderTabs(builder);
            this.RenderPanels(builder);
        }

        private void RenderTabs(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "ul");
            builder.AddAttribute(1, "id", this.TabControlId);

            var sequence = 2;
            foreach (var extension in this.extensions)
            {
                builder.OpenElement(sequence, "li");
                builder.AddAttribute(sequence + 1, "class", extension.CssClass);
                builder.OpenElement(sequence + 2, "a");
                builder.AddAttribute(sequence + 3, "href", "#" + extension.EditPageTabId);
                builder.AddContent(sequence + 4, extension.Text);
                builder.CloseElement();
                builder.CloseElement();
                sequence += 6;
            }

            builder.CloseElement();
        }

        private void RenderPanels(RenderTreeBuilder builder)
        {
            builder.OpenElement(1000, "div");
            builder.AddAttribute(1001, "id", this.PanelControlId);

            var sequence = 1002;
            foreach (var extension in this.extensions)
            {
                var controlSrc = extension.UserControlSrc;
                builder.OpenComponent<PanelTabExtensionControl>(sequence);
                builder.AddComponentParameter(sequence + 1, nameof(PanelTabExtensionControl.PanelId), extension.EditPageTabId);
                builder.AddComponentParameter(
                    sequence + 2,
                    nameof(PanelTabExtensionControl.ChildContent),
                    (RenderFragment)(childBuilder => this.RenderExtensionControl(childBuilder, 0, controlSrc)));
                builder.CloseComponent();
                sequence += 4;
            }

            builder.CloseElement();
        }
    }
}
