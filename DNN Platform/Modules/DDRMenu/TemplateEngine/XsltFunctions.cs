// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.DDRMenu.TemplateEngine
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Security;

    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Web.DDRMenu.DNNCommon;

    public class XsltFunctions
    {
        public bool UserIsInRole(string roleName)
        {
            return UserController.Instance.GetCurrentUserInfo().IsInRole(roleName);
        }

        public string GetLoginURL()
        {
            return DNNAbstract.GetLoginUrl();
        }

        public string GetLoginText()
        {
            try
            {
                return Localization.GetString(
                    DNNAbstract.IsAuthenticated() ? "Logout" : "Login",
                    DNNContext.Current.ResolveUrl("~/Admin/Skins/" + Localization.LocalResourceDirectory + "/Login.ascx"));
            }
            catch (Exception)
            {
                return "Login";
            }
        }

        public string GetUserURL()
        {
            return DNNAbstract.GetUserUrl();
        }

        public string GetUserText()
        {
            try
            {
                if (DNNAbstract.IsAuthenticated())
                {
                    return UserController.Instance.GetCurrentUserInfo().DisplayName;
                }
                else
                {
                    // The host control used to come from the Items["DDRMenuHostControl"] slot and
                    // only served to call Control.ResolveUrl, which IClientApiControl does not
                    // carry. The DNN context performs the identical application-relative
                    // resolution and also works when the host is a module control.
                    var resourceFileRoot = DNNContext.Current.ResolveUrl($"~/Admin/Skins/{Localization.LocalResourceDirectory}/User.ascx");
                    return Localization.GetString("Register", resourceFileRoot);
                }
            }
            catch (Exception exc)
            {
                // Never render the exception text into the menu; log it and fall back to the
                // anonymous label, which is what a visitor would otherwise have seen.
                Exceptions.LogException(exc);
                return Localization.GetString("Register");
            }
        }

        public string EscapeXML(string xml)
        {
            return SecurityElement.Escape(xml);
        }

        public string HtmlEncode(string s)
        {
            // HttpUtility.HtmlEncode is replaced by the WebUtility equivalent, which performs the
            // same HTML entity encoding.
            return System.Net.WebUtility.HtmlEncode(s);
        }

        public string GetString(string name, string resourceFile)
        {
            var resolver = XsltTemplateProcessor.CurrentResolver;
            var resolvedFile = string.Empty;
            if (resolver != null)
            {
                var localFile = resolver.Resolve(
                    resourceFile,
                    PathResolver.RelativeTo.Manifest,
                    PathResolver.RelativeTo.Skin,
                    PathResolver.RelativeTo.Module,
                    PathResolver.RelativeTo.Dnn);
                if (File.Exists(HttpContextSource.MapPath(localFile)))
                {
                    resolvedFile = localFile;
                }
            }

            if (string.IsNullOrEmpty(resolvedFile))
            {
                resolvedFile = "~/" + resourceFile;
            }

            return Localization.GetString(name, resolvedFile);
        }

        public bool ActionCommandIs(string commandName, string constName)
        {
            try
            {
                return
                    commandName.Equals(
                        typeof(DotNetNuke.Entities.Modules.Actions.ModuleActionType).InvokeMember(
                            constName, BindingFlags.GetField, null, null, new object[0]));
            }
            catch (MissingFieldException)
            {
                return false;
            }
        }
    }
}
