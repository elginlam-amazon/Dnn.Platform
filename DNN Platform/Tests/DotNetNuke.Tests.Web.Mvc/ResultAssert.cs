// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;

    using NUnit.Framework;

    /// <summary>Assertions on the action results returned by the module controllers.</summary>
    public static class ResultAssert
    {
        /// <summary>Asserts that the result is an empty result.</summary>
        /// <param name="result">The result to check.</param>
        public static void IsEmpty(IActionResult result)
        {
            Assert.That(result, Is.InstanceOf<EmptyResult>());
        }

        /// <summary>Asserts that the result denies the request.</summary>
        /// <param name="result">The result to check.</param>
        public static void IsUnauthorized(IActionResult result)
        {
            Assert.That(result, Is.InstanceOf<UnauthorizedResult>());
        }

        /// <summary>Asserts that the result renders the given view.</summary>
        /// <param name="result">The result to check.</param>
        /// <param name="viewName">The expected view name.</param>
        public static void IsView(IActionResult result, string viewName)
        {
            IsView(result, viewName, string.Empty, new RouteValueDictionary());
        }

        /// <summary>Asserts that the result renders the given view with the given view data.</summary>
        /// <param name="result">The result to check.</param>
        /// <param name="viewName">The expected view name.</param>
        /// <param name="masterName">The expected master page (layout) name.</param>
        /// <param name="expectedViewData">The expected view data entries.</param>
        public static void IsView(IActionResult result, string viewName, string masterName, RouteValueDictionary expectedViewData)
        {
            ViewResult viewResult = result.AssertCast<ViewResult>();
            StringsEqualOrBothNullOrEmpty(viewName, viewResult.ViewName);
            StringsEqualOrBothNullOrEmpty(viewName, viewResult.ViewName);

            DictionaryAssert.ContainsEntries(expectedViewData, viewResult.ViewData);
        }

        private static TCast AssertCast<TCast>(this IActionResult result)
            where TCast : class
        {
            var castResult = result as TCast;
            Assert.That(castResult, Is.Not.Null);
            return castResult;
        }

        private static void StringsEqualOrBothNullOrEmpty(string expected, string actual)
        {
            if (string.IsNullOrEmpty(expected))
            {
                Assert.That(actual, Is.Null.Or.Empty);
            }
            else
            {
                Assert.That(actual, Is.EqualTo(expected));
            }
        }
    }
}
