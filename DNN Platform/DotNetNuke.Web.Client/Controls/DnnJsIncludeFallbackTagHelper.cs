// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.ClientResourceManagement
{
    using System.Globalization;
    using System.Text;

    using Microsoft.AspNetCore.Razor.TagHelpers;

    /// <summary>Emit a fallback block for a script in the same part of the page.</summary>
    [HtmlTargetElement("DnnJsIncludeFallback")]
    [HtmlTargetElement("dnn-js-include-fallback")]
    public class DnnJsIncludeFallbackTagHelper : TagHelper
    {
        /// <summary>Initializes a new instance of the <see cref="DnnJsIncludeFallbackTagHelper"/> class.</summary>
        public DnnJsIncludeFallbackTagHelper()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="DnnJsIncludeFallbackTagHelper"/> class.</summary>
        /// <param name="objectName">The JS object name to verify is defined in global scope.</param>
        /// <param name="fileName">The path to load if the object is not defined.</param>
        public DnnJsIncludeFallbackTagHelper(string objectName, string fileName)
        {
            this.ObjectName = objectName;
            this.FileName = fileName;
        }

        /// <summary>Gets or sets the object name.</summary>
        [HtmlAttributeName("ObjectName")]
        public string ObjectName { get; set; }

        /// <summary>Gets or sets the file name.</summary>
        [HtmlAttributeName("FileName")]
        public string FileName { get; set; }

        /// <inheritdoc />
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            // Ported verbatim from RenderControl(HtmlTextWriter).
            output.TagName = "script";
            output.TagMode = TagMode.StartTagAndEndTag;
            output.Attributes.SetAttribute("type", "text/javascript");

            var script = new StringBuilder();
            if (this.ObjectName.Contains('.'))
            {
                // generate function check
                script.Append(CultureInfo.InvariantCulture, $"if (typeof {this.ObjectName} != 'function') {{");
            }
            else
            {
                // generate object check
                script.Append(CultureInfo.InvariantCulture, $"if (typeof {this.ObjectName} == 'undefined') {{");
            }

            script.Append(CultureInfo.InvariantCulture, $"document.write('<script src=\"{this.FileName}\" type=\"text/javascript\"></' + 'script>');");
            script.Append('}');

            output.Content.SetHtmlContent(script.ToString());
        }
    }
}
