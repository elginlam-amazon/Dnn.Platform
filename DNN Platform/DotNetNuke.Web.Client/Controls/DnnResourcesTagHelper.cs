// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.ClientResourceManagement
{
    using System.Text;

    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Web.Client.Providers;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Razor.TagHelpers;

    /// <summary>Renders client resources.</summary>
    /// <remarks>
    /// This replaces the WebForms <c>DnnResources</c> <c>Literal</c> control. Any child content of the
    /// tag is rendered first (the <c>Literal.Text</c> equivalent), followed by the rendered dependencies.
    /// </remarks>
    [HtmlTargetElement("DnnResources")]
    [HtmlTargetElement("dnn-resources")]
    public class DnnResourcesTagHelper : TagHelper
    {
        private readonly IClientResourceController clientResourceController;
        private readonly IHttpContextAccessor httpContextAccessor;

        /// <summary>Initializes a new instance of the <see cref="DnnResourcesTagHelper"/> class.</summary>
        /// <param name="clientResourceController">The client resource controller.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        public DnnResourcesTagHelper(IClientResourceController clientResourceController, IHttpContextAccessor httpContextAccessor)
        {
            this.clientResourceController = clientResourceController;
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <summary>Gets or sets the application path.</summary>
        [HtmlAttributeName("ApplicationPath")]
        public string ApplicationPath { get; set; }

        /// <summary>Gets or sets the provider.</summary>
        [HtmlAttributeName("Provider")]
        public string Provider { get; set; }

        /// <summary>Gets or sets the URL of the async-postback loader script asset.</summary>
        [HtmlAttributeName("ScriptPath")]
        public string ScriptPath { get; set; } = ClientResourceLoaderScript.DefaultScriptPath;

        /// <summary>Gets or sets the view context.</summary>
        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance must be the first content inside the page's form.
        /// </summary>
        /// <remarks>
        /// <see cref="DnnBodyProvider"/> achieved this in WebForms by re-parenting its <c>BodySCRIPTS</c>
        /// placeholder to index 0 of the form's control collection. There is no control tree to reorder in
        /// ASP.NET Core, so the equivalent requirement is positional: when this is <see langword="true"/>
        /// the tag must be placed as the first child of the layout's <c>&lt;form&gt;</c> element.
        /// </remarks>
        [HtmlAttributeNotBound]
        public bool RenderFirstInForm =>
            this.Provider == ClientResourceProviders.DnnBodyProvider && DnnBodyProvider.RenderFirstInForm;

        private HttpContext HttpContext => this.ViewContext?.HttpContext ?? this.httpContextAccessor?.HttpContext;

        /// <inheritdoc />
        public override void Init(TagHelperContext context)
        {
            // Ported from OnInit, see ClientResourceLoaderTagHelper.Init: the WebForms ScriptManager was the
            // channel that carried the $crm_ data items; on ASP.NET Core that channel is the loader script
            // asset referenced by ScriptPath, so a misconfigured page fails loudly as it did originally.
            if (ClientResourceLoaderScript.IsAsyncPostBackHandlerEnabled(this.HttpContext) && string.IsNullOrWhiteSpace(this.ScriptPath))
            {
                throw new ScriptManagerRequiredException("DnnResources requires a client resource loader script (ScriptPath) when AsyncPostBackHandlerEnabled is true.");
            }

            base.Init(context);
        }

        /// <inheritdoc />
        public override async System.Threading.Tasks.Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = null;
            output.TagMode = TagMode.StartTagAndEndTag;

            var markup = new StringBuilder();

            // base.Render(writer) — the Literal's own content.
            var childContent = await output.GetChildContentAsync().ConfigureAwait(false);
            markup.Append(childContent.GetContent());

            markup.Append(this.clientResourceController.RenderDependencies(ResourceType.All, this.Provider, this.ApplicationPath));

            var http = this.HttpContext;
            if (this.Provider == ClientResourceProviders.DnnBodyProvider && AsyncPostBackDataItems.IsInAsyncPostBack(http))
            {
                // Get scripts to be loaded during async postback. The WebForms control renamed the
                // "BodySCRIPTS" holder control to "$crm_BodySCRIPTS" and called ScriptManager.RegisterDataItem;
                // AsyncPostBackDataItems applies the same "$crm_" + placeholder-name key protocol.
                var js = this.clientResourceController.RenderDependencies(ResourceType.Script, ClientResourceProviders.DnnBodyProvider, this.ApplicationPath);
                var css = this.clientResourceController.RenderDependencies(ResourceType.Stylesheet, ClientResourceProviders.DefaultCssProvider, this.ApplicationPath);
                AsyncPostBackDataItems.RegisterDataItem(http, DnnBodyProvider.DnnBodyPlaceHolderName, $"{js}{css}");
            }

            // Ported from OnPreRender — shares the single loader asset with ClientResourceLoaderTagHelper.
            if (ClientResourceLoaderScript.IsAsyncPostBackHandlerEnabled(http))
            {
                markup.Append(ClientResourceLoaderScript.Render(http, this.ScriptPath, this.ApplicationPath));
            }

            output.Content.SetHtmlContent(markup.ToString());
        }
    }
}
