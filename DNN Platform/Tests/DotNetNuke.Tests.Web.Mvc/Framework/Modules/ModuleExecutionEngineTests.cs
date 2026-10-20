// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Framework.Modules
{
    using System;
    using System.IO;

    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class ModuleExecutionEngineTests
    {
        [Test]
        public void ExecuteModule_Requires_NonNull_ModuleRequestContext()
        {
            var engine = new ModuleExecutionEngine();
            Assert.Throws<ArgumentNullException>(() => engine.ExecuteModule(null));
        }

        [Test]
        public void ExecuteModule_Returns_Null_If_Application_Is_Null()
        {
            // Arrange
            var engine = new ModuleExecutionEngine();
            var requestContext = new ModuleRequestContext() { ModuleApplication = null };

            // Act
            ModuleRequestResult result = engine.ExecuteModule(requestContext);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ExecuteModule_Executes_ModuleApplication_If_Not_Null()
        {
            // Arrange
            var engine = new ModuleExecutionEngine();

            var moduleApp = new Mock<ModuleApplication>();
            var requestContext = new ModuleRequestContext() { ModuleApplication = moduleApp.Object };

            // Act
            engine.ExecuteModule(requestContext);

            // Assert
            moduleApp.Verify(app => app.ExecuteRequest(It.IsAny<ModuleRequestContext>()));
        }

        [Test]
        public void ExecuteModule_Returns_Result_Of_Executing_ModuleApplication()
        {
            // Arrange
            var engine = new ModuleExecutionEngine();
            var expected = new ModuleRequestResult();

            var moduleApp = new Mock<ModuleApplication>();
            moduleApp.Setup(app => app.ExecuteRequest(It.IsAny<ModuleRequestContext>()))
                    .Returns(expected);
            var requestContext = new ModuleRequestContext() { ModuleApplication = moduleApp.Object };

            // Act
            ModuleRequestResult actual = engine.ExecuteModule(requestContext);

            // Assert
            Assert.That(actual, Is.SameAs(expected));
        }

        [Test]
        public void ExecuteModuleResult_Requires_NonNull_Arguments()
        {
            // Arrange
            var engine = new ModuleExecutionEngine();

            // Act, Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.Throws<ArgumentNullException>(() => engine.ExecuteModuleResult(null, new StringWriter()));
                Assert.Throws<ArgumentNullException>(() => engine.ExecuteModuleResult(new ModuleRequestResult(), null));
            }
        }

        [Test]
        public void ExecuteModuleResult_Writes_RenderedOutput_To_Writer()
        {
            // Arrange
            var engine = new ModuleExecutionEngine();
            const string expectedOutput = "<p>Hello from the module.</p>";
            var moduleRequestResult = new ModuleRequestResult { RenderedOutput = expectedOutput };
            var writer = new StringWriter();

            // Act
            engine.ExecuteModuleResult(moduleRequestResult, writer);

            // Assert
            Assert.That(writer.ToString(), Is.EqualTo(expectedOutput));
        }
    }
}
