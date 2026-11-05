// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Services.Localization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>
    /// The DNNLocaleEditControl control provides a standard UI component for selecting
    /// a Locale.
    /// </summary>
    /// <remarks>
    /// The control implemented <c>IPostBackEventHandler</c> purely to switch between the native and
    /// the English language names. <see cref="RaisePostBackEvent"/> is preserved and is now invoked
    /// directly from the radio buttons instead of through a <c>__doPostBack</c> round trip.
    /// The <c>onLocaleChanged</c> inline script that showed/hid the "language not selected" message
    /// is gone: the message is now rendered conditionally, which Blazor updates on every change.
    /// </remarks>
    public class DNNLocaleEditControl : TextEditControl
    {
        private string displayMode = "Native";
        private LanguagesListType listType = LanguagesListType.Enabled;

        /// <summary>Gets the type of language list that is offered.</summary>
        protected LanguagesListType ListType => this.listType;

        /// <summary>Gets the language name flavour that is displayed (<c>Native</c> or <c>English</c>).</summary>
        protected string DisplayMode => this.displayMode;

        /// <summary>Gets the portal settings.</summary>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Breaking change")]
        protected PortalSettings PortalSettings => PortalSettings.Current;

        /// <summary>Switches the displayed language name flavour.</summary>
        /// <param name="eventArgument">Either <c>Native</c> or <c>English</c>.</param>
        /// <remarks>Replaces <c>IPostBackEventHandler.RaisePostBackEvent</c>.</remarks>
        public void RaisePostBackEvent(string eventArgument)
        {
            this.displayMode = eventArgument;
        }

        /// <summary>OnAttributesChanged runs when the CustomAttributes property has changed.</summary>
        protected override void OnAttributesChanged()
        {
            // Get the List settings out of the "Attributes"
            if (this.CustomAttributes != null)
            {
                foreach (Attribute attribute in this.CustomAttributes)
                {
                    var listAtt = attribute as LanguagesListTypeAttribute;
                    if (listAtt != null)
                    {
                        this.listType = listAtt.ListType;
                        break;
                    }
                }
            }
        }

        /// <summary>RenderViewMode renders the View (readonly) mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderViewMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            Locale locale = LocaleController.Instance.GetLocale(this.StringValue);

            builder.OpenElement(0, "div");
            this.AddStyleAttributes(builder, 1);
            if (locale != null)
            {
                builder.AddContent(5, locale.Text);
            }

            builder.CloseElement();
        }

        /// <summary>RenderEditMode renders the Edit mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Render div
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "dnnLeft");

            var cultures = new List<CultureInfo>();
            switch (this.ListType)
            {
                case LanguagesListType.All:
                    var culturesArray = CultureInfo.GetCultures(CultureTypes.SpecificCultures);
                    Array.Sort(culturesArray, new CultureInfoComparer(this.DisplayMode));
                    cultures = culturesArray.ToList();
                    break;
                case LanguagesListType.Supported:
                    cultures = LocaleController.Instance.GetLocales(Null.NullInteger).Values
                        .Select(c => CultureInfo.GetCultureInfo(c.Code))
                        .ToList();
                    break;
                case LanguagesListType.Enabled:
                    cultures = LocaleController.Instance.GetLocales(this.PortalSettings.PortalId).Values
                        .Select(c => CultureInfo.GetCultureInfo(c.Code))
                        .ToList();
                    break;
            }

            var promptValue = this.StringValue == Null.NullString && cultures.Count > 1 && !this.Required;

            // Render the Select Tag
            builder.OpenElement(10, "select");
            builder.AddAttribute(11, "name", this.UniqueID);
            builder.AddAttribute(12, "id", this.ClientID);
            builder.AddAttribute(13, "aria-label", this.Name);
            builder.AddAttribute(14, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, this.OnInputChanged));

            // Render None selected option
            builder.OpenElement(15, "option");
            builder.AddAttribute(16, "value", Null.NullString);
            if (this.StringValue == Null.NullString)
            {
                // Add the Selected Attribute
                builder.AddAttribute(17, "selected", "selected");
            }

            builder.AddContent(18, Localization.GetString("Not_Specified", Localization.SharedResourceFile));
            builder.CloseElement();

            builder.OpenRegion(20);
            foreach (var culture in cultures)
            {
                this.RenderOption(builder, culture);
            }

            builder.CloseRegion();

            // Close Select Tag
            builder.CloseElement();

            if (promptValue)
            {
                builder.AddMarkupContent(30, "<br />");

                builder.OpenElement(31, "span");
                builder.AddAttribute(32, "class", "dnnFormMessage dnnFormError");
                builder.AddContent(33, Localization.GetString("LanguageNotSelected", Localization.SharedResourceFile));
                builder.CloseElement();
            }

            // Render break
            builder.AddMarkupContent(40, "<br />");

            // Render Span
            builder.OpenElement(41, "span");
            builder.AddAttribute(42, "class", "dnnFormRadioButtons");

            // Render Button Row
            this.RenderModeButtons(builder, 43);

            // close span
            builder.CloseElement();

            // close div
            builder.CloseElement();
        }

        private bool IsSelected(string locale)
        {
            return locale == this.StringValue;
        }

        private void RenderModeButtons(RenderTreeBuilder builder, int sequence)
        {
            this.RenderModeButton(builder, sequence, "English", Localization.GetString("EnglishName", Localization.GlobalResourceFile));
            this.RenderModeButton(builder, sequence + 20, "Native", Localization.GetString("NativeName", Localization.GlobalResourceFile));
        }

        private void RenderModeButton(RenderTreeBuilder builder, int sequence, string mode, string text)
        {
            builder.OpenElement(sequence, "input");
            builder.AddAttribute(sequence + 1, "type", "radio");
            builder.AddAttribute(sequence + 2, "aria-label", "Mode");
            builder.AddAttribute(sequence + 3, "name", this.UniqueID + "_mode");
            builder.AddAttribute(sequence + 4, "value", mode);
            if (this.DisplayMode == mode)
            {
                builder.AddAttribute(sequence + 5, "checked", "checked");
            }
            else
            {
                // Replaces Page.ClientScript.GetPostBackEventReference(this, mode).
                builder.AddAttribute(
                    sequence + 6,
                    "onchange",
                    EventCallback.Factory.Create<ChangeEventArgs>(this, () => this.RaisePostBackEvent(mode)));
            }

            builder.CloseElement();
            builder.AddContent(sequence + 10, text);
        }

        private void RenderOption(RenderTreeBuilder builder, CultureInfo culture)
        {
            string localeName;

            if (this.DisplayMode == "Native")
            {
                localeName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(culture.NativeName);
            }
            else
            {
                localeName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(culture.EnglishName);
            }

            builder.OpenElement(0, "option");

            // Add the Value Attribute
            builder.AddAttribute(1, "value", culture.Name);

            if (this.IsSelected(culture.Name))
            {
                builder.AddAttribute(2, "selected", "selected");
            }

            builder.AddContent(3, localeName);
            builder.CloseElement();
        }
    }
}
