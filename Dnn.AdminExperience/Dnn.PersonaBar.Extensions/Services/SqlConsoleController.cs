// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.SqlConsole.Services
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;

    using Dnn.PersonaBar.Library;
    using Dnn.PersonaBar.Library.Attributes;
    using Dnn.PersonaBar.SqlConsole.Components;

    using DotNetNuke.Abstractions.Logging;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Data;
    using DotNetNuke.Services.Log.EventLog;
    using DotNetNuke.Web.Api;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Data.SqlClient;

    using ValidateAntiForgeryToken = DotNetNuke.Web.Api.ValidateAntiForgeryTokenAttribute;

    [MenuPermission(Scope = ServiceScope.Host)]
    public class SqlConsoleController : PersonaBarApiController
    {
        private const string ScriptDelimiterRegex = "(?<=(?:[^\\w]+|^))GO(?=(?: |\\t)*?(?:\\r?\\n|$))";

        private static readonly Regex SqlObjRegex = new Regex(
            ScriptDelimiterRegex,
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private ISqlQueryController controller = SqlQueryController.Instance;

        ////private const int MaxOutputRecords = 500;
        [HttpGet]
        public IActionResult GetSavedQueries()
        {
            return this.Ok(new
            {
                queries = this.controller.GetQueries(),
                connections = this.controller.GetConnections(),
            });
        }

        [HttpGet]
        public IActionResult GetSavedQuery(int id)
        {
            return this.Ok(this.controller.GetQuery(id));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveQuery(SqlQuery query)
        {
            query.CreatedOnDate = DateTime.Now;
            query.CreatedByUserId = this.UserInfo.UserID;
            query.LastModifiedOnDate = DateTime.Now;
            query.LastModifiedByUserId = this.UserInfo.UserID;

            if (query.QueryId <= 0)
            {
                var saveQueries = this.controller.GetQueries();
                var saveQuery = saveQueries.FirstOrDefault(q => q.Name.Equals(query.Name, StringComparison.OrdinalIgnoreCase));
                if (saveQuery != null)
                {
                    query.QueryId = saveQuery.QueryId;
                }
            }

            if (query.QueryId > 0)
            {
                this.controller.UpdateQuery(query);
            }
            else
            {
                this.controller.AddQuery(query);
            }

            return this.Ok(query);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteQuery(SqlQuery query)
        {
            var savedQuery = this.controller.GetQuery(query.QueryId);
            if (savedQuery != null)
            {
                this.controller.DeleteQuery(savedQuery);

                return this.Ok(new { });
            }

            return DnnApiResults.Response(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RunQuery(AdhocSqlQuery query)
        {
            var connectionstring = Config.GetConnectionString(query.ConnectionStringName);

            var outputTables = new List<DataTable>();
            string errorMessage;

            var runAsQuery = RunAsScript(query.Query);
            if (runAsQuery)
            {
                errorMessage = DataProvider.Instance().ExecuteScript(connectionstring, query.Query, query.Timeout);
            }
            else
            {
                try
                {
                    var dr = DataProvider.Instance().ExecuteSQLTemp(connectionstring, query.Query, query.Timeout, out errorMessage);
                    if (dr != null)
                    {
                        do
                        {
                            var table = new DataTable { Locale = CultureInfo.CurrentCulture };
                            table.Load(dr);
                            outputTables.Add(table);
                        }
                        while (!dr.IsClosed);
                    }
                }
                catch (SqlException sqlException)
                {
                    errorMessage = sqlException.Message;
                }
            }

            this.RecordAuditEventLog(query.Query);

            var statusCode = string.IsNullOrEmpty(errorMessage) ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
            return DnnApiResults.Response(statusCode, new { Data = runAsQuery ? null : outputTables, Error = errorMessage });
        }

        private static bool RunAsScript(string query)
        {
            return SqlObjRegex.IsMatch(query);
        }

        private void RecordAuditEventLog(string query)
        {
            var props = new LogProperties { new LogDetailInfo("User", this.UserInfo.Username), new LogDetailInfo("SQL Query", query) };

            // Add the event log with host portal id.
            var log = new LogInfo
            {
                LogUserID = this.UserInfo.UserID,
                LogTypeKey = nameof(EventLogType.HOST_SQL_EXECUTED),
                LogProperties = props,
                BypassBuffering = true,
                LogPortalID = Null.NullInteger,
            };

            LogController.Instance.AddLog(log);
        }
    }
}
