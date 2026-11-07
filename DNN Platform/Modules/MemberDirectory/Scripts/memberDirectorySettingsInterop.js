// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// The client side seam of the migrated member directory settings control (Settings.razor).
//
// Settings.ascx emitted this as an inline <script> block. A Blazor render cannot execute a
// <script> element it writes, so the block moved here and Settings.razor.cs imports this module
// and calls setUpSettings() after the first render, then toggleFilter() after every later render
// (the "filter by" selection is server state now, so the re-apply replaces the
// Sys.WebForms.PageRequestManager.add_endRequest re-initialization the post back needed).
//
// The <%= filterBySelector.ClientID %> interpolation is gone: the radio group's container keeps
// the control's ID as its fixed element id, "filterBySelector".

const filterBySelectorId = 'filterBySelector';

/**
 * Shows the filter list that matches the current "filter by" selection and hides the others.
 * This is the original toggleFilter(), with the same four cases and default.
 */
export function toggleFilter() {
    const $ = window.jQuery;
    if (!$) {
        return;
    }

    const filterValue = $('#' + filterBySelectorId + ' input:checked').val();
    switch (filterValue) {
        case 'Group':
            $('select[name$=groupList]').show();
            $('select[name$=relationShipList]').hide();
            $('select[name$=propertyList]').hide();
            $('input[name$=propertyValue]').hide();
            break;
        case 'Relationship':
            $('select[name$=groupList]').hide();
            $('select[name$=relationShipList]').show();
            $('select[name$=propertyList]').hide();
            $('input[name$=propertyValue]').hide();
            break;
        case 'ProfileProperty':
            $('select[name$=groupList]').hide();
            $('select[name$=relationShipList]').hide();
            $('select[name$=propertyList]').show();
            $('input[name$=propertyValue]').show();
            break;
        default:
            $('select[name$=groupList]').hide();
            $('select[name$=relationShipList]').hide();
            $('select[name$=propertyList]').hide();
            $('input[name$=propertyValue]').hide();
            break;
    }
}

/**
 * Applies the filter visibility and keeps it in sync with the radio group.
 * Replaces the original setUpSettings() plus its jQuery(document).ready(...) call.
 */
export function setUpSettings() {
    const $ = window.jQuery;
    if (!$) {
        return;
    }

    toggleFilter();
    $('#' + filterBySelectorId).off('change.memberDirectorySettings').on('change.memberDirectorySettings', function () {
        toggleFilter();
    });
}
