// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc
{
    using DotNetNuke.Web.Mvc.Framework.ActionResults;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;

    using NUnit.Framework;

    public static class ResultAssert
    {
        public static void IsEmpty(IActionResult result)
        {
            Assert.That(result, Is.InstanceOf<EmptyResult>());
        }

        public static void IsUnauthorized(IActionResult result)
        {
            Assert.That(result, Is.InstanceOf<UnauthorizedResult>());
        }

        public static void IsView(IActionResult result, string viewName)
        {
            IsView(result, viewName, string.Empty, new RouteValueDictionary());
        }

        public static void IsView(IActionResult result, string viewName, string masterName, RouteValueDictionary expectedViewData)
        {
            var viewResult = result.AssertCast<DnnViewResult>();
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
