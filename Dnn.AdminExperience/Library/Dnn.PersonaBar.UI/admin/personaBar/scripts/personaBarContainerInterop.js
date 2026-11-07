// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// The client side seam of the migrated Persona Bar container (PersonaBarContainer.razor).
//
// PersonaBarContainer.ascx emitted this block as an inline <script> inside the container panel:
//
//     (function($) {
//         var parentBody = window.parent.document.body;
//         var parentBody$ = $(parentBody);
//         parentBody$.addClass('personabar-visible');
//
//         $(document.body).ready(function() {
//             var w = window, d = document, e = d.documentElement,
//                 g = d.getElementsByTagName('body')[0],
//                 x = w.innerWidth || e.clientWidth || g.clientWidth,
//                 desktopIframe = document.getElementById('personaBar-iframe');
//
//             w["personaBarSettings"] = <%=PersonaBarSettings%>;
//
//             var src = '<%= AppPath %>/DesktopModules/admin/Dnn.PersonaBar/index.html';
//             src += '?cdv=' + '<%= BuildNumber %>';
//             desktopIframe.src = src;
//
//             if (/iPad/i.test(navigator.userAgent)) {
//                 $(desktopIframe).addClass('ipad').width(x).wrap('<div class="pb-scroll-wrapper"></div>');
//             }
//         });
//     })(jQuery);
//
// A Blazor render cannot emit script: a <script> element added by a render tree is never executed
// by the browser. The block therefore moved here, and PersonaBarContainer.razor.cs imports this
// module and calls initPersonaBarContainer from OnAfterRenderAsync(firstRender) - the first point
// at which JS interop is legal - passing the same three server side values the markup
// interpolated.

/**
 * Wires up the Persona Bar container: marks the parent document, publishes the settings and
 * points the Persona Bar iframe at the application's index.html.
 * @param {string} personaBarSettings The JSON of IPersonaBarContainer.GetConfiguration()
 *     (was the <%=PersonaBarSettings%> literal, which was emitted unquoted as a JS object).
 * @param {string} appPath The virtual application root path (was <%= AppPath %>).
 * @param {string} buildNumber The client resource version used as the cache buster (was <%= BuildNumber %>).
 */
export function initPersonaBarContainer(personaBarSettings, appPath, buildNumber) {
    const $ = window.jQuery;

    // Add a class to parent body immediately to indicate PB will be visible
    // This is useful to make room for PB placeholder. The .ascx did this at script parse time;
    // the earliest equivalent for a component is the module import, so it is done here first,
    // before anything that could fail, and with plain DOM so it does not depend on jQuery.
    const parentBody = window.parent.document.body;
    if (parentBody) {
        parentBody.classList.add('personabar-visible');
    }

    const wireUp = function () {
        const w = window,
            d = document,
            e = d.documentElement,
            g = d.getElementsByTagName('body')[0],
            x = w.innerWidth || e.clientWidth || (g && g.clientWidth),
            desktopIframe = d.getElementById('personaBar-iframe');

        // The markup emitted the configuration as a JS object literal; here it arrives as the
        // JSON string JsonConvert.SerializeObject produced, so it is parsed back into the same
        // object before being published under the same global name.
        w['personaBarSettings'] = typeof personaBarSettings === 'string'
            ? JSON.parse(personaBarSettings)
            : personaBarSettings;

        if (!desktopIframe) {
            return;
        }

        let src = appPath + '/DesktopModules/admin/Dnn.PersonaBar/index.html';
        src += '?cdv=' + buildNumber;

        desktopIframe.src = src;

        if (/iPad/i.test(navigator.userAgent)) {
            // Only this branch needs jQuery (.wrap has no terse plain DOM equivalent); the rest of
            // the initialization above must never be skipped just because jQuery has not loaded,
            // or the Persona Bar silently never appears.
            if ($) {
                $(desktopIframe)
                    .addClass('ipad')
                    .width(x)
                    .wrap('<div class="pb-scroll-wrapper"></div>');
            } else {
                desktopIframe.classList.add('ipad');
                desktopIframe.style.width = x + 'px';
                const wrapper = document.createElement('div');
                wrapper.className = 'pb-scroll-wrapper';
                desktopIframe.parentNode.insertBefore(wrapper, desktopIframe);
                wrapper.appendChild(desktopIframe);
            }
        }
    };

    // $(document.body).ready(...) in the original; by the time a component's OnAfterRenderAsync
    // runs the document is already interactive, but the readyState check keeps the original
    // deferral for the case where it is not.
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', wireUp, { once: true });
    } else {
        wireUp();
    }
}
