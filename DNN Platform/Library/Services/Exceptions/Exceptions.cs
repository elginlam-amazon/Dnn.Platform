// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Exceptions
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;

    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Entities.Host;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Localization;
    using DotNetNuke.Services.Log.EventLog;
    using DotNetNuke.UI;
    using DotNetNuke.UI.Modules;
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualBasic.CompilerServices;

    /// <summary>Exceptions class provides operation to log most of the exceptions occurred in system.</summary>
    /// <remarks>
    /// <para>
    /// Logging is an import part of a good architecture. A good practice is to provide enough logging to accurately be able to identify which
    /// code caused an exception. Some exceptions are expected and you can display information to the user to help him figure out what happened.
    /// It is however bad practice to catch any and all exceptions (general exception) and display this to users. In such situations it is best
    /// to provide the user with a general message and only log the details in a way that the details are either just visible to administrators
    /// (Event Log) or only to SuperUsers (hosts) in the server logs.
    /// </para>
    /// <para>
    /// Currently we implement a lot of custom exception to use in different levels:
    /// <list type="bullet">
    /// <item><see cref="ModuleLoadException"/></item>
    /// <item><see cref="ObjectHydrationException"/></item>
    /// <item><see cref="PageLoadException"/></item>
    /// <item><see cref="SchedulerException"/></item>
    /// <item><see cref="SearchException"/></item>
    /// <item><see cref="SecurityException"/></item>
    /// </list>
    /// </para>
    /// </remarks>
    [StandardModule]
    public sealed class Exceptions
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<Exceptions>();

        /// <summary>Gets the exception info.</summary>
        /// <param name="e">The exception.</param>
        /// <returns>Exception info.</returns>
        public static ExceptionInfo GetExceptionInfo(Exception e)
        {
            var objExceptionInfo = new ExceptionInfo(e);

            while (e.InnerException != null)
            {
                e = e.InnerException;
            }

            var st = new StackTrace(e, true);
            var sf = st.GetFrame(0);
            if (sf != null)
            {
                try
                {
                    // Get the corresponding method for that stack frame.
                    var mi = sf.GetMethod();

                    // Get the namespace where that method is defined.
                    var res = mi.DeclaringType.Namespace + ".";

                    // Append the type name.
                    res += mi.DeclaringType.Name + ".";

                    // Append the name of the method.
                    res += mi.Name;
                    objExceptionInfo.Method = res;
                }
                catch (Exception exc)
                {
                    Logger.ExceptionsGetExceptionInfoReflectionPermissionException(exc);

                    objExceptionInfo.Method = "N/A - Reflection Permission required";
                }

                if (!string.IsNullOrEmpty(sf.GetFileName()))
                {
                    objExceptionInfo.FileName = sf.GetFileName();
                    objExceptionInfo.FileColumnNumber = sf.GetFileColumnNumber();
                    objExceptionInfo.FileLineNumber = sf.GetFileLineNumber();
                }
            }

            return objExceptionInfo;
        }

        /// <summary>Processes a 404 (not found) condition for the current request's raw URL.</summary>
        public static void ProcessHttpException()
        {
            var notFoundErrorString = Localization.GetString("ResourceNotFound", Localization.SharedResourceFile);
            var exc = new BadHttpRequestException(notFoundErrorString, 404);
            ProcessHttpException(exc, HttpContextSource.CurrentOrThrow.Request.GetRawUrl());
        }

        /// <summary>Processes a 404 (not found) condition for the given URL.</summary>
        /// <param name="url">The URL that could not be found.</param>
        public static void ProcessHttpException(string url)
        {
            var notFoundErrorString = Localization.GetString("ResourceNotFound", Localization.SharedResourceFile);
            var exc = new BadHttpRequestException(notFoundErrorString, 404);
            ProcessHttpException(exc, url);
        }

        /// <summary>Processes an HTTP status exception for the current request's raw URL.</summary>
        /// <param name="exc">The exception, carrying the HTTP status code.</param>
        /// <remarks>
        /// On ASP.NET Framework this took a <c>System.Web.HttpException</c>, which does not exist on
        /// modern .NET. <see cref="BadHttpRequestException"/> is the ASP.NET Core replacement: it
        /// carries a <see cref="BadHttpRequestException.StatusCode"/> exactly as
        /// <c>HttpException.GetHttpCode()</c> did.
        /// </remarks>
        public static void ProcessHttpException(BadHttpRequestException exc)
        {
            ProcessHttpException(exc, HttpContextSource.CurrentOrThrow.Request.GetRawUrl());
        }

        /// <summary>Processes a 404 (not found) condition for the given request.</summary>
        /// <param name="request">The request that could not be served.</param>
        public static void ProcessHttpException(HttpRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var notFoundErrorString = Localization.GetString("ResourceNotFound", Localization.SharedResourceFile);
            var exc = new BadHttpRequestException(notFoundErrorString, 404);
            ProcessHttpException(exc, request.GetRawUrl());
        }

        /// <summary>Processes the module load exception.</summary>
        /// <param name="objPortalModuleBase">The control.</param>
        /// <param name="exc">The exception.</param>
        public static void ProcessModuleLoadException(PortalModuleBase objPortalModuleBase, Exception exc)
        {
            ProcessModuleLoadException((IComponent)objPortalModuleBase, exc);
        }

        /// <summary>Processes the module load exception.</summary>
        /// <param name="objPortalModuleBase">The control.</param>
        /// <param name="exc">The exception.</param>
        /// <param name="displayErrorMessage">if set to <see langword="true"/> display error message.</param>
        public static void ProcessModuleLoadException(PortalModuleBase objPortalModuleBase, Exception exc, bool displayErrorMessage)
        {
            ProcessModuleLoadException((IComponent)objPortalModuleBase, exc, displayErrorMessage);
        }

        /// <summary>Processes the module load exception.</summary>
        /// <param name="friendlyMessage">The friendly message.</param>
        /// <param name="objPortalModuleBase">The control.</param>
        /// <param name="exc">The exception.</param>
        /// <param name="displayErrorMessage">if set to <see langword="true"/> display error message.</param>
        public static void ProcessModuleLoadException(string friendlyMessage, PortalModuleBase objPortalModuleBase, Exception exc, bool displayErrorMessage)
        {
            ProcessModuleLoadException(friendlyMessage, (IComponent)objPortalModuleBase, exc, displayErrorMessage);
        }

        /// <summary>Processes the module load exception.</summary>
        /// <param name="ctrl">The control.</param>
        /// <param name="exc">The exception.</param>
        public static void ProcessModuleLoadException(IComponent ctrl, Exception exc)
        {
            // Exit Early if ThreadAbort Exception
            if (ThreadAbortCheck(exc))
            {
                return;
            }

            ProcessModuleLoadException(ctrl, exc, true);
        }

        /// <summary>Processes the module load exception.</summary>
        /// <param name="ctrl">The control.</param>
        /// <param name="exc">The exception.</param>
        /// <param name="displayErrorMessage">if set to <see langword="true"/> displays an error message.</param>
        public static void ProcessModuleLoadException(IComponent ctrl, Exception exc, bool displayErrorMessage)
        {
            // Exit Early if ThreadAbort Exception
            if (ThreadAbortCheck(exc))
            {
                return;
            }

            string friendlyMessage = Localization.GetString("ErrorOccurred");
            var ctrlModule = ctrl as IModuleControl;
            if (ctrlModule == null)
            {
                // Regular Control
                friendlyMessage = Localization.GetString("ErrorOccurred");
            }
            else
            {
                // IModuleControl
                string moduleTitle = Null.NullString;
                if (ctrlModule != null && ctrlModule.ModuleContext.Configuration != null)
                {
                    moduleTitle = ctrlModule.ModuleContext.Configuration.ModuleTitle;
                }

                friendlyMessage = string.Format(CultureInfo.CurrentCulture, Localization.GetString("ModuleUnavailable"), moduleTitle);
            }

            ProcessModuleLoadException(friendlyMessage, ctrl, exc, displayErrorMessage);
        }

        /// <summary>Processes the module load exception.</summary>
        /// <param name="friendlyMessage">The friendly message.</param>
        /// <param name="ctrl">The control.</param>
        /// <param name="exc">The exception.</param>
        public static void ProcessModuleLoadException(string friendlyMessage, IComponent ctrl, Exception exc)
        {
            // Exit Early if ThreadAbort Exception
            if (ThreadAbortCheck(exc))
            {
                return;
            }

            ProcessModuleLoadException(friendlyMessage, ctrl, exc, true);
        }

        /// <summary>Processes the module load exception.</summary>
        /// <param name="friendlyMessage">The friendly message.</param>
        /// <param name="ctrl">The control.</param>
        /// <param name="exc">The exception.</param>
        /// <param name="displayErrorMessage">if set to <see langword="true"/> display error message.</param>
        public static void ProcessModuleLoadException(string friendlyMessage, IComponent ctrl, Exception exc, bool displayErrorMessage)
        {
            // Exit Early if ThreadAbort Exception
            if (ThreadAbortCheck(exc))
            {
                return;
            }

            try
            {
                if (!Host.UseCustomErrorMessages)
                {
                    throw new ModuleLoadException(friendlyMessage, exc);
                }
                else
                {
                    var ctrlModule = ctrl as IModuleControl;
                    ModuleLoadException lex = null;
                    var friendlyMessageOverride = exc.Message;

                    if (ctrlModule == null)
                    {
                        lex = new ModuleLoadException(friendlyMessageOverride, exc);
                    }
                    else
                    {
                        lex = new ModuleLoadException(friendlyMessageOverride, exc, ctrlModule.ModuleContext.Configuration);
                    }

                    // publish the exception
                    var objExceptionLog = new ExceptionLogController();
                    objExceptionLog.AddLog(lex);

                    // Some modules may want to suppress an error message
                    // and just log the exception.
                    if (displayErrorMessage)
                    {
                        // The message placeholder (injected by UI.Modules.ModuleHost) is found on the
                        // DNN component tree; a module control that is not part of that tree cannot
                        // host the message, so the exception is logged above and rethrown here.
                        var treeControl = ctrl as IDnnControl;
                        var errorPlaceholder = treeControl?.Parent?.FindControl("MessagePlaceHolder") as IDnnControl;

                        if (errorPlaceholder != null)
                        {
                            // hide the module
                            treeControl.Visible = false;
                            errorPlaceholder.Visible = true;
                            errorPlaceholder.Controls.Add(new ErrorContainer(friendlyMessage, lex).Container);
                        }
                        else if (treeControl != null)
                        {
                            // there's no ErrorPlaceholder, add it to the module's control collection
                            treeControl.Controls.Add(new ErrorContainer(friendlyMessage, lex).Container);
                        }
                        else
                        {
                            throw lex;
                        }
                    }
                }
            }
            catch (Exception exc2)
            {
                Logger.ExceptionsProcessModuleLoadExceptionUnexpectedException(exc2);
                ProcessPageLoadException(exc2);
            }

            Logger.ExceptionsProcessModuleLoadException(exc, friendlyMessage, ctrl);
        }

        /// <summary>Processes the page load exception.</summary>
        /// <param name="exc">The exception.</param>
        public static void ProcessPageLoadException(Exception exc)
        {
            string appURL = Globals.ApplicationURL();
            if (appURL.IndexOf("?", StringComparison.Ordinal) == Null.NullInteger)
            {
                appURL += "?def=ErrorMessage";
            }
            else
            {
                appURL += "&def=ErrorMessage";
            }

            ProcessPageLoadException(exc, appURL);
        }

        /// <summary>Processes the page load exception.</summary>
        /// <param name="exc">The exception.</param>
        /// <param name="url">The URL.</param>
        public static void ProcessPageLoadException(Exception exc, string url)
        {
            Logger.ExceptionsProcessPageLoadExceptionWithUrl(exc, url);
            if (ThreadAbortCheck(exc))
            {
                return;
            }

            if (!Host.UseCustomErrorMessages)
            {
                throw new PageLoadException(exc == null ? string.Empty : exc.Message, exc);
            }
            else
            {
                var lex = new PageLoadException(exc == null ? string.Empty : exc.Message, exc);

                // publish the exception
                var objExceptionLog = new ExceptionLogController();
                objExceptionLog.AddLog(lex);
                if (!string.IsNullOrEmpty(url))
                {
                    // redirect
                    var context = HttpContextSource.Current;
                    if (context == null)
                    {
                        // No request to redirect (e.g. scheduler/background thread). The exception is
                        // logged above; rethrow so the caller is not left believing it was handled.
                        throw lex;
                    }

                    if (url.Contains("error=terminate", StringComparison.OrdinalIgnoreCase))
                    {
                        // Server.Transfer has no ASP.NET Core equivalent. Matching Framework/PageBase,
                        // the error page is reached by a redirect instead of an in-process transfer.
                        context.Response.Clear();
                        context.Response.Redirect(Globals.ResolveUrl(DotNetNuke.Common.ErrorPage.Path));
                    }
                    else
                    {
                        context.Response.Redirect(url);
                    }
                }
            }
        }

        /// <summary>Logs a <see cref="ModuleLoadException"/>.</summary>
        /// <param name="exc">The exception.</param>
        public static void LogException(ModuleLoadException exc)
        {
            Logger.ExceptionsLogModuleLoadException(exc);
            var objExceptionLog = new ExceptionLogController();
            objExceptionLog.AddLog(exc, ExceptionLogController.ExceptionLogType.MODULE_LOAD_EXCEPTION);
        }

        /// <summary>Logs a <see cref="PageLoadException"/>.</summary>
        /// <param name="exc">The exception.</param>
        public static void LogException(PageLoadException exc)
        {
            Logger.ExceptionsLogPageLoadException(exc);
            var objExceptionLog = new ExceptionLogController();
            objExceptionLog.AddLog(exc, ExceptionLogController.ExceptionLogType.PAGE_LOAD_EXCEPTION);
        }

        /// <summary>Logs a <see cref="SchedulerException"/>.</summary>
        /// <param name="exc">The exception.</param>
        public static void LogException(SchedulerException exc)
        {
            Logger.ExceptionsLogSchedulerException(exc);
            var objExceptionLog = new ExceptionLogController();
            objExceptionLog.AddLog(exc, ExceptionLogController.ExceptionLogType.SCHEDULER_EXCEPTION);
        }

        /// <summary>Logs a <see cref="SecurityException"/>.</summary>
        /// <param name="exc">The exception.</param>
        public static void LogException(SecurityException exc)
        {
            Logger.ExceptionsLogSecurityException(exc);
            var objExceptionLog = new ExceptionLogController();
            objExceptionLog.AddLog(exc, ExceptionLogController.ExceptionLogType.SECURITY_EXCEPTION);
        }

        /// <summary>Logs an exception.</summary>
        /// <param name="exc">The exception.</param>
        public static void LogException(Exception exc)
        {
            Logger.ExceptionsLogGeneralException(exc);
            var objExceptionLog = new ExceptionLogController();
            objExceptionLog.AddLog(exc, ExceptionLogController.ExceptionLogType.GENERAL_EXCEPTION);
        }

        /// <summary>Processes the scheduler exception.</summary>
        /// <param name="exc">The exception.</param>
        public static void ProcessSchedulerException(Exception exc)
        {
            Logger.ExceptionsProcessSchedulerException(exc);
            var objExceptionLog = new ExceptionLogController();
            objExceptionLog.AddLog(exc, ExceptionLogController.ExceptionLogType.SCHEDULER_EXCEPTION);
        }

        /// <summary>Logs the search exception.</summary>
        /// <param name="exc">The exception.</param>
        public static void LogSearchException(SearchException exc)
        {
            Logger.ExceptionsLogSearchException(exc);
            var objExceptionLog = new ExceptionLogController();
            objExceptionLog.AddLog(exc, ExceptionLogController.ExceptionLogType.SEARCH_INDEXER_EXCEPTION);
        }

        /// <summary>Resets a thread abort if the exception is a <see cref="ThreadAbortException"/>.</summary>
        /// <param name="exc">The exception.</param>
        /// <returns><see langword="true"/> if <paramref name="exc"/> is a <see cref="ThreadAbortException"/> instance, otherwise <see langword="false"/>.</returns>
        private static bool ThreadAbortCheck(Exception exc)
        {
            if (exc is ThreadAbortException)
            {
                // Thread.ResetAbort is not supported on modern .NET (SYSLIB0006) and throws
                // PlatformNotSupportedException; thread aborts no longer exist, so there is nothing
                // to reset. The detection result is preserved so callers still exit early.
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void ProcessHttpException(BadHttpRequestException exc, string url)
        {
            var notFoundErrorString = Localization.GetString("ResourceNotFound", Localization.SharedResourceFile);
            Logger.ExceptionsProcessHttpException(exc, notFoundErrorString, url);

            var log = new LogInfo
            {
                BypassBuffering = true,
                LogTypeKey = nameof(EventLogType.HOST_ALERT),
            };
            log.LogProperties.Add(new LogDetailInfo(notFoundErrorString, "URL"));
            var context = HttpContextSource.Current;
            if (context != null)
            {
                log.LogProperties.Add(new LogDetailInfo("URL:", url));
            }

            LogController.Instance.AddLog(log);

            throw exc;
        }
    }
}
