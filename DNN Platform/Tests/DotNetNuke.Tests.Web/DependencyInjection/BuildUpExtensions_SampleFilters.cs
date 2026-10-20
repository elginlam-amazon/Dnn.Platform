// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Tests.Web.DependencyInjection
{
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.DependencyInjection;

    using Microsoft.AspNetCore.Mvc.Filters;

    public partial class BuildUpExtensionsTests
    {
        // On .NET Framework these sample filters derived from System.Web.Http.Filters.ActionFilterAttribute
        // and overrode OnActionExecuted(HttpActionExecutedContext). DotNetNuke.Web/Api now runs on
        // ASP.NET Core, and BuildUp operates on Microsoft.AspNetCore.Mvc.Filters.IFilterMetadata, so the
        // sample filters implement IActionFilter instead. The injected [Dependency] property behavior is
        // unchanged.
        private class PrivateFilter : IActionFilter
        {
            [Dependency]
            private IEventLogger EventLogger { get; set; }

            public void OnActionExecuting(ActionExecutingContext context)
            {
            }

            public void OnActionExecuted(ActionExecutedContext context)
            {
                this.EventLogger.AddLog("Test", "Message", EventLogType.ADMIN_ALERT);
            }
        }

        private class ProtectedFilter : IActionFilter
        {
            [Dependency]
            protected IEventLogger EventLogger { get; set; }

            public void OnActionExecuting(ActionExecutingContext context)
            {
            }

            public void OnActionExecuted(ActionExecutedContext context)
            {
                this.EventLogger.AddLog("Test", "Message", EventLogType.ADMIN_ALERT);
            }
        }

        private class ProtectedInternalFilter : IActionFilter
        {
            [Dependency]
            protected internal IEventLogger EventLogger { get; set; }

            public void OnActionExecuting(ActionExecutingContext context)
            {
            }

            public void OnActionExecuted(ActionExecutedContext context)
            {
                this.EventLogger.AddLog("Test", "Message", EventLogType.ADMIN_ALERT);
            }
        }

        private class InternalFilter : IActionFilter
        {
            [Dependency]
            internal IEventLogger EventLogger { get; set; }

            public void OnActionExecuting(ActionExecutingContext context)
            {
            }

            public void OnActionExecuted(ActionExecutedContext context)
            {
                this.EventLogger.AddLog("Test", "Message", EventLogType.ADMIN_ALERT);
            }
        }

        private class PublicFilter : IActionFilter
        {
            [Dependency]
            public IEventLogger EventLogger { get; set; }

            public void OnActionExecuting(ActionExecutingContext context)
            {
            }

            public void OnActionExecuted(ActionExecutedContext context)
            {
                this.EventLogger.AddLog("Test", "Message", EventLogType.ADMIN_ALERT);
            }
        }

        private class PublicNoDependencyAttributeFilter : IActionFilter
        {
            public IEventLogger EventLogger { get; set; }

            public void OnActionExecuting(ActionExecutingContext context)
            {
            }

            public void OnActionExecuted(ActionExecutedContext context)
            {
                // This should throw an exception as EventLogger will be null
                this.EventLogger.AddLog("Test", "Message", EventLogType.ADMIN_ALERT);
            }
        }

        private class NoSetFilter : IActionFilter
        {
            [Dependency]
            public IEventLogger EventLogger { get; }

            public void OnActionExecuting(ActionExecutingContext context)
            {
            }

            public void OnActionExecuted(ActionExecutedContext context)
            {
                this.EventLogger.AddLog("Test", "Message", EventLogType.ADMIN_ALERT);
            }
        }
    }
}
