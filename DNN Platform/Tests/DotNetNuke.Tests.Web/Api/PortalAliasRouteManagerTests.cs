// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Tests.Web.Api
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.Abstractions.Portals;
    using DotNetNuke.Common.Internal;
    using DotNetNuke.Entities.Portals;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Web.Api;

    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using NUnit.Framework;

#pragma warning disable CS0618 // IPortalAliasController is deprecated, but the fixture must keep mocking it to match the legacy service lookups.

    [TestFixture]
    public class PortalAliasRouteManagerTests
    {
        private Mock<IPortalAliasService> mockPortalAliasService;
        private FakeServiceProvider serviceProvider;

        [SetUp]
        public void SetUp()
        {
            this.mockPortalAliasService = new Mock<IPortalAliasService>();
            this.mockPortalAliasService.As<IPortalAliasController>();

            this.serviceProvider = FakeServiceProvider.Setup(
                services =>
                {
                    services.AddSingleton(this.mockPortalAliasService.Object);
                });

            // Requires.NotNullOrEmpty/NotNegative build their message through
            // Localization.GetExceptionMessage, which reads PortalSettings.Current and therefore
            // PortalController.Instance. ServiceLocator caches its factory as a method group bound
            // to the Globals.DependencyProvider that was current when it was first created, and
            // ClearInstance() does not reset that factory — so once this fixture's per-test
            // provider is disposed, the guard-clause tests would see ObjectDisposedException
            // instead of the ArgumentException they assert. A testable instance short-circuits the
            // locator before it reaches that stale factory. Individual tests override it.
            PortalController.SetTestableInstance(Mock.Of<IPortalController>());
        }

        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();

            this.mockPortalAliasService = null;

            PortalController.ClearInstance();
            TestableGlobals.ClearInstance();
        }

        [Test]
        [TestCase("mfn", "url", 0, "API/mfn/url")]
        [TestCase("mfn", "url", 1, "{prefix0}/API/mfn/url")]
        [TestCase("mfn", "url", 2, "{prefix0}/{prefix1}/API/mfn/url")]
        [TestCase("fee/foo", "{contoller}/{action}/{id}", 4, "{prefix0}/{prefix1}/{prefix2}/{prefix3}/API/fee/foo/{contoller}/{action}/{id}")]
        public void GetRouteUrl(string moduleFolderName, string url, int count, string expected)
        {
            // Arrange

            // Act
            string result = new PortalAliasRouteManager(this.mockPortalAliasService.Object).GetRouteUrl(moduleFolderName, url, count);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("", 0, typeof(ArgumentException), "moduleFolderName should be required")]
        [TestCase("name", -1, typeof(ArgumentOutOfRangeException), "count should be >= 0")]
        public void GetRouteUrlThrowsOnBadArguments(string moduleFolderName, int count, Type expectedException, string message)
        {
            // Arrange

            // Act
            try
            {
                new PortalAliasRouteManager(this.mockPortalAliasService.Object).GetRouteUrl(moduleFolderName, "url", count);
            }
            catch (Exception e)
            {
                if (e.GetType() == expectedException)
                {
                    Assert.Pass();
                    return;
                }
            }

            // Assert
            Assert.Fail(message);
        }

        [Test]
        public void ParentPortalOnVirtualDirReturnsAnEmptyPrefix()
        {
            // Arrange
            var mockPortalController = new Mock<IPortalController>();
            var portals = new ArrayList { new PortalInfo { PortalID = 1 } };
            mockPortalController.Setup(x => x.GetPortals()).Returns(portals);
            PortalController.SetTestableInstance(mockPortalController.Object);

            this.mockPortalAliasService.Setup(x => x.GetPortalAliasesByPortalId(It.IsAny<int>())).Returns(new[]
                {
                    new PortalAliasInfo { HTTPAlias = "valid.lvh.me/vdir" },
                });
            this.mockPortalAliasService.As<IPortalAliasController>().Setup(x => x.GetPortalAliasesByPortalId(It.IsAny<int>())).Returns(new[]
                {
                    new PortalAliasInfo { HTTPAlias = "valid.lvh.me/vdir" },
                });

            var mockGlobals = new Mock<IGlobals>();
            mockGlobals.Setup(x => x.ApplicationPath).Returns("/vdir");
            TestableGlobals.SetTestableInstance(mockGlobals.Object);

            // Act
            List<int> prefixes = new PortalAliasRouteManager(this.mockPortalAliasService.Object).GetRoutePrefixCounts().ToList();

            // Assert
            Assert.That(prefixes, Is.EquivalentTo(new[] { 0 }));
        }

        [Test]
        public void SingleParentPortalReturnsAnEmptyPrefix()
        {
            // Arrange
            var mockPortalController = new Mock<IPortalController>();
            var portals = new ArrayList { new PortalInfo { PortalID = 1 } };
            mockPortalController.Setup(x => x.GetPortals()).Returns(portals);
            PortalController.SetTestableInstance(mockPortalController.Object);

            this.mockPortalAliasService.Setup(x => x.GetPortalAliasesByPortalId(It.IsAny<int>())).Returns(new[]
                {
                    new PortalAliasInfo { HTTPAlias = "valid.lvh.me" },
                });
            this.mockPortalAliasService.As<IPortalAliasController>().Setup(x => x.GetPortalAliasesByPortalId(It.IsAny<int>())).Returns(new[]
                {
                    new PortalAliasInfo { HTTPAlias = "valid.lvh.me" },
                });

            var mockGlobals = new Mock<IGlobals>();
            mockGlobals.Setup(x => x.ApplicationPath).Returns(string.Empty);
            TestableGlobals.SetTestableInstance(mockGlobals.Object);

            // Act
            List<int> prefixes = new PortalAliasRouteManager(this.mockPortalAliasService.Object).GetRoutePrefixCounts().ToList();

            // Assert
            Assert.That(prefixes, Is.EquivalentTo(new[] { 0 }));
        }

        [Test]
        public void PrefixCountsAreCached()
        {
            // Arrange
            var mockPortalController = new Mock<IPortalController>();
            var portals = new ArrayList { new PortalInfo { PortalID = 1 } };
            mockPortalController.Setup(x => x.GetPortals()).Returns(portals);
            PortalController.SetTestableInstance(mockPortalController.Object);

            this.mockPortalAliasService.Setup(x => x.GetPortalAliasesByPortalId(It.IsAny<int>())).Returns(new[] { new PortalAliasInfo { HTTPAlias = "valid.lvh.me" } });
            this.mockPortalAliasService.As<IPortalAliasController>().Setup(x => x.GetPortalAliasesByPortalId(It.IsAny<int>())).Returns(new[] { new PortalAliasInfo { HTTPAlias = "valid.lvh.me" } });

            var mockGlobals = new Mock<IGlobals>();
            mockGlobals.Setup(x => x.ApplicationPath).Returns(string.Empty);
            TestableGlobals.SetTestableInstance(mockGlobals.Object);

            // Act
            var parm = new PortalAliasRouteManager(this.mockPortalAliasService.Object);
            parm.GetRoutePrefixCounts();
            parm.GetRoutePrefixCounts();

            // Assert
            mockPortalController.Verify(x => x.GetPortals(), Times.Once());
        }

        [Test]
        public void PrefixCountsCacheCanBeCleared()
        {
            // Arrange
            var mockPortalController = new Mock<IPortalController>();
            var portals = new ArrayList { new PortalInfo { PortalID = 1 } };
            mockPortalController.Setup(x => x.GetPortals()).Returns(portals);
            PortalController.SetTestableInstance(mockPortalController.Object);

            this.mockPortalAliasService.Setup(x => x.GetPortalAliasesByPortalId(It.IsAny<int>())).Returns(new[] { new PortalAliasInfo { HTTPAlias = "valid.lvh.me" } });
            this.mockPortalAliasService.As<IPortalAliasController>().Setup(x => x.GetPortalAliasesByPortalId(It.IsAny<int>())).Returns(new[] { new PortalAliasInfo { HTTPAlias = "valid.lvh.me" } });

            var mockGlobals = new Mock<IGlobals>();
            mockGlobals.Setup(x => x.ApplicationPath).Returns(string.Empty);
            TestableGlobals.SetTestableInstance(mockGlobals.Object);

            // Act
            var parm = new PortalAliasRouteManager(this.mockPortalAliasService.Object);
            parm.GetRoutePrefixCounts();
            parm.ClearCachedData();
            parm.GetRoutePrefixCounts();

            // Assert
            mockPortalController.Verify(x => x.GetPortals(), Times.Exactly(2));
        }

        [Test]
        public void VirtralDirWithChildPortalHasABlankAndASinglePrefix()
        {
            // Arrange
            var mockPortalController = new Mock<IPortalController>();
            var portals = new ArrayList { new PortalInfo { PortalID = 1 } };
            mockPortalController.Setup(x => x.GetPortals()).Returns(portals);
            PortalController.SetTestableInstance(mockPortalController.Object);

            this.mockPortalAliasService.Setup(x => x.GetPortalAliasesByPortalId(It.IsAny<int>())).Returns(new[]
                {
                    new PortalAliasInfo { HTTPAlias = "valid.lvh.me/vdir" },
                    new PortalAliasInfo { HTTPAlias = "valid.lvh.me/vdir/child" },
                });
            this.mockPortalAliasService.As<IPortalAliasController>()
                .Setup(x => x.GetPortalAliasesByPortalId(It.IsAny<int>())).Returns(new[]
                {
                    new PortalAliasInfo { HTTPAlias = "valid.lvh.me/vdir" },
                    new PortalAliasInfo { HTTPAlias = "valid.lvh.me/vdir/child" },
                });

            var mockGlobals = new Mock<IGlobals>();
            mockGlobals.Setup(x => x.ApplicationPath).Returns("/vdir");
            TestableGlobals.SetTestableInstance(mockGlobals.Object);

            // Act
            List<int> prefixes = new PortalAliasRouteManager(this.mockPortalAliasService.Object).GetRoutePrefixCounts().ToList();

            // Assert
            Assert.That(prefixes, Is.EqualTo(new[] { 1, 0 }).AsCollection);
        }

        [Test]
        [TestCase("mfn", "name", 0, "mfn-name-0")]
        [TestCase("mfn", "", 1, "mfn--1")]
        [TestCase("first", "second", 99, "first-second-99")]
        public void GetRouteNameHashesNameInCorrectFormat(string moduleFolderName, string routeName, int count, string expected)
        {
            // Arrange

            // Act
            var result = new PortalAliasRouteManager(this.mockPortalAliasService.Object).GetRouteName(moduleFolderName, routeName, count);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("mfn", "name", "ce.lvh.me", "mfn-name-0")]
        [TestCase("mfn", "", "ce.lvh.me/child", "mfn--1")]
        [TestCase("first", "second", "ce.lvh.me/child1/child2/child3/child4/child5", "first-second-5")]
        public void GetRouteNameWithPortalAliasInfoHashesNameInCorrectFormat(string moduleFolderName, string routeName, string httpAlias, string expected)
        {
            // Arrange

            // Act
            var result = new PortalAliasRouteManager(this.mockPortalAliasService.Object).GetRouteName(moduleFolderName, routeName, new PortalAliasInfo { HTTPAlias = httpAlias });

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        [TestCase("")]
        [TestCase(null)]
        public void GetRouteNameThrowsOnEmptyModuleFolderName(string moduleFolderName)
        {
            // Arrange

            // Act
            Assert.Throws<ArgumentException>(() => new PortalAliasRouteManager(this.mockPortalAliasService.Object).GetRouteName(moduleFolderName, string.Empty, 0));

            // Assert
        }

        [Test]
        public void GetRouteNameThrowsOnCountLessThan0()
        {
            // Arrange

            // Act
            Assert.Throws<ArgumentOutOfRangeException>(() => new PortalAliasRouteManager(this.mockPortalAliasService.Object).GetRouteName("foo", string.Empty, -1));

            // Assert
        }

        [Test]
        public void GetAllRouteValuesWorksWithNullRouteValues()
        {
            // Arrange

            // Act
            new PortalAliasRouteManager(this.mockPortalAliasService.Object).GetAllRouteValues(new PortalAliasInfo { HTTPAlias = string.Empty }, null);

            // Assert
            Assert.Pass();
        }

        [Test]
        public void GetAllRouteValuesPreservesPassedInRouteValues()
        {
            // Arrange

            // Act
            var result = new PortalAliasRouteManager(this.mockPortalAliasService.Object).GetAllRouteValues(
                new PortalAliasInfo { HTTPAlias = string.Empty },
                new { value1 = 1, value2 = 2 });

            // Assert
            var expected = new Dictionary<string, object> { { "value1", 1 }, { "value2", 2 } };
            Assert.That(result, Is.EquivalentTo(expected));
        }

        [Test]
        public void GetAllRouteValuesExtractsChildPortalParams()
        {
            // Arrange

            // Act
            var result =
                new PortalAliasRouteManager(this.mockPortalAliasService.Object).GetAllRouteValues(
                    new PortalAliasInfo { HTTPAlias = "ce.lvh.me/child" },
                    null);

            // Assert
            var expected = new Dictionary<string, object> { { "prefix0", "child" } };
            Assert.That(result, Is.EquivalentTo(expected));
        }

        [Test]
        public void GetAllRouteValuesExtractsManyChildPortalParamsAndPreservesRouteValues()
        {
            // Arrange

            // Act
            var result =
                new PortalAliasRouteManager(this.mockPortalAliasService.Object).GetAllRouteValues(
                    new PortalAliasInfo { HTTPAlias = "ce.lvh.me/child0/child1/child2/child3" },
                    new { value1 = 1, value2 = 2 });

            // Assert
            var expected = new Dictionary<string, object> { { "prefix0", "child0" }, { "prefix1", "child1" }, { "prefix2", "child2" }, { "prefix3", "child3" }, { "value1", 1 }, { "value2", 2 } };
            Assert.That(result, Is.EquivalentTo(expected));
        }
    }

#pragma warning restore CS0618
}
