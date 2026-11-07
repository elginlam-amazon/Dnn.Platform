// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.UI.WebControls
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;

    using DotNetNuke.UI.WebControls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;

    /// <summary>A file edit control.</summary>
    /// <remarks>
    /// The editor is a <see cref="DnnFilePickerUploader"/>, which the Web Forms version obtained
    /// with <c>Page.LoadControl("~/controls/filepickeruploader.ascx")</c> and added to its
    /// <c>Controls</c> collection. The <c>.ascx</c> is gone — the uploader is a Blazor component
    /// now — so it is rendered as a child component by <see cref="RenderEditMode"/> and configured
    /// through its parameters, which is the same configuration the code-behind applied. The picker
    /// still owns the posted data (as the original remark says), so
    /// <see cref="LoadPostData"/> only compares its file id with the current value.
    /// </remarks>
    public class DnnFileEditControl : IntegerEditControl
    {
        /// <summary>Gets or sets the current file extension filter.</summary>
        [Parameter]
        public string FileFilter { get; set; }

        /// <summary>Gets or sets the current file path.</summary>
        [Parameter]
        public string FilePath { get; set; }

        /// <summary>Gets or sets the rendered file picker instance.</summary>
        private DnnFilePickerUploader FileControl { get; set; }

        /// <remarks>
        ///   In this case because the file control is a contained control, we do not need
        ///   to process the PostBackData (it has been handled by the File Control).  We just use
        ///   this method as the Framework calls it for us.
        /// </remarks>
        /// <inheritdoc />
        public override bool LoadPostData(string postDataKey, NameValueCollection postCollection)
        {
            bool dataChanged = false;
            string presentValue = this.StringValue;
            string postedValue = (this.FileControl?.FileID ?? this.IntegerValue).ToString(CultureInfo.InvariantCulture);
            if (!presentValue.Equals(postedValue, StringComparison.Ordinal))
            {
                this.Value = postedValue;
                dataChanged = true;
            }

            return dataChanged;
        }

        /// <inheritdoc />
        protected override void RenderEditMode(RenderTreeBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.OpenComponent<DnnFilePickerUploader>(0);
            builder.AddAttribute(1, "ID", $"{this.ID}FileControl");
            builder.AddAttribute(2, "FileFilter", this.FileFilter);
            builder.AddAttribute(3, "FilePath", this.FilePath);
            builder.AddAttribute(4, "FileID", this.IntegerValue);
            builder.AddAttribute(5, "UsePersonalFolder", true);
            builder.AddAttribute(6, "User", this.User);
            builder.AddAttribute(7, "Required", true);
            builder.AddAttribute(
                8,
                "FileChangedCallback",
                EventCallback.Factory.Create<EventArgs>(this, this.OnFileChanged));
            builder.AddComponentReferenceCapture(9, instance => this.FileControl = instance as DnnFilePickerUploader);
            builder.CloseComponent();
        }

        private void OnFileChanged(EventArgs e)
        {
            this.SetPostValue(string.Empty, (this.FileControl?.FileID ?? this.IntegerValue).ToString(CultureInfo.InvariantCulture));
            this.CommitPostData();
        }
    }
}
