// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    using System;
    using System.Linq;

    using Microsoft.AspNetCore.Routing;

    /// <summary>Validates that the constraints on a route are of a type that can be processed.</summary>
    internal static class ConstraintValidation
    {
        public static void Validate(DnnMvcRoute route)
        {
            if (route?.Constraints == null)
            {
                return;
            }

            foreach (var kvp in route.Constraints.Where(kvp => kvp.Value is not string).Where(kvp => kvp.Value is not IRouteConstraint))
            {
                throw new InvalidOperationException($"Invalid constraint '{kvp.Key}'. Constraints must be a string or an {nameof(IRouteConstraint)}.");
            }
        }
    }
}
