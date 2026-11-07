// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc.Framework.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;

    using DotNetNuke.Tests.Utilities.Fakes;
    using DotNetNuke.Tests.Web.Mvc.Fakes;
    using DotNetNuke.Tests.Web.Mvc.Fakes.Filters;
    using DotNetNuke.Web.Mvc.Framework.Modules;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;

    using Microsoft.Extensions.DependencyInjection;

    using Moq;
    using NUnit.Framework;

    [TestFixture]
    public class ResultCapturingActionInvokerTests
    {
        private ResultCapturingActionInvoker actionInvoker;
        private FakeServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            this.serviceProvider = FakeServiceProvider.Setup(
                services => services.AddSingleton(Mock.Of<IControllerFactory>()));
            this.actionInvoker = new ResultCapturingActionInvoker();
        }

        [TearDown]
        public void TearDown()
        {
            this.actionInvoker = null;

            // The fake filters publish their result through static state, reset it so the tests stay
            // order-independent.
            FakeRedirectAttribute.Result = new EmptyResult();
            FakeHandleExceptionRedirectAttribute.IsExceptionHandled = false;
            this.serviceProvider.Dispose();
        }

        [Test]
        public async Task InvokeActionResult_Sets_ResultOfLastInvoke()
        {
            // Arrange
            var httpContext = MockHelper.CreateMockHttpContext();

            var controller = new FakeController();
            controller.ControllerContext = MockHelper.CreateMockControllerContext(httpContext, new RouteData(), controller);

            // Act
            await this.RunAction(controller, controller.ControllerContext, nameof(FakeController.Index));

            // Assert
            Assert.That(this.actionInvoker.ResultOfLastInvoke, Is.Not.Null);
            Assert.That(this.actionInvoker.ResultOfLastInvoke, Is.InstanceOf<ViewResult>());
        }

        [Test]
        public async Task ActionInvoker_InvokeExceptionFilters_IsExceptionHandled_True()
        {
            // Arrange
            var httpContext = MockHelper.CreateMockHttpContext();
            var controller = SetupController(httpContext);
            FakeHandleExceptionRedirectAttribute.IsExceptionHandled = true;
            var expectedResult = FakeHandleExceptionRedirectAttribute.Result;

            // Act
            await this.RunAction(controller, controller.ControllerContext, nameof(FakeDnnController.ActionWithExceptionFilter));

            // Assert
            Assert.That(this.actionInvoker.ResultOfLastInvoke, Is.EqualTo(expectedResult));
        }

        [Test]
        public void ActionInvoker_InvokeExceptionFilters_IsExceptionHandled_False()
        {
            // Arrange
            var httpContext = MockHelper.CreateMockHttpContext();
            var controller = SetupController(httpContext);
            FakeHandleExceptionRedirectAttribute.IsExceptionHandled = false;
            var expectedResult = FakeRedirectAttribute.Result;

            // Act
            Assert.ThrowsAsync<Exception>(() => this.RunAction(controller, controller.ControllerContext, nameof(FakeDnnController.ActionWithExceptionFilter)));

            // Assert
            Assert.That(this.actionInvoker.ResultOfLastInvoke, Is.EqualTo(expectedResult));
        }

        [Test]
        public async Task ActionInvoker_InvokeOnExecutingFilters()
        {
            // Arrange
            var httpContext = MockHelper.CreateMockHttpContext();
            var controller = SetupController(httpContext);
            var expectedResult = FakeRedirectAttribute.Result;

            // Act
            await this.RunAction(controller, controller.ControllerContext, nameof(FakeDnnController.ActionWithOnExecutingFilter));

            // Assert
            Assert.That(this.actionInvoker.ResultOfLastInvoke, Is.EqualTo(expectedResult));
        }

        [Test]
        public async Task ActionInvoker_InvokeOnExecutedFilters()
        {
            // Arrange
            var httpContext = MockHelper.CreateMockHttpContext();
            var controller = SetupController(httpContext);
            var expectedResult = FakeRedirectAttribute.Result;

            // Act
            await this.RunAction(controller, controller.ControllerContext, nameof(FakeDnnController.ActionWithOnExecutedFilter));

            // Assert
            Assert.That(this.actionInvoker.ResultOfLastInvoke, Is.EqualTo(expectedResult));
        }

        private static FakeDnnController SetupController(HttpContext context)
        {
            var controller = new FakeDnnController();
            controller.ControllerContext = MockHelper.CreateMockControllerContext(context, new RouteData(), controller);
            return controller;
        }

        /// <summary>
        /// Runs an action of the given controller through the capturing filter the way
        /// <see cref="ModuleApplication"/> drives it, which replaces the MVC 5
        /// <c>ControllerActionInvoker.InvokeAction(ControllerContext, string)</c> call of the original tests.
        /// </summary>
        /// <param name="controller">The controller instance owning the action.</param>
        /// <param name="controllerContext">The controller context of the request.</param>
        /// <param name="actionName">The name of the action method to run.</param>
        /// <returns>A task.</returns>
        /// <remarks>
        /// ASP.NET Core has no action invoker to subclass: <see cref="ResultCapturingActionInvoker"/> is a
        /// filter. This helper reproduces the pipeline MVC would run: the capturing filter is attached to
        /// the request and is the outermost action filter, the action's own filters run inside its
        /// continuation, and the produced result is handed to the result filter stage. The fake filters are
        /// plain <see cref="Attribute"/>s implementing <see cref="IActionFilter"/>/
        /// <see cref="IExceptionFilter"/>, so they are not auto-discovered and are pulled off the action
        /// method by reflection, exactly like MVC's filter providers do for attribute filters.
        /// </remarks>
        private async Task RunAction(object controller, ControllerContext controllerContext, string actionName)
        {
            var method = controller.GetType().GetMethod(actionName);
            var attributes = method.GetCustomAttributes(inherit: true);
            var actionFilters = attributes.OfType<IActionFilter>().ToList();
            var exceptionFilters = attributes.OfType<IExceptionFilter>().ToList();

            var filters = new List<IFilterMetadata>(attributes.OfType<IFilterMetadata>()) { this.actionInvoker };

            this.actionInvoker.AttachTo(controllerContext.HttpContext);
            try
            {
                var executingContext = new ActionExecutingContext(
                    controllerContext,
                    filters,
                    new Dictionary<string, object>(),
                    controller);

                await this.actionInvoker.OnActionExecutionAsync(
                    executingContext,
                    () => this.InvokeActionAsync(controller, controllerContext, method, filters, actionFilters, exceptionFilters, executingContext));

                var resultExecutingContext = new ResultExecutingContext(
                    controllerContext,
                    filters,
                    this.actionInvoker.ResultOfLastInvoke ?? new EmptyResult(),
                    controller);

                await this.actionInvoker.OnResultExecutionAsync(
                    resultExecutingContext,
                    () => Task.FromResult(new ResultExecutedContext(
                        controllerContext,
                        filters,
                        resultExecutingContext.Result,
                        controller)));
            }
            finally
            {
                ResultCapturingActionInvoker.DetachFrom(controllerContext.HttpContext);
            }
        }

        private async Task<ActionExecutedContext> InvokeActionAsync(
            object controller,
            ControllerContext controllerContext,
            MethodInfo method,
            IList<IFilterMetadata> filters,
            IList<IActionFilter> actionFilters,
            IList<IExceptionFilter> exceptionFilters,
            ActionExecutingContext executingContext)
        {
            foreach (var filter in actionFilters)
            {
                filter.OnActionExecuting(executingContext);
            }

            var executedContext = new ActionExecutedContext(controllerContext, filters, controller);
            if (executingContext.Result is not null)
            {
                // A filter short-circuited the action: MVC skips the action and reports the result back to
                // the outer filters with Canceled set.
                executedContext.Canceled = true;
                executedContext.Result = executingContext.Result;
                return executedContext;
            }

            Exception thrown = null;
            try
            {
                executedContext.Result = (IActionResult)method.Invoke(controller, null);
            }
            catch (TargetInvocationException exception)
            {
                thrown = exception.InnerException;
                executedContext.Exception = thrown;
            }

            foreach (var filter in actionFilters)
            {
                filter.OnActionExecuted(executedContext);
            }

            if (thrown is null)
            {
                return executedContext;
            }

            // Exception filters wrap the action in ASP.NET Core; they get the chance to produce a result and
            // to mark the exception as handled, after which the capturing filter records the result.
            var exceptionContext = new ExceptionContext(controllerContext, filters) { Exception = thrown };
            foreach (var filter in exceptionFilters)
            {
                filter.OnException(exceptionContext);
            }

            await this.actionInvoker.OnExceptionAsync(exceptionContext);

            if (!exceptionContext.ExceptionHandled)
            {
                // Unhandled: MVC rethrows, which is what the original InvokeAction call did.
                ExceptionDispatchInfo.Capture(thrown).Throw();
            }

            executedContext.Exception = null;
            executedContext.ExceptionHandled = true;
            executedContext.Result = exceptionContext.Result;
            return executedContext;
        }
    }
}
