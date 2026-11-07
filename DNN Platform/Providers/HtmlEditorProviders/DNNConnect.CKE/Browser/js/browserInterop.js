// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// The client side seam of Browser.razor. It replaces the
// Page.ClientScript.RegisterStartupScript(...) calls of Browser.aspx.cs, because a <script>
// element written by a Blazor render is never executed. Each helper emits exactly the statement
// the WebForms page registered.

/** Scrolls the selected (or just uploaded) file into view - ScrollToSelectedFile(elementId). */
export function scrollToElement(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollIntoView();
    }
}

/** Shows a message - "javascript:alert(...)". */
export function showAlert(message) {
    window.alert(message);
}

/** Shows a message and closes the browser window - "javascript:alert(...);self.close();". */
export function alertAndClose(message) {
    window.alert(message);
    self.close();
}

/** Re-applies the list/detail/icons view the hidden ListViewState field carries, after a re-render. */
export function switchView(viewMode) {
    if (window.SwitchView && viewMode) {
        window.SwitchView(viewMode);
    }
}

/** Closes the browser window - Cancel_Click's "javascript:self.close();". */
export function closeWindow() {
    self.close();
}

/** Reads the live value of an input, which the jQuery sliders write into directly. */
export function getElementValue(elementId) {
    const element = document.getElementById(elementId);
    return element ? element.value : null;
}

/** Hides the "Please wait" panel - TreeTabs_NodeClick's "jQuery('#panelLoading').hide();". */
export function hideLoading() {
    const panel = document.getElementById('panelLoading');
    if (panel) {
        panel.style.display = 'none';
    }
}

/** Starts the download of the selected file - replaces FileManager.WriteFileToResponse. */
export function downloadFile(url, fileName) {
    const link = document.createElement('a');
    link.href = url;
    if (fileName) {
        link.download = fileName;
    }

    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

/**
 * The CKEditor callback protocol of GetJavaScriptCode: hands the picked file/page URL back to the
 * CKEDITOR instance that opened this window and closes it.
 */
export function callCKEditorFunction(funcNum, fileUrl, errorMessage) {
    const E = window.top.opener;
    E.CKEDITOR.tools.callFunction(funcNum, fileUrl, errorMessage);
    self.close();
}

/**
 * GetSelectedImageOrLink: reports the image/link currently selected inside the editor back to the
 * server, so the browser can preselect its folder and file. Replaces the
 * jQuery.PageMethod('Browser.aspx', 'SetFile', ...) call with a POST to SetFileEndpoint, which is
 * the endpoint the [WebMethod] page method became.
 */
export function getSelectedImageOrLink(editorInstanceName, setFileUrl) {
    const reload = function () {
        if (location.href.indexOf('reload') === -1) {
            location.replace(location.href + '&reload=true');
        }
    };

    const setFile = function (url) {
        fetch(setFileUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ fileUrl: url }),
        }).then(reload, reload);
    };

    const editor = window.top.opener;
    if (!editor || typeof editor.CKEDITOR === 'undefined') {
        return;
    }

    const instance = editor.CKEDITOR.instances[editorInstanceName];
    if (!instance) {
        return;
    }

    const selection = instance.getSelection();
    const element = selection ? selection.getStartElement() : null;
    if (!element) {
        return;
    }

    const name = element.getName();
    if (name === 'img') {
        const imageUrl = element.getAttribute('src');
        if (imageUrl && imageUrl.indexOf('LinkClick') === -1 && imageUrl.indexOf('http:') === -1 && imageUrl.indexOf('https:') === -1) {
            setFile(imageUrl);
        } else {
            reload();
        }
    } else if (name === 'a') {
        const fileUrl = element.getAttribute('href');
        if (fileUrl && fileUrl.indexOf('LinkClick') === -1 && fileUrl.indexOf('http:') === -1 && fileUrl.indexOf('https:') === -1) {
            setFile(fileUrl);
        } else {
            reload();
        }
    }
}

/** Resizer_Click's "SliderScript": sizes the preview and wires the two jQuery UI sliders. */
export function setupSliders(imageWidth, imageHeight, defaultWidth, defaultHeight) {
    ResizeMe('#imgResized', 360, 300);
    SetupSlider('#SliderWidth', 1, imageWidth, 1, 'horizontal', defaultWidth, '#txtWidth');
    SetupSlider('#SliderHeight', 1, imageHeight, 1, 'vertical', defaultHeight, '#txtHeight');
}

/**
 * Rotate_Click's "CropZoomScript": the cropzoom editor over the original image, its preview and
 * clear buttons, and the save which posted back to cmdCropNow - now a .NET callback.
 */
export function setupCropZoom(options, dotNetRef) {
    jQuery(function () {
        jQuery('#imgResized').hide();

        const cropzoom = jQuery('#ImageOriginal').cropzoom({
            width: 400,
            height: 300,
            bgColor: '#CCC',
            enableRotation: true,
            enableZoom: true,
            selector: {
                w: 100,
                h: 80,
                showPositionsOnDrag: true,
                showDimetionsOnDrag: true,
                bgInfoLayer: '#FFF',
                infoFontSize: 10,
                infoFontColor: 'blue',
                maxHeight: null,
                maxWidth: null,
                centered: true,
                borderColor: 'blue',
                borderColorHover: '#9eda29',
            },
            image: {
                source: options.imageUrl,
                width: options.imageWidth,
                height: options.imageHeight,
                minZoom: 10,
                maxZoom: 150,
            },
        });

        jQuery('#PreviewCrop').off('click.dnnck').on('click.dnnck', function () {
            jQuery('#lblCropInfo').hide();
            jQuery('#imgResized')
                .attr('src', options.processImageUrl + '?fileId=' + options.fileId + '&' + cropzoom.PreviewParams())
                .show();
            ResizeMe('#imgResized', 360, 300);
        });

        jQuery('#ClearCrop').off('click.dnnck').on('click.dnnck', function () {
            jQuery('#imgResized').hide();
            jQuery('#lblCropInfo').show();
            cropzoom.restore();
        });

        jQuery('#CropNow').off('click.dnnck').on('click.dnnck', function (e) {
            e.preventDefault();
            cropzoom.send(
                options.processImageUrl,
                'POST',
                {
                    newFileName: jQuery('#txtCropImageName').val(),
                    saveFile: true,
                    fileId: options.fileId,
                },
                function () { dotNetRef.invokeMethodAsync('CropNowAsync'); });
        });
    });
}

/**
 * The inline "setupFileUpload" block of Browser.aspx: a .razor render never executes a <script>
 * element, so the jQuery-File-Upload wiring lives here and is called from OnAfterRenderAsync.
 */
export function setupFileUpload(options) {
    const apply = function (overrideFile) {
        jQuery('#fileupload').fileupload({
            url: options.fileUploaderUrl,
            acceptFileTypes: new RegExp('(\\.|\\/)(' + options.acceptFileTypes + ')', 'i'),
            maxFileSize: options.maxFileSize,
            done: function () {
                options.dotNetRef.invokeMethodAsync('UploadNowAsync');
            },
            formData: {
                storageFolderID: options.currentFolderId,
                portalID: options.portalId,
                overrideFiles: overrideFile,
            },
            dropZone: jQuery('#dropzone'),
        });
    };

    jQuery(function () {
        jQuery(document).off('change.dnnckupload').on('change.dnnckupload', '#OverrideFile', function () {
            apply(this.checked);
        });

        apply(jQuery('#OverrideFile').is(':checked'));

        jQuery(document).off('dragover.dnnckupload').on('dragover.dnnckupload', function (e) {
            const dropZone = jQuery('#dropzone');
            const timeout = window.dropZoneTimeout;
            if (!timeout) {
                dropZone.addClass('ui-state-highlight');
            } else {
                clearTimeout(timeout);
            }

            let found = false;
            let node = e.target;
            do {
                if (node === dropZone[0]) {
                    found = true;
                    break;
                }

                node = node.parentNode;
            } while (node != null);

            if (found) {
                dropZone.addClass('ui-widget-content');
            } else {
                dropZone.removeClass('ui-widget-content');
            }

            window.dropZoneTimeout = setTimeout(function () {
                window.dropZoneTimeout = null;
                dropZone.removeClass('ui-state-highlight ui-widget-content');
            }, 100);
        });
    });
}
