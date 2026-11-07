// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// jQuery/DNN plugin wiring for ModuleSettings.razor. This replaces the inline <script> block of
// Modulesettings.ascx: Blazor never executes a <script> element rendered from a component, so the
// script became a real ES module which the component imports after its first render.
//
// The MS AJAX "add_endRequest" re-initialisation of the original is gone: there are no partial
// postbacks on ASP.NET Core, and the component calls setUpDnnModuleSettings again itself whenever
// it needs to.
//
// The compareDate client validation function of the original is also gone: the start/end date
// comparison is now validated on the server (ModuleSettings.Validate), which is where the save
// decision is made.
//
// The isShareableRowViewOnly show/hide of the original is likewise gone; the component renders
// that row's visibility declaratively from the isShareable checkbox state.

export function setUpDnnModuleSettings(deleteButtonId, containerComboId, moduleId, baseUrl, text) {
    const $ = window.jQuery;
    if (!$) {
        return;
    }

    $('#dnnModuleSettings').dnnTabs().dnnPanels();

    $('#msModuleSettings .dnnFormExpandContent a').dnnExpandAll({
        expandText: text.expandAll,
        collapseText: text.collapseAll,
        targetArea: '#msModuleSettings'
    });

    $('#msPageSettings .dnnFormExpandContent a').dnnExpandAll({
        expandText: text.expandAll,
        collapseText: text.collapseAll,
        targetArea: '#msPageSettings'
    });

    $('#' + deleteButtonId).dnnConfirm({
        text: text.deleteItem,
        yesText: text.yes,
        noText: text.no,
        title: text.confirm
    });

    $('.dnnContainerPreview').dnnPreview({
        containerSelector: containerComboId,
        baseUrl: baseUrl,
        noSelectionMessage: text.previewNoSelection,
        alertCloseText: text.close,
        alertOkText: text.ok,
        useComboBox: true,
        ModuleId: moduleId
    });
}
