// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// The client side seam of the migrated Options page (Options.razor) and of the
// CKEditorOptions component.
//
// Options.aspx carried three inline client side pieces that a Blazor render cannot emit as a
// <script> element (a script element added by a render is never executed):
//
//   1. the six localized strings js/Options.js reads off the window object
//      (saveGroupName, deleteGroup, editGroupName, newGroupName, deleteToolbar, newRowName),
//   2. the "javascript:self.close();" start-up script ClosePage() registered,
//   3. CKEditorOptions.ascx's jQuery bootstrap ($('#...').dnnTabs().dnnPanels()) plus the
//      win.pageLoad() entry point js/Options.js defines, which the WebForms page lifecycle
//      (and the UpdatePanel end-request handler) used to invoke.
//
// They are the named helpers below, called from the components through IJSRuntime.

/**
 * Publishes the localized strings js/Options.js reads off the window object.
 * Replaces the inline <script> block of Options.aspx.
 * @param {Object} strings The localized strings, keyed exactly as the original variables.
 */
export function setLocalizedStrings(strings) {
    if (!strings) {
        return;
    }

    window.saveGroupName = strings.saveGroupName;
    window.deleteGroup = strings.deleteGroup;
    window.editGroupName = strings.editGroupName;
    window.newGroupName = strings.newGroupName;
    window.deleteToolbar = strings.deleteToolbar;
    window.newRowName = strings.newRowName;
}

/**
 * Runs the options page client side set-up: the dnnTabs()/dnnPanels() initialization that was the
 * inline script of CKEditorOptions.ascx and the win.pageLoad() entry point of js/Options.js.
 * Called after every (re)render, which is what the ASP.NET AJAX add_endRequest handler did.
 * @param {string} panelId The element id of the settings panel (pnlEditor).
 */
export function initOptionsPage(panelId) {
    const jq = window.jQuery;
    if (!jq) {
        return;
    }

    const panel = jq('#' + panelId);
    if (panel.length && typeof panel.dnnTabs === 'function') {
        const tabs = panel.dnnTabs();
        if (typeof tabs.dnnPanels === 'function') {
            tabs.dnnPanels();
        }
    }

    if (typeof window.pageLoad === 'function') {
        window.pageLoad();
    }
}

/**
 * Shows the notification bar. Replaces the ScriptManager.RegisterStartupScript call of
 * CKEditorOptions.ShowNotification, which emitted ShowNotificationBar(...) into the page.
 * @param {string} message The message to show.
 * @param {string} type The notification type ("success" or "error").
 * @param {string} imagePath The path the notification images are served from.
 */
export function showNotificationBar(message, type, imagePath) {
    if (typeof window.ShowNotificationBar === 'function') {
        window.ShowNotificationBar(message, type, imagePath);
    }
}

/**
 * Reads the current value of an element the client script maintains directly.
 *
 * js/Options.js writes the serialized toolbar into the ToolbarSet hidden input with
 * jQuery.val(), which raises no change event, so the component's bound field is not updated by
 * the browser. The command handlers read the live value back through this helper, which is what
 * the WebForms postback did when it posted the hidden field.
 * @param {string} id The element id.
 * @returns {string} The value, or null when the element does not exist.
 */
export function getElementValue(id) {
    const element = document.getElementById(id);
    return element ? element.value : null;
}

/**
 * Closes the options popup. Replaces the "javascript:self.close();" start-up script
 * Options.aspx.cs ClosePage() registered.
 */
export function closeWindow() {
    window.self.close();
}

/**
 * Shows an alert and closes the options popup, which is what the "errorcloseScript" start-up
 * script of CKEditorOptions.Page_Load did for a user without permission.
 * @param {string} message The message to alert.
 */
export function alertAndClose(message) {
    window.alert(message);
    window.self.close();
}
