// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Tests.Web.Mvc
{
    using System.Collections.Generic;

    using Microsoft.AspNetCore.Routing;

    using NUnit.Framework;

    /// <summary>Assertions on dictionaries of route/view data values.</summary>
    public static class DictionaryAssert
    {
        /// <summary>Asserts that the actual dictionary contains every entry of the given object's properties.</summary>
        /// <param name="expected">An object whose properties are the expected entries.</param>
        /// <param name="actual">The dictionary to check.</param>
        public static void ContainsEntries(object expected, IDictionary<string, object> actual)
        {
            ContainsEntries(new RouteValueDictionary(expected), actual);
        }

        /// <summary>Asserts that the actual dictionary contains every expected entry.</summary>
        /// <param name="expected">The expected entries.</param>
        /// <param name="actual">The dictionary to check.</param>
        public static void ContainsEntries(IDictionary<string, object> expected, IDictionary<string, object> actual)
        {
            foreach (KeyValuePair<string, object> pair in expected)
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(actual, Does.ContainKey(pair.Key));
                    Assert.That(actual[pair.Key], Is.EqualTo(pair.Value));
                }
            }
        }
    }
}
