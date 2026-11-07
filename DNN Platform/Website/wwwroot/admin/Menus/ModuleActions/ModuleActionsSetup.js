/*
 * Licensed to the .NET Foundation under one or more agreements.
 * The .NET Foundation licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information
 *
 * Replaces the inline <script> block of admin/Menus/ModuleActions/ModuleActions.ascx, which is not
 * legal in a .razor document. The body is carried over unchanged apart from reading its values from
 * the `settings` object the component passes through IJSRuntime instead of from <%= %> expressions,
 * and from starting immediately when the document has already finished loading (a Blazor component
 * renders after window.load, so binding to the load event alone would never run).
 */
/*globals jQuery, window, document, dnn */
var dnn = dnn || {};

(function ($) {
    'use strict';

    dnn.moduleActionForms = dnn.moduleActionForms || {};

    // ModuleActions.js builds the Url of every action that has no Url of its own as
    //     "javascript: __doPostBack('<actionButton unique id>', '<action id>')"
    // and WebForms posted the page form back. The replacement renders one static server-side form
    // per action menu, so the shim writes the action id into that form's hidden field and submits
    // it; the action therefore arrives at ModuleActions.HandleActionAsync as the same argument the
    // WebForms __EVENTARGUMENT carried. It is only installed when the page does not already have a
    // __doPostBack of its own (MS AJAX installs one), so a hybrid page keeps its own behaviour.
    if (typeof window.__doPostBack !== 'function') {
        window.__doPostBack = function (target, argument) {
            var registration = dnn.moduleActionForms[target];
            if (!registration) {
                return;
            }

            var form = document.getElementById(registration.formId);
            var field = document.getElementById(registration.fieldId);
            if (!form || !field) {
                return;
            }

            field.value = argument;
            form.submit();
        };
    }

    dnn.setUpModuleActions = function (settings) {
        dnn.moduleActionForms[settings.actionButton] = {
            formId: settings.formId,
            fieldId: settings.fieldId
        };

        var moduleId = settings.moduleId;
        var displayQuickSettings = settings.displayQuickSettings;
        var supportsQuickSettings = settings.supportsQuickSettings;

        function setUpActions() {
            var tabId = settings.tabId;

            //Initialise the actions menu plugin
            $('#' + settings.actionButtonClientId).dnnModuleActions({
                    actionButton: settings.actionButton,
                    moduleId: moduleId,
                    tabId: tabId,
                    customActions: JSON.parse(settings.customActionsJson),
                    adminActions: JSON.parse(settings.adminActionsJson),
                    panes: JSON.parse(settings.panesJson),
                    customText: settings.customText,
                    adminText: settings.adminText,
                    moveText: settings.moveText,
                    topText: settings.topText,
                    upText: settings.upText,
                    downText: settings.downText,
                    bottomText: settings.bottomText,
                    movePaneText: settings.movePaneText,
                    deleteText: settings.deleteText,
                    yesText: settings.yesText,
                    noText: settings.noText,
                    confirmTitle: settings.confirmTitle,
                    sharedText: settings.sharedText,
                    rootFolder: settings.rootFolder,
                    supportsMove: settings.supportsMove,
                    supportsQuickSettings: supportsQuickSettings,
                    displayQuickSettings: displayQuickSettings,
                    isShared: settings.isShared,
                    moduleTitle: settings.moduleTitle
                }
            );
        }

        // register window resize on ajaxComplete to reposition action menus - only in edit mode
        // after page fully load
        var resizeThrottle;
        $(window).on('resize scroll', function () {
            if (resizeThrottle) {
                clearTimeout(resizeThrottle);
                resizeThrottle = null;
            }
            resizeThrottle = setTimeout(
                function () {
                    var menu = $('.actionMenu');
                    menu.each(function () {
                        var $this = $(this);
                        var id = $this.attr('id');
                        if (id) {
                            var mId = id.split('-')[1];
                            if (moduleId == mId) {
                                var container = $(".DnnModule-" + mId);
                                var root = $('ul.dnn_mact', $this);
                                var containerPosition = container.offset();
                                var containerWidth = container.width();

                                var rootMenuWidth = (supportsQuickSettings) ? 85 : 65;

                                var $body = $(document.body);
                                var positionCss = $body.css('position');
                                var marginLeft = parseInt($body.css('margin-left'));

                                root.css({
                                    position: "absolute",
                                    marginLeft: 0,
                                    marginTop: 0,
                                    top: containerPosition.top,
                                    left: containerPosition.left + containerWidth - rootMenuWidth - (positionCss === "relative" ? marginLeft : 0)
                                });

                                if (displayQuickSettings) {
                                    var ul = $('#moduleActions-' + mId + ' .dnn_mact > li.actionQuickSettings > ul');
                                    var $self = ul.parent();
                                    if ($self.length > 0) {
                                        var windowHeight = $(window).height();
                                        var windowScroll = $(window).scrollTop();
                                        var thisTop = $self.offset().top;
                                        var atViewPortTop = (thisTop - windowScroll) < windowHeight / 2;

                                        var ulHeight = ul.height();

                                        if (!atViewPortTop) {
                                            ul.css({
                                                top: -ulHeight,
                                                right: 0
                                            }).show('slide', { direction: 'down' }, 80, function () {
                                                dnn.addIframeMask(ul[0]);
                                                displayQuickSettings = false;
                                            });
                                        }
                                        else {
                                            ul.css({
                                                top: 20,
                                                right: 0
                                            }).show('slide', { direction: 'up' }, 80, function () {
                                                dnn.addIframeMask(ul[0]);
                                                displayQuickSettings = false;
                                            });
                                        }
                                    }
                                }

                            }
                        }
                    });
                    resizeThrottle = null;
                },
                100
            );
        });

        // Webkit based browsers (like Chrome and Safari) can access images width and height properties only after images have been fully loaded.
        // It will cause menu action out of scope, TO fix this, use the load event instead of document ready.
        function start() {
            setUpActions();

            $(document).ajaxComplete(function () {
                $(window).resize();
            });
            $(window).resize();
        }

        if (document.readyState === 'complete') {
            // A Blazor component renders after window.load has already fired, so the load handler
            // below would never run; this is the same work, immediately.
            start();
        } else {
            $(window).on('load', start);
        }
    };
} (jQuery));
