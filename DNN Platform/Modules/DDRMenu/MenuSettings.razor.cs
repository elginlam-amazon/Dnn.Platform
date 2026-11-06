// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.DDRMenu
{
    using System;

    using DotNetNuke.Entities.Modules;
    using DotNetNuke.UI.WebControls;

    /// <summary>Implements the settings view of the module.</summary>
    /// <remarks>
    /// Was <c>MenuSettings.ascx</c> + <c>MenuSettings.ascx.cs</c> +
    /// <c>MenuSettings.ascx.designer.cs</c>. The markup is <c>MenuSettings.razor</c>; the eleven
    /// designer-generated fields are the editor properties below (the <c>IncludeHiddenSection</c>
    /// <c>HtmlTableRow</c> became <see cref="IncludeHiddenSectionVisible"/>, which drives an
    /// <c>@if</c> around that row).
    /// </remarks>
    public partial class MenuSettings : ModuleSettingsBase
    {
        private bool settingsLoaded;

        /// <summary>Gets the editor of the <c>MenuStyle</c> setting.</summary>
        protected TextEditControl MenuStyle { get; private set; }

        /// <summary>Gets the editor of the <c>NodeXmlPath</c> setting.</summary>
        protected TextEditControl NodeXmlPath { get; private set; }

        /// <summary>Gets the editor of the <c>NodeSelector</c> setting.</summary>
        protected TextEditControl NodeSelector { get; private set; }

        /// <summary>Gets the editor of the <c>IncludeNodes</c> setting.</summary>
        protected TextEditControl IncludeNodes { get; private set; }

        /// <summary>Gets the editor of the <c>ExcludeNodes</c> setting.</summary>
        protected TextEditControl ExcludeNodes { get; private set; }

        /// <summary>Gets the editor of the <c>NodeManipulator</c> setting.</summary>
        protected TextEditControl NodeManipulator { get; private set; }

        /// <summary>Gets the editor of the <c>IncludeContext</c> setting.</summary>
        protected CheckEditControl IncludeContext { get; private set; }

        /// <summary>Gets the editor of the <c>IncludeHidden</c> setting.</summary>
        protected CheckEditControl IncludeHidden { get; private set; }

        /// <summary>Gets the editor of the <c>TemplateArguments</c> setting.</summary>
        protected MultiLineTextEditControl TemplateArguments { get; private set; }

        /// <summary>Gets the editor of the <c>ClientOptions</c> setting.</summary>
        protected MultiLineTextEditControl ClientOptions { get; private set; }

        /// <summary>Gets a value indicating whether the "Include hidden nodes" row renders.</summary>
        /// <remarks>Replaces <c>IncludeHiddenSection.Visible</c>.</remarks>
        protected bool IncludeHiddenSectionVisible { get; private set; }

        /// <inheritdoc />
        /// <remarks>
        /// The WebForms implementation returned early when <c>IsPostBack</c> was true so that the
        /// editors kept the values the user had typed. A Blazor component is not re-created per post
        /// back, so the equivalent guard is to populate only on the first pass.
        /// </remarks>
        public override void LoadSettings()
        {
            if (this.settingsLoaded)
            {
                return;
            }

            this.settingsLoaded = true;
            this.EnsureEditors();

            // BL0005: these editors are created and owned by this component (no parent supplies
            // their parameters), which is exactly how the code behind assigned .Value before.
#pragma warning disable BL0005
            this.MenuStyle.Value = this.Settings["MenuStyle"] ?? string.Empty;
            this.NodeXmlPath.Value = this.Settings["NodeXmlPath"] ?? string.Empty;
            this.NodeSelector.Value = this.Settings["NodeSelector"] ?? string.Empty;
            this.IncludeNodes.Value = this.Settings["IncludeNodes"] ?? string.Empty;
            this.ExcludeNodes.Value = this.Settings["ExcludeNodes"] ?? string.Empty;
            this.NodeManipulator.Value = this.Settings["NodeManipulator"] ?? string.Empty;
            this.IncludeContext.Value = Convert.ToBoolean(this.Settings["IncludeContext"] ?? "false");
            this.IncludeHidden.Value = Convert.ToBoolean(this.Settings["IncludeHidden"] ?? "false");
            this.TemplateArguments.Value = this.Settings["TemplateArguments"] ?? string.Empty;
            this.ClientOptions.Value = this.Settings["ClientOptions"] ?? string.Empty;
#pragma warning restore BL0005
        }

        /// <inheritdoc />
        public override void UpdateSettings()
        {
            this.EnsureEditors();

            ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "MenuStyle", (this.MenuStyle.Value ?? string.Empty).ToString());
            ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "NodeXmlPath", (this.NodeXmlPath.Value ?? string.Empty).ToString());
            ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "NodeSelector", (this.NodeSelector.Value ?? string.Empty).ToString());
            ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "IncludeNodes", (this.IncludeNodes.Value ?? string.Empty).ToString());
            ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ExcludeNodes", (this.ExcludeNodes.Value ?? string.Empty).ToString());
            ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "NodeManipulator", (this.NodeManipulator.Value ?? string.Empty).ToString());
            ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "IncludeContext", (this.IncludeContext.Value ?? "false").ToString());
            ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "IncludeHidden", (this.IncludeHidden.Value ?? "false").ToString());
            ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "TemplateArguments", (this.TemplateArguments.Value ?? string.Empty).ToString());
            ModuleController.Instance.UpdateModuleSetting(this.ModuleId, "ClientOptions", (this.ClientOptions.Value ?? string.Empty).ToString());
        }

        /// <inheritdoc />
        /// <remarks>
        /// Creates the editors the <c>.ascx</c> markup used to declare and computes the flag that
        /// was <c>OnPreRender</c>'s <c>IncludeHiddenSection.Visible</c> assignment; the component's
        /// pre-render phase runs after the first render, so the flag is computed in the <c>Init</c>
        /// phase, which still precedes rendering.
        /// </remarks>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            this.EnsureEditors();
            this.IncludeHiddenSectionVisible = DNNAbstract.IncludeHiddenSupported();
        }

        private void EnsureEditors()
        {
            if (this.MenuStyle != null)
            {
                return;
            }

            // Each editor keeps the ID its dnn: tag had, because that is what names the form field
            // the DNN client scripts and EditControl.LoadPostData work with.
#pragma warning disable BL0005
            this.MenuStyle = new TextEditControl { ID = "MenuStyle", Name = "MenuStyle" };
            this.NodeXmlPath = new TextEditControl { ID = "NodeXmlPath", Name = "NodeXmlPath" };
            this.NodeSelector = new TextEditControl { ID = "NodeSelector", Name = "NodeSelector" };
            this.IncludeNodes = new TextEditControl { ID = "IncludeNodes", Name = "IncludeNodes" };
            this.ExcludeNodes = new TextEditControl { ID = "ExcludeNodes", Name = "ExcludeNodes" };
            this.NodeManipulator = new TextEditControl { ID = "NodeManipulator", Name = "NodeManipulator" };
            this.IncludeContext = new CheckEditControl { ID = "IncludeContext", Name = "IncludeContext" };
            this.IncludeHidden = new CheckEditControl { ID = "IncludeHidden", Name = "IncludeHidden" };
            this.TemplateArguments = new MultiLineTextEditControl { ID = "TemplateArguments", Name = "TemplateArguments" };
            this.ClientOptions = new MultiLineTextEditControl { ID = "ClientOptions", Name = "ClientOptions" };
#pragma warning restore BL0005
        }
    }
}
