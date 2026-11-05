// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Security;
    using DotNetNuke.Security.Permissions;
    using DotNetNuke.Services.Personalization;
    using DotNetNuke.UI.Modules;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>ActionLink provides a button for a single action.</summary>
    /// <remarks>
    /// Was a <c>System.Web.UI.WebControls.HyperLink</c>; it is now a
    /// <see cref="DnnComponentBase"/> (DNN's replacement for <c>System.Web.UI.Control</c>) that
    /// renders the same anchor. <see cref="Text"/>, <see cref="NavigateUrl"/> and
    /// <see cref="ToolTip"/>, which came from the hyperlink, are declared here; <c>CssClass</c> and
    /// <c>Visible</c> come from the base. <c>EnableViewState = false</c> is gone with view state
    /// itself.
    /// </remarks>
    public class ActionLink : DnnComponentBase
    {
        /// <summary>Initializes a new instance of the <see cref="ActionLink"/> class.</summary>
        public ActionLink()
        {
            this.RequireEditMode = false;
            this.Security = "Edit";
            this.ControlKey = string.Empty;
            this.Title = string.Empty;
        }

        /// <summary>Gets or sets the title (and localization key) of the action.</summary>
        [Parameter]
        public string Title { get; set; }

        /// <summary>Gets or sets the key of the module control the action navigates to.</summary>
        [Parameter]
        public string ControlKey { get; set; }

        /// <summary>Gets or sets the name of the query string parameter the action passes.</summary>
        [Parameter]
        public string KeyName { get; set; }

        /// <summary>Gets or sets the value of the query string parameter the action passes.</summary>
        [Parameter]
        public string KeyValue { get; set; }

        /// <summary>Gets or sets the <see cref="SecurityAccessLevel"/> required to see this action.</summary>
        [Parameter]
        public string Security { get; set; }

        /// <summary>Gets or sets a value indicating whether the action is only shown in edit mode.</summary>
        [Parameter]
        public bool RequireEditMode { get; set; }

        /// <inheritdoc />
        protected override string TagName => "a";

        /// <summary>Gets or sets the module control this action belongs to.</summary>
        [Parameter]
        public IModuleControl ModuleControl { get; set; }

        /// <summary>Gets or sets the text of the anchor.</summary>
        /// <remarks>Replaces <c>HyperLink.Text</c>.</remarks>
        [Parameter]
        public string Text { get; set; } = string.Empty;

        /// <summary>Gets or sets the URL of the anchor.</summary>
        /// <remarks>Replaces <c>HyperLink.NavigateUrl</c>.</remarks>
        [Parameter]
        public string NavigateUrl { get; set; } = string.Empty;

        /// <summary>Gets or sets the tool tip of the anchor.</summary>
        /// <remarks>Replaces <c>WebControl.ToolTip</c>.</remarks>
        [Parameter]
        public string ToolTip { get; set; }

        /// <summary>Resolves the visibility, the text and the URL of this action.</summary>
        /// <param name="e">The event arguments.</param>
        /// <remarks>
        /// This is the body of the WebForms <c>OnPreRender</c> override. It runs on
        /// <see cref="DnnComponentBase.OnLoad"/> instead, because it computes what the control
        /// renders and Blazor raises its pre-render hook after the first render.
        /// </remarks>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (this.Visible && this.IsVisible((SecurityAccessLevel)Enum.Parse(typeof(SecurityAccessLevel), this.Security)))
            {
                this.Text = this.Title;
                this.NavigateUrl = this.ControlKey != string.Empty
                                ? this.ModuleControl.ModuleContext.EditUrl(this.KeyName, this.KeyValue, this.ControlKey)
                                : this.ModuleControl.ModuleContext.EditUrl(this.Title);

                if (this.CssClass == string.Empty)
                {
                    this.CssClass = "dnnPrimaryAction";
                }
            }
            else
            {
                this.Visible = false;
            }

            if (!string.IsNullOrEmpty(this.NavigateUrl))
            {
                this.Attributes["href"] = this.NavigateUrl;
            }

            if (!string.IsNullOrEmpty(this.ToolTip))
            {
                this.Attributes["title"] = this.ToolTip;
            }
        }

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.AddContent(sequence++, this.Text);
            base.RenderContents(builder, ref sequence);
        }

        private bool IsVisible(SecurityAccessLevel security)
        {
            bool isVisible = false;
            if (ModulePermissionController.HasModuleAccess(security, Null.NullString, this.ModuleControl.ModuleContext.Configuration))
            {
                if ((this.RequireEditMode != true || Personalization.GetUserMode() == PortalSettings.Mode.Edit) || (security == SecurityAccessLevel.Anonymous || security == SecurityAccessLevel.View))
                {
                    isVisible = true;
                }
            }

            return isVisible;
        }
    }
}
