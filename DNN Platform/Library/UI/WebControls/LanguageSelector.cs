// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>Language Selector control.</summary>
    /// <remarks>
    /// Was a <c>System.Web.UI.Control</c> that built a tree of <c>RadioButton</c>/<c>CheckBox</c>
    /// controls in <c>CreateChildControls</c> and read the selection back with
    /// <c>FindControl</c>. It is now a <see cref="DnnComponentBase"/> that renders the same list and
    /// keeps the selection in a dictionary keyed by culture name, which is what
    /// <see cref="SelectedLanguages"/> reads and writes.
    /// </remarks>
    public class LanguageSelector : DnnComponentBase
    {
        private readonly Dictionary<string, bool> selection = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private LanguageSelectionMode selectionMode = LanguageSelectionMode.Single;
        private LanguageSelectionObject selectionObject = LanguageSelectionObject.SpecificCulture;
        private LanguageItemStyle itemStyle = LanguageItemStyle.FlagAndCaption;
        private LanguageListDirection listDirection = LanguageListDirection.Vertical;
        private bool selectionInitialized;

        /// <summary>The way a language is presented in the list.</summary>
        public enum LanguageItemStyle
        {
            /// <summary>Display only the flag.</summary>
            FlagOnly = 1,

            /// <summary>Display the flag and a caption.</summary>
            FlagAndCaption = 2,

            /// <summary>Display only the caption.</summary>
            CaptionOnly = 3,
        }

        /// <summary>The direction the language list flows in.</summary>
        public enum LanguageListDirection
        {
            /// <summary>Horizontal language list.</summary>
            Horizontal = 1,

            /// <summary>Vertical language list.</summary>
            Vertical = 2,
        }

        /// <summary>Language Selection mode, offered to the user: single select or multiple select.</summary>
        public enum LanguageSelectionMode
        {
            /// <summary>Multiple select.</summary>
            Multiple = 1,

            /// <summary>Single select.</summary>
            [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", Justification = "Breaking change")]
            Single = 2,
        }

        /// <summary>Selection object: Language ("de", "en") or Locale ("de-DE", "en-US").</summary>
        public enum LanguageSelectionObject
        {
            /// <summary>A language without a culture, e.g. "de" or "en".</summary>
            NeutralCulture = 1,

            /// <summary>A langauge/culture pair, e.g. "de-DE" or "en-US".</summary>
            SpecificCulture = 2,
        }

        /// <summary>Gets or sets selection mode (single, multiple).</summary>
        [Parameter]
        public LanguageSelectionMode SelectionMode
        {
            get => this.selectionMode;

            set
            {
                if (this.selectionMode != value)
                {
                    this.selectionMode = value;
                    this.ResetSelection();
                }
            }
        }

        /// <summary>Gets or sets the type of objects to be selectable: NeutralCulture ("de") or SpecificCulture ("de-DE").</summary>
        [Parameter]
        public LanguageSelectionObject SelectionObject
        {
            get => this.selectionObject;

            set
            {
                if (this.selectionObject != value)
                {
                    this.selectionObject = value;
                    this.ResetSelection();
                }
            }
        }

        /// <summary>Gets or sets the style of the language items.</summary>
        [Parameter]
        public LanguageItemStyle ItemStyle
        {
            get => this.itemStyle;
            set => this.itemStyle = value;
        }

        /// <summary>Gets or sets the direction of the language list.</summary>
        [Parameter]
        public LanguageListDirection ListDirection
        {
            get => this.listDirection;
            set => this.listDirection = value;
        }

        /// <summary>Gets or sets the list of selected languages.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "Breaking change")]
        public string[] SelectedLanguages
        {
            get
            {
                this.EnsureSelection();
                var a = new ArrayList();
                if (GetCultures(this.SelectionObject == LanguageSelectionObject.SpecificCulture).Length < 2)
                {
                    // return single language
                    var settings = PortalController.Instance.GetCurrentSettings();
                    foreach (string strLocale in LocaleController.Instance.GetLocales(settings.PortalId).Keys)
                    {
                        a.Add(strLocale);
                    }
                }
                else
                {
                    // create list of selected languages
                    foreach (CultureInfo c in GetCultures(this.SelectionObject == LanguageSelectionObject.SpecificCulture))
                    {
                        if (this.selection.TryGetValue(c.Name, out var isSelected) && isSelected)
                        {
                            a.Add(c.Name);
                        }
                    }
                }

                return a.ToArray(typeof(string)) as string[];
            }

            set
            {
                ArgumentNullException.ThrowIfNull(value);

                this.EnsureSelection();
                if (this.SelectionMode == LanguageSelectionMode.Single && value.Length > 1)
                {
                    throw new ArgumentException("Selection mode 'single' cannot have more than one selected item.");
                }

                foreach (CultureInfo c in GetCultures(this.SelectionObject == LanguageSelectionObject.SpecificCulture))
                {
                    this.selection[c.Name] = false;
                }

                foreach (string strLocale in value)
                {
                    if (this.selection.ContainsKey(strLocale))
                    {
                        this.selection[strLocale] = true;
                    }
                }
            }
        }

        /// <inheritdoc />
        protected override string TagName => null;

        /// <summary>Create Child Controls.</summary>
        /// <remarks>
        /// Replaces <c>CreateChildControls</c>: the controls are the markup built by
        /// <see cref="RenderContents"/>, so what is created here is the selection state and the
        /// visibility of the control.
        /// </remarks>
        protected override void CreateChildControls()
        {
            var cultures = GetCultures(this.SelectionObject == LanguageSelectionObject.SpecificCulture);
            this.selection.Clear();
            foreach (var c in cultures)
            {
                // the single-select list starts out on the system locale, as the radio buttons did
                this.selection[c.Name] = this.SelectionMode == LanguageSelectionMode.Single
                    && c.Name == Localization.SystemLocale;
            }

            // Hide if not more than one language
            if (cultures.Length < 2)
            {
                this.Visible = false;
            }

            this.selectionInitialized = true;
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            this.EnsureSelection();
        }

        /// <inheritdoc />
        protected override void RenderContents(RenderTreeBuilder builder, ref int sequence)
        {
            ArgumentNullException.ThrowIfNull(builder);

            this.EnsureSelection();

            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "dnnLangSelector");
            builder.OpenElement(sequence++, "ul");
            if (this.ListDirection == LanguageListDirection.Horizontal)
            {
                builder.AddAttribute(sequence++, "class", "dnnLangSelectorHorizontal");
            }

            foreach (var c in GetCultures(this.SelectionObject == LanguageSelectionObject.SpecificCulture))
            {
                var culture = c;
                var inputId = (this.SelectionMode == LanguageSelectionMode.Single ? "opt" : "chk") + culture.Name;
                builder.OpenElement(sequence++, "li");

                builder.OpenElement(sequence++, "input");
                builder.AddAttribute(sequence++, "type", this.SelectionMode == LanguageSelectionMode.Single ? "radio" : "checkbox");
                builder.AddAttribute(sequence++, "id", inputId);
                if (this.SelectionMode == LanguageSelectionMode.Single)
                {
                    builder.AddAttribute(sequence++, "name", (this.ID ?? string.Empty) + "_Locale");
                }

                builder.AddAttribute(sequence++, "checked", this.selection.TryGetValue(culture.Name, out var isSelected) && isSelected);
                builder.AddAttribute(sequence++, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(
                    this,
                    args => this.ItemChanged(culture.Name, args)));
                builder.CloseElement();

                builder.OpenElement(sequence++, "label");
                builder.AddAttribute(sequence++, "for", inputId);
                if (this.ItemStyle != LanguageItemStyle.CaptionOnly)
                {
                    builder.OpenElement(sequence++, "img");
                    builder.AddAttribute(sequence++, "src", Common.Globals.ResolveUrl("~/images/Flags/" + culture.Name + ".gif"));
                    builder.AddAttribute(sequence++, "alt", culture.DisplayName);
                    builder.AddAttribute(sequence++, "style", "vertical-align:middle");
                    builder.CloseElement();
                }

                if (this.ItemStyle != LanguageItemStyle.FlagOnly)
                {
                    builder.AddMarkupContent(sequence++, "&nbsp;" + culture.DisplayName);
                }

                builder.CloseElement();

                builder.CloseElement();
            }

            builder.CloseElement();
            builder.CloseElement();

            base.RenderContents(builder, ref sequence);
        }

        /// <summary>retrieve the cultures, currently supported by the portal.</summary>
        /// <param name="specific">true: locales, false: neutral languages.</param>
        /// <returns>Array of cultures.</returns>
        private static CultureInfo[] GetCultures(bool specific)
        {
            var a = new ArrayList();
            var settings = PortalController.Instance.GetCurrentSettings();
            foreach (string strLocale in LocaleController.Instance.GetLocales(settings.PortalId).Keys)
            {
                var c = new CultureInfo(strLocale);
                if (specific)
                {
                    a.Add(c);
                }
                else
                {
                    CultureInfo p = c.Parent;
                    if (!a.Contains(p))
                    {
                        a.Add(p);
                    }
                }
            }

            return (CultureInfo[])a.ToArray(typeof(CultureInfo));
        }

        private void ItemChanged(string cultureName, ChangeEventArgs args)
        {
            var isSelected = args?.Value is bool value
                ? value
                : Convert.ToBoolean(args?.Value, CultureInfo.InvariantCulture);

            if (this.SelectionMode == LanguageSelectionMode.Single && isSelected)
            {
                foreach (var key in this.selection.Keys.ToList())
                {
                    this.selection[key] = false;
                }
            }

            this.selection[cultureName] = isSelected;
        }

        private void ResetSelection()
        {
            if (this.selectionInitialized)
            {
                this.CreateChildControls();
            }
        }

        private void EnsureSelection()
        {
            if (this.selectionInitialized)
            {
                return;
            }

            this.EnsureChildControls();
        }
    }
}
