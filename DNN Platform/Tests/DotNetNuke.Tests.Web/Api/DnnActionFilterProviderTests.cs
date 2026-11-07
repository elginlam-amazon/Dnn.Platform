// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Tests.Web.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Tests.Utilities.Mocks;
    using DotNetNuke.Web.Api;
    using DotNetNuke.Web.Api.Internal;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Authorization;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;

    using NUnit.Framework;

    [TestFixture]
    public class DnnActionFilterProviderTests
    {
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
            var filterProvider = new DnnActionFilterProvider();
            filterProvider.OnProvidersExecuting(context);
            filterProvider.OnProvidersExecuted(context);
            var filters = context.Results.ToList();

            // Assert
            Assert.That(filters, Has.Count.EqualTo(1));
            Assert.That(filters.First().Filter, Is.InstanceOf<RequireHostAttribute>());
        }

        [Test]
        public void RequiresHostAttributeNotAddedWhenAnOverrideAuthFilterPresent()
        {
            // Arrange
            var context = CreateProviderContext(new DnnAuthorizeAttribute());

            // Act
            var filterProvider = new DnnActionFilterProvider();
            filterProvider.OnProvidersExecuting(context);
            filterProvider.OnProvidersExecuted(context);
            var filters = context.Results.ToList();

            // Assert
            Assert.That(filters, Has.Count.EqualTo(1));
            Assert.That(filters.First().Filter, Is.InstanceOf<DnnAuthorizeAttribute>());
        }

        [Test]
        public void RequiresHostAttributeAddedWhenNoOverrideAuthFilterPresent()
        {
            // Arrange
            // an authorization filter which is NOT an IOverrideDefaultAuthLevel
            var context = CreateProviderContext(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));

            // Act
            var filterProvider = new DnnActionFilterProvider();
            filterProvider.OnProvidersExecuting(context);
            filterProvider.OnProvidersExecuted(context);
            var filters = context.Results.ToList();

            // Assert
            Assert.That(filters, Has.Count.EqualTo(2));
            Assert.That(filters.Last().Filter, Is.InstanceOf<RequireHostAttribute>());
        }

        /// <summary>
        /// Creates the filter provider context. Replaces the Web API 2
        /// <c>HttpActionDescriptor</c>/<c>HttpControllerDescriptor</c> mocks whose <c>GetFilters()</c> the
        /// legacy provider called: the ASP.NET Core <see cref="IFilterProvider"/> is additive, so the
        /// filters which were already discovered are seeded into <see cref="FilterProviderContext.Results"/>.
        /// </summary>
        /// <param name="filters">The filters already contributed by the MVC default filter provider.</param>
        /// <returns>A <see cref="FilterProviderContext"/> for an action on a <see cref="DnnApiController"/>.</returns>
        private static FilterProviderContext CreateProviderContext(params IFilterMetadata[] filters)
        {
            var actionDescriptor = new ControllerActionDescriptor
            {
                ActionName = "Index",
                ControllerName = "Sample",
                ControllerTypeInfo = typeof(SampleApiController).GetTypeInfo(),
                MethodInfo = typeof(SampleApiController).GetMethod(nameof(SampleApiController.Index)),
            };

            var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), actionDescriptor);
            var items = new List<FilterItem>(
                filters.Select(filter => new FilterItem(new FilterDescriptor(filter, FilterScope.Action), filter)));

            return new FilterProviderContext(actionContext, items);
        }

        /// <summary>A controller used to represent an action within the DNN services framework.</summary>
        internal class SampleApiController : DnnApiController
        {
            /// <summary>The action the filters are provided for.</summary>
            /// <returns>An <see cref="IActionResult"/>.</returns>
            public IActionResult Index()
            {
                return this.Ok();
            }
        }
    }
}
