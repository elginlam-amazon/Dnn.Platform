// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Linq;

    using Microsoft.AspNetCore.Routing;

    /// <summary>Validates that the constraints on a DNN service route are of a type that endpoint routing can process.</summary>
    /// <remarks>
    /// A constraint of an ASP.NET Core <see cref="RouteEndpoint"/> is either an inline/parsable constraint
    /// expression (a <see cref="string"/>, resolved by the inline constraint resolver) or an
    /// <c>IRouteConstraint</c> instance. Anything else cannot be mapped, so the routes are validated
    /// while the module registers them rather than when the host builds its endpoints.
    /// </remarks>
    internal static class ConstraintValidation
    {
        /// <summary>Validates the constraints of the <paramref name="route"/>.</summary>
        /// <param name="route">The route to validate.</param>
        public static void Validate(DnnServiceRoute route)
        {
            Contract.Assert(route != null);
            Contract.Assert(route.Url != null);

            if (route.Constraints == null)
            {
                return;
            }

            foreach (var kvp in route.Constraints.Where(kvp => kvp.Value is not string).Where(kvp => kvp.Value is not Microsoft.AspNetCore.Routing.IRouteConstraint))
            {
                throw new InvalidOperationException($"Invalid Constraint '{kvp.Key}', a route constraint must be a constraint expression or an {typeof(Microsoft.AspNetCore.Routing.IRouteConstraint).FullName}");
            }
        }
    }
}
