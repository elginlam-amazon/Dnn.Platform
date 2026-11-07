// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Tests.Web.Api
{
    using System.Collections.Generic;
    using System.Net;
    using System.Reflection;

    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Web.Api.Internal;

    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Infrastructure;

    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class DnnControllerSelectorTests
    {
        private FakeServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            this.serviceProvider = FakeServiceProvider.Setup();
        }

        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();
        }

        [TestCase("Sample")]
        [TestCase("SAmple")]
        [TestCase("SAMPLE")]
        public void SelectControllerIsCaseInsensitive(string controllerTypeName)
        {
            const string expectedNamespace = "ExpectedNamespace";

            var controllerType = GetMockControllerType("Sample", expectedNamespace);
            var selector = new DnnControllerSelector(GetActionDescriptorCollectionProvider(controllerType));

            // Act
            var result = selector.SelectController(controllerTypeName, new[] { expectedNamespace }, "http://foo.com/api/sample");

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.ControllerTypeFullName, Is.EqualTo(controllerType.FullName));
            }
        }

        [Test]
        public void SelectControllerIgnoresControllersInUnexpectedNamespaces()
        {
            const string expectedNamespace = "ExpectedNamespace";
            const string unexpectedNamespace = "UnexpectedNamespace";
            const string controllerName = "Sample";

            var controllerType = GetMockControllerType(controllerName, unexpectedNamespace);
            var selector = new DnnControllerSelector(GetActionDescriptorCollectionProvider(controllerType));

            // Act
            var result = selector.SelectController(controllerName, new[] { expectedNamespace }, "http://foo.com/api/sample");

            // Assert
            // The legacy selector threw an HttpResponseException carrying a 404. ASP.NET Core selects
            // controllers through endpoint selection, so the selector reports the same failure (and the
            // same status code and message) through its result instead of throwing.
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ControllerTypeFullName, Is.Null);
                Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            }
        }

        [Test]
        public void SelectControllerThrowsOnAmgiuousControllers()
        {
            const string expectedNamespace = "ExpectedNamespace";
            const string expectedNamespace2 = "ExpectedNamespace2";
            const string controllerName = "Sample";

            var controllerType = GetMockControllerType(controllerName, expectedNamespace);
            var controllerType2 = GetMockControllerType(controllerName, expectedNamespace2);
            var selector = new DnnControllerSelector(GetActionDescriptorCollectionProvider(controllerType, controllerType2));

            // Act
            var result = selector.SelectController(
                controllerName,
                new[] { expectedNamespace, expectedNamespace2 },
                "http://foo.com/api/sample");

            // Assert
            // Same failure the legacy HttpResponseException reported, with the ambiguity status code.
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.ControllerTypeFullName, Is.Null);
                Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            }
        }

        /// <summary>
        /// Creates the action descriptor collection provider the selector reads. Replaces the Web API 2
        /// <c>IHttpControllerTypeResolver</c> which the legacy test replaced in
        /// <c>HttpConfiguration.Services</c>.
        /// </summary>
        /// <param name="controllerTypes">The controller types which are part of the application.</param>
        /// <returns>An <see cref="IActionDescriptorCollectionProvider"/>.</returns>
        private static IActionDescriptorCollectionProvider GetActionDescriptorCollectionProvider(params TypeInfo[] controllerTypes)
        {
            var descriptors = new List<ActionDescriptor>();
            foreach (var controllerType in controllerTypes)
            {
                descriptors.Add(new ControllerActionDescriptor
                {
                    ActionName = "Index",
                    ControllerName = controllerType.Name,
                    ControllerTypeInfo = controllerType,
                });
            }

            var provider = new Mock<IActionDescriptorCollectionProvider>();
            provider.SetupGet(p => p.ActionDescriptors).Returns(new ActionDescriptorCollection(descriptors, 1));
            return provider.Object;
        }

        private static TypeInfo GetMockControllerType(string controllerName, string @namespace)
        {
            var mockType = new Mock<TypeInfo>();

            mockType.Setup(t => t.Name).Returns(controllerName + "Controller");
            mockType.Setup(t => t.FullName).Returns(string.Format("{0}.{1}Controller", @namespace, controllerName));
            return mockType.Object;
        }
    }
}
