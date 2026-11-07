// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.ClientResourceManagement
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Web.Client.Cdf;
    using DotNetNuke.Web.Client.ResourceManager;

    using Microsoft.AspNetCore.Razor.TagHelpers;

    /// <summary>Allows for registration of CSS and JavaScript resources.</summary>
    /// <remarks>
    /// This replaces the WebForms <c>DnnHtmlInclude</c> <c>Literal</c>, which parsed its <c>Text</c>
    /// property in <c>OnLoad</c> and then cleared it. The Tag Helper parses its child content instead and
    /// renders nothing, which is the same observable behavior.
    /// </remarks>
    [HtmlTargetElement("DnnHtmlInclude")]
    [HtmlTargetElement("dnn-html-include")]
    public class DnnHtmlIncludeTagHelper : TagHelper
    {
        /// <summary>The pattern that matches a tag.</summary>
        public const string TagPattern = @"<{0}((\s+\w+(\s*=\s*(?:"".*?""|'.*?'|[^'"">\s]+))?)+\s*|\s*)/?>";

        /// <summary>The pattern that matches an attribute.</summary>
        public const string AttributePattern = @"{0}(\s*=\s*(?:""(?<val>.*?)""|'(?<val>.*?)'|(?<val>[^'"">\s]+)))";

        private const string MatchAllAttributes = @"(\S+)=[""']?((?:.(?![""']?\s+(?:\S+)=|[>""']))+.)[""']?";

        private static readonly CompositeFormat TagPatternFormat = CompositeFormat.Parse(TagPattern);

        private static readonly Regex LinkTagRegex = new Regex(string.Format(CultureInfo.InvariantCulture, TagPatternFormat, "link"), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex ScriptTagRegex = new Regex(string.Format(CultureInfo.InvariantCulture, TagPatternFormat, "script"), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly IClientResourceController clientResourceController;

        /// <summary>Initializes a new instance of the <see cref="DnnHtmlIncludeTagHelper"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        public DnnHtmlIncludeTagHelper(IClientResourceController clientResourceController)
        {
            this.clientResourceController = clientResourceController;
        }

        /// <summary>Gets or sets the provider to force for this resource include.</summary>
        [HtmlAttributeName("ForceProvider")]
        public string ForceProvider { get; set; }

        /// <summary>Gets or sets the priority for this resource include.</summary>
        [HtmlAttributeName("Priority")]
        public int Priority { get; set; } = 100;

        /// <summary>Gets or sets the group for this resource include.</summary>
        [HtmlAttributeName("Group")]
        public int Group { get; set; } = 100;

        /// <summary>Gets or sets the markup to parse, when it is not supplied as child content.</summary>
        /// <remarks>This is the equivalent of the original control's <c>Text</c> property.</remarks>
        [HtmlAttributeName("Text")]
        public string Text { get; set; }

        /// <inheritdoc />
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var innerHtml = this.Text;
            if (string.IsNullOrEmpty(innerHtml))
            {
                var childContent = await output.GetChildContentAsync().ConfigureAwait(false);
                innerHtml = childContent.GetContent();
            }

            this.RegisterIncludes(innerHtml);

            // The original cleared its Text after registering, so nothing was rendered.
            output.TagName = null;
            output.TagMode = TagMode.StartTagAndEndTag;
            output.Content.SetHtmlContent(string.Empty);
        }

        private void RegisterIncludes(string innerHtml)
        {
            this.RegisterIncludes(this.GetIncludes(innerHtml, ClientDependencyType.Css), ClientDependencyType.Css);
            this.RegisterIncludes(this.GetIncludes(innerHtml, ClientDependencyType.Javascript), ClientDependencyType.Javascript);
        }

        private void RegisterIncludes(IEnumerable<BasicFile> files, ClientDependencyType dependencyType)
        {
            foreach (var file in files)
            {
                switch (dependencyType)
                {
                    case ClientDependencyType.Css:
                        var styleSheet = this.clientResourceController.CreateStylesheet(file.FilePath)
                                                        .SetProvider(file.ForceProvider)
                                                        .SetPriority(file.Priority);
                        foreach (var a in file.HtmlAttributes)
                        {
                            styleSheet.AddAttribute(a.Key, a.Value);
                        }

                        styleSheet.Register();
                        break;
                    case ClientDependencyType.Javascript:
                        var script = this.clientResourceController.CreateScript(file.FilePath)
                                                        .SetProvider(file.ForceProvider)
                                                        .SetPriority(file.Priority);
                        foreach (var a in file.HtmlAttributes)
                        {
                            script.AddAttribute(a.Key, a.Value);
                        }

                        script.Register();
                        break;
                }
            }
        }

        private List<BasicFile> GetIncludes(string innerHtml, ClientDependencyType dependencyType)
        {
            Regex tagRegex;
            string sourceAttribute, mime;
            if (dependencyType == ClientDependencyType.Css)
            {
                tagRegex = LinkTagRegex;
                sourceAttribute = "href";
                mime = "text/css";
            }
            else
            {
                tagRegex = ScriptTagRegex;
                sourceAttribute = "src";
                mime = null;
            }

            var files = new List<BasicFile>();
            if (string.IsNullOrEmpty(innerHtml))
            {
                return files;
            }

            foreach (Match match in tagRegex.Matches(innerHtml))
            {
                var allAttributes = Regex.Matches(match.Value, MatchAllAttributes, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                                         .Cast<Match>()
                                         .ToArray();

                var type = allAttributes.FirstOrDefault(x =>
                {
                    if (x.Groups.Count < 3)
                    {
                        return false;
                    }

                    return x.Groups[1].Value == "type";
                });

                var href = allAttributes.FirstOrDefault(x =>
                {
                    if (x.Groups.Count < 3)
                    {
                        return false;
                    }

                    return x.Groups[1].Value == sourceAttribute;
                });

                if (href == null || (type == null && mime != null) || (type != null && mime != null && type.Groups[2].Value != mime))
                {
                    continue;
                }

                var attributes = allAttributes.Where(x =>
                {
                    if (x.Groups.Count < 3)
                    {
                        return false;
                    }

                    return x.Groups[1].Value != sourceAttribute && x.Groups[1].Value != "type";
                }).ToDictionary(x => x.Groups[1].Value, x => x.Groups[2].Value);

                var file = new BasicFile(dependencyType)
                {
                    FilePath = href.Groups[2].Value,
                    Group = this.Group,
                    Priority = this.Priority,
                    ForceProvider = this.ForceProvider,
                };

                foreach (var a in attributes)
                {
                    file.HtmlAttributes.Add(a.Key, a.Value);
                }

                files.Add(file);
            }

            return files;
        }
    }
}
