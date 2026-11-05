// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls.Internal
{
    using System;
    using System.IO;

    using DotNetNuke.Entities.Icons;
    using DotNetNuke.Framework;
    using DotNetNuke.Framework.JavaScriptLibraries;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Utilities;

    /// <summary>
    /// A TriState permission control built specifically for use in the PermissionGrid control
    /// This control is not general in any way shape of form and should NOT be used outside
    /// of the PermissionGrid.
    /// </summary>
    /// <remarks>
    /// Was a <c>System.Web.UI.WebControls.HiddenField</c>. It is now a
    /// <see cref="DotNetNuke.UI.WebControls.BaseWebControl"/>, the control shape the DNN client API
    /// uses on ASP.NET Core: the control still renders the image plus the hidden input the
    /// <c>dnn.permissiontristate.js</c> behaviour cycles through, and the posted value is read
    /// through <see cref="IClientApiPage.GetRequestValue(string)"/> instead of
    /// <c>HiddenField.Value</c>'s view state.
    /// </remarks>
    public class PermissionTriState : BaseWebControl
    {
        private readonly string grantImagePath;
        private readonly string denyImagePath;
        private readonly string nullImagePath;
        private readonly string lockImagePath;
        private readonly string grantAltText;
        private readonly string denyAltText;
        private readonly string nullAltText;
        private bool valueSet;

        /// <summary>Initializes a new instance of the <see cref="PermissionTriState"/> class.</summary>
        public PermissionTriState()
            : base("input")
        {
            // kind of ugly to lookup this data each time, but doesn't seem worth the effort to
            // maintain statics for the paths but require a control instance to initialize them
            // and lazy load the text bits when the page instance (or localization) changes
            LookupScriptValues(this, out this.grantImagePath, out this.denyImagePath, out this.nullImagePath, out this.lockImagePath, out this.grantAltText, out this.denyAltText, out this.nullAltText);

            // Replaces the OnInit override: BaseWebControl raises PreRender, which is the earliest
            // point at which the control knows the page it belongs to.
            this.PreRender += (sender, args) => RegisterScripts(this.Page, this);
        }

        /// <summary>Gets or sets a value indicating whether this control represents the full control permission.</summary>
        public bool IsFullControl { get; set; }

        /// <summary>Gets or sets a value indicating whether this control represents the view permission.</summary>
        public bool IsView { get; set; }

        /// <summary>Gets or sets a value indicating whether this control is locked.</summary>
        /// <remarks>
        /// Locked is currently not used on a post-back and therefore the
        /// value on postback is undefined at this time.
        /// </remarks>
        public bool Locked { get; set; }

        /// <summary>Gets or sets the key of the permission this control grants or denies.</summary>
        public string PermissionKey { get; set; }

        /// <summary>Gets or sets a value indicating whether this control can express "deny".</summary>
        public bool SupportsDenyMode { get; set; }

        /// <summary>Gets or sets the value of the hidden input, which is <c>True</c>, <c>False</c> or <c>Null</c>.</summary>
        /// <remarks>Replaces <c>HiddenField.Value</c>.</remarks>
        public string Value
        {
            get
            {
                if (!this.valueSet)
                {
                    var posted = this.Page?.GetRequestValue(this.UniqueID);
                    if (!string.IsNullOrEmpty(posted))
                    {
                        this.Value = posted;
                    }
                }

                this.Attributes.TryGetValue("value", out var value);
                return value;
            }

            set
            {
                this.valueSet = true;
                this.Attributes["value"] = value;
            }
        }

        /// <summary>Registers the client side behaviour of the tri-state controls of a page, once per page.</summary>
        /// <param name="page">The page the controls live on.</param>
        /// <param name="ctl">One of the controls, used to look up the localized tool tips.</param>
        /// <remarks>
        /// The <c>System.Web.UI.Page</c>/<c>Control</c> parameters became the Client API
        /// abstractions, which is where keyed client script blocks are registered on ASP.NET Core.
        /// </remarks>
        public static void RegisterScripts(IClientApiPage page, IClientApiControl ctl)
        {
            const string ScriptKey = "initTriState";
            if (page == null)
            {
                return;
            }

            if (!ClientAPI.IsClientScriptBlockRegistered(page, ScriptKey))
            {
                AJAX.RegisterScriptManager();
                JavaScript.RequestRegistration(CommonJs.jQuery);
                ClientAPI.RegisterClientScriptBlock(page, "dnn.permissiontristate.js");

                ClientAPI.RegisterStartUpScript(page, ScriptKey, "<script type='text/javascript'>" + GetInitScript(ctl) + "</script>");
            }
        }

        /// <summary>Gets the client side initialization script of the tri-state controls.</summary>
        /// <param name="ctl">One of the controls, used to look up the localized tool tips.</param>
        /// <returns>The script.</returns>
        public static string GetInitScript(IClientApiControl ctl)
        {
            LookupScriptValues(ctl, out var grantImagePath, out var denyImagePath, out var nullImagePath, out _, out var grantAltText, out var denyAltText, out var nullAltText);

            return $$"""
                     jQuery(document).ready(
                     function() {
                         var images = { 'True': '{{ClientAPI.EscapeForJavascript(grantImagePath)}}', 'False': '{{ClientAPI.EscapeForJavascript(denyImagePath)}}', 'Null': '{{ClientAPI.EscapeForJavascript(nullImagePath)}}' };
                         var toolTips = { 'True': '{{ClientAPI.EscapeForJavascript(grantAltText)}}', 'False': '{{ClientAPI.EscapeForJavascript(denyAltText)}}', 'Null': '{{ClientAPI.EscapeForJavascript(nullAltText)}}' };
                         var tsm = dnn.controls.triStateManager(images, toolTips);
                         jQuery('.tristate').each( function(i, elem) {
                           tsm.initControl( elem );
                         });
                      });
                     """;
        }

        /// <summary>Renders the image and the hidden input of this control.</summary>
        /// <param name="writer">The writer to render to.</param>
        /// <remarks>Replaces <c>Render(HtmlTextWriter)</c>.</remarks>
        protected override void Render(TextWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            string imagePath;
            string altText;
            switch (this.Value)
            {
                case "True":
                    imagePath = this.grantImagePath;
                    altText = this.grantAltText;
                    break;

                case "False":
                    imagePath = this.denyImagePath;
                    altText = this.denyAltText;
                    break;

                default:
                    imagePath = this.nullImagePath;
                    altText = this.nullAltText;
                    break;
            }

            string cssClass = "tristate";
            if (this.Locked)
            {
                imagePath = this.lockImagePath;
                cssClass += " lockedPerm";

                // altText is set based on Value
            }

            if (!this.SupportsDenyMode)
            {
                cssClass += " noDenyPerm";
            }

            if (this.IsFullControl)
            {
                cssClass += " fullControl";
            }

            if (this.IsView && !this.Locked)
            {
                cssClass += " view";
            }

            if (!string.IsNullOrEmpty(this.PermissionKey) && !this.IsView && !this.IsFullControl)
            {
                cssClass += " " + this.PermissionKey.ToLowerInvariant();
            }

            writer.Write($"<img src='{imagePath}' alt='{altText}' />");

            this.CssClass = cssClass;
            this.Attributes["type"] = "hidden";
            if (!string.IsNullOrEmpty(this.UniqueID))
            {
                this.Attributes["name"] = this.UniqueID;
            }

            base.Render(writer);
        }

        private static void LookupScriptValues(IClientApiControl ctl, out string grantImagePath, out string denyImagePath, out string nullImagePath, out string lockImagePath, out string grantAltText, out string denyAltText, out string nullAltText)
        {
            grantImagePath = IconController.IconURL("Grant");
            denyImagePath = IconController.IconURL("Deny");
            nullImagePath = IconController.IconURL("Unchecked");
            lockImagePath = IconController.IconURL("Lock");

            grantAltText = Localization.GetString("PermissionTypeGrant");
            denyAltText = Localization.GetString("PermissionTypeDeny");
            nullAltText = Localization.GetString("PermissionTypeNull");
        }
    }
}
