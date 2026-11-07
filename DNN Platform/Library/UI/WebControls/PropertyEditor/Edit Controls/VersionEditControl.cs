// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.WebControls
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Web;

    /// <summary>The VersionEditControl control provides a standard UI component for editing <see cref="Version"/> properties.</summary>
    public class VersionEditControl : EditControl
    {
        /// <summary>Gets the value expressed as a <see cref="Version"/>.</summary>
        protected Version Version
        {
            get
            {
                return this.Value as Version;
            }
        }

        /// <summary>Gets or sets stringValue is the value of the control expressed as a String.</summary>
        /// <value>A string representing the Value.</value>
        protected override string StringValue
        {
            get
            {
                return this.Value.ToString();
            }

            set
            {
                this.Value = new Version(value);
            }
        }

        /// <inheritdoc />
        public override bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            ArgumentNullException.ThrowIfNull(postCollection);

            string majorVersion = postCollection[postDataKey + "_Major"];
            string minorVersion = postCollection[postDataKey + "_Minor"];
            string buildVersion = postCollection[postDataKey + "_Build"];
            bool dataChanged = false;
            Version presentValue = this.Version;
            var postedValue = new Version(majorVersion + "." + minorVersion + "." + buildVersion);
            if (!postedValue.Equals(presentValue))
            {
                this.Value = postedValue;
                dataChanged = true;
            }

            return dataChanged;
        }

        /// <summary>Renders one of the three version-part drop downs.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        /// <param name="sequence">The render tree sequence number to use.</param>
        /// <param name="type">The version part (<c>Major</c>, <c>Minor</c> or <c>Build</c>).</param>
        /// <param name="val">The currently selected value.</param>
        protected void RenderDropDownList(RenderTreeBuilder builder, int sequence, string type, int val)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Render the Select Tag
            builder.OpenElement(sequence, "select");
            builder.AddAttribute(sequence + 1, "name", this.UniqueID + "_" + type);
            builder.AddAttribute(sequence + 2, "style", "width:60px");
            builder.AddAttribute(sequence + 3, "aria-label", type);
            builder.AddAttribute(
                sequence + 4,
                "onchange",
                EventCallback.Factory.Create<ChangeEventArgs>(this, args => this.OnPartChanged(type, args)));

            builder.OpenRegion(sequence + 5);
            for (int i = 0; i <= 99; i++)
            {
                builder.OpenElement(0, "option");

                // Add the Value Attribute
                builder.AddAttribute(1, "value", i.ToString(CultureInfo.InvariantCulture));
                if (val == i)
                {
                    // Add the Selected Attribute
                    builder.AddAttribute(2, "selected", "selected");
                }

                builder.AddContent(3, i.ToString("00", CultureInfo.CurrentCulture));
                builder.CloseElement();
            }

            builder.CloseRegion();

            // Close Select Tag
            builder.CloseElement();
        }

        /// <summary>OnDataChanged runs when the PostbackData has changed.  It raises the <see cref="EditControl.ValueChanged"/> Event.</summary>
        /// <param name="e">The event arguments.</param>
        protected override void OnDataChanged(EventArgs e)
        {
            var args = new PropertyEditorEventArgs(this.Name);
            args.Value = this.Value;
            args.OldValue = this.OldValue;
            args.StringValue = this.StringValue;

            this.OnValueChanged(args);
        }

        /// <summary>RenderEditMode renders the Edit mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // The three drop downs together make up one value, so the parts that the user did not
            // touch have to be present when LoadPostData reassembles the Version.
            this.SeedPostData();

            // Render a containing span Tag
            builder.OpenElement(0, "span");
            this.AddStyleAttributes(builder, 1);

            // Render Major
            this.RenderDropDownList(builder, 10, "Major", this.Version.Major);

            builder.AddMarkupContent(20, "&nbsp;");

            // Render Minor
            this.RenderDropDownList(builder, 30, "Minor", this.Version.Minor);

            builder.AddMarkupContent(40, "&nbsp;");

            // Render Build
            this.RenderDropDownList(builder, 50, "Build", this.Version.Build);

            // Close Span Tag
            builder.CloseElement();
        }

        /// <summary>RenderViewMode renders the View (readonly) mode of the control.</summary>
        /// <param name="builder">The render tree builder to write to.</param>
        protected override void RenderViewMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenElement(0, "span");
            this.AddStyleAttributes(builder, 1);
            if (this.Version != null)
            {
                builder.AddContent(5, this.Version.ToString(3));
            }

            builder.CloseElement();
        }

        private void SeedPostData()
        {
            var version = this.Version;
            if (version == null)
            {
                return;
            }

            this.SetPostValue("_Major", version.Major.ToString(CultureInfo.InvariantCulture));
            this.SetPostValue("_Minor", version.Minor.ToString(CultureInfo.InvariantCulture));
            this.SetPostValue("_Build", version.Build.ToString(CultureInfo.InvariantCulture));
        }

        private void OnPartChanged(string type, ChangeEventArgs args)
        {
            this.SetPostValue("_" + type, args?.Value?.ToString());
            this.CommitPostData();
        }
    }
}
