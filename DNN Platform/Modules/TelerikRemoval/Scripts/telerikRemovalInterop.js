// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// The client side seam of the migrated Telerik Removal view (View.razor).
//
// View.ascx.cs registered this through the WebForms script manager:
//
//     this.Page.ClientScript.RegisterOnSubmitStatement(
//         typeof(View), nameof(View), "$('#telerikRemoval').addClass('running');");
//
// which ran as the postback that performed the removal left the browser, revealing the overlay
// and the spinner defined by module.css (#telerikRemoval.running:before and
// #telerikRemoval.running .loading).
//
// A Blazor render cannot emit script: a <script> element added by a render tree is never executed
// by the browser. The statement therefore lives here, and View.razor.cs imports this module in
// OnAfterRenderAsync(firstRender) - the first point at which JS interop is legal - and calls
// setRunning when the removal starts and again when it ends. jQuery is not required: the class is
// toggled through the element's own classList, which is what addClass does.

/**
 * Adds or removes the "running" class of the module's root element.
 * @param {string} elementId The id of the module's root element (was '#telerikRemoval').
 * @param {boolean} running Whether the removal is running.
 */
export function setRunning(elementId, running) {
    const element = document.getElementById(elementId);
    if (!element) {
        return;
    }

    if (running) {
        element.classList.add('running');
    } else {
        element.classList.remove('running');
    }
}
