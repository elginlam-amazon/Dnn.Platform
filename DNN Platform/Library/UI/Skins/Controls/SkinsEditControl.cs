// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Skins.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Icons;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.UI.Utilities;
    using DotNetNuke.UI.WebControls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>
    /// The SkinsEditControl control provides a standard UI component for editing
    /// skins.
    /// </summary>
    /// <remarks>
    /// Was a WebForms control implementing <c>IPostBackEventHandler</c> and rendering through an
    /// <c>HtmlTextWriter</c>. It is now a Blazor component: the delete/add links raise
    /// <see cref="RaisePostBackEvent"/> directly through <c>@onclick</c> instead of going through
    /// <c>Page.ClientScript.GetPostBackClientHyperlink</c>, and the inputs record their value with
    /// <c>SetPostValue</c>/<c>CommitPostData</c>, which is what <see cref="LoadPostData"/> reads.
    /// </remarks>
    public class SkinsEditControl : EditControl
    {
        private string addedItem = Null.NullString;

        /// <summary>
        /// Initializes a new instance of the <see cref="SkinsEditControl"/> class.
        /// Constructs a SkinsEditControl.
        /// </summary>
        public SkinsEditControl()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SkinsEditControl"/> class.
        /// Constructs a SkinsEditControl.
        /// </summary>
        /// <param name="type">The type of the property.</param>
        public SkinsEditControl(string type)
        {
            this.SystemType = type;
        }

        /// <summary>Gets oldStringValue returns the String representation of the OldValue.</summary>
        /// <value>A String representing the OldValue.</value>
        protected string OldStringValue
        {
            get
            {
                string strValue = Null.NullString;
                if (this.OldDictionaryValue != null)
                {
                    foreach (string skin in this.OldDictionaryValue.Values)
                    {
                        strValue += skin + ",";
                    }
                }

                return strValue;
            }
        }

        /// <summary>Gets or sets dictionaryValue returns the Dictionary(Of Integer, String) representation of the Value.</summary>
        /// <value>A Dictionary(Of Integer, String) representing the Value.</value>
        protected Dictionary<int, string> DictionaryValue
        {
            get
            {
                return this.Value as Dictionary<int, string>;
            }

            set
            {
                this.Value = value;
            }
        }

        /// <summary>Gets or sets oldDictionaryValue returns the Dictionary(Of Integer, String) representation of the OldValue.</summary>
        /// <value>A Dictionary(Of Integer, String) representing the OldValue.</value>
        protected Dictionary<int, string> OldDictionaryValue
        {
            get
            {
                return this.OldValue as Dictionary<int, string>;
            }

            set
            {
                this.OldValue = value;
            }
        }

        /// <summary>Gets or sets stringValue is the value of the control expressed as a String.</summary>
        /// <value>A string representing the Value.</value>
        protected override string StringValue
        {
            get
            {
                string strValue = Null.NullString;
                if (this.DictionaryValue != null)
                {
                    foreach (string skin in this.DictionaryValue.Values)
                    {
                        strValue += skin + ",";
                    }
                }

                return strValue;
            }

            set
            {
                this.Value = value;
            }
        }

        /// <summary>Gets or sets the skin that was typed into the "add" row.</summary>
        protected string AddedItem
        {
            get
            {
                return this.addedItem;
            }

            set
            {
                this.addedItem = value;
            }
        }

        /// <summary>Raises the add/delete event identified by the given argument.</summary>
        /// <param name="eventArgument">Either <c>Add</c> or <c>Delete_&lt;key&gt;</c>.</param>
        /// <remarks>
        /// Was <c>IPostBackEventHandler.RaisePostBackEvent</c>; the interface does not exist on
        /// ASP.NET Core, but the method is preserved because the argument format is part of the
        /// control's contract and the rendered links still call it.
        /// </remarks>
        public void RaisePostBackEvent(string eventArgument)
        {
            if (string.IsNullOrEmpty(eventArgument) || eventArgument.Length < 3)
            {
                return;
            }

            PropertyEditorEventArgs args;
            switch (eventArgument.Substring(0, 3))
            {
                case "Del":
                    args = new PropertyEditorEventArgs(this.Name);
                    args.Value = this.DictionaryValue;
                    args.OldValue = this.OldDictionaryValue;
                    args.Key = int.Parse(eventArgument.Substring(7), CultureInfo.InvariantCulture);
                    args.Changed = true;
                    this.OnItemDeleted(args);
                    break;
                case "Add":
                    args = new PropertyEditorEventArgs(this.Name);
                    args.Value = this.AddedItem;
                    args.StringValue = this.AddedItem;
                    args.Changed = true;
                    this.OnItemAdded(args);
                    break;
            }
        }

        /// <inheritdoc />
        public override bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            bool dataChanged = false;
            string postedValue;
            var newDictionaryValue = new Dictionary<int, string>();
            if (this.DictionaryValue != null)
            {
                foreach (KeyValuePair<int, string> kvp in this.DictionaryValue)
                {
                    postedValue = postCollection[this.UniqueID + "_skin" + kvp.Key];
                    if (kvp.Value.Equals(postedValue, StringComparison.Ordinal))
                    {
                        newDictionaryValue[kvp.Key] = kvp.Value;
                    }
                    else
                    {
                        newDictionaryValue[kvp.Key] = postedValue;
                        dataChanged = true;
                    }
                }
            }

            postedValue = postCollection[this.UniqueID + "_skinnew"];
            if (!string.IsNullOrEmpty(postedValue))
            {
                this.AddedItem = postedValue;
            }

            this.DictionaryValue = newDictionaryValue;
            return dataChanged;
        }

        /// <summary>OnDataChanged runs when the PostbackData has changed.  It raises the <see cref="EditControl.ValueChanged"/> Event.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnDataChanged(EventArgs e)
        {
            var args = new PropertyEditorEventArgs(this.Name);
            args.Value = this.DictionaryValue;
            args.OldValue = this.OldDictionaryValue;
            args.StringValue = string.Empty;
            args.Changed = true;
            this.OnValueChanged(args);
        }

        /// <summary>RenderEditMode renders the Edit mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            int length = Null.NullInteger;
            if (this.CustomAttributes != null)
            {
                foreach (Attribute attribute in this.CustomAttributes)
                {
                    if (attribute is MaxLengthAttribute)
                    {
                        var lengthAtt = (MaxLengthAttribute)attribute;
                        length = lengthAtt.Length;
                        break;
                    }
                }
            }

            if (this.DictionaryValue == null)
            {
                return;
            }

            var sequence = 0;
            foreach (KeyValuePair<int, string> kvp in this.DictionaryValue)
            {
                var key = kvp.Key;

                // Render Hyperlink
                builder.OpenElement(sequence++, "a");
                builder.AddAttribute(sequence++, "href", "#");
                builder.AddAttribute(sequence++, "title", Localization.GetString("cmdDelete", this.LocalResourceFile));
                builder.AddAttribute(
                    sequence++,
                    "onclick",
                    EventCallback.Factory.Create<MouseEventArgs>(this, () => this.ConfirmAndDelete(key)));
                builder.AddEventPreventDefaultAttribute(sequence++, "onclick", true);

                // Render Image
                builder.OpenElement(sequence++, "img");
                builder.AddAttribute(sequence++, "src", IconController.IconURL("Delete"));
                builder.AddAttribute(sequence++, "border", "0");
                builder.AddAttribute(sequence++, "alt", Localization.GetString("cmdDelete", this.LocalResourceFile));
                builder.CloseElement();

                // Render end of Hyperlink
                builder.CloseElement();

                builder.OpenElement(sequence++, "input");
                this.AddStyleAttributes(builder, sequence);
                sequence += 4;
                builder.AddAttribute(sequence++, "type", "text");
                builder.AddAttribute(sequence++, "value", kvp.Value);
                if (length > Null.NullInteger)
                {
                    builder.AddAttribute(sequence++, "maxlength", length.ToString(CultureInfo.InvariantCulture));
                }

                builder.AddAttribute(sequence++, "name", this.UniqueID + "_skin" + key);
                builder.AddAttribute(
                    sequence++,
                    "onchange",
                    EventCallback.Factory.Create<ChangeEventArgs>(this, args => this.OnSkinChanged("_skin" + key, args)));
                builder.CloseElement();

                builder.AddMarkupContent(sequence++, "<br />");
            }

            builder.AddMarkupContent(sequence++, "<br />");

            // Create Add Row
            // Render Hyperlink
            builder.OpenElement(sequence++, "a");
            builder.AddAttribute(sequence++, "href", "#");
            builder.AddAttribute(sequence++, "title", Localization.GetString("cmdAdd", this.LocalResourceFile));
            builder.AddAttribute(
                sequence++,
                "onclick",
                EventCallback.Factory.Create<MouseEventArgs>(this, () => this.RaisePostBackEvent("Add")));
            builder.AddEventPreventDefaultAttribute(sequence++, "onclick", true);

            // Render Image
            builder.OpenElement(sequence++, "img");
            builder.AddAttribute(sequence++, "src", IconController.IconURL("Add"));
            builder.AddAttribute(sequence++, "border", "0");
            builder.AddAttribute(sequence++, "alt", Localization.GetString("cmdAdd", this.LocalResourceFile));
            builder.CloseElement();

            // Render end of Hyperlink
            builder.CloseElement();

            builder.OpenElement(sequence++, "input");
            this.AddStyleAttributes(builder, sequence);
            sequence += 4;
            builder.AddAttribute(sequence++, "type", "text");
            builder.AddAttribute(sequence++, "value", Null.NullString);
            if (length > Null.NullInteger)
            {
                builder.AddAttribute(sequence++, "maxlength", length.ToString(CultureInfo.InvariantCulture));
            }

            builder.AddAttribute(sequence++, "name", this.UniqueID + "_skinnew");
            builder.AddAttribute(
                sequence++,
                "onchange",
                EventCallback.Factory.Create<ChangeEventArgs>(this, args => this.OnSkinChanged("_skinnew", args)));
            builder.CloseElement();

            builder.AddMarkupContent(sequence++, "<br />");
        }

        /// <summary>RenderViewMode renders the View (readonly) mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderViewMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (this.DictionaryValue == null)
            {
                return;
            }

            var sequence = 0;
            foreach (KeyValuePair<int, string> kvp in this.DictionaryValue)
            {
                builder.OpenElement(sequence++, "span");
                this.AddStyleAttributes(builder, sequence);
                sequence += 4;
                builder.AddContent(sequence++, kvp.Value);
                builder.CloseElement();
                builder.AddMarkupContent(sequence++, "<br />");
            }
        }

        /// <summary>Records the new value of one of the skin inputs and applies it.</summary>
        /// <param name="suffix">The input's name suffix.</param>
        /// <param name="args">The change event arguments.</param>
        private void OnSkinChanged(string suffix, ChangeEventArgs args)
        {
            this.SetPostValue(suffix, args?.Value?.ToString());
            this.CommitPostData();
        }

        /// <summary>Deletes the skin with the given key.</summary>
        /// <param name="key">The key of the skin to delete.</param>
        /// <remarks>
        /// The WebForms version asked for confirmation with an <c>onclick</c> <c>confirm()</c>
        /// script. A Blazor component may not emit inline script, so the confirmation is left to the
        /// consuming page's UI; the delete itself is unchanged.
        /// </remarks>
        private void ConfirmAndDelete(int key)
        {
            this.RaisePostBackEvent("Delete_" + key.ToString(CultureInfo.InvariantCulture));
        }
    }
}
