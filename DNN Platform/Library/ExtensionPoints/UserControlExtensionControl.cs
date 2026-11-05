// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.ExtensionPoints
{
    using System;

    /// <summary>Renders the user-control extension points registered for a module/group.</summary>
    /// <remarks>
    /// Was a WebForms control that called <c>Page.LoadControl</c> in <c>OnInit</c> and added the
    /// result to <c>Controls</c>. It now records the control sources in <c>OnInit</c> and
    /// <see cref="DefaultExtensionControl"/> renders them through <c>DynamicComponent</c>; the
    /// action dispatch below is unchanged apart from reading the captured component instances.
    /// </remarks>
    public class UserControlExtensionControl : DefaultExtensionControl
    {
        /// <summary>Dispatches the bind action to every loaded extension control.</summary>
        /// <param name="portalId">The portal id.</param>
        /// <param name="tabId">The tab id.</param>
        /// <param name="moduleId">The module id.</param>
        public void BindAction(int portalId, int tabId, int moduleId)
        {
            foreach (var control in this.Controls)
            {
                var actionsControl = control as IUserControlActions;
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
            foreach (var control in this.Controls)
            {
                var actionsControl = control as IUserControlActions;
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
            foreach (var control in this.Controls)
            {
                var actionsControl = control as IUserControlActions;
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
                var extension = extensionPointManager.GetUserControlExtensionPointFirstByPriority(this.Module, this.Name);
                this.LoadControl(extension);
            }
            else
            {
                foreach (var extension in extensionPointManager.GetUserControlExtensionPoints(this.Module, this.Group))
                {
                    this.LoadControl(extension);
                }
            }
        }

        private void LoadControl(IUserControlExtensionPoint extension)
        {
            this.ControlSources.Add(extension.UserControlSrc);
        }
    }
}
