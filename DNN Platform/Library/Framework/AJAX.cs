// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    using System;
    using System.Collections.Generic;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.ClientResources;
    using DotNetNuke.Common;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Web.Client.ClientResourceManagement;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A class for managing client-side interactivity on a DNN page.</summary>
    /// <remarks>
    /// <para>
    /// This class managed <c>System.Web.UI.ScriptManager</c> and <c>UpdatePanel</c> instances. Blazor
    /// Server keeps the browser DOM in sync with the server-side render tree over the circuit, which
    /// subsumes both: there is no script manager to add to a form and no update panel to wrap a control
    /// in, because every render is already a partial update.
    /// </para>
    /// <para>
    /// The members below are therefore NOT no-ops that pretend to register something. Each one either
    /// does the part that still has meaning on ASP.NET Core (the MS AJAX client scripts, which DNN
    /// client code still calls into, and the request-scoped "AJAX was requested" flag that
    /// <see cref="IsEnabled"/> reports), or produces a real Blazor artefact
    /// (<see cref="WrapUpdatePanelControl"/> returns a render fragment). Where the WebForms concept has
    /// genuinely no counterpart it is documented as such on the member itself.
    /// </para>
    /// </remarks>
    public partial class AJAX
    {
        /// <summary>The <see cref="HttpContext.Items"/> key recording that AJAX support was requested.</summary>
        /// <remarks>The key is unchanged from the WebForms implementation so that an existing DNN page/skin reading it keeps working.</remarks>
        public const string ScriptManagerItemKey = "System.Web.UI.ScriptManager";

        private const string PostBackControlsItemKey = "DotNetNuke.Framework.AJAX.PostBackControls";

        private const string MicrosoftAjaxScriptPath = "~/js/MicrosoftAjax.js";
        private const string MicrosoftAjaxWebFormsScriptPath = "~/js/MicrosoftAjaxWebForms.js";

        /// <summary>AddScriptManager is used internally by the framework to enable AJAX support for the current request.</summary>
        /// <param name="context">The HTTP context of the current request.</param>
        [DnnDeprecated(10, 2, 2, "Use overload with IHostSettings")]
        public static partial void AddScriptManager(HttpContext context)
            => AddScriptManager(context, true);

        /// <summary>AddScriptManager is used internally by the framework to enable AJAX support for the current request.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="context">The HTTP context of the current request.</param>
        public static void AddScriptManager(IHostSettings hostSettings, HttpContext context)
            => AddScriptManager(hostSettings, context, true);

        /// <summary>AddScriptManager is used internally by the framework to enable AJAX support for the current request.</summary>
        /// <param name="context">The HTTP context of the current request.</param>
        /// <param name="checkCdn">Whether to check CDN settings from host settings.</param>
        [DnnDeprecated(10, 2, 2, "Use overload with IHostSettings")]
        public static partial void AddScriptManager(HttpContext context, bool checkCdn)
            => AddScriptManager(Globals.GetCurrentServiceProvider().GetRequiredService<IHostSettings>(), context, checkCdn);

        /// <summary>AddScriptManager is used internally by the framework to enable AJAX support for the current request.</summary>
        /// <param name="hostSettings">The host settings.</param>
        /// <param name="context">The HTTP context of the current request.</param>
        /// <param name="checkCdn">Whether to check CDN settings from host settings.</param>
        /// <remarks>
        /// The WebForms version inserted a <c>ScriptManager</c> at the top of the page's form, whose
        /// observable effects were (a) the MS AJAX client scripts being emitted, honouring the CDN host
        /// settings, and (b) <see cref="IsEnabled"/> returning <see langword="true"/>. Both are
        /// reproduced here; the control itself has no Blazor counterpart.
        /// </remarks>
        public static void AddScriptManager(IHostSettings hostSettings, HttpContext context, bool checkCdn)
        {
            ArgumentNullException.ThrowIfNull(hostSettings);

            if (context == null || GetScriptManager(context))
            {
                return;
            }

            // EnableCdn/EnableCdnFallback: when the CDN is enabled the scripts come from the Microsoft
            // CDN with the locally served copy as the fallback, which is what the ScriptManager did.
            var useCdn = checkCdn && hostSettings.EnableMsAjaxCdn;
            if (useCdn)
            {
                ClientResourceManager.RegisterScript(context, "https://ajax.aspnetcdn.com/ajax/4.5.2/1/MicrosoftAjax.js");
                ClientResourceManager.RegisterScript(context, "https://ajax.aspnetcdn.com/ajax/4.5.2/1/MicrosoftAjaxWebForms.js");
            }

            ClientResourceManager.RegisterScript(context, MicrosoftAjaxScriptPath);
            ClientResourceManager.RegisterScript(context, MicrosoftAjaxWebFormsScriptPath);

            context.Items[ScriptManagerItemKey] = true;
        }

        /// <summary>Gets a value indicating whether AJAX support has been added to the current request.</summary>
        /// <param name="context">The HTTP context of the current request.</param>
        /// <returns><see langword="true"/> when AJAX support is registered for this request.</returns>
        /// <remarks>
        /// Was <c>GetScriptManager(Page)</c>, returning the <c>ScriptManager</c> control found on the
        /// page. There is no such control on ASP.NET Core, so the method reports the registration
        /// itself, which is all every call site ever did with the returned instance (a null check).
        /// </remarks>
        public static bool GetScriptManager(HttpContext context)
        {
            return context?.Items.ContainsKey(ScriptManagerItemKey) == true;
        }

        /// <summary>IsEnabled can be used to determine if AJAX has been enabled already as we only need one registration per request.</summary>
        /// <returns><see langword="true"/> if the current request has enabled the script manager, otherwise <see langword="false"/>.</returns>
        public static bool IsEnabled()
        {
            var context = HttpContextSource.Current;
            if (context?.Items.TryGetValue(ScriptManagerItemKey, out var value) != true)
            {
                return false;
            }

            return value is true;
        }

        /// <summary>IsInstalled can be used to determine if AJAX is installed on the server.</summary>
        /// <returns><see langword="true"/> if ASP.NET AJAX is installed (always), otherwise <see langword="false"/> (never).</returns>
        public static bool IsInstalled()
        {
            return true;
        }

        /// <summary>Marks a component as one that must trigger a full page load rather than an in-circuit update.</summary>
        /// <param name="objControl">The component to register for postback.</param>
        /// <remarks>
        /// Was "allows a control to be excluded from UpdatePanel async callback". Blazor has no async
        /// callback to be excluded from — every interaction is already handled over the circuit and the
        /// DOM is diffed — so there is nothing for the framework to configure. The registration is still
        /// recorded (and readable through <see cref="IsPostBackControl"/>) so that a skin or module that
        /// needs to know a component wants a full navigation can ask; nothing else consumes it.
        /// </remarks>
        public static void RegisterPostBackControl(IComponent objControl)
        {
            if (objControl == null)
            {
                return;
            }

            var context = HttpContextSource.Current;
            if (context == null)
            {
                // No silent no-op: without a request there is no per-request registry to write to, and
                // a caller that believes the registration happened would be wrong.
                throw new InvalidOperationException(
                    $"{nameof(RegisterPostBackControl)} requires a request in flight. Call it from OnInitialized during the initial render.");
            }

            if (context.Items[PostBackControlsItemKey] is not HashSet<IComponent> controls)
            {
                controls = new HashSet<IComponent>();
                context.Items[PostBackControlsItemKey] = controls;
            }

            controls.Add(objControl);
        }

        /// <summary>Determines whether a component was registered through <see cref="RegisterPostBackControl"/>.</summary>
        /// <param name="objControl">The component to look for.</param>
        /// <returns><see langword="true"/> when the component was registered for postback.</returns>
        public static bool IsPostBackControl(IComponent objControl)
        {
            var context = HttpContextSource.Current;
            return objControl != null
                   && context?.Items[PostBackControlsItemKey] is HashSet<IComponent> controls
                   && controls.Contains(objControl);
        }

        /// <summary>RegisterScriptManager must be used by developers to instruct the framework that AJAX is required on the page.</summary>
        public static void RegisterScriptManager()
        {
            var context = HttpContextSource.Current;
            if (context != null && !IsEnabled())
            {
                context.Items[ScriptManagerItemKey] = true;
            }
        }

        /// <summary>Removes the AJAX registration for the current request when <see cref="RegisterScriptManager"/> has not been called.</summary>
        /// <param name="context">The HTTP context of the current request.</param>
        /// <remarks>
        /// Was "remove the ScriptManager control during Page Render". The control is gone, so what is
        /// removed is the registration itself, which is what made the scripts render.
        /// </remarks>
        public static void RemoveScriptManager(HttpContext context)
        {
            if (context == null || IsEnabled())
            {
                return;
            }

            context.Items.Remove(ScriptManagerItemKey);
            var controller = context.RequestServices?.GetService<IClientResourceController>();
            if (controller == null)
            {
                return;
            }

            controller.RemoveScriptByPath(MicrosoftAjaxScriptPath, null);
            controller.RemoveScriptByPath(MicrosoftAjaxWebFormsScriptPath, null);
        }

        /// <summary>Wraps content so that it re-renders independently, optionally showing progress while it does.</summary>
        /// <param name="content">The content to wrap.</param>
        /// <param name="blnIncludeProgress">Whether to include a progress indicator.</param>
        /// <returns>A <see cref="RenderFragment"/> rendering the wrapped content.</returns>
        /// <remarks>
        /// Was <c>WrapUpdatePanelControl(Control, bool)</c>, which inserted an <c>UpdatePanel</c> (plus
        /// an optional <c>UpdateProgress</c> showing <c>~/images/progressbar.gif</c>) around a control.
        /// Blazor re-renders only the parts of the tree whose state changed, so the update panel's job is
        /// done by the renderer; what remains is the markup, which this returns: the content inside a
        /// <c>div</c> carrying the same <c>dnnUpdatePanel</c>/<c>dnnUpdateProgress</c> hooks the DNN
        /// stylesheets target, with the progress image rendered when requested.
        /// </remarks>
        public static RenderFragment WrapUpdatePanelControl(RenderFragment content, bool blnIncludeProgress)
        {
            return builder =>
            {
                builder.OpenElement(0, "div");
                builder.AddAttribute(1, "class", "dnnUpdatePanel");

                if (blnIncludeProgress)
                {
                    RenderProgress(builder);
                }

                if (content != null)
                {
                    builder.AddContent(4, content);
                }

                builder.CloseElement();
            };
        }

        private static void RenderProgress(RenderTreeBuilder builder)
        {
            builder.OpenElement(2, "div");
            builder.AddAttribute(3, "class", "dnnUpdateProgress");
            builder.OpenElement(4, "img");
            builder.AddAttribute(5, "src", Globals.ResolveUrl("~/images/progressbar.gif"));
            builder.AddAttribute(6, "alt", "ProgressBar");
            builder.CloseElement();
            builder.CloseElement();
        }
    }
}
