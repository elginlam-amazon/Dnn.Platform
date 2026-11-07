// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// The client side seam of the migrated group list (List.razor).
//
// List.ascx emitted this whole block as an inline <script>. A Blazor render cannot emit script:
// a <script> element added by a render tree is never executed by the browser, so the block moved
// here and List.razor imports this module and calls initGroupList from OnAfterRenderAsync.
//
// The behavior is unchanged: the "join-group-icn" anchors the list item template renders post to
// the module's Web API service (SocialGroups/ModerationService/JoinGroup) through the DNN
// services framework, and the completion handler either follows the URL the service returned or
// relabels the anchor to the localized "Pending" text and disables it.

/**
 * Wires the group list's join handlers.
 * Replaces the jQuery(document).ready(...) block of List.ascx.
 * @param {Object} options The values the inline script interpolated server side.
 * @param {number} options.moduleId The module id the services framework is scoped to (<%=ModuleId %>).
 * @param {string} options.pendingText The localized "Pending" label (<%=LocalizeString("Pending")%>).
 */
export function initGroupList(options) {
    const jQuery = window.jQuery;
    if (!jQuery || !options) {
        return;
    }

    jQuery(function ($) {
        $('.join-group-icn a').click(function (event) {
            event.preventDefault();
            var groupId = $(this).attr('groupId');
            var groupViewTabId = $(this).attr('groupViewTabId');
            groupJoin(groupId, groupViewTabId);
        });

        function groupJoin(id, groupViewTabId) {
            var data = {};
            data.roleId = id;
            data.groupViewTabId = groupViewTabId;
            groupPost('JoinGroup', data, groupJoinComplete, id);
        }

        function groupJoinComplete(result, id) {
            if (result.URL != '' && typeof (result.URL) != 'undefined') {
                window.location.href = result.URL;
            } else {
                $('#groupJoin-' + id).text(options.pendingText)
                    .removeClass('dnnTertiaryAction')
                    .addClass('dnnDisabled')
                    .off('click')
                    .click(function (event) { event.preventDefault });
            }
        }

        function groupPost(method, data, callback, groupId) {
            var sf = $.ServicesFramework(options.moduleId);

            $.ajax({
                type: "POST",
                url: sf.getServiceRoot('SocialGroups') + "ModerationService/" + method,
                beforeSend: sf.setModuleHeaders,
                data: data,
                success: function (data) {
                    if (typeof (callback) != "undefined") {
                        callback(data, groupId);
                    }
                },
                error: function (xhr, status, error) {
                    alert(error);
                }
            });
        }
    });
}
