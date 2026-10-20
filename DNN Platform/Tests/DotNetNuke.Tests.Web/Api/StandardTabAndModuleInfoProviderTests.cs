// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Api
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Web;

    using Microsoft.AspNetCore.Http;

    using DotNetNuke.Data;
    using DotNetNuke.Entities.Modules;
    using DotNetNuke.Entities.Tabs;
    using DotNetNuke.Tests.Utilities.Mocks;
    using DotNetNuke.Web.Api;
    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class StandardTabAndModuleInfoProviderTests
    {
        private const int ValidPortalId = 0;
        private const int ValidTabModuleId = 999;
        private const int ValidModuleId = 456;
        private const int ValidTabId = 46;

        private const string MonikerSettingName = "Moniker";
        private const string MonikerSettingValue = "TestMoniker";

        private Mock<IModuleController> _mockModuleController;
        private IModuleController _moduleController;
        private Mock<ITabController> _mockTabController;
        private Mock<ITabModulesController> _mockTabModuleController;
        private Mock<DataProvider> _mockDataProvider;

        private ITabController _tabController;
        private ITabModulesController _tabModuleController;
        private TabInfo _tabInfo;
        private ModuleInfo _moduleInfo;

        [SetUp]
        public void Setup()
        {
            MockComponentProvider.CreateDataCacheProvider();
            this._mockDataProvider = MockComponentProvider.CreateDataProvider();
            this._mockDataProvider.Setup(d => d.GetProviderPath()).Returns(string.Empty);
            this._mockDataProvider.Setup(d => d.GetPortals(It.IsAny<string>())).Returns<string>(GetPortalsCallBack);

            this.RegisterMock(ModuleController.SetTestableInstance, out this._mockModuleController, out this._moduleController);
            this.RegisterMock(TabController.SetTestableInstance, out this._mockTabController, out this._tabController);
            this.RegisterMock(TabModulesController.SetTestableInstance, out this._mockTabModuleController, out this._tabModuleController);

            this._tabInfo = new TabInfo { TabID = ValidTabId };
            this._moduleInfo = new ModuleInfo
            {
                TabModuleID = ValidTabModuleId,
                TabID = ValidTabId,
                ModuleID = ValidModuleId,
                PortalID = ValidPortalId,
            };

            this._mockTabController.Setup(x => x.GetTab(ValidTabId, ValidPortalId)).Returns(this._tabInfo);
            this._mockModuleController.Setup(x => x.GetModule(ValidModuleId, ValidTabId, false)).Returns(this._moduleInfo);
            this._mockModuleController.Setup(x => x.GetTabModule(ValidTabModuleId)).Returns(this._moduleInfo);
            this._mockTabModuleController.Setup(x => x.GetTabModuleIdsBySetting(MonikerSettingName, MonikerSettingValue)).Returns(
                new List<int> { ValidTabModuleId });
            this._mockTabModuleController.Setup(x => x.GetTabModuleSettingsByName(MonikerSettingName)).Returns(
                new Dictionary<int, string> { { ValidTabModuleId, MonikerSettingValue } });
        }

        [TearDown]
        public void TearDown()
        {
            ModuleController.ClearInstance();
            TabController.ClearInstance();
        }

        [Test]
        public void ValidTabAndModuleIdLoadsActiveModule()
        {
            // Arrange
            var request = CreateRequest(headers: new (string, string)[] { ("tabid", ValidTabId.ToString(CultureInfo.InvariantCulture)), ("moduleid", ValidModuleId.ToString(CultureInfo.InvariantCulture)) });

            // Act
            ModuleInfo returnedModuleInfo;
            var result = new StandardTabAndModuleInfoProvider().TryFindModuleInfo(request, out returnedModuleInfo);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(result, Is.True);
                Assert.That(returnedModuleInfo, Is.SameAs(this._moduleInfo));
            }
        }

        [Test]
        public void ExistingMonikerValueInHeaderShouldFindTheCorrectModuleInfo()
        {
            // Arrange
            var request = CreateRequest(headers: new (string, string)[] { ("X-DNN-MONIKER", MonikerSettingValue) });

            // Act
            ModuleInfo returnedModuleInfo;
            var result = new StandardTabAndModuleInfoProvider().TryFindModuleInfo(request, out returnedModuleInfo);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(result, Is.True);
                Assert.That(returnedModuleInfo, Is.SameAs(this._moduleInfo));
            }
        }

        [Test]
        public void ExistingMonikerValueInQueryStringShouldFindTheCorrectModuleInfo()
        {
            // Arrange
            var request = CreateRequest(uri: "http://mysite.lvh.me/API/internalservices/controlbar/ToggleUserMode?moniker=" + HttpUtility.UrlEncode(MonikerSettingValue));

            // Act
            ModuleInfo returnedModuleInfo;
            var result = new StandardTabAndModuleInfoProvider().TryFindModuleInfo(request, out returnedModuleInfo);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(result, Is.True);
                Assert.That(returnedModuleInfo, Is.SameAs(this._moduleInfo));
            }
        }

        [Test]
        public void NonExistingMonikerValueShouldFailToReturnResult()
        {
            // Arrange
            var request = CreateRequest(headers: new (string, string)[] { ("X-DNN-MONIKER", "This moniker does not exist") });

            // Act
            ModuleInfo returnedModuleInfo;
            var result = new StandardTabAndModuleInfoProvider().TryFindModuleInfo(request, out returnedModuleInfo);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(result, Is.False);
                Assert.That(returnedModuleInfo, Is.Null);
            }
        }

        [Test]
        public void MissingAllHeadersShouldFailToReturnResult()
        {
            // Arrange
            var request = CreateRequest();

            // Act
            ModuleInfo returnedModuleInfo;
            var result = new StandardTabAndModuleInfoProvider().TryFindModuleInfo(request, out returnedModuleInfo);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(result, Is.False);
                Assert.That(returnedModuleInfo, Is.Null);
            }
        }

        [Test]
        public void OmittedTabIdWillNotLoadModule()
        {
            // Arrange
            // no tabid
            var request = CreateRequest(headers: new (string, string)[] { ("moduleid", ValidModuleId.ToString(CultureInfo.InvariantCulture)) });

            // Act
            ModuleInfo returnedModuleInfo;
            var result = new StandardTabAndModuleInfoProvider().TryFindModuleInfo(request, out returnedModuleInfo);

            // Assert
            this._mockTabController.Verify(x => x.GetTab(It.IsAny<int>(), It.IsAny<int>()), Times.Never());
            this._mockModuleController.Verify(x => x.GetModule(It.IsAny<int>(), It.IsAny<int>(), false), Times.Never());
            using (Assert.EnterMultipleScope())
            {
                Assert.That(returnedModuleInfo, Is.Null);
                Assert.That(result, Is.False);
            }
        }

        [Test]
        public void OmittedModuleIdWillNotLoadModule()
        {
            // Arrange
            // no moduleid
            var request = CreateRequest(headers: new (string, string)[] { ("tabid", ValidTabId.ToString(CultureInfo.InvariantCulture)) });

            // Act
            ModuleInfo returnedModuleInfo;
            var result = new StandardTabAndModuleInfoProvider().TryFindModuleInfo(request, out returnedModuleInfo);

            // Assert
            this._mockModuleController.Verify(x => x.GetModule(It.IsAny<int>(), It.IsAny<int>(), false), Times.Never());
            using (Assert.EnterMultipleScope())
            {
                Assert.That(returnedModuleInfo, Is.Null);
                Assert.That(result, Is.False);
            }
        }

        [Test]
        public void TabIdInHeaderTakesPriority()
        {
            // Arrange
            var request = CreateRequest(uri: string.Format("http://foo.com?{0}={1}", "tabid", ValidTabId + 1), headers: new (string, string)[] { ("tabid", ValidTabId.ToString(CultureInfo.InvariantCulture)) });

            // Act
            int tabId;
            var result = new StandardTabAndModuleInfoProvider().TryFindTabId(request, out tabId);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(tabId, Is.EqualTo(ValidTabId));
                Assert.That(result, Is.True);
            }
        }

        [Test]
        public void ModuleIdInHeaderTakesPriority()
        {
            // Arrange
            var request = CreateRequest(uri: string.Format("http://foo.com?{0}={1}", "moduleid", ValidTabId + 1), headers: new (string, string)[] { ("moduleid", ValidTabId.ToString(CultureInfo.InvariantCulture)) });

            // Act
            int moduleId;
            var result = new StandardTabAndModuleInfoProvider().TryFindModuleId(request, out moduleId);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(moduleId, Is.EqualTo(ValidTabId));
                Assert.That(result, Is.True);
            }
        }

        [Test]
        [TestCase("tabid")]
        [TestCase("TABID")]
        [TestCase("tAbiD")]
        public void TabIdInHeaderAllowsTabIdToBeFound(string headerName)
        {
            // Arrange
            var request = CreateRequest(headers: new (string, string)[] { (headerName, ValidTabId.ToString(CultureInfo.InvariantCulture)) });

            // Act
            int tabId;
            var result = new StandardTabAndModuleInfoProvider().TryFindTabId(request, out tabId);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(tabId, Is.EqualTo(ValidTabId));
                Assert.That(result, Is.True);
            }
        }

        [Test]
        [TestCase("moduleid")]
        [TestCase("MODULEID")]
        [TestCase("modULeid")]
        public void ModuleIdInHeaderAllowsModuleIdToBeFound(string headerName)
        {
            // Arrange
            var request = CreateRequest(headers: new (string, string)[] { (headerName, ValidModuleId.ToString(CultureInfo.InvariantCulture)) });

            // Act
            int moduleId;
            var result = new StandardTabAndModuleInfoProvider().TryFindModuleId(request, out moduleId);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(moduleId, Is.EqualTo(ValidModuleId));
                Assert.That(result, Is.True);
            }
        }

        [Test]
        [TestCase("x-dnn-moniker")]
        [TestCase("X-Dnn-Moniker")]
        [TestCase("X-DNN-MONIKER")]
        public void MonikerInHeaderAllowsModuleToBeFound(string headerName)
        {
            // Arrange
            var request = CreateRequest(headers: new (string, string)[] { (headerName, MonikerSettingValue) });

            // Act
            int moduleId;
            var result = new StandardTabAndModuleInfoProvider().TryFindModuleId(request, out moduleId);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(result, Is.True);
                Assert.That(moduleId, Is.EqualTo(ValidModuleId));
            }
        }

        [Test]
        [TestCase("tabid")]
        [TestCase("TABID")]
        [TestCase("tAbiD")]
        public void TabIdInQueryStringAllowsTabIdToBeFound(string paramName)
        {
            // Arrange
            var request = CreateRequest(uri: string.Format("http://foo.com?{0}={1}", paramName, ValidTabId));

            // Act
            int tabId;
            var result = new StandardTabAndModuleInfoProvider().TryFindTabId(request, out tabId);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(tabId, Is.EqualTo(ValidTabId));
                Assert.That(result, Is.True);
            }
        }

        [Test]
        [TestCase("moduleid")]
        [TestCase("MODULEID")]
        [TestCase("modULeid")]
        public void ModuleIdInQueryStringAllowsModuleIdToBeFound(string paramName)
        {
            // Arrange
            var request = CreateRequest(uri: string.Format("http://foo.com?{0}={1}", paramName, ValidModuleId));

            // Act
            int moduleId;
            var result = new StandardTabAndModuleInfoProvider().TryFindModuleId(request, out moduleId);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(moduleId, Is.EqualTo(ValidModuleId));
                Assert.That(result, Is.True);
            }
        }

        [Test]
        public void NoTabIdInRequestReturnsNoTabId()
        {
            // Arrange

            // Act
            int tabId;
            var result = new StandardTabAndModuleInfoProvider().TryFindTabId(CreateRequest(), out tabId);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(result, Is.False);
                Assert.That(tabId, Is.EqualTo(-1));
            }
        }

        [Test]
        public void NoModuleIdInRequestReturnsNoModuleId()
        {
            // Arrange

            // Act
            int moduleId;
            var result = new StandardTabAndModuleInfoProvider().TryFindModuleId(CreateRequest(), out moduleId);

            using (Assert.EnterMultipleScope())
            {
                // Assert
                Assert.That(result, Is.False);
                Assert.That(moduleId, Is.EqualTo(-1));
            }
        }

        private static Microsoft.AspNetCore.Http.HttpRequest CreateRequest(string uri = null, (string Name, string Value)[] headers = null)
        {
            // On .NET Framework these tests built a System.Net.Http.HttpRequestMessage; the
            // StandardTabAndModuleInfoProvider now reads an Microsoft.AspNetCore.Http.HttpRequest.
            var context = new DefaultHttpContext();
            var request = context.Request;
            if (uri != null)
            {
                var parsed = new Uri(uri, UriKind.RelativeOrAbsolute);
                if (parsed.IsAbsoluteUri)
                {
                    request.Scheme = parsed.Scheme;
                    request.Host = new HostString(parsed.Host, parsed.IsDefaultPort ? 80 : parsed.Port);
                    request.Path = parsed.AbsolutePath;
                    request.QueryString = new QueryString(parsed.Query);
                }
                else
                {
                    var parts = uri.Split('?');
                    request.Path = parts[0];
                    request.QueryString = parts.Length > 1 ? new QueryString("?" + parts[1]) : QueryString.Empty;
                }
            }

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers[header.Name] = header.Value;
                }
            }

            return request;
        }

        private static IDataReader GetPortalsCallBack(string culture)
        {
            return GetPortalCallBack(0, culture);
        }

        private static IDataReader GetPortalCallBack(int portalId, string culture)
        {
            var table = new DataTable("Portal");

            var cols = new[]
            {
                "PortalID", "PortalGroupID", "PortalName", "LogoFile", "FooterText", "ExpiryDate",
                "UserRegistration", "BannerAdvertising", "AdministratorId", "Currency", "HostFee",
                "HostSpace", "PageQuota", "UserQuota", "AdministratorRoleId", "RegisteredRoleId",
                "Description", "KeyWords", "BackgroundFile", "GUID", "PaymentProcessor",
                "ProcessorUserId",
                "ProcessorPassword", "SiteLogHistory", "Email", "DefaultLanguage", "TimezoneOffset",
                "AdminTabId", "HomeDirectory", "SplashTabId", "HomeTabId", "LoginTabId", "RegisterTabId",
                "UserTabId", "SearchTabId", "Custom404TabId", "Custom500TabId", "TermsTabId", "PrivacyTabId", "SuperTabId",
                "CreatedByUserID", "CreatedOnDate", "LastModifiedByUserID", "LastModifiedOnDate",
                "CultureCode",
            };

            foreach (var col in cols)
            {
                table.Columns.Add(col);
            }

            const int homePage = 1;
            table.Rows.Add(portalId, null, "My Website", "Logo.png", "Copyright (c) 2018 DNN Corp.", null,
                "2", "0", "2", "USD", "0", "0", "0", "0", "0", "1", "My Website",
                "DotNetNuke, DNN, Content, Management, CMS", null, "1057AC7A-3C08-4849-A3A6-3D2AB4662020",
                null, null, null, "0", "admin@changeme.invalid", "en-US", "-8", "58", "Portals/0", null,
                homePage.ToString(), null, null, "57", "56", "-1", "-1", null, null, "7", "-1", "2011-08-25 07:34:11",
                "-1", "2011-08-25 07:34:29", culture);

            return table.CreateDataReader();
        }

        private void RegisterMock<T>(Action<T> register, out Mock<T> mock, out T instance)
            where T : class
        {
            mock = new Mock<T>();
            instance = mock.Object;
            register(instance);
        }
    }
}
