// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Routing
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Linq;

    using Microsoft.AspNetCore.Routing;

    /// <summary>Validates that the constraints on a DNN MVC route are of a type that endpoint routing can process.</summary>
    /// <remarks>
    /// A constraint of an ASP.NET Core <see cref="RouteEndpoint"/> is either an inline/parsable constraint
    /// expression (a <see cref="string"/>, resolved by the inline constraint resolver) or an
    /// <c>IRouteConstraint</c> instance. Anything else cannot be mapped, so the routes are validated
    /// when the module registers them rather than when the host builds its endpoints.
    /// </remarks>
    internal static class ConstraintValidation
    {
        public static void Validate(DnnMvcRoute route)
        {
            Contract.Assert(route != null);
            Contract.Assert(route.Url != null);

            if (route.Constraints == null)
            {
                return;
            }

            foreach (var kvp in route.Constraints.Where(kvp => kvp.Value is not string).Where(kvp => kvp.Value is not IRouteConstraint))
            {
                throw new InvalidOperationException("Invalid Constraint", new RouteConstraintNameException(typeof(IRouteConstraint).FullName));
            }
        }

        private class RouteConstraintNameException : Exception
        {
            /// <inheritdoc />
            public RouteConstraintNameException(string message)
                : base(message)
            {
            }
        }
    }
}
