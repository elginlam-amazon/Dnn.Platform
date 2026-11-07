// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// The initialization glue the search skin object (~/admin/Skins/search.ascx) used to declare in an
// inline <script> block. A Razor component may not declare a script element, so the body was moved
// here verbatim and is invoked by Search.razor through IJSRuntime once the component has rendered.
//
// settings           - the dnn.searchSkinObject settings object (delayTriggerAutoSearch,
//                      minCharRequiredTriggerAutoSearch, searchType, enableWildSearch, cultureCode,
//                      portalId), built server side exactly as the <%= %> expressions did.
// siteBtnId/webBtnId - the ids of the "site"/"web" radio buttons of the classic rendering.
// useDropDownList    - whether the drop-down rendering is used, which selected the second branch of
//                      the original script.
(function () {
    window.dnnSearchSkinObjectInit = function (settings, siteBtnId, webBtnId, useDropDownList) {
        $(function () {
            if (typeof dnn == "undefined" || typeof dnn.searchSkinObject == "undefined") {
                return;
            }

            var searchSkinObject = new dnn.searchSkinObject(settings);
            searchSkinObject.init();

            if (!useDropDownList) {
                // attach classic search
                var siteBtn = $('#' + siteBtnId);
                var webBtn = $('#' + webBtnId);
                var clickHandler = function () {
                    if (siteBtn.is(':checked')) searchSkinObject.settings.searchType = 'S';
                    else searchSkinObject.settings.searchType = 'W';
                };
                siteBtn.on('change', clickHandler);
                webBtn.on('change', clickHandler);
            } else {
                // attach dropdown search
                if (typeof dnn.initDropdownSearch != 'undefined') {
                    dnn.initDropdownSearch(searchSkinObject);
                }
            }
        });
    };
})();

// Reports whether the "web" radio button of the classic rendering is checked. This replaces the
// WebForms WebRadioButton.Checked the Click handler read off the postback: the radios are plain
// inputs now, so their state is read from the DOM by Search.razor.cs.
window.dnnSearchSkinObjectIsWebChecked = function (webBtnId) {
    var el = document.getElementById(webBtnId);
    return !!el && el.checked;
};
