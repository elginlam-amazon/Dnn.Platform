// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// The client side seam of DNNConnect.CKEditorProvider.Web.EditorControl.
//
// The WebForms control registered its CKEditor configuration and bootstrap through
// ScriptManager.RegisterClientScriptBlock / RegisterStartupScript / RegisterOnSubmitStatement.
// A Blazor render cannot execute a <script> element it emits, so when the editor renders outside
// a DNN page (i.e. there is no page client-script seam to register on) the very same script
// source is handed to the named helpers below from OnAfterRenderAsync. Named helpers are used
// rather than eval() so the component stays usable under a CSP that forbids eval.

/**
 * Runs the editor configuration / bootstrap source the component generated.
 * @param {string} script The script source.
 */
export function runEditorScript(script) {
    if (!script) {
        return;
    }

    // eslint-disable-next-line no-new-func
    new Function(script)();
}

/**
 * Attaches a statement to the submit event of every form, which is what
 * ScriptManager.RegisterOnSubmitStatement did through the WebForms submit pipeline. The statement
 * writes the editor's html back onto its text area (and destroys the instance) before the form is
 * sent, so the posted value is the edited one.
 * @param {string} script The statement source.
 */
export function registerOnSubmit(script) {
    if (!script) {
        return;
    }

    // eslint-disable-next-line no-new-func
    const statement = new Function(script);

    document.addEventListener('submit', function () {
        statement();
    }, true);
}
