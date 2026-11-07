// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// Replaces the inline <script> block that used to live in Profile.ascx. A .razor file cannot carry
// a <script> element, so the body of the original IIFE moved here unchanged and is exposed as
// dnn.setUpProfile(), which Profile.razor.cs invokes through IJSRuntime after every render.
//
// The original wired itself up with $(document).ready plus
// Sys.WebForms.PageRequestManager.getInstance().add_endRequest, so that it re-ran after every
// partial postback. Blazor has no PageRequestManager; the component calls this after every render
// instead, which is the same contract. The editor's client id used to be emitted into the script as
// <%= ProfileProperties.ClientID %> and is now passed in as the single argument.
/*globals jQuery, window */
(function ($) {
    'use strict';

    window.dnn = window.dnn || {};

    function getRandomString() {
        return (Math.random() + 1).toString(36).substring(2, 6) + (Math.random() + 1).toString(36).substring(2, 6);
    }

    window.dnn.setUpProfile = function (profilePropertiesClientId) {
        $('.dnnButtonDropdown').dnnSettingDropdown();
        $('#' + profilePropertiesClientId).parent().dnnPanels();
        $('input[data-name="Country"]').attr('autocomplete', getRandomString());
        $('input[data-name="Region"]').attr('autocomplete', getRandomString());
    };
}(jQuery));
