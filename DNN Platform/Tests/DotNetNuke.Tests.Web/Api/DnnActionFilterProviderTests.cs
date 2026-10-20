// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Tests.Utilities.Mocks;
    using DotNetNuke.Web.Api;
    using DotNetNuke.Web.Api.Internal;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;

    using Moq;

    using NUnit.Framework;

    [TestFixture]
    public class DnnActionFilterProviderTests
    {
        // DnnActionFilterProvider was ported from a Web API System.Web.Http.Filters.IFilterProvider
        // (GetFilters(config, actionDescriptor)) to an ASP.NET Core
        // Microsoft.AspNetCore.Mvc.Filters.IFilterProvider (OnProvidersExecuting(FilterProviderContext)).
        // It adds a RequireHostAttribute to services framework (DnnApiController) actions that do not
        // already carry an IOverrideDefaultAuthLevel filter, and injects [Dependency] properties.
        [SetUp]
        public void SetUp()
        {
            CBO.SetTestableInstance(new MockCBO());
        }

        [TearDown]
        public void TearDown()
        {
            CBO.ClearInstance();
        }

        [Test]
        public void RequiresHostAttributeAddedWhenNoOtherActionFiltersPresent()
        {
            // Arrange
            var context = CreateProviderContext();

            // Act
            var filterProvider = new DnnActionFilterProvider(Mock.Of<IServiceProvider>());
            filterProvider.OnProvidersExecuting(context);

            // Assert
            Assert.That(context.Results, Has.Count.EqualTo(1));
            Assert.That(context.Results.Single().Filter, Is.InstanceOf<RequireHostAttribute>());
        }

        [Test]
        public void RequiresHostAttributeNotAddedWhenAnOverrideAuthFilterPresent()
        {
            // Arrange
            var context = CreateProviderContext(new DnnAuthorizeAttribute());

            // Act
            var filterProvider = new DnnActionFilterProvider(Mock.Of<IServiceProvider>());
            filterProvider.OnProvidersExecuting(context);

            // Assert
            Assert.That(context.Results, Has.Count.EqualTo(1));
            Assert.That(context.Results.Single().Filter, Is.InstanceOf<DnnAuthorizeAttribute>());
        }

        [Test]
        public void RequiresHostAttributeAddedWhenNoOverrideAuthFilterPresent()
        {
            // Arrange - an authorization filter that does not override the default auth level.
            var context = CreateProviderContext(new Mock<IAuthorizationFilter>().Object);

            // Act
            var filterProvider = new DnnActionFilterProvider(Mock.Of<IServiceProvider>());
            filterProvider.OnProvidersExecuting(context);

            // Assert
            Assert.That(context.Results, Has.Count.EqualTo(2));
            Assert.That(context.Results.Last().Filter, Is.InstanceOf<RequireHostAttribute>());
        }

        private static FilterProviderContext CreateProviderContext(params IFilterMetadata[] existingFilters)
        {
            var actionDescriptor = new ControllerActionDescriptor
            {
                // The provider only processes actions on DnnApiController-derived controllers.
                ControllerTypeInfo = typeof(SampleDnnApiController).GetTypeInfo(),
            };

            var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), actionDescriptor);

            var results = existingFilters
                .Select(filter => new FilterItem(new FilterDescriptor(filter, FilterScope.Action), filter))
                .ToList();

            return new FilterProviderContext(actionContext, results);
        }

        private sealed class SampleDnnApiController : DnnApiController
        {
        }
    }
}
