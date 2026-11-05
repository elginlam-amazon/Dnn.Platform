// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Services.Upgrade.Internals
{
    using System.IO;
    using System.Net.Http;

    using DotNetNuke.Application;
    using DotNetNuke.Common;

    public class UpdateService
    {
        private static string ApplicationVersion
        {
            get
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return Globals.FormatVersion(version, "00", 3, string.Empty);
            }
        }

        private static string ApplicationName
        {
            get
            {
                return DotNetNukeContext.Current.Application.Name;
            }
        }

        public static StreamReader GetLanguageList()
        {
            string url = DotNetNukeContext.Current.Application.UpgradeUrl + "/languages.aspx";
            url += "?core=" + ApplicationVersion;
            url += "&type=Framework";
            url += "&name=" + ApplicationName;

            StreamReader myResponseReader = GetResponseAsStream(url);
            return myResponseReader;
        }

        public static string GetLanguageDownloadUrl(string cultureCode)
        {
            string url = DotNetNukeContext.Current.Application.UpgradeUrl + "/languages.aspx";
            url += "?core=" + ApplicationVersion;
            url += "&type=Framework";
            url += "&name=" + ApplicationName;
            url += "&culture=" + cultureCode;

            StreamReader myResponseReader = GetResponseAsStream(url);
            string downloadUrl = myResponseReader.ReadToEnd();
            return downloadUrl;
        }

        private static StreamReader GetResponseAsStream(string url)
        {
            // The service call goes through the named "DnnExternalRequest" HttpClient so that the
            // host's timeout/proxy/user-agent settings apply and sockets are pooled.
            var client = Globals.GetExternalHttpClient();

            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // We use form contentType
            request.Headers.Accept.ParseAdd("text/xml");
            request.Headers.AcceptCharset.ParseAdd("utf-8");

            // Get the response handle. The synchronous Send overload keeps this (public, synchronous)
            // API from blocking on an async call.
            var response = client.Send(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            // Now, we read the response (the string), and output it.
            // Note: the caller owns the returned reader; the response stream stays open until it is read.
            var myResponse = response.Content.ReadAsStream();

            // read the stream into streamreader
            var myResponseReader = new StreamReader(myResponse);
            return myResponseReader;
        }
    }
}
