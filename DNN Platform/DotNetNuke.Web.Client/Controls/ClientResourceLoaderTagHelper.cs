// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client.ClientResourceManagement
{
    using DotNetNuke.Abstractions.ClientResources;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Razor.TagHelpers;

    /// <summary>The central Tag Helper with which all client resource paths are registered.</summary>
    /// <remarks>
    /// This replaces the WebForms <c>ClientResourceLoader</c> control. The inner-property
    /// <c>&lt;Paths&gt;</c> collection (which relied on <c>[ParseChildren]</c>/<c>[PersistenceMode]</c>,
    /// features with no ASP.NET Core equivalent) is now bound through the <c>Paths</c> attribute, e.g.
    /// <c>&lt;dnn:ClientResourceLoader Paths="@Model.ResourcePaths" /&gt;</c>.
    /// </remarks>
    [HtmlTargetElement("ClientResourceLoader")]
    [HtmlTargetElement("client-resource-loader")]
    public class ClientResourceLoaderTagHelper : TagHelper
    {
        private readonly IClientResourceController clientResourceController;
        private readonly IHttpContextAccessor httpContextAccessor;

        /// <summary>Initializes a new instance of the <see cref="ClientResourceLoaderTagHelper"/> class.</summary>
        /// <param name="clientResourceController">The client resources controller to use for resource registration.</param>
        /// <param name="httpContextAccessor">The HTTP context accessor.</param>
        public ClientResourceLoaderTagHelper(IClientResourceController clientResourceController, IHttpContextAccessor httpContextAccessor)
        {
            this.clientResourceController = clientResourceController;
            this.httpContextAccessor = httpContextAccessor;
            this.Paths = [];
        }

        /// <summary>Gets or sets the paths collection.</summary>
        [HtmlAttributeName("Paths")]
        public ClientResourcePathCollection Paths { get; set; }

        /// <summary>Gets or sets the URL of the async-postback loader script asset.</summary>
        [HtmlAttributeName("ScriptPath")]
        public string ScriptPath { get; set; } = ClientResourceLoaderScript.DefaultScriptPath;

        /// <summary>Gets or sets the application path used to resolve a <c>~/</c> rooted <see cref="ScriptPath"/>.</summary>
        [HtmlAttributeName("ApplicationPath")]
        public string ApplicationPath { get; set; }

        /// <summary>Gets or sets the view context.</summary>
        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        private HttpContext HttpContext => this.ViewContext?.HttpContext ?? this.httpContextAccessor?.HttpContext;

        /// <inheritdoc />
        public override void Init(TagHelperContext context)
        {
            // Ported from OnInit: the async post-back handler used to require a ScriptManager on the page,
            // because the ScriptManager was the channel that carried the $crm_ data items to the client.
            // On ASP.NET Core that channel is the loader script asset referenced by ScriptPath, so the
            // equivalent precondition is that a loader asset is configured whenever the handler is enabled.
            // Like the original, a misconfigured page fails loudly rather than silently doing nothing.
            if (ClientResourceLoaderScript.IsAsyncPostBackHandlerEnabled(this.HttpContext) && string.IsNullOrWhiteSpace(this.ScriptPath))
            {
                throw new ScriptManagerRequiredException("The ClientResourceLoader requires a client resource loader script (ScriptPath) when AsyncPostBackHandlerEnabled is true.");
            }

            base.Init(context);
        }

        /// <inheritdoc />
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            // Ported from OnLoad.
            foreach (var path in this.Paths ?? [])
            {
                this.clientResourceController.RegisterPathNameAlias(path.Name, path.Path);
            }

            // Ported from OnPreRender: path names are normalized to lower case...
            foreach (var path in this.Paths ?? [])
            {
                path.Name = path.Name.ToLowerInvariant();
            }

            output.TagName = null;
            output.TagMode = TagMode.StartTagAndEndTag;

            // ...and the async-postback handler script is emitted (now a reference to the shared asset).
            if (ClientResourceLoaderScript.IsAsyncPostBackHandlerEnabled(this.HttpContext))
            {
                output.Content.SetHtmlContent(
                    ClientResourceLoaderScript.Render(this.HttpContext, this.ScriptPath, this.ApplicationPath));
            }
        }
    }
}
