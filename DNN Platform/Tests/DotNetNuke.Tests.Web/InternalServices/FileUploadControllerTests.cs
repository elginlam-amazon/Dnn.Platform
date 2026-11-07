// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Tests.Web.InternalServices
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Common.Lists;
    using DotNetNuke.Data;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Entities.Users;
    using DotNetNuke.Services.Cache;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Tests.Utilities.Mocks;
    using DotNetNuke.Web.InternalServices;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Primitives;

    using Moq;

    using NUnit.Framework;

    /// <summary>Tests FileUploadController methods.</summary>
    [TestFixture]
    public class FileUploadControllerTests
    {
        private Mock<DataProvider> mockDataProvider;
        private FileUploadController testInstance;
        private Mock<CachingProvider> mockCachingProvider;
        private Mock<IPortalController> mockPortalController;
        private Mock<IUserController> mockUserController;
        private TestSynchronizationContext synchronizationContext = new TestSynchronizationContext();
        private FakeServiceProvider serviceProvider;

        [SetUp]
        public void SetUp()
        {
            this.SetupDataProvider();
            this.SetupCachingProvider();
            this.SetupPortalSettings();
            this.SetupServiceProvider();
            this.SetupSynchronizationContext();
        }

        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();
            (this.testInstance as IDisposable)?.Dispose();

            UserController.ClearInstance();
            PortalController.ClearInstance();
        }

        [Test]
        [SetCulture("tr-TR")]
        public async Task UploadFromLocal_ShouldUploadFile_WithTrCultureAsync()
        {
            // On Web API 2 the body was a MultipartFormDataContent assigned to ApiController.Request.
            // On ASP.NET Core, ControllerBase.Request is read-only and derives from the HttpContext, so the
            // multipart body is modelled as an IFormFile on a DefaultHttpContext wired through ControllerContext.
            var fileBytes = Encoding.UTF8.GetBytes("Hello World!");
            var fileStream = new MemoryStream(fileBytes);
            var postedFile = new FormFile(fileStream, 0, fileBytes.Length, "postfile", "testing")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain",
            };

            var httpContext = new DefaultHttpContext();
            httpContext.RequestServices = this.serviceProvider;
            httpContext.Request.Method = HttpMethods.Post;
            httpContext.Request.ContentType = "multipart/form-data; boundary=----DnnTestBoundary";
            httpContext.Request.Form = new FormCollection(
                new Dictionary<string, StringValues>(),
                new FormFileCollection { postedFile });

            this.testInstance.ControllerContext = new ControllerContext { HttpContext = httpContext, };

            await this.testInstance.UploadFromLocal(-1);

            Assert.That(this.synchronizationContext.IsUploadFileCalled(), Is.True);
        }

        private void SetupPortalSettings()
        {
            this.mockPortalController = MockComponentProvider.CreatePortalController();
            this.mockPortalController.Setup(x => x.GetCurrentPortalSettings()).Returns(new PortalSettings() { PortalId = 0 });
            PortalController.SetTestableInstance(this.mockPortalController.Object);

            // PortalSettings.UserInfo delegates to UserController.Instance.GetCurrentUserInfo(). On
            // net48 that resolved the real controller, which returns an anonymous (non-super-user)
            // UserInfo when nobody is signed in. Resolving it from the test container yields a mock
            // whose default return is null, which the controller would dereference. Restore the
            // anonymous user the upload path was always exercised with.
            this.mockUserController = new Mock<IUserController>();
            this.mockUserController.Setup(x => x.GetCurrentUserInfo()).Returns(new UserInfo());
            UserController.SetTestableInstance(this.mockUserController.Object);
        }

        private void SetupDataProvider()
        {
            this.mockDataProvider = MockComponentProvider.CreateDataProvider();
            this.mockDataProvider.Setup(x => x.GetListEntriesByListName("ImageTypes", string.Empty, It.IsAny<int>()))
                .Returns(Mock.Of<IDataReader>());
        }

        private void SetupCachingProvider()
        {
            this.mockCachingProvider = MockComponentProvider.CreateDataCacheProvider();
            this.mockCachingProvider.Setup(x => x.GetItem(It.IsAny<string>()))
                .Returns(Enumerable.Empty<ListEntryInfo>());
        }

        private void SetupServiceProvider()
        {
            this.serviceProvider = FakeServiceProvider.Setup(
                services =>
                {
                    services.AddSingleton(this.mockPortalController.Object);
                    services.AddSingleton(this.mockCachingProvider.Object);
                    services.AddSingleton(this.mockDataProvider.Object);
                });
        }

        private void SetupSynchronizationContext()
        {
            this.synchronizationContext = new TestSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(this.synchronizationContext);
            this.testInstance = new FileUploadController();
        }

        private class TestSynchronizationContext : SynchronizationContext
        {
            private bool isUploadFileCalled;

            public override void Post(SendOrPostCallback d, object state)
            {
                d(state);
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                isUploadFileCalled = true;
            }

            public bool IsUploadFileCalled()
            {
                return isUploadFileCalled;
            }
        }
    }
}
