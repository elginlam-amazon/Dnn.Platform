// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Installer.Log
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Instrumentation;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.Logging;

    /// <summary>The Logger class provides an Installer Log.</summary>
    public partial class Logger
    {
        private static readonly ILogger DnnLogger = DnnLoggingController.GetLogger<Logger>();
        private readonly List<LogEntry> logs;
        private string errorClass;
        private bool hasWarnings;
        private string highlightClass;
        private string normalClass;
        private bool valid;

        /// <summary>Initializes a new instance of the <see cref="Logger"/> class.</summary>
        public Logger()
        {
            this.logs = new List<LogEntry>();

            this.valid = true;
            this.hasWarnings = Null.NullBoolean;
        }

        public bool HasWarnings
        {
            get
            {
                return this.hasWarnings;
            }
        }

        /// <summary>Gets a List of Log Entries.</summary>
        public IList<LogEntry> Logs
        {
            get
            {
                return this.logs;
            }
        }

        /// <summary>Gets a value indicating whether the Installation was Valid.</summary>
        public bool Valid
        {
            get
            {
                return this.valid;
            }
        }

        /// <summary>Gets or sets the CSS class used for Error Log Entries.</summary>
        public string ErrorClass
        {
            get
            {
                if (string.IsNullOrEmpty(this.errorClass))
                {
                    this.errorClass = "NormalRed";
                }

                return this.errorClass;
            }

            set
            {
                this.errorClass = value;
            }
        }

        /// <summary>Gets or sets the CSS class used for Log Entries that should be highlighted.</summary>
        public string HighlightClass
        {
            get
            {
                if (string.IsNullOrEmpty(this.highlightClass))
                {
                    this.highlightClass = "NormalBold";
                }

                return this.highlightClass;
            }

            set
            {
                this.highlightClass = value;
            }
        }

        /// <summary>Gets or sets the CSS class used for normal Log Entries.</summary>
        public string NormalClass
        {
            get
            {
                if (string.IsNullOrEmpty(this.normalClass))
                {
                    this.normalClass = "Normal";
                }

                return this.normalClass;
            }

            set
            {
                this.normalClass = value;
            }
        }

        /// <summary>The AddFailure method adds a new LogEntry of type Failure to the Logs collection.</summary>
        /// <remarks>This method also sets the Valid flag to false.</remarks>
        /// <param name="failure">The description of the LogEntry.</param>
        public void AddFailure(string failure)
        {
            this.logs.Add(new LogEntry(LogType.Failure, failure));
            DnnLogger.InstallLoggerLogFailure(failure);
            this.valid = false;
        }

        public void AddFailure(Exception ex)
        {
            this.AddFailure(Util.EXCEPTION + ex);
            Exceptions.Exceptions.LogException(ex);
        }

        /// <summary>The AddInfo method adds a new LogEntry of type Info to the Logs collection.</summary>
        /// <param name="info">The description of the LogEntry.</param>
        public void AddInfo(string info)
        {
            this.logs.Add(new LogEntry(LogType.Info, info));
            DnnLogger.InstallLoggerLogInfo(info);
        }

        /// <summary>The AddWarning method adds a new LogEntry of type Warning to the Logs collection.</summary>
        /// <param name="warning">The description of the LogEntry.</param>
        public void AddWarning(string warning)
        {
            this.logs.Add(new LogEntry(LogType.Warning, warning));
            DnnLogger.InstallLoggerLogWarning(warning);
            this.hasWarnings = true;
        }

        /// <summary>The EndJob method adds a new LogEntry of type EndJob to the Logs collection.</summary>
        /// <param name="job">The description of the LogEntry.</param>
        public void EndJob(string job)
        {
            this.logs.Add(new LogEntry(LogType.EndJob, job));
            DnnLogger.InstallLoggerLogInfo(job);
        }

        /// <summary>GetLogsTable formats log entries as an HTML table.</summary>
        /// <returns>The log entries rendered as an HTML <c>&lt;table&gt;</c>, ready to be emitted into a Blazor component.</returns>
        /// <remarks>
        /// On .NET Framework this returned a <c>System.Web.UI.HtmlControls.HtmlTable</c> control
        /// that callers added to a <c>PlaceHolder</c>. There are no WebForms controls on modern
        /// .NET, so the same table is produced as markup; render it with <c>@logger.GetLogsTable()</c>.
        /// Cell text is HTML-encoded exactly as <c>HtmlTableCell.InnerText</c> used to be.
        /// </remarks>
        public MarkupString GetLogsTable()
        {
            return new MarkupString(this.GetLogsTableHtml());
        }

        /// <summary>GetLogsTableHtml formats log entries as an HTML table.</summary>
        /// <returns>The log entries rendered as an HTML <c>&lt;table&gt;</c>.</returns>
        public string GetLogsTableHtml()
        {
            var table = new StringBuilder();
            table.Append("<table>");
            foreach (LogEntry entry in this.Logs)
            {
                string cssClass = null;
                switch (entry.Type)
                {
                    case LogType.Failure:
                    case LogType.Warning:
                        cssClass = this.ErrorClass;
                        break;
                    case LogType.StartJob:
                    case LogType.EndJob:
                        cssClass = this.HighlightClass;
                        break;
                    case LogType.Info:
                        cssClass = this.NormalClass;
                        break;
                }

                var classAttribute = string.IsNullOrEmpty(cssClass)
                    ? string.Empty
                    : " class=\"" + WebUtility.HtmlEncode(cssClass) + "\"";

                table.Append("<tr>");
                table.Append("<td").Append(classAttribute).Append('>')
                    .Append(WebUtility.HtmlEncode(Util.GetLocalizedString("LOG.PALogger." + entry.Type)))
                    .Append("</td>");
                table.Append("<td").Append(classAttribute).Append('>')
                    .Append(WebUtility.HtmlEncode(entry.Description))
                    .Append("</td>");
                table.Append("</tr>");

                if (entry.Type == LogType.EndJob)
                {
                    table.Append("<tr><td colspan=\"2\">&nbsp;</td></tr>");
                }
            }

            table.Append("</table>");

            return table.ToString();
        }

        public void ResetFlags()
        {
            this.valid = true;
        }

        /// <summary>The StartJob method adds a new LogEntry of type StartJob to the Logs collection.</summary>
        /// <param name="job">The description of the LogEntry.</param>
        public void StartJob(string job)
        {
            this.logs.Add(new LogEntry(LogType.StartJob, job));
            DnnLogger.InstallLoggerLogInfo(job);
        }
    }
}
