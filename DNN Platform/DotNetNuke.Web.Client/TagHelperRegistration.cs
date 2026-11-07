// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Client
{
    /// <summary>
    /// Describes how to register this assembly's Tag Helpers, which replace the WebForms
    /// <c>dnn:</c> server controls of the client resource management pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Add the following to the consuming project's <c>_ViewImports.cshtml</c>:
    /// </para>
    /// <code>
    /// @addTagHelper *, DotNetNuke.Web.Client
    /// @tagHelperPrefix dnn:
    /// </code>
    /// <para>
    /// With the <c>dnn:</c> prefix in place the existing markup keeps working unchanged, because Tag
    /// Helper element and attribute matching is case insensitive and every Tag Helper below targets both
    /// its original PascalCase name and a kebab-case alias:
    /// </para>
    /// <list type="table">
    /// <listheader><term>WebForms control</term><description>Tag Helper / element names</description></listheader>
    /// <item><term><c>&lt;dnn:DnnJsInclude&gt;</c></term><description><c>DnnJsIncludeTagHelper</c> — <c>DnnJsInclude</c>, <c>dnn-js-include</c></description></item>
    /// <item><term><c>&lt;dnn:DnnCssInclude&gt;</c></term><description><c>DnnCssIncludeTagHelper</c> — <c>DnnCssInclude</c>, <c>dnn-css-include</c></description></item>
    /// <item><term><c>&lt;dnn:DnnJsExclude&gt;</c></term><description><c>DnnJsExcludeTagHelper</c> — <c>DnnJsExclude</c>, <c>dnn-js-exclude</c></description></item>
    /// <item><term><c>&lt;dnn:DnnCssExclude&gt;</c></term><description><c>DnnCssExcludeTagHelper</c> — <c>DnnCssExclude</c>, <c>dnn-css-exclude</c></description></item>
    /// <item><term><c>&lt;dnn:DnnHtmlInclude&gt;</c></term><description><c>DnnHtmlIncludeTagHelper</c> — <c>DnnHtmlInclude</c>, <c>dnn-html-include</c></description></item>
    /// <item><term><c>&lt;dnn:DnnJsIncludeFallback&gt;</c></term><description><c>DnnJsIncludeFallbackTagHelper</c> — <c>DnnJsIncludeFallback</c>, <c>dnn-js-include-fallback</c></description></item>
    /// <item><term><c>&lt;dnn:DnnResources&gt;</c></term><description><c>DnnResourcesTagHelper</c> — <c>DnnResources</c>, <c>dnn-resources</c></description></item>
    /// <item><term><c>&lt;dnn:ClientResourceLoader&gt;</c></term><description><c>ClientResourceLoaderTagHelper</c> — <c>ClientResourceLoader</c>, <c>client-resource-loader</c></description></item>
    /// </list>
    /// <para>
    /// Attribute changes the 17 downstream projects must be aware of:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <c>ClientResourceLoader</c>'s inner <c>&lt;Paths&gt;&lt;ClientResourcePath /&gt;&lt;/Paths&gt;</c>
    /// element is gone (<c>[ParseChildren]</c>/<c>[PersistenceMode]</c> have no ASP.NET Core equivalent).
    /// Bind the collection instead: <c>Paths="@paths"</c> where <c>paths</c> is a
    /// <c>ClientResourcePathCollection</c>.
    /// </description></item>
    /// <item><description>
    /// <c>HtmlAttributes</c> is populated from markup with the <c>html-attribute-</c> prefix
    /// (e.g. <c>html-attribute-data-foo="bar"</c>); <c>HtmlAttributesAsString</c> is unchanged.
    /// </description></item>
    /// <item><description>
    /// <c>DnnHtmlInclude</c> accepts the markup to parse either as child content or via <c>Text</c>.
    /// </description></item>
    /// <item><description>
    /// A <c>DnnResources</c> tag whose <c>Provider</c> is <c>DnnBodyProvider</c> must be placed as the
    /// first child of the layout's <c>&lt;form&gt;</c> element — this replaces the WebForms behavior of
    /// re-parenting the <c>BodySCRIPTS</c> placeholder to index 0 of the form
    /// (see <c>DnnResourcesTagHelper.RenderFirstInForm</c>).
    /// </description></item>
    /// <item><description>
    /// The async-postback loader script is no longer emitted inline; it is the static asset
    /// <c>js/dnn.clientresourceloader.js</c> staged into the website, referenced by both
    /// <c>ClientResourceLoader</c> and <c>DnnResources</c> (once per request).
    /// </description></item>
    /// </list>
    /// </remarks>
    public static class TagHelperRegistration
    {
        /// <summary>The <c>@addTagHelper</c> directive that registers this assembly's Tag Helpers.</summary>
        public const string AddTagHelperDirective = "@addTagHelper *, DotNetNuke.Web.Client";

        /// <summary>The <c>@tagHelperPrefix</c> directive that keeps the legacy <c>dnn:</c> markup prefix working.</summary>
        public const string TagHelperPrefixDirective = "@tagHelperPrefix dnn:";
    }
}
