// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

/* Ported from the inline <script> block of EditHtml.ascx.
   Blazor does not execute <script> elements that a component renders, so the block became this
   file, which EditHtml.razor includes and calls into after every render. The only part that was
   dropped is the Sys.WebForms.PageRequestManager end-request subscription, whose purpose — run the
   setup again after a partial update — is served by the per-render call from the component. */
/*globals jQuery, window, Sys, CKEDITOR */
var dnn = dnn || {};
dnn.htmlModule = dnn.htmlModule || {};

(function ($) {
    'use strict';

    function resizeDnnEditHtml() {
        $('.ehCurrentContent').height($(window).height() - $('.ehActions').height() - $('.dnnEditHtml ').outerHeight(true) + $('.dnnEditHtml ').innerHeight());
        $('window.frameElement, body, html').css('overflow', 'hidden');
        if ($('.ehccContent.EditView').length) {
            // RadEditor
            if (typeof $find === "function") {
                var editor = $find($(".RadEditor").attr('id'));
                if (editor) {
                    editor.setSize($('.ehCurrentContent').width(), $('.ehCurrentContent').height() - $('.divCurrentVersion').height() - $('.ehmContent').height());
                    $('.ehCurrentContent').css('overflow', 'hidden');
                }
            }

            // CK Editor
            var editorid = $(".ehCurrentContent textarea.editor").attr('id');
            if (editorid) {
                var ckeditor = CKEDITOR.instances[editorid];
                if (ckeditor && ckeditor.status == 'ready') {
                    var newEditorHeight = $('.ehCurrentContent').height();
                    if ($('.dnnTextPanelView').length > 0) { newEditorHeight = newEditorHeight - $('.dnnTextPanelView').height(); }
                    if ($('.divCurrentVersion').length > 0) { newEditorHeight = newEditorHeight - $('.divCurrentVersion').height(); }
                    if ($('.ehCurrentContent .dnnTextPanel p').length > 0) { newEditorHeight = newEditorHeight - $('.ehCurrentContent .dnnTextPanel p').height(); }
                    if ($('.ehmContent').length > 0) { newEditorHeight = newEditorHeight - $('.ehmContent').height(); }
                    ckeditor.resize($('.ehCurrentContent').width(), newEditorHeight);
                    $('.ehCurrentContent').css('overflow', 'hidden');
                    $('.ehCurrentContent .dnnTextPanel p').css('margin-bottom', '0');
                }
            }

            // Basic editor
            var basiceditor = $('.dnnTextPanel .dnnFormItem textarea');
            if (basiceditor) {
                $('.ehCurrentContent').css('overflow', 'hidden');
                $(basiceditor).css('max-width', $('.ehCurrentContent').width() - 18);
                $(basiceditor).css('height', $('.ehCurrentContent').height() - 36 - 37 - 30);
            }
        }
    }

    dnn.htmlModule.setupEditHtml = function () {
        if (!$('#dnnEditHtml').length) {
            return;
        }

        $('#dnnEditHtml').dnnPanels();
        if (window.frameElement && window.frameElement.id == "iPopUp") {
            var ckeditorid = $(".ehCurrentContent textarea.editor").attr('id');
            if (ckeditorid) {
                CKEDITOR.on("instanceReady", function (event) {
                    resizeDnnEditHtml();
                });
            } else {
                resizeDnnEditHtml();
            }

            if (!dnn.htmlModule.resizeHooked) {
                dnn.htmlModule.resizeHooked = true;
                $(window).resize(function () {
                    var timeout;
                    if (timeout) { clearTimeout(timeout); }
                    timeout = setTimeout(function () {
                        timeout = null;
                        resizeDnnEditHtml();
                    }, 50);
                });
            }
        }
    };

    $(document).ready(function () {
        dnn.htmlModule.setupEditHtml();
    });
}(jQuery));
