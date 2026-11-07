// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace Dnn.PersonaBar.SqlConsole.Components
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Common;
    using DotNetNuke.Data;
    using DotNetNuke.Framework;

    public class SqlQueryController : ServiceLocator<ISqlQueryController, SqlQueryController>, ISqlQueryController
    {
        /// <inheritdoc />
        public void AddQuery(SqlQuery query)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<SqlQuery>();
                rep.Insert(query);
            }
        }

        /// <inheritdoc />
        public void DeleteQuery(SqlQuery query)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<SqlQuery>();
                rep.Delete(query);
            }
        }

        /// <inheritdoc />
        public IEnumerable<SqlQuery> GetQueries()
        {
            IEnumerable<SqlQuery> queries;

            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<SqlQuery>();
                queries = rep.Get();
            }

            return queries;
        }

        /// <inheritdoc />
        public SqlQuery GetQuery(int id)
        {
            SqlQuery query;

            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<SqlQuery>();
                query = rep.GetById(id);
            }

            return query;
        }

        /// <inheritdoc />
        public SqlQuery GetQuery(string name)
        {
            List<SqlQuery> queries;
            SqlQuery query = null;

            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<SqlQuery>();
                queries = rep.Find("where name = @0", name).ToList();
                if (queries != null && queries.Count > 0)
                {
                    query = queries.ElementAt(0);
                }
            }

            return query;
        }

        /// <inheritdoc />
        public void UpdateQuery(SqlQuery query)
        {
            using (IDataContext ctx = DataContext.Instance())
            {
                var rep = ctx.GetRepository<SqlQuery>();
                rep.Update(query);
            }
        }

        /// <inheritdoc />
        public IList<string> GetConnections()
        {
            var connections = new List<string>();

            // Enumerates the configured connection strings through ConfigurationSource, the platform's
            // registered IConfiguration seam, which replaces ConfigurationManager.ConnectionStrings.
            // Each child key of the "ConnectionStrings" section is a connection string name, exactly
            // as ConnectionStringSettings.Name was; the two local provider templates stay excluded.
            foreach (var connection in ConfigurationSource.GetSection("ConnectionStrings").GetChildren())
            {
                if (!connection.Key.Equals("localmysqlserver", StringComparison.OrdinalIgnoreCase) &&
                    !connection.Key.Equals("localsqlserver", StringComparison.OrdinalIgnoreCase))
                {
                    connections.Add(connection.Key);
                }
            }

            return connections;
        }

        /// <inheritdoc />
        protected override Func<ISqlQueryController> GetFactory()
        {
            return () => new SqlQueryController();
        }
    }
}
