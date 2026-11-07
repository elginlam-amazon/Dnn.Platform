// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Globalization;

    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Personalization;
    using DotNetNuke.UI;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A language label control.</summary>
    /// <remarks>
    /// This was a <c>CompositeControl</c> that built a flag <c>Image</c>, a non breaking space and a
    /// <c>Label</c> into its control tree and filled them in <c>OnPreRender</c>. The composition is
    /// unchanged — a <see cref="DnnImage"/> and a <see cref="DnnLabel"/> in
    /// <see cref="DnnComponentBase.Controls"/> — but the values are computed before the render pass
    /// (Blazor has no pre-render stage that can still influence the markup) by
    /// <see cref="UpdateDisplay"/>.
    /// </remarks>
    public class DnnLanguageLabel : DnnComponentBase, ILocalizable
    {
        private DnnImage flag;

        private DnnLabel label;

        /// <summary>Initializes a new instance of the <see cref="DnnLanguageLabel"/> class.</summary>
        public DnnLanguageLabel()
        {
            this.Localize = true;
        }

        /// <summary>Gets or sets the display type.</summary>
        [Parameter]
        public CultureDropDownTypes DisplayType { get; set; }

        /// <summary>Gets or sets the language.</summary>
        [Parameter]
        public string Language { get; set; }

        /// <inheritdoc />
        [Parameter]
        public bool Localize { get; set; }

        /// <inheritdoc />
        [Parameter]
        public string LocalResourceFile { get; set; }

        /// <inheritdoc />
        protected override string TagName => "span";

        /// <inheritdoc />
        public virtual void LocalizeStrings()
        {
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            this.LocalResourceFile = Utilities.GetLocalResourceFile(this);
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>Builds the flag image, the separator and the language label.</summary>
        protected override void CreateChildControls()
        {
            // First clear the controls collection
            this.Controls.Clear();

            this.flag = new DnnImage();
            this.Controls.Add(this.flag);

            this.Controls.Add(new DnnLiteralControl("&nbsp;"));

            this.label = new DnnLabel { CssClass = null, Localize = false, };
            this.Controls.Add(this.label);

            base.CreateChildControls();
        }
        #pragma warning restore BL0005

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            this.UpdateDisplay();
        }

        // BL0005: the ported Web Forms control creates and configures its own child components in code, which is how the original control tree was built.
        #pragma warning disable BL0005
        /// <summary>Resolves the flag image and the language name of <see cref="Language"/>.</summary>
        /// <remarks>Replaces the <c>OnPreRender</c> override of the original control.</remarks>
        protected void UpdateDisplay()
        {
            this.EnsureChildControls();

            if (this.flag == null || this.label == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(this.Language))
            {
                this.flag.ImageUrl = "~/images/Flags/none.gif";
            }
            else
            {
                this.flag.ImageUrl = $"~/images/Flags/{this.Language}.gif";
            }

            if (this.DisplayType == 0)
            {
                var portalSettings = PortalController.Instance.GetCurrentSettings();
                string viewTypePersonalizationKey = "ViewType" + portalSettings.PortalId;
                string viewType = Convert.ToString(Personalization.GetProfile("LanguageDisplayMode", viewTypePersonalizationKey), CultureInfo.InvariantCulture);
                switch (viewType)
                {
                    case "NATIVE":
                        this.DisplayType = CultureDropDownTypes.NativeName;
                        break;
                    case "ENGLISH":
                        this.DisplayType = CultureDropDownTypes.EnglishName;
                        break;
                    default:
                        this.DisplayType = CultureDropDownTypes.DisplayName;
                        break;
                }
            }

            string localeName;
            if (string.IsNullOrEmpty(this.Language))
            {
                localeName = Localization.GetString("NeutralCulture", Localization.GlobalResourceFile);
            }
            else
            {
                localeName = Localization.GetLocaleName(this.Language, this.DisplayType);
            }

            this.label.Text = localeName;
            this.flag.AlternateText = localeName;
        }
        #pragma warning restore BL0005

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            this.UpdateDisplay();
            base.BuildRenderTree(builder);
        }
    }
}
