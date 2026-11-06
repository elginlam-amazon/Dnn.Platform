// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DNN.Integration.Test.Framework.Helpers
{
    using System;

    using Microsoft.Extensions.Configuration;

    public static class AppConfigHelper
    {
        private const string DefaultWebsiteName = "http://dnnce.lvh.me";
        private const string DefaultHostName = "host";
        private const string DefaultHostPwd = "dnnhost";
        private const string DefaultLoginCookie = ".DOTNETNUKE";

        private static readonly Lazy<IConfigurationRoot> LazyConfiguration = new Lazy<IConfigurationRoot>(BuildConfiguration);

        private static string _siteUrl;
        private static string _hostUserName;
        private static string _hostPassword;
        private static string _objectQualifier;
        private static string _connectionString;
        private static string _loginCookieString;

        public static string ConnectionString =>
            _connectionString ?? (_connectionString = Configuration.GetConnectionString("SiteSqlServer"));

        public static string ObjectQualifier =>
            _objectQualifier ?? (_objectQualifier = GetAppSetting("objectQualifier"));

        public static string SiteUrl =>
            _siteUrl ?? (_siteUrl = GetAppSetting("siteUrl") ?? DefaultWebsiteName);

        public static string HostUserName =>
            _hostUserName ?? (_hostUserName = GetAppSetting("hostUserName") ?? DefaultHostName);

        public static string HostPassword =>
            _hostPassword ?? (_hostPassword = GetAppSetting("hostPassword") ?? DefaultHostPwd);

        public static string LoginCookie =>
            _loginCookieString ?? (_loginCookieString = GetAppSetting("loginCookie") ?? DefaultLoginCookie);

        private static IConfigurationRoot Configuration => LazyConfiguration.Value;

        public static string GetAppSetting(string key)
        {
            // Values live under the "appSettings" section (the appsettings.json equivalent of the
            // legacy web.config/app.config <appSettings> element); a flat top-level key or an
            // environment variable of the same name is also honoured.
            var value = Configuration[$"appSettings:{key}"];
            if (value is null)
            {
                value = Configuration[key];
            }

            return value;
        }

        public static bool GetAppSettingAsBool(string key, bool defaultValue)
        {
            bool value;
            return bool.TryParse(GetAppSetting(key), out value) ? value : defaultValue;
        }

        public static int GetAppSettingAsInt(string key, int defaultValue)
        {
            int value;
            return int.TryParse(GetAppSetting(key), out value) ? value : defaultValue;
        }

        private static IConfigurationRoot BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();
        }
    }
}
