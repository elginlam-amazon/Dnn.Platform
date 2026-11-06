// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// The client side seam of the migrated member directory view (View.razor).
//
// View.ascx emitted this whole block as an inline <script>. A Blazor render tree NEVER executes a
// <script> element it writes, so the block moved here and View.razor.cs imports this module and
// calls initMemberDirectory from OnAfterRenderAsync(firstRender), passing every value the inline
// script interpolated server side.
//
// The behavior is unchanged: the knockout view model of Scripts/MemberDirectory.js is constructed
// with the same two settings objects, md.init is called against the module's root element, and the
// two Enter-key handlers of the basic and advanced search bars are re-wired.

/**
 * Waits for a global to be defined, polling on a bounded retry.
 *
 * MemberDirectory.js and the jQuery/knockout libraries are registered through
 * IClientResourceController, which the server does not await, so they may not have evaluated by
 * the time OnAfterRenderAsync(firstRender) imports this module. Returning silently in that case
 * would leave an empty member list with no error anywhere, so readiness is made deterministic
 * instead of assumed.
 * @param {function(): any} resolve Reads the global being waited for.
 * @param {number} timeoutMs How long to keep polling before giving up.
 * @returns {Promise<any>} The value, or undefined if it never appeared.
 */
function waitFor(resolve, timeoutMs = 10000) {
    return new Promise(function (done) {
        const deadline = Date.now() + timeoutMs;
        (function poll() {
            const value = resolve();
            if (value) {
                done(value);
            } else if (Date.now() >= deadline) {
                done(undefined);
            } else {
                window.setTimeout(poll, 50);
            }
        })();
    });
}

/**
 * Builds the member directory view model and wires the search bars.
 * Replaces the $(document).ready(...) block of View.ascx.
 * @param {Object} options The values the inline script interpolated server side.
 * @param {string} options.clientId The id of the module's root element (was memberDirectory.ClientID).
 * @param {number} options.moduleId The module id the services framework is scoped to.
 * @param {Object} options.settings The first MemberDirectory settings object.
 * @param {Object} options.composeMessageSettings The compose-message settings object.
 */
export async function initMemberDirectory(options) {
    if (!options) {
        throw new Error('MemberDirectory: initMemberDirectory called without options.');
    }

    const jQuery = await waitFor(() => window.jQuery || window.$);
    if (!jQuery) {
        // thrown, not swallowed: View.razor.cs logs the JSException through IEventLogger.
        throw new Error('MemberDirectory: jQuery did not load, the member directory cannot start.');
    }

    const MemberDirectoryCtor = await waitFor(
        () => (typeof window.MemberDirectory === 'function' ? window.MemberDirectory : undefined));
    if (!MemberDirectoryCtor) {
        throw new Error(
            'MemberDirectory: Scripts/MemberDirectory.js did not load, the member directory cannot start.');
    }

    jQuery(function ($) {
        // $.ServicesFramework(<%=ModuleContext.ModuleId %>) was interpolated into the inline
        // script; the module id is passed instead and the framework is resolved here, where
        // jQuery and the DNN services framework plugin are available.
        const settings = Object.assign({}, options.settings, {
            servicesFramework: $.ServicesFramework(options.moduleId),
        });

        const md = new MemberDirectoryCtor($, window.ko, settings, options.composeMessageSettings);

        md.init('#' + options.clientId);

        $('#mdBasicSearchBar input[type=text]').keydown(function (e) {
            if (e.which == 13) {
                $('#mdBasicSearchBar a[class*=dnnPrimaryAction]').focus().click();
                e.preventDefault();
            }
        });

        $('#mdAdvancedSearchForm input[type=text]').keydown(function (e) {
            if (e.which == 13) {
                $('#mdAdvancedSearchForm a[class*=dnnPrimaryAction]').focus().click();
                e.preventDefault();
            }
        });
    });
}
