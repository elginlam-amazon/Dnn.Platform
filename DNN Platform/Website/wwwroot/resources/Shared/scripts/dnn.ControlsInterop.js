/*
 * JS interop entry points for the DNN server controls that were migrated from Web Forms to Blazor.
 *
 * Each function here replaces a script that a control used to emit inline through
 * ScriptManager.RegisterStartupScript / ClientScriptManager.RegisterClientScriptBlock, by wrapping
 * an existing jQuery plugin in a named global that IJSRuntime can call.
 *
 * Controls whose client factory is ALREADY a named global call it directly and are deliberately NOT
 * redefined here:
 *   dnn.createDropDownList           (Resources/Shared/components/DropDownList/dnn.DropDownList.js)
 *   dnn.createFileUpload             (Resources/Shared/components/FileUpload/dnn.FileUpload.js)
 *   dnn.initializePasswordStrength   (Resources/Shared/scripts/dnn.PasswordStrength.js)
 */
var dnn = dnn || {};

/* Replaces the inline script of DnnFormEditor (`$(sel).dnnTabs().dnnPanels()` + dnnExpandAll). */
dnn.setupFormEditor = function (selector, expandAllOptions) {
    function setup() {
        jQuery(selector).dnnTabs().dnnPanels();

        (expandAllOptions || []).forEach(function (options) {
            jQuery(options.selector).dnnExpandAll({
                expandText: options.expandText,
                collapseText: options.collapseText,
                targetArea: options.targetArea
            });
        });
    }

    jQuery(document).ready(function () {
        setup();

        if (typeof Sys !== 'undefined' && Sys.WebForms) {
            Sys.WebForms.PageRequestManager.getInstance().add_endRequest(setup);
        }
    });
};

/* Replaces the inline `$(sel).dnnComboBox(options)` / `$(sel).selectize({})` startup scripts. */
dnn.createComboBox = function (selector, options) {
    jQuery(document).ready(function () {
        jQuery(selector).dnnComboBox(options || {});
    });
};

/* Replaces the inline `$(sel).dnnTagsInput(options)` startup script of the Tags control. */
dnn.createTagsInput = function (selector, options) {
    jQuery(document).ready(function () {
        jQuery(selector).dnnTagsInput(options || {});
    });
};

/* Replaces the inline `$(scope).dnnFileUpload(settings)` script of filepickeruploader.ascx. */
dnn.createFilePickerUploader = function (selector, settings) {
    function init() {
        jQuery(selector).dnnFileUpload(settings || {});
    }

    jQuery(document).ready(function () {
        init();

        if (typeof Sys !== 'undefined' && Sys.WebForms) {
            Sys.WebForms.PageRequestManager.getInstance().add_endRequest(init);
        }
    });
};

/* Replaces the client navigation emitted by DnnRibbonBarTool and DnnUrlControl. */
dnn.navigateTo = function (url, target) {
    if (!url) {
        return;
    }

    if (target) {
        window.open(url, target);
    } else {
        window.location.href = url;
    }
};

/* Replaces the Web Forms `OnClientClick` attribute of DnnButton / DnnImageButton: the script that
   used to be emitted inline is evaluated here and its result decides whether the server side click
   handler runs, which is exactly what returning false from an `onclick` attribute did. */
dnn.runClientClick = function (script) {
    if (!script) {
        return true;
    }

    try {
        /* the scripts DNN assigns are statements ending in `return <bool>;` (e.g. the
           `postBackConfirm` confirmation), so they are wrapped in a function before evaluation. */
        var result = new Function(script).call(window);
        return result !== false;
    } catch (e) {
        if (window.console && window.console.error) {
            window.console.error('dnn.runClientClick failed', e);
        }

        return true;
    }
};

/* Replaces the inline script DnnScriptBlock emitted into the page: the script text is registered
   once per key and evaluated in page scope. */
dnn.registerScriptBlock = function (key, script) {
    dnn.__scriptBlocks = dnn.__scriptBlocks || {};
    if (!script || dnn.__scriptBlocks[key]) {
        return;
    }

    dnn.__scriptBlocks[key] = true;
    try {
        new Function(script).call(window);
    } catch (e) {
        if (window.console && window.console.error) {
            window.console.error('dnn.registerScriptBlock failed for ' + key, e);
        }
    }
};

/* Replaces the inline datepicker/datetimepicker initialization of DnnDatePicker. */
dnn.createDatePicker = function (selector, settings) {
    jQuery(document).ready(function () {
        var $element = jQuery(selector);
        if (!$element.length) {
            return;
        }

        if (settings && settings.showTime && jQuery.fn.datetimepicker) {
            $element.datetimepicker(settings);
        } else if (jQuery.fn.datepicker) {
            $element.datepicker(settings || {});
        }
    });
};

/* Bridges DnnFormPasswordItem to the existing dnn.initializePasswordStrength: the options were
   serialized server side by DNN's own serializer and emitted inside the startup script, so they are
   handed over as that exact JSON text and parsed here rather than re-serialized by the interop
   layer, which would rename the members. */
dnn.initializePasswordStrengthJson = function (selector, optionsJson) {
    var options = {};
    try {
        options = optionsJson ? JSON.parse(optionsJson) : {};
    } catch (e) {
        options = {};
    }

    if (typeof dnn.initializePasswordStrength === 'function') {
        dnn.initializePasswordStrength(selector, options);
    }
};
