// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Tests.Core.Services.CryptographyProviders
{
    using System;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Services.Cryptography;
    using DotNetNuke.Tests.Utilities.Fakes;

    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    public class FipsCompilanceCryptographyProviderTests
    {
        private FakeServiceProvider serviceProvider;

        [SetUp]
        public void SetUp()
        {
            // Config.GetDecryptionkey() now reads the machineKey from web.config via an
            // IApplicationStatusInfo resolved from the DI container. Point the application map path
            // at the test output directory, which contains a minimal web.config with a machineKey.
            var appStatus = new Mock<IApplicationStatusInfo>();
            appStatus.SetupGet(x => x.ApplicationMapPath).Returns(AppContext.BaseDirectory);
            this.serviceProvider = FakeServiceProvider.Setup(
                services => services.AddSingleton(appStatus.Object));
        }

        [TearDown]
        public void TearDown()
        {
            this.serviceProvider.Dispose();
        }
        [Test]
        public void EncryptData_Should_Return_Encrypted_String()
        {
            var message = "Hello world!";
            var encryptionKey = Config.GetDecryptionkey();

            // Arrange
            var provider = new FipsCompilanceCryptographyProvider();

            // Act
            var encryptedValue = provider.EncryptParameter(message, encryptionKey);

            // Assert
            Assert.That(encryptedValue, Is.Not.EqualTo(message));
        }

        [Test]
        public void DecryptData_Should_Return_Empty_String_If_Data_Is_Not_Encrypted()
        {
            var message = "Hello world!";
            var encryptionKey = Config.GetDecryptionkey();

            // Arrange
            var provider = new FipsCompilanceCryptographyProvider();

            // Act
            var decryptedValue = provider.DecryptParameter(message, encryptionKey);

            // Assert
            Assert.That(decryptedValue, Is.EqualTo(string.Empty));
        }
    }
}
