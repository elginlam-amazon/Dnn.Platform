// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.HttpModules.Exceptions
{
    using System;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Log.EventLog;

    using Microsoft.AspNetCore.Diagnostics;
    using Microsoft.AspNetCore.Http;

    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Logs the exceptions that nothing else handled. Replaces the <c>Application_Error</c>
    /// handler of the legacy <c>ExceptionModule</c> HTTP module.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is no <c>HttpServerUtility.GetLastError()</c> on ASP.NET Core. The failure is read
    /// from <see cref="IExceptionHandlerFeature"/> when this middleware runs inside the error
    /// handling branch installed by <c>UseDnnExceptionHandler</c>; when it is installed in the
    /// normal pipeline it observes the exception as it propagates and re-throws it so that the
    /// error handler still produces the error page.
    /// </para>
    /// <para>
    /// Both positions are supported on purpose, and an <see cref="HttpContext.Items"/> marker
    /// makes sure a single failure is never logged twice.
    /// </para>
    /// </remarks>
    public class DnnUnhandledExceptionMiddleware
    {
        private const string LoggedKey = "DNN_UnhandledExceptionLogged";

        private static readonly ILogger Logger = DnnLoggingController.GetLogger<DnnUnhandledExceptionMiddleware>();

        private readonly RequestDelegate next;

        /// <summary>Initializes a new instance of the <see cref="DnnUnhandledExceptionMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public DnnUnhandledExceptionMiddleware(RequestDelegate next)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>Logs an unhandled exception the way the legacy <c>ExceptionModule</c> did.</summary>
        /// <param name="context">The current request.</param>
        /// <param name="exception">The failure to log.</param>
        public static void LogUnhandledException(HttpContext context, Exception exception)
        {
            try
            {
                if (context == null || exception == null)
                {
                    return;
                }

                if (context.Items.ContainsKey(LoggedKey))
                {
                    return;
                }

                if (!Initialize.ProcessHttpModule(context.Request, false, false))
                {
                    return;
                }

                if (IsHttpStatusException(exception))
                {
                    // HttpExceptions are logged elsewhere
                    return;
                }

                context.Items[LoggedKey] = true;

                var lex = new UnhandledException("Unhandled Error: ", exception);
                var objExceptionLog = new ExceptionLogController();
                try
                {
                    objExceptionLog.AddLog(lex);
                }
                catch (Exception ex)
                {
                    Logger.ExceptionModuleAddLogException(ex);
                }
            }
            catch (Exception exc)
            {
                // it is possible when terminating the request for the context not to exist
                // in this case we just want to exit since there is nothing else we can do
                Logger.ExceptionModuleOnErrorRequestException(exc);
            }
        }

        /// <summary>Logs the unhandled exception for the current request.</summary>
        /// <param name="context">The current request.</param>
        /// <returns>A task that completes when the request has been handled.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            // When installed in the error-handling branch the exception is already available.
            var handlerFeature = context.Features.Get<IExceptionHandlerPathFeature>() as IExceptionHandlerFeature
                                 ?? context.Features.Get<IExceptionHandlerFeature>();
            if (handlerFeature?.Error != null)
            {
                LogUnhandledException(context, handlerFeature.Error);
                await this.next(context).ConfigureAwait(false);
                return;
            }

            try
            {
                await this.next(context).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LogUnhandledException(context, exception);
                throw;
            }
        }

        /// <summary>
        /// The replacement for the legacy <c>lastException is HttpException</c> test: an
        /// exception which only means "answer with this HTTP status code".
        /// </summary>
        /// <param name="exception">The failure to classify.</param>
        /// <returns><see langword="true"/> when the exception carries an HTTP status rather than an application failure.</returns>
        private static bool IsHttpStatusException(Exception exception)
        {
            return exception is BadHttpRequestException
                || exception is OperationCanceledException
                || exception.GetType().FullName == "Microsoft.AspNetCore.Http.BadHttpRequestException"
                || exception.GetType().FullName == "Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException";
        }
    }
}
