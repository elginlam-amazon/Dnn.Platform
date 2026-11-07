// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// Ported verbatim from the inline <dnn:DnnScriptBlock ID="confirmScript"> block of
// admin/Menus/DNNActions/DDRActionsMenu.ascx. A Razor component may not emit a <script>
// element (Blazor does not execute scripts rendered from inside a component), so the block
// lives here and DDRActionsMenu invokes dnn.setUpDnnActionsConfirm(settings) through IJSRuntime.
//
// *** THE TWO STRINGS PER DIALOG ARE NOT INTERCHANGEABLE *** and the server passes both:
//   settings.deleteModuleKey / settings.deleteSharedModuleKey
//     the RAW, UN-LOCALIZED resource key ("DeleteModule.Confirm" / "DeleteSharedModule.Confirm").
//     The .ascx produced it with the SINGLE-argument Localization.GetSafeJSString(string), which
//     only JavaScript-encodes its argument and performs NO resource lookup. It is used solely to
//     build the href *selector*, because the anchors DNN renders contain
//     confirm('DeleteModule.Confirm') with the key, not with translated text.
//   settings.deleteModuleText / settings.deleteSharedModuleText
//     the LOCALIZED text, from GetSafeJSString(key, Localization.SharedResourceFile). It is what
//     the replacement dnnConfirm dialog shows.
// Collapsing these two into one value would either break the selector (no anchor matches, so the
// native confirm() is never replaced) or show a resource key to the user.

var dnn = dnn || {};

dnn.setUpDnnActionsConfirm = function (settings) {
    (function ($) {
        $(document).ready(function () {
            var rewire = function (key, text) {
                var confirmString = "confirm('" + key + "')";
                $("a[href*=\"" + confirmString + "\"]").attr("href", function () {
                    // remove its original confirm function
                    return $(this).attr("href").replace(/if\(.+?\)\{(.+?)\}/, "$1");
                }).dnnConfirm({
                    text: text,
                    yesText: settings.yesText,
                    noText: settings.noText,
                    title: settings.title
                });
            };

            // Regular Confirm
            rewire(settings.deleteModuleKey, settings.deleteModuleText);

            // Module Sharing Confirm
            rewire(settings.deleteSharedModuleKey, settings.deleteSharedModuleText);
        });
    }(jQuery));
};
