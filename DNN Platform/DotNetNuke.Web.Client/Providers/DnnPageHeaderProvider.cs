// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.Providers
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Web.Client.Cdf;

    using Microsoft.AspNetCore.Http;

    /// <summary>Registers resources in the page header.</summary>
    public class DnnPageHeaderProvider : DnnFileRegistrationProvider
    {
        /// <summary>The default name of the provider.</summary>
        public const string DefaultName = "DnnPageHeaderProvider";

        /// <summary>The name of the placeholder in which CSS will be rendered.</summary>
        public const string CssPlaceHolderName = "ClientDependencyHeadCss";

        /// <summary>The name of the placeholder in which JS will be rendered.</summary>
        public const string JsPlaceHolderName = "ClientDependencyHeadJs";

        /// <summary>Initializes a new instance of the <see cref="DnnPageHeaderProvider"/> class.</summary>
        /// <param name="compositeFileProcessor">The composite file processor used to bundle and minify resources.</param>
        /// <param name="hostSettings">The host settings, used to detect debug mode.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        public DnnPageHeaderProvider(
            ICompositeFileProcessor compositeFileProcessor,
            IHostSettings hostSettings,
            IHttpContextAccessor httpContextAccessor)
            : base(compositeFileProcessor, hostSettings, httpContextAccessor, DefaultName)
        {
        }

        /// <inheritdoc />
        public override void Initialize(string name, System.Collections.Specialized.NameValueCollection config)
        {
            // Assign the provider a default name if it doesn't have one
            if (string.IsNullOrEmpty(name))
            {
                name = DefaultName;
            }

            base.Initialize(name, config);
        }

        /// <summary>Renders the dependencies for the page header.</summary>
        /// <param name="jsDependencies">The JavaScript dependencies.</param>
        /// <param name="cssDependencies">The CSS dependencies.</param>
        /// <param name="htmlAttributes">The HTML attributes to apply to each rendered tag.</param>
        /// <returns>The markup to emit in the page header.</returns>
        /// <remarks>
        /// For some reason ampersands that aren't html escaped are not compliant to HTML standards when they exist in 'link' or 'script' tags in URLs,
        /// we need to replace the ampersands with &amp;amp; . This is only required for this one w3c compliancy, the URL itself is a valid URL.
        /// </remarks>
        public override string RegisterDependencies(
            IEnumerable<IResource> jsDependencies,
            IEnumerable<IResource> cssDependencies,
            IDictionary<string, string> htmlAttributes)
        {
            var jsScriptBlock = EscapeAmpersands(this.RenderJsDependencies(jsDependencies, htmlAttributes));
            var cssStyleBlock = EscapeAmpersands(this.RenderCssDependencies(cssDependencies, htmlAttributes));

            var http = this.HttpContext;
            if (AsyncPostBackDataItems.IsInAsyncPostBack(http))
            {
                // The legacy provider renamed the JS placeholder control to "$crm_ClientDependencyHeadJs"
                // and pushed both blocks through ScriptManager.RegisterDataItem.
                AsyncPostBackDataItems.RegisterDataItem(http, JsPlaceHolderName, $"{jsScriptBlock}{cssStyleBlock}");
            }

            return $"{jsScriptBlock}{cssStyleBlock}";
        }

        /// <inheritdoc />
        protected override string RenderJsDependencies(IEnumerable<IResource> jsDependencies, IDictionary<string, string> htmlAttributes)
        {
            if (jsDependencies is null || !jsDependencies.Any())
            {
                return string.Empty;
            }

            var sb = new StringBuilder();

#pragma warning disable CS0618 // Composite files are deprecated but the behavior is preserved
            if (this.IsDebuggingEnabled || !this.EnableCompositeFiles)
#pragma warning restore CS0618
            {
                foreach (var dependency in jsDependencies)
                {
                    sb.Append(this.RenderSingleJsFile(GetFilePath(dependency), htmlAttributes));
                }
            }
            else
            {
                var comp = this.CompositeFileProcessor.ProcessCompositeList(jsDependencies, ClientDependencyType.Javascript);
                foreach (var s in comp)
                {
                    sb.Append(this.RenderSingleJsFile(s, htmlAttributes));
                }
            }

            return sb.ToString();
        }

        /// <inheritdoc />
        protected override string RenderSingleJsFile(string js, IDictionary<string, string> htmlAttributes)
        {
            return string.Format(CultureInfo.InvariantCulture, HtmlEmbedFormats.ScriptEmbedWithSource, js, htmlAttributes.ToHtmlAttributes());
        }

        /// <inheritdoc />
        protected override string RenderCssDependencies(IEnumerable<IResource> cssDependencies, IDictionary<string, string> htmlAttributes)
        {
            if (cssDependencies is null || !cssDependencies.Any())
            {
                return string.Empty;
            }

            var sb = new StringBuilder();

#pragma warning disable CS0618 // Composite files are deprecated but the behavior is preserved
            if (this.IsDebuggingEnabled || !this.EnableCompositeFiles)
#pragma warning restore CS0618
            {
                foreach (var dependency in cssDependencies)
                {
                    sb.Append(this.RenderSingleCssFile(GetFilePath(dependency), htmlAttributes));
                }
            }
            else
            {
                var comp = this.CompositeFileProcessor.ProcessCompositeList(cssDependencies, ClientDependencyType.Css);
                foreach (var s in comp)
                {
                    sb.Append(this.RenderSingleCssFile(s, htmlAttributes));
                }
            }

            return sb.ToString();
        }

        /// <inheritdoc />
        protected override string RenderSingleCssFile(string css, IDictionary<string, string> htmlAttributes)
        {
            return string.Format(CultureInfo.InvariantCulture, HtmlEmbedFormats.CssEmbedWithSource, css, htmlAttributes.ToHtmlAttributes());
        }
    }
}
