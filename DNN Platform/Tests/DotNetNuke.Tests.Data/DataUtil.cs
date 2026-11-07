// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Data
{
    using System;
    using System.Data;
    using System.Globalization;

    using DotNetNuke.Data;
    using DotNetNuke.Tests.Instance.Utilities;
    using DotNetNuke.Tests.Utilities;

    using Microsoft.Data.SqlClient;

    /// <summary>
    /// Creates, seeds and queries the fixture database used by the data-layer tests.
    /// </summary>
    /// <remarks>
    /// The net48 implementation used SQL Server Compact (a <c>.sdf</c> file opened through
    /// <c>System.Data.SqlServerCe</c>). SQL Server Compact has no build for modern .NET and no
    /// successor package, so the fixture database is now a real SQL Server database created on
    /// the configured server (LocalDB by default) through <c>Microsoft.Data.SqlClient</c> — the
    /// same provider the platform's <c>PetaPocoDataContext</c> and <c>SqlDataProvider</c> use, so
    /// the fixtures and the code under test speak one SQL dialect.
    /// </remarks>
    public class DataUtil
    {
        private const string DefaultServerName = @"(localdb)\MSSQLLocalDB";
        private const string ServerNameSettingKey = "TestDatabaseServer";

        /// <summary>Gets the SQL Server instance that hosts the fixture databases.</summary>
        /// <remarks>
        /// Configurable through the <c>TestDatabaseServer</c> setting (<c>appsettings.json</c> or
        /// an environment variable) so the suite can run against LocalDB, a container or a shared
        /// server without code changes.
        /// </remarks>
        public static string ServerName => TestConfiguration.GetSetting(ServerNameSettingKey) ?? DefaultServerName;

        /// <summary>Creates an empty fixture database, replacing any existing one.</summary>
        /// <param name="databaseName">The database name.</param>
        public static void CreateDatabase(string databaseName)
        {
            DeleteDatabase(databaseName);

            ExecuteAdminNonQuery(string.Format(
                CultureInfo.InvariantCulture,
                "CREATE DATABASE {0}",
                QuoteName(databaseName)));
        }

        /// <summary>Drops the fixture database if it exists.</summary>
        /// <param name="databaseName">The database name.</param>
        public static void DeleteDatabase(string databaseName)
        {
            // Pooled connections to the database being dropped would keep it in use; the SQL CE
            // implementation got this for free by deleting a file it had already closed.
            SqlConnection.ClearAllPools();

            ExecuteAdminNonQuery(string.Format(
                CultureInfo.InvariantCulture,
                "IF DB_ID({0}) IS NOT NULL BEGIN ALTER DATABASE {1} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {1}; END",
                QuoteLiteral(databaseName),
                QuoteName(databaseName)));
        }

        /// <summary>Runs a non-query script against the fixture database.</summary>
        /// <param name="databaseName">The database name.</param>
        /// <param name="sqlScript">The script to run.</param>
        public static void ExecuteNonQuery(string databaseName, string sqlScript)
        {
            ExecuteNonQuery(databaseName, ReplaceTokens(sqlScript), (cmd) => cmd.ExecuteNonQuery());
        }

        /// <summary>Runs a scalar script against the fixture database.</summary>
        /// <param name="databaseName">The database name.</param>
        /// <param name="sqlScript">The script to run.</param>
        /// <returns>The scalar result as an <see cref="int"/>.</returns>
        public static int ExecuteScalar(string databaseName, string sqlScript)
        {
            return ExecuteQuery(databaseName, ReplaceTokens(sqlScript), (cmd) => ConvertScalar<int>(cmd.ExecuteScalar()));
        }

        /// <summary>Gets the connection string for a fixture database.</summary>
        /// <param name="databaseName">The database name.</param>
        /// <returns>The connection string.</returns>
        public static string GetConnectionString(string databaseName)
        {
            return string.Format(CultureInfo.InvariantCulture, DataResources.ConnectionString, ServerName, databaseName);
        }

        /// <summary>Gets a single field value from a row identified by its primary key.</summary>
        /// <typeparam name="T">The field type.</typeparam>
        /// <param name="databaseName">The database name.</param>
        /// <param name="tableName">The table name.</param>
        /// <param name="field">The field to read.</param>
        /// <param name="primaryKeyField">The primary key field name.</param>
        /// <param name="primaryKeyValue">The primary key value.</param>
        /// <returns>The field value.</returns>
        public static T GetFieldValue<T>(string databaseName, string tableName, string field, string primaryKeyField, string primaryKeyValue)
        {
            return ExecuteQuery(
                databaseName,
                string.Format(CultureInfo.InvariantCulture, DataResources.GetFieldValue, field, tableName, primaryKeyField, primaryKeyValue),
                (cmd) => ConvertScalar<T>(cmd.ExecuteScalar()));
        }

        /// <summary>Gets the highest value of an identity column, i.e. the last inserted id.</summary>
        /// <param name="databaseName">The database name.</param>
        /// <param name="tableName">The table name.</param>
        /// <param name="primaryKeyField">The primary key field name.</param>
        /// <returns>The last added record id.</returns>
        public static int GetLastAddedRecordID(string databaseName, string tableName, string primaryKeyField)
        {
            return ExecuteScalar(databaseName, string.Format(CultureInfo.InvariantCulture, DataResources.GetLastAddedRecordID, tableName, primaryKeyField));
        }

        /// <summary>Gets the number of rows in a table.</summary>
        /// <param name="databaseName">The database name.</param>
        /// <param name="tableName">The table name.</param>
        /// <returns>The record count.</returns>
        public static int GetRecordCount(string databaseName, string tableName)
        {
            return ExecuteScalar(databaseName, string.Format(CultureInfo.InvariantCulture, DataResources.RecordCountScript, tableName));
        }

        /// <summary>Gets the number of rows in a table matching a field value.</summary>
        /// <param name="databaseName">The database name.</param>
        /// <param name="tableName">The table name.</param>
        /// <param name="field">The field to filter on.</param>
        /// <param name="value">The value to filter on.</param>
        /// <returns>The record count.</returns>
        public static int GetRecordCount(string databaseName, string tableName, string field, string value)
        {
            return ExecuteScalar(databaseName, string.Format(CultureInfo.InvariantCulture, DataResources.GetRecordCountByField, tableName, field, value));
        }

        /// <summary>Reads a whole table into a <see cref="DataTable"/>.</summary>
        /// <param name="databaseName">The database name.</param>
        /// <param name="tableName">The table name.</param>
        /// <returns>The table contents.</returns>
        public static DataTable GetTable(string databaseName, string tableName)
        {
            return ExecuteQuery<DataTable>(
                databaseName,
                string.Format(CultureInfo.InvariantCulture, DataResources.GetTable, tableName),
                (cmd) =>
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        var table = new DataTable();
                        table.Locale = CultureInfo.InvariantCulture;
                        table.Load(reader);
                        return table;
                    }
                });
        }

        /// <summary>Creates the fixture database and seeds it with the given number of dogs.</summary>
        /// <param name="count">The number of rows to insert.</param>
        public static void SetUpDatabase(int count)
        {
            string[] dogAges = Constants.PETAPOCO_DogAges.Split(',');
            string[] dogNames = Constants.PETAPOCO_DogNames.Split(',');

            CreateDatabase(Constants.PETAPOCO_DatabaseName);
            ExecuteNonQuery(Constants.PETAPOCO_DatabaseName, Constants.PETAPOCO_CreateDogTableSql);
            var index = 0;
            for (int i = 0; i < count; i++)
            {
                if (index == 5)
                {
                    index = 0;
                }

                ExecuteNonQuery(Constants.PETAPOCO_DatabaseName, string.Format(CultureInfo.InvariantCulture, Constants.PETAPOCO_InsertDogRow, dogNames[index], dogAges[index]));
                index++;
            }
        }

        private static TReturn ExecuteQuery<TReturn>(string databaseName, string sqlScript, Func<SqlCommand, TReturn> command)
        {
            using (var connection = new SqlConnection(GetConnectionString(databaseName)))
            {
                connection.Open();

                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sqlScript;
                    return command(cmd);
                }
            }
        }

        private static void ExecuteNonQuery(string databaseName, string sqlScript, Action<SqlCommand> command)
        {
            using (var connection = new SqlConnection(GetConnectionString(databaseName)))
            {
                connection.Open();

                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sqlScript;
                    command(cmd);
                }
            }
        }

        /// <summary>
        /// Runs a statement that cannot execute inside the fixture database itself (creating or
        /// dropping it), against the server's <c>master</c> database.
        /// </summary>
        /// <param name="sqlScript">The statement to run.</param>
        private static void ExecuteAdminNonQuery(string sqlScript)
        {
            var connectionString = string.Format(CultureInfo.InvariantCulture, DataResources.AdminConnectionString, ServerName);
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = sqlScript;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Converts a scalar result to the requested type. SQL Server returns the CLR type that
        /// matches the column (and <see cref="DBNull"/> for nulls), so the net48 code's direct
        /// <c>(T)</c> unboxing is replaced by a conversion that also copes with a widened
        /// aggregate result such as <c>COUNT</c>/<c>MAX</c>.
        /// </summary>
        /// <typeparam name="T">The requested type.</typeparam>
        /// <param name="value">The raw scalar value.</param>
        /// <returns>The converted value.</returns>
        /// <exception cref="InvalidOperationException">
        /// The query produced no value (or <c>NULL</c>) but <typeparamref name="T"/> is a
        /// non-nullable value type. The net48 <c>(int)</c>/<c>(T)</c> unboxing threw in that
        /// case, and a fixture assertion must keep failing loudly rather than silently reading a
        /// missing row as <c>0</c>.
        /// </exception>
        private static T ConvertScalar<T>(object value)
        {
            if (value is null || value is DBNull)
            {
                var underlyingType = Nullable.GetUnderlyingType(typeof(T));
                if (typeof(T).IsValueType && underlyingType is null)
                {
                    throw new InvalidOperationException(
                        $"The scalar query returned no value, which cannot be represented as {typeof(T).Name}.");
                }

                return default;
            }

            if (value is T typed)
            {
                return typed;
            }

            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            return (T)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        private static string ReplaceTokens(string sql)
        {
            return sql.Replace("{databaseOwner}", DataProvider.Instance().DatabaseOwner)
                        .Replace("{objectQualifier}", DataProvider.Instance().ObjectQualifier);
        }

        /// <summary>Quotes an identifier for use in DDL, the way <c>QUOTENAME</c> does.</summary>
        /// <param name="name">The identifier.</param>
        /// <returns>The bracket-quoted identifier.</returns>
        private static string QuoteName(string name)
        {
            return "[" + name.Replace("]", "]]") + "]";
        }

        /// <summary>Quotes a string literal for use in DDL.</summary>
        /// <param name="value">The literal value.</param>
        /// <returns>The single-quoted literal.</returns>
        private static string QuoteLiteral(string value)
        {
            return "N'" + value.Replace("'", "''") + "'";
        }
    }
}
