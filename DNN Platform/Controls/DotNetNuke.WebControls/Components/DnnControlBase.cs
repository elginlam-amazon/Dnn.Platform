// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.UI.WebControls.Components
{
    using System;
    using System.Collections;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    /// <summary>
    /// Base class of the Blazor components of this library, providing the client side
    /// initialization the WebForms controls performed through
    /// <see cref="BaseWebControl.RegisterInitialize"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original emitted a start-up script of the shape
    /// <c>dnn.setVar('&lt;clientid&gt;_p', '&lt;json&gt;'); dnn.controls.&lt;func&gt;($get('&lt;clientid&gt;'));</c>.
    /// Over a Blazor circuit there is no page to register a script block on, so the very same pair of
    /// calls is issued through <see cref="IJSRuntime"/> from <see cref="OnAfterRenderAsync(bool)"/>,
    /// which is the only place JS interop is allowed to run. The marshalled property JSON no longer
    /// needs <c>ClientAPI.EscapeForJavascript</c>, because interop arguments are serialized rather
    /// than embedded into script source, and <c>$get(clientId)</c> is replaced by the
    /// <see cref="ElementReference"/> the derived component captures with <c>@ref</c>.
    /// </para>
    /// <para>
    /// The <c>__ENCAddToList</c> weak-reference list of the VB original was an edit-and-continue
    /// debugging artifact emitted by the compiler with no observable behavior and is not carried over.
    /// </para>
    /// <para>
    /// Render mode: this type carries no <c>@rendermode</c> of its own. It is a plain
    /// <see cref="ComponentBase"/> derivative; each concrete component declares its own render mode
    /// (the interactive ones need <c>InteractiveServer</c> both for their event handlers and for the
    /// JS interop this class performs, which cannot run during static server rendering).
    /// </para>
    /// </remarks>
    public abstract class DnnControlBase : ComponentBase
    {
        /// <summary>Path of the JS module of this library, served as an RCL static web asset.</summary>
        private const string InteropModulePath = "./_content/DotNetNuke.WebControls/js/dnn.controls.interop.js";

        private readonly string generatedId = "dnn_" + Guid.NewGuid().ToString("N");

        private IJSObjectReference interopModule;

        /// <summary>Gets or sets the identifier rendered onto the root element of the component.</summary>
        /// <remarks>Replaces the <c>ID</c> property of the WebForms controls.</remarks>
        [Parameter]
        public string Id { get; set; }

        /// <summary>Gets or sets a value indicating whether the client script drives the component.</summary>
        /// <remarks>
        /// The WebForms controls always handed their behavior over to <c>dnn.controls.*</c>. The
        /// Blazor components render and handle their own interactions, so the client script is only
        /// initialized when a host explicitly opts in (for instance to keep an existing
        /// <c>dnn.controls</c> behavior attached to the rendered markup).
        /// </remarks>
        [Parameter]
        public bool UseClientScript { get; set; }

        /// <summary>Gets the client side identifier of the component.</summary>
        public string ClientId => string.IsNullOrEmpty(this.Id) ? this.generatedId : this.Id;

        /// <summary>Gets or sets the JS runtime the client side initialization goes through.</summary>
        [Inject]
        protected IJSRuntime JSRuntime { get; set; }

        /// <summary>Gets or sets the root element of the component, used in place of <c>$get(clientId)</c>.</summary>
        protected ElementReference RootElement { get; set; }

        /// <summary>
        /// Gets the name of the <c>dnn.controls</c> function that initializes this component.
        /// </summary>
        /// <remarks>Returns <see langword="null"/> when the component needs no client behavior.</remarks>
        protected virtual string ClientInitFunction => null;

        /// <summary>Gets the object whose <see cref="ClientPropertyAttribute"/> members are marshalled.</summary>
        /// <remarks>Returns <see langword="null"/> when there is nothing to marshal.</remarks>
        protected virtual object ClientPropertySource => null;

        /// <summary>Gets a value indicating whether the client side initialization has to run.</summary>
        protected virtual bool RequiresClientInitialization =>
            this.UseClientScript && !string.IsNullOrEmpty(this.ClientInitFunction);

        /// <summary>
        /// Gets a value indicating whether the component performs the client side initializations
        /// the legacy controls queued.
        /// </summary>
        /// <remarks>
        /// The WebForms controls registered their initialization as a page start-up script through
        /// <see cref="BaseWebControl.RegisterInitialize"/>; those requests are now queued in
        /// <see cref="ClientInitializationQueue"/> and performed here, which is the only place JS
        /// interop may run.
        /// </remarks>
        protected virtual bool FlushesPendingClientInitializations => true;

        /// <summary>
        /// Issues the <c>dnn.setVar</c> / <c>dnn.controls.&lt;func&gt;</c> pair the original
        /// <see cref="BaseWebControl.RegisterInitialize"/> registered as a start-up script.
        /// </summary>
        /// <param name="func">The name of the <c>dnn.controls</c> initialization function.</param>
        /// <param name="propJson">The marshalled client properties of the component.</param>
        /// <returns>A task that completes once both calls have been made.</returns>
        protected async Task InitializeClientAsync(string func, string propJson)
        {
            if (string.IsNullOrEmpty(func) || this.JSRuntime is null)
            {
                return;
            }

            await this.JSRuntime.InvokeVoidAsync("dnn.setVar", this.ClientId + "_p", propJson ?? "{}");
            await this.JSRuntime.InvokeVoidAsync("dnn.controls." + func, this.RootElement);
        }

        /// <summary>Collects the extra properties handed to the client script.</summary>
        /// <returns>The properties, keyed by their client side name.</returns>
        protected virtual Hashtable GetClientProperties()
        {
            return new Hashtable();
        }

        /// <summary>
        /// Performs every client side initialization the legacy controls queued.
        /// </summary>
        /// <returns>A task that completes once the queued initializations have run.</returns>
        protected async Task FlushPendingClientInitializationsAsync()
        {
            if (this.JSRuntime is null)
            {
                return;
            }

            foreach (var initialization in ClientInitializationQueue.Drain())
            {
                if (!string.IsNullOrEmpty(initialization.Function))
                {
                    await this.JSRuntime.InvokeVoidAsync(
                        "dnn.setVar", initialization.ClientId + "_p", initialization.PropertyJson ?? "{}");
                    await this.JSRuntime.InvokeVoidAsync(
                        "dnn.controls." + initialization.Function, initialization.ClientId);
                }
                else if (!string.IsNullOrEmpty(initialization.Script))
                {
                    // The queued statement is the very source the original emitted inside its
                    // start-up script block, so running it is required for parity. It goes through
                    // the named `runControlScript` helper of the RCL JS module rather than `eval`,
                    // which keeps the component usable under a CSP that forbids `eval`.
                    await this.RunControlScriptAsync(initialization.Script);
                }
            }
        }

        /// <summary>
        /// Runs a client script configured on the control through the named helper of the
        /// library's JS module.
        /// </summary>
        /// <param name="script">The script source configured on the control.</param>
        /// <returns>A task that completes once the script has run.</returns>
        protected async Task RunControlScriptAsync(string script)
        {
            if (string.IsNullOrEmpty(script))
            {
                return;
            }

            var module = await this.GetInteropModuleAsync();
            if (module is not null)
            {
                await module.InvokeVoidAsync("runControlScript", script);
            }
        }

        /// <summary>
        /// Opens a URL in a named browsing context through the named helper of the library's JS
        /// module, so neither the URL nor the target is ever concatenated into script source.
        /// </summary>
        /// <param name="url">The URL to open.</param>
        /// <param name="target">The name of the target browsing context.</param>
        /// <returns>A task that completes once the window has been requested.</returns>
        protected async Task OpenWindowAsync(string url, string target)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            var module = await this.GetInteropModuleAsync();
            if (module is not null)
            {
                await module.InvokeVoidAsync("openWindow", url, target);
            }
        }

        /// <inheritdoc/>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (firstRender && this.FlushesPendingClientInitializations)
            {
                await this.FlushPendingClientInitializationsAsync();
            }

            if (!firstRender || !this.RequiresClientInitialization)
            {
                return;
            }

            var properties = this.GetClientProperties();
            var source = this.ClientPropertySource;
            var json = source is null
                ? ScriptGenerator.GetMarshalledPropertyJSON(this, properties)
                : ScriptGenerator.GetMarshalledPropertyJSON(source, properties);

            await this.InitializeClientAsync(this.ClientInitFunction, json);
        }

        /// <summary>Imports the JS module of this library on first use and caches it.</summary>
        /// <returns>The module reference, or <see langword="null"/> when there is no JS runtime.</returns>
        private async Task<IJSObjectReference> GetInteropModuleAsync()
        {
            if (this.JSRuntime is null)
            {
                return null;
            }

            return this.interopModule ??= await this.JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", InteropModulePath);
        }
    }
}
