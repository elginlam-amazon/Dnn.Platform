// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.Modules.TelerikRemoval.UserControls
{
    using System;

    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    using WrappedDnnCheckBox = DotNetNuke.Web.UI.WebControls.DnnCheckBox;

    /// <summary>
    /// A wrapper around <see cref="WrappedDnnCheckBox"/>
    /// to support localization.
    /// </summary>
    /// <remarks>
    /// The <c>DesignMode</c> short circuit of the original <c>Localize</c> getter is gone: it was a
    /// Visual Studio WebForms designer concept (<c>System.ComponentModel.Component.DesignMode</c>)
    /// and a Blazor component is never instantiated by a designer, so the branch could never be
    /// taken. The <c>Render(HtmlTextWriter)</c> override that called
    /// <see cref="LocalizeStrings"/> immediately before rendering is replaced by the
    /// <see cref="BuildRenderTree"/> override below, which is the render step of the migrated base
    /// component. The base <see cref="WrappedDnnCheckBox"/> has no <c>ToolTip</c> property - it
    /// renders its attributes verbatim - so the tooltip is carried in the <c>title</c> attribute,
    /// which is the very markup the WebForms <c>ToolTip</c> produced.
    /// </remarks>
    public class DnnCheckBox : WrappedDnnCheckBox, ILocalizable
    {
        private bool localize = true;

        /// <inheritdoc />
        [Parameter]
        public string LocalResourceFile { get; set; }

        // BL0007: the parameter keeps the property accessors of the original control, which
        // forwarded to a backing field.
        #pragma warning disable BL0007
        /// <inheritdoc />
        [Parameter]
        public bool Localize
        {
            get
            {
                return this.localize;
            }

            set
            {
                this.localize = value;
            }
        }
        #pragma warning restore BL0007

        // BL0007: the parameter keeps the property accessors of the original Web Forms control,
        // which forwarded to the control's attribute collection.
        #pragma warning disable BL0007
        /// <summary>Gets or sets the tooltip of the checkbox.</summary>
        /// <remarks>
        /// Replaces <c>CheckBox.ToolTip</c>; it is stored in (and rendered from) the <c>title</c>
        /// attribute, which is what the WebForms property emitted.
        /// </remarks>
        [Parameter]
        public string ToolTip
        {
            get => this.Attributes.TryGetValue("title", out var toolTip) ? toolTip : null;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    this.Attributes.Remove("title");
                }
                else
                {
                    this.Attributes["title"] = value;
                }
            }
        }
        #pragma warning restore BL0007

        /// <inheritdoc />
        public virtual void LocalizeStrings()
        {
            if (!this.Localize)
            {
                return;
            }

            if (!string.IsNullOrEmpty(this.ToolTip))
            {
                this.ToolTip = this.GetLocalizedString(this.ToolTip);
            }

            if (!string.IsNullOrEmpty(this.Text))
            {
                var unlocalizedText = this.Text;
                this.Text = this.GetLocalizedString(unlocalizedText);
                if (string.IsNullOrEmpty(this.Text))
                {
                    this.Text = unlocalizedText;
                }

                if (string.IsNullOrEmpty(this.ToolTip))
                {
                    this.ToolTip = this.GetLocalizedString(unlocalizedText + ".ToolTip");
                    if (string.IsNullOrEmpty(this.ToolTip))
                    {
                        this.ToolTip = unlocalizedText;
                    }
                }
            }
        }

        /// <inheritdoc />
        /// <remarks>Replaces the <c>Render(HtmlTextWriter)</c> override.</remarks>
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.LocalizeStrings();
            base.BuildRenderTree(builder);
        }

        /// <summary>Resolves a resource key against this control's resource file.</summary>
        /// <param name="key">The resource key.</param>
        /// <returns>The localized text, or the key when no resource file can be determined.</returns>
        /// <remarks>
        /// The original called
        /// <see cref="Utilities.GetLocalizedStringFromParent(string, DotNetNuke.UI.Utilities.IClientApiControl)"/>,
        /// which derived the resource file by walking up the WebForms control tree. A Blazor
        /// component declared in markup has no such parent, so <see cref="LocalResourceFile"/> -
        /// which the hosting module control supplies - is used when it is set, and the parent walk
        /// is kept as the fallback for a control the skinning engine parents itself.
        /// </remarks>
        private string GetLocalizedString(string key)
        {
            if (!string.IsNullOrEmpty(this.LocalResourceFile))
            {
                return Localization.GetString(key, this.LocalResourceFile);
            }

            return Utilities.GetLocalizedStringFromParent(key, this);
        }
    }
}
