// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.HttpModules.Analytics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common;
    using DotNetNuke.HttpModules.Config;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.Services.Analytics;
    using DotNetNuke.Services.Log.EventLog;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Injects the configured web analytics scripts into the rendered HTML page. This is the
    /// ASP.NET Core replacement for the legacy <c>AnalyticsModule</c> <c>IHttpModule</c>.
    /// </summary>
    /// <remarks>
    /// The WebForms module hooked <c>CDefault.InitComplete</c>/<c>PreRender</c> and added a
    /// <c>LiteralControl</c> to the control found by <c>page.FindControl(engine.ElementId)</c>.
    /// On ASP.NET Core there is no control tree, so the rendered HTML response is buffered and
    /// each engine's script is inserted into the element identified by
    /// <see cref="AnalyticsEngine.ElementId"/>: at the start of that element when
    /// <see cref="AnalyticsEngine.InjectTop"/> is <see langword="true"/> (the former
    /// <c>Controls.AddAt(0, …)</c> during <c>InitComplete</c>) and at its end otherwise (the
    /// former <c>Controls.Add(…)</c> during <c>PreRender</c>).
    /// </remarks>
    public class DnnAnalyticsMiddleware
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<DnnAnalyticsMiddleware>();

        private readonly RequestDelegate next;
        private readonly IEventLogger eventLogger;
        private readonly IApplicationStatusInfo appStatus;

        /// <summary>Initializes a new instance of the <see cref="DnnAnalyticsMiddleware"/> class.</summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="eventLogger">The event logger.</param>
        /// <param name="appStatus">The application status.</param>
        public DnnAnalyticsMiddleware(RequestDelegate next, IEventLogger eventLogger, IApplicationStatusInfo appStatus)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.eventLogger = eventLogger ?? Globals.GetCurrentServiceProvider().GetRequiredService<IEventLogger>();
            this.appStatus = appStatus ?? Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>();
        }

        /// <summary>Runs the request and injects the analytics scripts into the rendered HTML.</summary>
        /// <param name="context">The current request.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            List<AnalyticsEngine> engines = null;
            try
            {
                // First check if we are upgrading/installing or if it is a non-page request.
                if (Initialize.ProcessHttpModule(context.Request, false, false))
                {
                    engines = this.GetConfiguredEngines();
                }
            }
            catch (Exception ex)
            {
                var log = new LogInfo { LogTypeKey = nameof(EventLogType.HOST_ALERT) };
                log.AddProperty("Analytics.AnalyticsModule", "OnPreRequestHandlerExecute");
                log.AddProperty("ExceptionMessage", ex.Message);
                this.eventLogger.AddLog(log);
                Logger.AnalyticsModuleOnPreRequestHandlerExecuteException(ex, log);
            }

            if (engines == null || engines.Count == 0)
            {
                await this.next(context).ConfigureAwait(false);
                return;
            }

            var originalBody = context.Response.Body;
            using var buffer = new MemoryStream();
            try
            {
                context.Response.Body = buffer;
                await this.next(context).ConfigureAwait(false);
            }
            finally
            {
                context.Response.Body = originalBody;
            }

            buffer.Position = 0;
            var contentType = context.Response.ContentType;
            var isHtml = !string.IsNullOrEmpty(contentType) &&
                         contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase);
            if (!isHtml)
            {
                await buffer.CopyToAsync(originalBody, context.RequestAborted).ConfigureAwait(false);
                return;
            }

            var html = Encoding.UTF8.GetString(buffer.ToArray());
            var injected = this.InjectAnalyticsScripts(html, engines);
            var bytes = Encoding.UTF8.GetBytes(injected);

            // The body length changed, so any pre-computed length is no longer valid.
            context.Response.ContentLength = bytes.Length;
            await originalBody.WriteAsync(bytes.AsMemory(0, bytes.Length), context.RequestAborted).ConfigureAwait(false);
        }

        /// <summary>Finds the HTML element with the given ID (or tag name) in <paramref name="html"/>.</summary>
        /// <param name="html">The rendered HTML.</param>
        /// <param name="elementId">The element ID the analytics engine targets.</param>
        /// <returns>The match of the element's opening tag, or <see langword="null"/> when it was not found.</returns>
        /// <remarks>
        /// The WebForms equivalent was <c>page.FindControl(engine.ElementId)</c> on an
        /// <c>HtmlContainerControl</c>; such a control renders an element whose <c>id</c>
        /// attribute is its client ID. DNN's default skins use <c>&lt;head id="Head"&gt;</c> /
        /// <c>&lt;body id="Body"&gt;</c>, so when no <c>id</c> matches we also accept an element
        /// whose tag name is the requested ID.
        /// </remarks>
        private static Match FindElement(string html, string elementId)
        {
            var escapedId = Regex.Escape(elementId);
            var byId = Regex.Match(
                html,
                "<(?<tag>[a-zA-Z][a-zA-Z0-9]*)(?=[^>]*\\sid\\s*=\\s*[\"']?" + escapedId + "[\"'\\s>])[^>]*>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (byId.Success)
            {
                return byId;
            }

            // REVIEW: behavior inferred from the DNN default skins — verify for custom skins.
            var byTag = Regex.Match(
                html,
                "<(?<tag>" + escapedId + ")(\\s[^>]*)?>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return byTag.Success ? byTag : null;
        }

        /// <summary>Inserts <paramref name="script"/> into the element identified by <paramref name="elementId"/>.</summary>
        /// <param name="html">The rendered HTML.</param>
        /// <param name="elementId">The element the script belongs to.</param>
        /// <param name="script">The rendered analytics script.</param>
        /// <param name="injectTop">
        /// <see langword="true"/> to insert the script as the element's first content,
        /// <see langword="false"/> to append it as its last content.
        /// </param>
        /// <returns>The HTML with the script inserted, or the input when the element was not found.</returns>
        private static string InjectIntoElement(string html, string elementId, string script, bool injectTop)
        {
            var element = FindElement(html, elementId);
            if (element == null)
            {
                return html;
            }

            if (injectTop)
            {
                return html.Insert(element.Index + element.Length, script);
            }

            var tag = element.Groups["tag"].Value;
            var closeTag = "</" + tag;
            var closeIndex = html.IndexOf(closeTag, element.Index + element.Length, StringComparison.OrdinalIgnoreCase);
            if (closeIndex < 0)
            {
                // Unclosed (or self-closing) element: the end of the element is the end of the document.
                return html + script;
            }

            return html.Insert(closeIndex, script);
        }

        /// <summary>Gets the configured analytics engines that have an element to inject into.</summary>
        /// <returns>The engines to inject, never <see langword="null"/>.</returns>
        private List<AnalyticsEngine> GetConfiguredEngines()
        {
            var engines = new List<AnalyticsEngine>();
            var analyticsEngines = AnalyticsEngineConfiguration.GetConfig(this.appStatus, this.eventLogger).AnalyticsEngines;
            if (analyticsEngines == null || analyticsEngines.Count == 0)
            {
                return engines;
            }

            foreach (AnalyticsEngine engine in analyticsEngines)
            {
                if (!string.IsNullOrEmpty(engine.ElementId))
                {
                    engines.Add(engine);
                }
            }

            return engines;
        }

        /// <summary>Renders and injects every configured engine's script into the page HTML.</summary>
        /// <param name="html">The rendered HTML.</param>
        /// <param name="engines">The configured engines.</param>
        /// <returns>The HTML with every analytics script injected.</returns>
        private string InjectAnalyticsScripts(string html, List<AnalyticsEngine> engines)
        {
            try
            {
                foreach (var engine in engines)
                {
                    AnalyticsEngineBase objEngine;
                    if (!string.IsNullOrEmpty(engine.EngineType))
                    {
                        var engineType = Type.GetType(engine.EngineType);
                        if (engineType == null)
                        {
                            objEngine = new GenericAnalyticsEngine();
                        }
                        else
                        {
                            objEngine = (AnalyticsEngineBase)Activator.CreateInstance(engineType);
                        }
                    }
                    else
                    {
                        objEngine = new GenericAnalyticsEngine();
                    }

                    if (objEngine == null)
                    {
                        continue;
                    }

                    var script = engine.ScriptTemplate;
                    if (string.IsNullOrEmpty(script))
                    {
                        continue;
                    }

                    script = objEngine.RenderScript(script);
                    if (string.IsNullOrEmpty(script))
                    {
                        continue;
                    }

                    html = InjectIntoElement(html, engine.ElementId, script, engine.InjectTop);
                }
            }
            catch (Exception ex)
            {
                var log = new LogInfo { LogTypeKey = nameof(EventLogType.HOST_ALERT) };
                log.AddProperty("Analytics.AnalyticsModule", "OnPagePreRender");
                log.AddProperty("ExceptionMessage", ex.Message);
                this.eventLogger.AddLog(log);
                Logger.AnalyticsModuleOnPagePreRenderException(ex);
            }

            return html;
        }
    }
}
