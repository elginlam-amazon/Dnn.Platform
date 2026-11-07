// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.ExtensionPoints
{
    using System;
    using System.Collections.Generic;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>Renders the edit-page-panel extension points registered for a module/group.</summary>
    /// <remarks>
    /// Was a WebForms control that wrapped each extension's user control in a
    /// <see cref="PanelEditPagePanelExtensionControl"/> and added it to <c>Controls</c>. The same
    /// nesting is now produced in <see cref="BuildRenderTree"/>; the action dispatch reads the
    /// captured child component instances (see <see cref="DefaultExtensionControl.Controls"/>)
    /// instead of walking a control tree, which reaches exactly the same set of controls.
    /// </remarks>
    public class EditPagePanelExtensionControl : DefaultExtensionControl
    {
        private readonly List<IEditPagePanelExtensionPoint> extensions = new List<IEditPagePanelExtensionPoint>();

        /// <summary>Dispatches the bind action to every loaded extension control.</summary>
        /// <param name="portalId">The portal id.</param>
        /// <param name="tabId">The tab id.</param>
        /// <param name="moduleId">The module id.</param>
        public void BindAction(int portalId, int tabId, int moduleId)
        {
            foreach (var extensionControl in this.Controls)
            {
                var actionsControl = extensionControl as IEditPagePanelControlActions;
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
                var actionsControl = extensionControl as IEditPagePanelControlActions;
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
                var actionsControl = extensionControl as IEditPagePanelControlActions;
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

            if (!string.IsNullOrEmpty(this.Name))
            {
                var extension = extensionPointManager.GetEditPagePanelExtensionPointFirstByPriority(this.Module, this.Name);
                if (extension != null)
                {
                    this.LoadControl(extension);
                }
            }
            else
            {
                foreach (var extension in extensionPointManager.GetEditPagePanelExtensionPoints(this.Module, this.Group))
                {
                    if (extension != null)
                    {
                        this.LoadControl(extension);
                    }
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

            var sequence = 0;
            foreach (var extension in this.extensions)
            {
                var controlSrc = extension.UserControlSrc;
                builder.OpenComponent<PanelEditPagePanelExtensionControl>(sequence);
                builder.AddComponentParameter(sequence + 1, nameof(PanelEditPagePanelExtensionControl.PanelId), extension.EditPagePanelId);
                builder.AddComponentParameter(sequence + 2, nameof(PanelEditPagePanelExtensionControl.Text), extension.Text);
                builder.AddComponentParameter(sequence + 3, nameof(PanelEditPagePanelExtensionControl.CssClass), extension.CssClass);
                builder.AddComponentParameter(
                    sequence + 4,
                    nameof(PanelEditPagePanelExtensionControl.ChildContent),
                    (RenderFragment)(childBuilder => this.RenderExtensionControl(childBuilder, 0, controlSrc)));
                builder.CloseComponent();
                sequence += 6;
            }
        }

        private void LoadControl(IEditPagePanelExtensionPoint extension)
        {
            this.extensions.Add(extension);
        }
    }
}
