// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// Client seam of the HTML module's inline (edit-in-place) editor.
//
// resetInlineEditor is the port of the start-up script HtmlModule.ascx.cs registered through
// ScriptManager.RegisterClientScriptBlock on an asynchronous post back:
//
//     if (typeof dnn !== 'undefined' && typeof dnn.controls !== 'undefined'
//         && typeof dnn.controls.controls !== 'undefined') {
//         var control = dnn.controls.controls['<clientid>'];
//         if (control && control.container !== $get('<clientid>')) {
//             dnn.controls.controls['<clientid>'] = null;
//         }
//     }
//
// The guard on dnn / dnn.controls / dnn.controls.controls is kept exactly, so the call is a no-op
// when the DNN client controls are not on the page, as the registered block was.
export function resetInlineEditor(clientId) {
    if (!clientId) {
        return;
    }

    if (typeof dnn !== 'undefined' && typeof dnn.controls !== 'undefined' && typeof dnn.controls.controls !== 'undefined') {
        const control = dnn.controls.controls[clientId];
        if (control && control.container !== document.getElementById(clientId)) {
            dnn.controls.controls[clientId] = null;
        }
    }
}

// Port of the formatting half of dnn.controls.dnntoolbarstub.js's dispatch: the eleven formatting
// buttons of the tool bar (bold, italic, underline, justify*, insert*list, outdent, indent and
// createlink) ran the matching rich-text command against the element being edited.
export function execEditorAction(clientId, action) {
    if (!clientId || !action) {
        return;
    }

    const editor = document.getElementById(clientId + '_edit') || document.getElementById(clientId);
    if (!editor) {
        return;
    }

    editor.focus();

    if (action === 'createlink') {
        const url = window.prompt('URL:', 'http://');
        if (!url) {
            return;
        }

        document.execCommand(action, false, url);
        return;
    }

    document.execCommand(action, false, null);
}
