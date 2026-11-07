// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Tests.Web.Mvc.Framework.Modules
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Web.Mvc.Framework.ActionResults;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Controllers;

    using Microsoft.Extensions.DependencyInjection;

    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class ModuleExecutionEngineTests
    {
        private FakeServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            this.serviceProvider = FakeServiceProvider.Setup(
                services =>
                {
                    // ModuleApplication resolves the Core IControllerFactory from the service provider
                    // (the MVC 5 DefaultControllerFactory no longer exists).
                    services.AddSingleton(Mock.Of<IControllerFactory>());
                    services.AddSingleton(Mock.Of<IHostSettings>());
                });
        }

        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();
        }

        [Test]
        public void ExecuteModule_Requires_NonNull_ModuleRequestContext()
        {
            var engine = new ModuleExecutionEngine();
            Assert.ThrowsAsync<ArgumentNullException>(() => engine.ExecuteModule(null));
        }

        [Test]
        public async Task ExecuteModule_Returns_Null_If_Application_Is_Null()
        {
            // Arrange
            var engine = new ModuleExecutionEngine();
            var moduleContext = new ModuleRequestContext() { ModuleApplication = null };

            // Act
            var result = await engine.ExecuteModule(moduleContext);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task ExecuteModule_Executes_ModuleApplication_If_Not_Null()
        {
            // Arrange
            var engine = new ModuleExecutionEngine();

            var moduleApp = new Mock<ModuleApplication>();
            var moduleContext = new ModuleRequestContext() { ModuleApplication = moduleApp.Object };

            // Act
            await engine.ExecuteModule(moduleContext);

            // Assert
            // Action execution is asynchronous in ASP.NET Core, the engine delegates to the async overload.
            moduleApp.Verify(app => app.ExecuteRequestAsync(It.IsAny<ModuleRequestContext>(), It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task ExecuteModule_Returns_Result_Of_Executing_ModuleApplication()
        {
            // Arrange
            var engine = new ModuleExecutionEngine();
            var expected = new ModuleRequestResult();

            var moduleApp = new Mock<ModuleApplication>();
            moduleApp.Setup(app => app.ExecuteRequestAsync(It.IsAny<ModuleRequestContext>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(expected));
            var moduleContext = new ModuleRequestContext() { ModuleApplication = moduleApp.Object };

            // Act
            var actual = await engine.ExecuteModule(moduleContext);

            // Assert
            Assert.That(actual, Is.SameAs(expected));
        }

        [Test]
        public async Task ExecuteModuleResult_Calls_IDnnViewResult_ExecuteResult()
        {
            // Arrange
            var engine = new ModuleExecutionEngine();

            var actionResultMock = new Mock<IActionResult>();

            var viewResultMock = actionResultMock.As<IDnnViewResult>();

            var actionContext = MockHelper.CreateMockControllerContext();
            var moduleRequestResult = new ModuleRequestResult
            {
                ActionResult = actionResultMock.Object,
                ActionContext = actionContext,
            };

            // Act
            await engine.ExecuteModuleResultAsync(moduleRequestResult, new StringWriter());

            // Assert
            viewResultMock.Verify(v => v.ExecuteResultAsync(It.IsAny<ActionContext>(), It.IsAny<TextWriter>()));
        }

        [Test]
        public async Task ExecuteModuleResult_Calls_IDnnViewResult_ExecuteResult_With_ModuleRequestResult_ControllerContext()
        {
            // Arrange
            var engine = new ModuleExecutionEngine();

            var actionResultMock = new Mock<IActionResult>();

            var viewResultMock = actionResultMock.As<IDnnViewResult>();

            var actionContext = MockHelper.CreateMockControllerContext();
            var moduleRequestResult = new ModuleRequestResult
            {
                ActionResult = actionResultMock.Object,
                ActionContext = actionContext,
            };

            // Act
            await engine.ExecuteModuleResultAsync(moduleRequestResult, new StringWriter());

            // Assert
            viewResultMock.Verify(v => v.ExecuteResultAsync(actionContext, It.IsAny<TextWriter>()));
        }
    }
}
