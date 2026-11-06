// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// The client side seam of the migrated console view (ViewConsole.razor).
//
// ViewConsole.ascx emitted this block as an inline <script>:
//
//     jQuery(document).ready(function($) {
//         $("#<%: Console.ClientID %>").dnnConsole(<%: GetClientSideSettingsObject() %>);
//     });
//
// A Blazor render cannot emit script: a <script> element added by a render tree is never executed
// by the browser. The block therefore moved here, and ViewConsole.razor.cs imports this module and
// calls initConsole from OnAfterRenderAsync(firstRender) - the first point at which JS interop is
// legal - passing the element id and the very same settings object the inline JSON literal held.
//
// jquery.console.js (the .dnnConsole plugin, registered unchanged through
// IClientResourceController) is what actually binds the console's behavior; this module only
// invokes it, which is all the inline script did.

/**
 * Applies the .dnnConsole jQuery plugin to the console element.
 * @param {string} elementId The id of the console element (was Console.ClientID).
 * @param {Object} settings The plugin settings (was GetClientSideSettingsObject()).
 * @param {boolean} settings.allowIconSizeChange Whether the icon size drop down is rendered.
 * @param {boolean} settings.allowDetailChange Whether the view drop down is rendered.
 * @param {string} settings.selectedSize The selected icon size (IconFile/IconFileLarge/IconNone).
 * @param {string} settings.showDetails The selected view (Show/Hide).
 * @param {number} settings.tabModuleID The tab module id personalization requests are posted with, or -1.
 * @param {boolean} settings.showTooltip Whether tooltips are composed from the title and description.
 */
export function initConsole(elementId, settings) {
    const jQuery = window.jQuery;
    if (!jQuery || !elementId) {
        return;
    }

    // The inline script's jQuery(document).ready(...) wrapper: by the time interop runs the
    // document is already parsed, but the plugin itself may still be loading, so the ready
    // callback is kept to preserve the original ordering guarantee.
    jQuery(function ($) {
        const $console = $('#' + elementId);
        if ($console.length === 0 || typeof $console.dnnConsole !== 'function') {
            return;
        }

        $console.dnnConsole(settings);
    });
}
