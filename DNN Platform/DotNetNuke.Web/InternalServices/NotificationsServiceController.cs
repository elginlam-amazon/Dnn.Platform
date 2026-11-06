// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.InternalServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using DotNetNuke.Instrumentation;
using DotNetNuke.Internal.SourceGenerators;
using DotNetNuke.Services.Social.Messaging.Internal;
using DotNetNuke.Services.Social.Notifications;
using DotNetNuke.Web.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

// Disambiguates the DNN services framework anti-forgery filter from the same-named
// Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute, which the Core MVC namespaces now
// bring into scope. DNN's attribute is the one that must apply here: it carries the auth-type
// bypass list fed by the authentication handlers and the iframe relaxation the upload flow needs.
using ValidateAntiForgeryToken = DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute;

/// <summary>A web API controller for notifications.</summary>
[DnnAuthorize]
public partial class NotificationsServiceController : DnnApiController
{
    private static readonly ILogger Logger = DnnLoggingController.GetLogger<NotificationsServiceController>();

    /// <summary>Dismisses a notification.</summary>
    /// <param name="postData">Information about the notification.</param>
    /// <returns>A response indicating success.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [DnnDeprecated(10, 3, 3, "Use overload taking NotificationRequest")]
    public partial IActionResult Dismiss(NotificationDTO postData)
        => this.Dismiss(postData?.ToNotificationRequest());

    /// <summary>Dismisses a notification.</summary>
    /// <param name="requestBody">Information about the notification.</param>
    /// <returns>A response indicating success.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Dismiss(NotificationRequest requestBody)
    {
        try
        {
            var recipient = InternalMessagingController.Instance.GetMessageRecipient(requestBody.NotificationId, this.UserInfo.UserID);
            if (recipient != null)
            {
                NotificationsController.Instance.DeleteNotificationRecipient(requestBody.NotificationId, this.UserInfo.UserID);
                return DnnApiResults.Response(HttpStatusCode.OK, new { Result = "success" });
            }

            return DnnApiResults.Error(HttpStatusCode.BadRequest, "Unable to dismiss notification");
        }
        catch (Exception exc)
        {
            Logger.NotificationsServiceControllerDismissException(exc);
            return DnnApiResults.Error(HttpStatusCode.InternalServerError, exc);
        }
    }

    /// <summary>Gets toasts for the user.</summary>
    /// <returns>A response with an object that has a <c>Toasts</c> field.</returns>
    [HttpGet]
    public IActionResult GetToasts()
    {
        var toasts = NotificationsController.Instance.GetToasts(this.UserInfo);
        IList<object> convertedObjects = toasts.Select(this.ToExpandoObject).ToList();
        return DnnApiResults.Response(HttpStatusCode.OK, new { Success = true, Toasts = convertedObjects.Take(3) });
    }

    private object ToExpandoObject(Notification notification)
    {
        return new { Subject = notification.Subject, Body = notification.Body };
    }
}
