/*
 * Licensed to the .NET Foundation under one or more agreements.
 * The .NET Foundation licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information
 *
 * DNN Client Resource Management async-postback loader.
 *
 * This file is the verbatim port of the handler script that was previously emitted inline by
 * ClientResourceLoader.OnPreRender and DnnResources.OnPreRender (the two copies were identical).
 * Every branch of the original script is preserved:
 *   - single load mode (one script at a time, chained through the completion handler)
 *   - multiple load mode (all scripts appended at once)
 *   - the Firefox special case (and the window.forceLoadScriptsInSingleMode override)
 *   - the $crm_ data item scraping of <script src> and <link href> tags
 *   - the Sys._ScriptLoader.loadScripts override that defers MS Ajax script loading
 *
 * The only structural change: the $crm_ data item scraping is factored into
 * window.dnnApplyCrmDataItems(dataItems) so that it can be driven both by the WebForms
 * Sys.WebForms.PageRequestManager pageLoading event (when MS Ajax is present) and by the
 * ASP.NET Core render path, which emits the same $crm_ payload into window.dnnCrmDataItems
 * (see DotNetNuke.Web.Client.Providers.AsyncPostBackDataItems).
 */
(function () {
    var loadScriptInSingleMode = function () {
        var s = document.createElement('script');
        s.type = 'text/javascript';
        s.src = window.dnnLoadScriptsInAjaxMode[0];
        s.onload = window.dnnLoadScriptsInAjaxModeComplete;
        s.onerror = window.dnnLoadScriptsInAjaxModeComplete;
        document.body.appendChild(s);
    };

    var loadScriptInMultipleMode = function () {
        for (var i = 0; i < window.dnnLoadScriptsInAjaxMode.length; i++) {
            var s = document.createElement('script');
            s.type = 'text/javascript';
            s.src = window.dnnLoadScriptsInAjaxMode[i];
            s.onload = window.dnnLoadScriptsInAjaxModeComplete;
            s.onerror = window.dnnLoadScriptsInAjaxModeComplete;
            document.body.appendChild(s);
        }
    };

    var isFirefox = navigator.userAgent.toLowerCase().indexOf('firefox') > -1;

    (function ($) {
        window.dnnApplyCrmDataItems = function (dataItems) {
            if (!dataItems) {
                return;
            }

            for (var item in dataItems) {
                if (item.indexOf('$crm_') > -1) {
                    var content = dataItems[item];

                    // check whether script already register to page.
                    if (typeof window.dnnLoadScriptsInAjaxMode === 'undefined') {
                        window.dnnLoadScriptsInAjaxMode = [];
                    }

                    var scripts = content.match(/<script.+?><\/script>/gi);
                    if (scripts && scripts.length > 0) {
                        for (var i = 0; i < scripts.length; i++) {
                            var src = scripts[i].match(/src="(.+?)"/i)[1];
                            if ($('script[src="' + src + '"]').length == 0) {
                                window.dnnLoadScriptsInAjaxMode.push(src);
                            }
                        }
                    }

                    var styles = content.match(/<link[^>]+?>/gi);
                    if (styles && styles.length > 0) {
                        for (var j = 0; j < styles.length; j++) {
                            var href = styles[j].match(/href="(.+?)"/i)[1];
                            if ($('link[href="' + href + '"]').length == 0) {
                                $(document.body).append(styles[j]);
                            }
                        }
                    }
                }
            }

            if (typeof window.dnnLoadScriptsInAjaxMode === 'undefined') {
                window.dnnLoadScriptsInAjaxMode = [];
            }

            if (window.dnnLoadScriptsInAjaxMode.length > 0) {
                window.loadingScriptsInAsyncRequest = true;
                if (isFirefox || window['forceLoadScriptsInSingleMode'] === true) {
                    loadScriptInSingleMode();
                } else {
                    loadScriptInMultipleMode();
                }
            }
        };

        if (typeof window.dnnLoadScriptsInAjaxModeComplete == 'undefined') {
            window.dnnLoadScriptsInAjaxModeComplete = function () {
                var src = $(this).attr('src');
                if (typeof window.dnnLoadScriptsInAjaxMode !== 'undefined') {
                    for (var i = 0; i < window.dnnLoadScriptsInAjaxMode.length; i++) {
                        if (window.dnnLoadScriptsInAjaxMode[i] == src) {
                            window.dnnLoadScriptsInAjaxMode.splice(i, 1);
                            break;
                        }
                    }

                    if (window.dnnLoadScriptsInAjaxMode.length == 0) {
                        window.loadingScriptsInAsyncRequest = false;
                        $(window).trigger('dnnScriptLoadComplete');
                    } else if (isFirefox || window['forceLoadScriptsInSingleMode'] === true) {
                        loadScriptInSingleMode();
                    }
                }
            };
        }

        // The WebForms (MS Ajax) path: scrape the data items of every partial post-back.
        if (typeof Sys !== 'undefined' && Sys.WebForms && Sys.WebForms.PageRequestManager) {
            Sys.WebForms.PageRequestManager.getInstance().add_pageLoading(function (sender, args) {
                window.dnnApplyCrmDataItems(args.get_dataItems());
            });

            var originalScriptLoad = Sys._ScriptLoader.getInstance().loadScripts;
            Sys._ScriptLoader.getInstance().loadScripts = function () {
                var self = this, args = arguments;
                if (window.loadingScriptsInAsyncRequest === true) {
                    $(window).one('dnnScriptLoadComplete', function () {
                        originalScriptLoad.apply(self, args);
                    });
                } else {
                    originalScriptLoad.apply(self, args);
                }
            };
        }

        // The ASP.NET Core path: the server emitted the $crm_ payload for this response.
        if (window.dnnCrmDataItems) {
            window.dnnApplyCrmDataItems(window.dnnCrmDataItems);
        }
    }(jQuery));
}());
