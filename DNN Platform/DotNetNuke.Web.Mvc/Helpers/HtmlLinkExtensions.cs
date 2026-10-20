// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Helpers
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Encodings.Web;

    using DotNetNuke.Common;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.Web.Mvc.Common;
    using DotNetNuke.Web.Mvc.Routing;

    using Microsoft.AspNetCore.Html;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.AspNetCore.Routing;

    /// <summary>Renders anchor elements that target DNN MVC module routes.</summary>
    public static class HtmlLinkExtensions
    {
        /// <summary>Renders an anchor element for the given action.</summary>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="linkText">The link text.</param>
        /// <param name="actionName">The action name.</param>
        /// <returns>The anchor HTML.</returns>
        public static IHtmlContent ActionLink(this DnnHtmlHelper htmlHelper, string linkText, string actionName)
        {
            return ActionLink(htmlHelper, linkText, actionName, null, new RouteValueDictionary(), new RouteValueDictionary());
        }

        /// <summary>Renders an anchor element for the given action and route values.</summary>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="linkText">The link text.</param>
        /// <param name="actionName">The action name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <returns>The anchor HTML.</returns>
        public static IHtmlContent ActionLink(this DnnHtmlHelper htmlHelper, string linkText, string actionName, object routeValues)
        {
            return ActionLink(htmlHelper, linkText, actionName, null, TypeHelper.ObjectToDictionary(routeValues), new RouteValueDictionary());
        }

        /// <summary>Renders an anchor element for the given action, route values, and HTML attributes.</summary>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="linkText">The link text.</param>
        /// <param name="actionName">The action name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <returns>The anchor HTML.</returns>
        public static IHtmlContent ActionLink(this DnnHtmlHelper htmlHelper, string linkText, string actionName, object routeValues, object htmlAttributes)
        {
            return ActionLink(htmlHelper, linkText, actionName, null, TypeHelper.ObjectToDictionary(routeValues), HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes));
        }

        /// <summary>Renders an anchor element for the given action and route values.</summary>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="linkText">The link text.</param>
        /// <param name="actionName">The action name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <returns>The anchor HTML.</returns>
        public static IHtmlContent ActionLink(this DnnHtmlHelper htmlHelper, string linkText, string actionName, RouteValueDictionary routeValues)
        {
            return ActionLink(htmlHelper, linkText, actionName, null, routeValues, new RouteValueDictionary());
        }

        /// <summary>Renders an anchor element for the given action, route values, and HTML attributes.</summary>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="linkText">The link text.</param>
        /// <param name="actionName">The action name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <returns>The anchor HTML.</returns>
        public static IHtmlContent ActionLink(this DnnHtmlHelper htmlHelper, string linkText, string actionName, RouteValueDictionary routeValues, IDictionary<string, object> htmlAttributes)
        {
            return ActionLink(htmlHelper, linkText, actionName, null, routeValues, htmlAttributes);
        }

        /// <summary>Renders an anchor element for the given action and controller.</summary>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="linkText">The link text.</param>
        /// <param name="actionName">The action name.</param>
        /// <param name="controllerName">The controller name.</param>
        /// <returns>The anchor HTML.</returns>
        public static IHtmlContent ActionLink(this DnnHtmlHelper htmlHelper, string linkText, string actionName, string controllerName)
        {
            return ActionLink(htmlHelper, linkText, actionName, controllerName, new RouteValueDictionary(), new RouteValueDictionary());
        }

        /// <summary>Renders an anchor element for the given action, controller, route values, and HTML attributes.</summary>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="linkText">The link text.</param>
        /// <param name="actionName">The action name.</param>
        /// <param name="controllerName">The controller name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <returns>The anchor HTML.</returns>
        public static IHtmlContent ActionLink(this DnnHtmlHelper htmlHelper, string linkText, string actionName, string controllerName, object routeValues, object htmlAttributes)
        {
            return ActionLink(htmlHelper, linkText, actionName, controllerName, TypeHelper.ObjectToDictionary(routeValues), HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes));
        }

        /// <summary>Renders an anchor element for the given action, controller, route values, and HTML attributes.</summary>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="linkText">The link text.</param>
        /// <param name="actionName">The action name.</param>
        /// <param name="controllerName">The controller name.</param>
        /// <param name="routeValues">The route values.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <returns>The anchor HTML.</returns>
        public static IHtmlContent ActionLink(this DnnHtmlHelper htmlHelper, string linkText, string actionName, string controllerName, RouteValueDictionary routeValues, IDictionary<string, object> htmlAttributes)
        {
            Requires.NotNullOrEmpty("linkText", linkText);

            return GenerateLink(linkText, actionName, controllerName, routeValues, htmlAttributes, htmlHelper.ModuleContext);
        }

        /// <summary>Renders an anchor element for the given action, controller, protocol, host, and fragment.</summary>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="linkText">The link text.</param>
        /// <param name="actionName">The action name.</param>
        /// <param name="controllerName">The controller name.</param>
        /// <param name="protocol">The protocol (unused).</param>
        /// <param name="hostName">The host name (unused).</param>
        /// <param name="fragment">The fragment (unused).</param>
        /// <param name="routeValues">The route values.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <returns>The anchor HTML.</returns>
        public static IHtmlContent ActionLink(this DnnHtmlHelper htmlHelper, string linkText, string actionName, string controllerName, string protocol, string hostName, string fragment, object routeValues, object htmlAttributes)
        {
            return ActionLink(htmlHelper, linkText, actionName, controllerName, protocol, hostName, fragment, TypeHelper.ObjectToDictionary(routeValues), HtmlHelper.AnonymousObjectToHtmlAttributes(htmlAttributes));
        }

        /// <summary>Renders an anchor element for the given action, controller, protocol, host, and fragment.</summary>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="linkText">The link text.</param>
        /// <param name="actionName">The action name.</param>
        /// <param name="controllerName">The controller name.</param>
        /// <param name="protocol">The protocol (unused).</param>
        /// <param name="hostName">The host name (unused).</param>
        /// <param name="fragment">The fragment (unused).</param>
        /// <param name="routeValues">The route values.</param>
        /// <param name="htmlAttributes">The HTML attributes.</param>
        /// <returns>The anchor HTML.</returns>
        public static IHtmlContent ActionLink(this DnnHtmlHelper htmlHelper, string linkText, string actionName, string controllerName, string protocol, string hostName, string fragment, RouteValueDictionary routeValues, IDictionary<string, object> htmlAttributes)
        {
            Requires.NotNullOrEmpty("linkText", linkText);

            return GenerateLink(linkText, actionName, controllerName, routeValues, htmlAttributes, htmlHelper.ModuleContext);
        }

        private static IHtmlContent GenerateLink(string linkText, string actionName, string controllerName, RouteValueDictionary routeValues, IDictionary<string, object> htmlAttributes, ModuleInstanceContext moduleContext)
        {
            routeValues["controller"] = controllerName;
            routeValues["action"] = actionName;
            var url = ModuleRoutingProvider.Instance().GenerateUrl(routeValues, moduleContext);

            var tagBuilder = new TagBuilder("a");
            tagBuilder.InnerHtml.SetContent(linkText ?? string.Empty);
            tagBuilder.MergeAttributes(htmlAttributes);
            tagBuilder.MergeAttribute("href", url);

            using var writer = new StringWriter();
            tagBuilder.WriteTo(writer, HtmlEncoder.Default);
            return new HtmlString(writer.ToString());
        }
    }
}
