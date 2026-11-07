// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace UnitTests.Subtext
{
    using System;
    using System.Collections.Specialized;
    using System.IO;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Tests.Instance.Utilities.HttpSimulator;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;

    using NUnit.Framework;

    ////[TestFixture]
    public class HttpSimulatorTests
    {
        ////[Test]
        public void CanGetSetSession()
        {
            using (new HttpSimulator("/", @"c:\inetpub\").SimulateRequest())
            {
                HttpContextSource.Current.Session.SetString("Test", "Success");
                Assert.That(HttpContextSource.Current.Session.GetString("Test"), Is.EqualTo("Success"), "Was not able to retrieve session variable.");
            }
        }

        ////[Test]
        public void CanGetSetApplicationVariables()
        {
            using (new HttpSimulator("/", @"c:\inetpub\").SimulateRequest())
            {
                // ASP.NET Core has no per-application state bag, so HttpContext.Current.Application
                // is replaced by the process-wide HttpSimulator.ApplicationState dictionary, which
                // is also published on HttpContext.Items.
                HttpSimulator.ApplicationState["Test"] = "Success";
                Assert.That(HttpSimulator.ApplicationState["Test"], Is.EqualTo("Success"), "Was not able to retrieve application variable.");
                Assert.That(HttpContextSource.Current.Items[HttpSimulator.ApplicationStateItemKey], Is.SameAs(HttpSimulator.ApplicationState));
            }
        }

        ////[Test]
        public void TestHttpHandlerWritesCorrectResponse()
        {
            using (var simulator = new HttpSimulator("/", @"c:\inetpub\"))
            {
                simulator.SetFormVariable("username", "phil")
                    .SetReferer(new Uri("http://example.com/1/"))
                    .SimulateRequest(new Uri("http://localhost/MyHandler.ashx?id=1234"));

                RequestDelegate handler = TestHttpHandler.ProcessRequest;
                handler(HttpContextSource.Current).GetAwaiter().GetResult();
                HttpContextSource.Current.Response.Body.Flush();

                // Pinned independently of the handler's own expression (which would make this a
                // tautology) and built with Path.Combine so it holds on any OS.
                var expected = Path.Combine(@"c:\inetpub\", "MyHandler.ashx") + ":phil:1234:http://example.com/1/";
                Assert.That(simulator.ResponseText, Is.EqualTo(expected), "The Expected Response is all wrong.");
            } // HttpContextSource.Current is set to null again.
        }

        ////[Test]
        public void CanDispose()
        {
            using (var simulator = new HttpSimulator())
            {
                simulator.SimulateRequest();
                Assert.That(HttpContextSource.Current, Is.Not.Null);
            }

            Assert.That(HttpContextSource.Current, Is.Null);
        }

        /*//[RowTest]
        ////[Row("http://localhost/Test/Default.aspx", "/Test", "/Test")]
        ////[Row("http://localhost/Test/Default.aspx", "/Test/", "/Test")]
        ////[Row("http://localhost/Test/Default.aspx", "//Test//", "/Test")]
        ////[Row("http://localhost/Test/Subtest/Default.aspx", "/Test", "/Test")]
        ////[Row("http://localhost/Test/Subtest/Default.aspx", "/Test/", "/Test")]
        ////[Row("http://localhost/Test/Subtest/Default.aspx", "//Test//", "/Test")]
        ////[Row("http://localhost/Test/Default.aspx", "", "/")]
        ////[Row("http://localhost/Test/Default.aspx", "/", "/")]
        ////[Row("http://localhost/Test/Default.aspx", null, "/")]*/
        public void CanSetApplicationPathCorrectly(string url, string appPath, string expectedAppPath)
        {
            var simulator = new HttpSimulator(appPath, @"c:\inetpub\wwwroot\site1\test");
            Assert.That(simulator.ApplicationPath, Is.EqualTo(expectedAppPath));
            simulator.SimulateRequest(new Uri(url));
            using (Assert.EnterMultipleScope())
            {
                // Request.ApplicationPath → Request.PathBase (empty PathBase means the root "/").
                var pathBase = HttpContextSource.Current.Request.PathBase.Value;
                Assert.That(string.IsNullOrEmpty(pathBase) ? "/" : pathBase, Is.EqualTo(expectedAppPath));

                // HttpRuntime.AppDomainAppVirtualPath and HostingEnvironment.ApplicationVirtualPath
                // are both replaced by HttpContextSource.ApplicationVirtualPath.
                Assert.That(HttpContextSource.ApplicationVirtualPath, Is.EqualTo(expectedAppPath));
            }
        }

        /*//[RowTest]
        ////[Row("http://localhost/Test/default.aspx", "/Test", @"c:\projects\test", @"c:\projects\test\", @"c:\projects\test\default.aspx")]
        ////[Row("http://localhost/Test/Subtest/default.aspx", "/Test", @"c:\projects\test", @"c:\projects\test\", @"c:\projects\test\Subtest\default.aspx")]
        ////[Row("http://localhost/test/default.aspx", "/", @"c:\inetpub\wwwroot\", @"c:\inetpub\wwwroot\", @"c:\inetpub\wwwroot\test\default.aspx")]
        ////[Row("http://localhost/test/default.aspx", "/", @"c:\inetpub\wwwroot", @"c:\inetpub\wwwroot\", @"c:\inetpub\wwwroot\test\default.aspx")]*/
        public void CanSetAppPhysicalPathCorrectly(string url, string appPath, string appPhysicalPath, string expectedPhysicalAppPath, string expectedPhysicalPath)
        {
            var simulator = new HttpSimulator(appPath, appPhysicalPath);
            Assert.That(simulator.PhysicalApplicationPath, Is.EqualTo(expectedPhysicalAppPath));
            simulator.SimulateRequest(new Uri(url), HttpVerb.GET);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(simulator.PhysicalPath, Is.EqualTo(expectedPhysicalPath));

                // HttpRuntime.AppDomainAppPath and HostingEnvironment.ApplicationPhysicalPath are
                // both replaced by the registered IWebHostEnvironment's content root.
                Assert.That(HttpContextSource.WebHostEnvironment.ContentRootPath, Is.EqualTo(expectedPhysicalAppPath));

                // Request.PhysicalPath has no Core equivalent; SimulatedHttpRequest publishes the
                // translated file path on HttpContext.Items instead.
                Assert.That(
                    HttpContextSource.Current.Items[SimulatedHttpRequest.PhysicalFilePathItemKey],
                    Is.EqualTo(expectedPhysicalPath));
            }
        }

        ////[Test]
        public void CanGetQueryString()
        {
            var simulator = new HttpSimulator();
            simulator.SimulateRequest(new Uri("http://localhost/Test.aspx?param1=value1&param2=value2&param3=value3"));
            for (var i = 1; i <= 3; i++)
            {
                Assert.That(HttpContextSource.Current.Request.Query["param" + i].ToString(), Is.EqualTo("value" + i), $"Could not find query string field 'param{i}'");
            }

            simulator.SimulateRequest(new Uri("http://localhost/Test.aspx?param1=new-value1&param2=new-value2&param3=new-value3&param4=new-value4"));
            for (var i = 1; i <= 4; i++)
            {
                Assert.That(HttpContextSource.Current.Request.Query["param" + i].ToString(), Is.EqualTo("new-value" + i), $"Could not find query string field 'param{i}'");
            }

            simulator.SimulateRequest(new Uri("http://localhost/Test.aspx?"));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(HttpContextSource.Current.Request.QueryString.ToString(), Is.EqualTo(string.Empty));
                Assert.That(HttpContextSource.Current.Request.Query, Is.Empty);
            }

            simulator.SimulateRequest(new Uri("http://localhost/Test.aspx"));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(HttpContextSource.Current.Request.QueryString.ToString(), Is.EqualTo(string.Empty));
                Assert.That(HttpContextSource.Current.Request.Query, Is.Empty);
            }

            simulator.SimulateRequest(new Uri("http://localhost/Test.aspx?param-name"));
            using (Assert.EnterMultipleScope())
            {
                Assert.That(HttpContextSource.Current.Request.QueryString.ToString(), Is.EqualTo("?param-name"));
                Assert.That(HttpContextSource.Current.Request.Query, Has.Count.EqualTo(1));
            }

            // A valueless query key yields an empty value in Core, where System.Web yielded null.
            Assert.That(HttpContextSource.Current.Request.Query["param-name"].ToString(), Is.Empty);
        }

        // //[Test]
        public void CanSimulateFormPost()
        {
            using (var simulator = new HttpSimulator())
            {
                var form = new NameValueCollection { { "Test1", "Value1" }, { "Test2", "Value2" } };
                simulator.SimulateRequest(new Uri("http://localhost/Test.aspx"), form);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(HttpContextSource.Current.Request.Form["Test1"].ToString(), Is.EqualTo("Value1"));
                    Assert.That(HttpContextSource.Current.Request.Form["Test2"].ToString(), Is.EqualTo("Value2"));
                }
            }

            using (var simulator = new HttpSimulator())
            {
                simulator.SetFormVariable("Test1", "Value1")
                    .SetFormVariable("Test2", "Value2")
                    .SimulateRequest(new Uri("http://localhost/Test.aspx"));

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(HttpContextSource.Current.Request.Form["Test1"].ToString(), Is.EqualTo("Value1"));
                    Assert.That(HttpContextSource.Current.Request.Form["Test2"].ToString(), Is.EqualTo("Value2"));
                }
            }
        }

        // //[Test]
        public void CanGetResponse()
        {
            var simulator = new HttpSimulator();
            simulator.SimulateRequest();
            HttpContextSource.Current.Response.WriteAsync("Hello World!").GetAwaiter().GetResult();
            HttpContextSource.Current.Response.Body.Flush();
            Assert.That(simulator.ResponseText, Is.EqualTo("Hello World!"));
        }

        // //[Test]
        public void CanGetReferer()
        {
            var simulator = new HttpSimulator();
            simulator.SetReferer(new Uri("http://example.com/Blah.aspx")).SimulateRequest();
            Assert.That(HttpContextSource.Current.Request.Headers["Referer"].ToString(), Is.EqualTo(new Uri("http://example.com/Blah.aspx").ToString()));

            simulator = new HttpSimulator();
            simulator.SimulateRequest().SetReferer(new Uri("http://x.example.com/Blah.aspx"));
            Assert.That(HttpContextSource.Current.Request.Headers["Referer"].ToString(), Is.EqualTo(new Uri("http://x.example.com/Blah.aspx").ToString()));
        }

        /*//[RowTest]
        ////[Row("http://localhost:60653/Test.aspx", null, null, "localhost", 60653, "/", "/Test.aspx", @"c:\InetPub\wwwRoot\")]
        ////[Row("http://localhost:60653/Test.aspx?test=true", null, null, "localhost", 60653, "/", "/Test.aspx", @"c:\InetPub\wwwRoot\")]
        ////[Row("http://localhost:60653/Test.aspx", "/", @"c:\InetPub\wwwRoot\", "localhost", 60653, "/", "/Test.aspx", @"c:\InetPub\wwwRoot\")]
        ////[Row("http://localhost:60653/Test/Test.aspx", "/", @"c:\InetPub\wwwRoot\", "localhost", 60653, "/", "/Test/Test.aspx", @"c:\InetPub\wwwRoot\")]
        ////[Row("http://localhost:60653/AppPath/Test.aspx", "/AppPath", @"c:\InetPub\wwwRoot\AppPath\", "localhost", 60653, "/AppPath", "/AppPath/Test.aspx", @"c:\InetPub\wwwRoot\AppPath\")]
        ////[Row("http://localhost:60653/AppPath/Test.aspx", "/AppPath/", @"c:\InetPub\wwwRoot\AppPath\", "localhost", 60653, "/AppPath", "/AppPath/Test.aspx", @"c:\InetPub\wwwRoot\AppPath\")]*/
        public void CanParseRequestUrl(string url, string appPath, string physicalPath, string expectedHost, int expectedPort, string expectedAppPath, string expectedPage, string expectedAppDomainAppPath)
        {
            var simulator = new HttpSimulator(appPath, physicalPath);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(simulator.ApplicationPath, Is.EqualTo(expectedAppPath));
                Assert.That(simulator.PhysicalApplicationPath, Is.EqualTo(expectedAppDomainAppPath));
            }
        }

        // [RowTest]
        ////[Row("http://localhost/AppPath/default.aspx", "/AppPath", "/AppPath/default.aspx")]
        ////[Row("http://localhost/AppPath/default.aspx", "/", "/AppPath/default.aspx")]
        public void CanGetLocalPathCorrectly(string url, string appPath, string expectedLocalPath)
        {
            var simulator = new HttpSimulator(appPath, @"c:\inetpub\wwwroot\AppPath\");
            simulator.SimulateRequest(new Uri(url));
            using (Assert.EnterMultipleScope())
            {
                // Request.Path is relative to PathBase in Core; together they are the old Path.
                var request = HttpContextSource.Current.Request;
                Assert.That(request.PathBase.Value + request.Path.Value, Is.EqualTo(expectedLocalPath));
                Assert.That(simulator.LocalPath, Is.EqualTo(expectedLocalPath));
            }
        }

        // [RowTest]
        //////[Row("http://localhost:60653/Test.aspx", null, null, "localhost", 60653, "/", "/Test.aspx", @"c:\InetPub\wwwRoot\")]
        //////[Row("http://localhost:60653/Test.aspx", "/", @"c:\InetPub\wwwRoot\", "localhost", 60653, "/", "/Test.aspx", @"c:\InetPub\wwwRoot\")]
        //////[Row("http://localhost:60653/Test/Test.aspx", "/", @"c:\InetPub\wwwRoot\", "localhost", 60653, "/", "/Test/Test.aspx", @"c:\InetPub\wwwRoot\")]
        //////[Row("http://localhost:60653/AppPath/Test.aspx", "/AppPath", @"c:\InetPub\wwwRoot\AppPath\", "localhost", 60653, "/AppPath", "/AppPath/Test.aspx", @"c:\InetPub\wwwRoot\AppPath\")]
        //////[Row("http://localhost:60653/AppPath/Test.aspx", "/AppPath/", @"c:\InetPub\wwwRoot\AppPath\", "localhost", 60653, "/AppPath", "/AppPath/Test.aspx", @"c:\InetPub\wwwRoot\AppPath\")]
        public void CanSimulateRequest(string url, string appPath, string physicalPath, string expectedHost, int expectedPort, string expectedAppPath, string expectedLocalPath, string expectedPhysicalPath)
        {
            var simulator = new HttpSimulator(appPath, physicalPath);
            simulator.SimulateRequest(new Uri(url));

            using (Assert.EnterMultipleScope())
            {
                var request = HttpContextSource.Current.Request;
                Assert.That(request.Host.Host, Is.EqualTo(expectedHost));
                Assert.That(request.Host.Port ?? 80, Is.EqualTo(expectedPort));

                var pathBase = request.PathBase.Value;
                Assert.That(string.IsNullOrEmpty(pathBase) ? "/" : pathBase, Is.EqualTo(expectedAppPath));

                // Request.PhysicalApplicationPath → the registered IWebHostEnvironment content root.
                Assert.That(HttpContextSource.WebHostEnvironment.ContentRootPath, Is.EqualTo(expectedPhysicalPath));
                Assert.That(request.PathBase.Value + request.Path.Value, Is.EqualTo(expectedLocalPath));
            }
        }

        // [RowTest]
        ////[Row("/", "/", @"c:\inetpub\wwwroot\")]
        ////[Row("/Test/Test.aspx", "/", @"c:\inetpub\wwwroot\Test\Test.aspx")]
        ////[Row("/Test/Blah/Test.aspx", "/", @"c:\inetpub\wwwroot\Test\Blah\Test.aspx")]
        ////[Row("/Test", "/Test", @"c:\inetpub\wwwroot")]
        ////[Row("/Test/", "/Test", @"c:\inetpub\wwwroot\")]
        public void CanMapPath(string virtualPath, string appPath, string expectedMapPath)
        {
            var url = new Uri("http://localhost/Test/Test.aspx");
            var simulator = new HttpSimulator(appPath, @"c:\inetpub\wwwroot\");
            simulator.SimulateRequest(url);

            // The System.Web.VirtualPath / HostingEnvironment._configMapPath reflection dance is
            // replaced by the registered IWebHostEnvironment: HttpSimulator.MapPath reproduces
            // the old ConfigMapPath.MapPath behaviour exactly, and HttpContextSource.MapPath is
            // the seam production code now calls instead of Request.MapPath.
            var environment = HttpSimulatorTester.CallGetEnvironment();
            Assert.That(environment, Is.Not.Null);
            Console.WriteLine("ContentRootPath: " + environment.ContentRootPath);
            Console.WriteLine("App-VPATH: " + HttpContextSource.ApplicationVirtualPath);
            Console.WriteLine("MAPPED: " + simulator.MapPath(virtualPath));

            Assert.That(simulator.MapPath(virtualPath), Is.EqualTo(expectedMapPath));

            // Assert the seam production code actually calls agrees with the old
            // ConfigMapPath behaviour — otherwise a virtual-directory install would map
            // paths one level too deep and this test would never notice.
            Assert.That(HttpContextSource.MapPath(virtualPath), Is.EqualTo(expectedMapPath));
        }

        // [Test]
        public void CanInstantiateVirtualPath()
        {
            // System.Web.VirtualPath (an internal type instantiated by reflection on net48) is
            // replaced by Microsoft.AspNetCore.Http.PathString, which is the type Request.Path and
            // Request.PathBase — and therefore HttpContextSource.ApplicationVirtualPath — use.
            var virtualPath = new PathString("/AppPath");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(virtualPath.HasValue, Is.True);
                Assert.That(virtualPath.Value, Is.EqualTo("/AppPath"));
            }
        }

        // [Test]
        public void CanGetHostingEnvironment()
        {
            // System.Web.Hosting.HostingEnvironment is replaced by the fake IWebHostEnvironment
            // that HttpSimulator registers with HttpContextSource.
            var environment = HttpSimulatorTester.CallGetEnvironment();
            Assert.That(environment, Is.Not.Null);
            environment = HttpSimulatorTester.CallGetEnvironment();
            Assert.That(environment, Is.Not.Null);
        }

        // [RowTest]
        ////[Row("/", "/")]
        ////[Row("", "/")]
        ////[Row("/test", "/test")]
        ////[Row("test/", "/test")]
        ////[Row("/test/", "/test")]
        ////[Row("/test/////", "/test")]
        ////[Row("////test/", "/test")]
        ////[Row("/test/test////", "/test/test")]
        ////[Row("/////test/test////", "/test/test")]
        ////[Row("/////test///test////", "/test/test")]
        public void CanNormalizeSlashes(string s, string expected)
        {
            Assert.That(HttpSimulatorTester.CallNormalizeSlashes(s), Is.EqualTo(expected));
        }

        // [Test]
        public void CanStripTrailing()
        {
            Assert.That(HttpSimulatorTester.CallStripTrailingBackSlashes(@"c:\blah\blah2\"), Is.EqualTo(@"c:\blah\blah2"));
        }

        /// <summary>
        /// The net48 <c>IHttpHandler</c> test fixture, ported to a
        /// <see cref="RequestDelegate"/> over the ASP.NET Core <see cref="HttpContext"/>.
        /// </summary>
        internal static class TestHttpHandler
        {
            /// <summary>Handles the simulated request, writing the same response as the old handler.</summary>
            /// <param name="context">The current request context.</param>
            /// <returns>A task that completes when the response has been written.</returns>
            public static async Task ProcessRequest(HttpContext context)
            {
                var physicalPath = HttpContextSource.MapPath("/MyHandler.ashx");
                var username = context.Request.Form["username"].ToString();
                var id = context.Request.Query["id"].ToString();
                var refererHeader = context.Request.Headers["Referer"].ToString();
                if (string.IsNullOrEmpty(refererHeader))
                {
                    return;
                }

                // Imagine, if you will, a bunch of complex interesting
                // and fascinating logic here.
                await context.Response.WriteAsync(physicalPath + ":" + username + ":" + id + ":" + refererHeader);
            }
        }
    }

    internal class HttpSimulatorTester : HttpSimulator
    {
        public static string CallNormalizeSlashes(string s)
        {
            return NormalizeSlashes(s);
        }

        public static string CallStripTrailingBackSlashes(string s)
        {
            return StripTrailingBackSlashes(s);
        }

        public static IWebHostEnvironment CallGetEnvironment()
        {
            return GetHostingEnvironment();
        }
    }
}
