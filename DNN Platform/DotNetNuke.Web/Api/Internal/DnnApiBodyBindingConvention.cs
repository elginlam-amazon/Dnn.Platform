// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.Threading;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.ApplicationModels;
    using Microsoft.AspNetCore.Mvc.ModelBinding;

    /// <summary>
    /// Restores the ASP.NET Web API 2 parameter-binding rule for <see cref="DnnApiController"/> actions: a
    /// complex-typed parameter is read from the request body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Web API 2 bound simple types (and types with a <c>TypeConverter</c> from string) from the route and
    /// query string, and bound a single complex type from the body — without any attribute. ASP.NET Core
    /// reverses that default: outside of a <c>[ApiController]</c>-annotated controller a complex type is bound
    /// from route/query/form values ONLY, and the request body is never read unless the parameter carries
    /// <c>[FromBody]</c>.
    /// </para>
    /// <para>
    /// <see cref="DnnApiController"/> deliberately does not carry <c>[ApiController]</c> — that attribute
    /// requires attribute routing on every action and throws at startup for the conventionally-routed
    /// controllers the DNN services framework maps. So the binding rule has to be reinstated explicitly, and
    /// it has to be reinstated as a CONVENTION rather than as ~25 hand-placed <c>[FromBody]</c> attributes:
    /// the rule is also part of the public contract for every third-party module controller deriving from
    /// <see cref="DnnApiController"/>, and those live outside this repository. Without it, every JSON POST in
    /// the services framework silently receives a default-valued DTO: no exception, no bad request, nothing in
    /// the log — a green build and a dead feature.
    /// </para>
    /// <para>
    /// A parameter is left alone when it already states its binding source (<c>[FromBody]</c>,
    /// <c>[FromForm]</c>, <c>[FromQuery]</c>, <c>[FromRoute]</c>, <c>[FromHeader]</c>, <c>[FromServices]</c>),
    /// when it is a simple type, or when it is one of the framework types that are never body-bound
    /// (<see cref="IFormFile"/>, <see cref="IFormFileCollection"/>, <see cref="CancellationToken"/>).
    /// </para>
    /// </remarks>
    internal sealed class DnnApiBodyBindingConvention : IApplicationModelConvention
    {
        /// <inheritdoc />
        public void Apply(ApplicationModel application)
        {
            ArgumentNullException.ThrowIfNull(application);

            foreach (var controller in application.Controllers)
            {
                if (!typeof(DnnApiController).IsAssignableFrom(controller.ControllerType))
                {
                    continue;
                }

                foreach (var action in controller.Actions)
                {
                    foreach (var parameter in action.Parameters)
                    {
                        if (ShouldBindFromBody(parameter))
                        {
                            parameter.BindingInfo ??= new BindingInfo();
                            parameter.BindingInfo.BindingSource = BindingSource.Body;
                        }
                    }
                }
            }
        }

        private static bool ShouldBindFromBody(ParameterModel parameter)
        {
            // An explicit binding source always wins -- never override what the action author stated.
            if (parameter.BindingInfo?.BindingSource != null)
            {
                return false;
            }

            var type = parameter.ParameterType;

            // Nullable<T> binds like T.
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (ModelMetadataExtensions.IsSimpleType(type))
            {
                return false;
            }

            // Framework types that have their own binding and must not be read as a body.
            return !typeof(IFormFile).IsAssignableFrom(type)
                && !typeof(IFormFileCollection).IsAssignableFrom(type)
                && !typeof(IFormCollection).IsAssignableFrom(type)
                && type != typeof(CancellationToken)
                && type != typeof(HttpContext)
                && type != typeof(HttpRequest)
                && type != typeof(HttpResponse);
        }

        /// <summary>Mirrors the framework's own "is this bound from a value provider" test.</summary>
        private static class ModelMetadataExtensions
        {
            /// <summary>Determines whether <paramref name="type"/> binds from route/query values rather than from the body.</summary>
            /// <param name="type">The parameter type.</param>
            /// <returns><see langword="true"/> when the type is a simple (value-provider bound) type.</returns>
            internal static bool IsSimpleType(Type type)
            {
                return type.IsPrimitive
                    || type.IsEnum
                    || type == typeof(string)
                    || type == typeof(decimal)
                    || type == typeof(DateTime)
                    || type == typeof(DateTimeOffset)
                    || type == typeof(DateOnly)
                    || type == typeof(TimeOnly)
                    || type == typeof(TimeSpan)
                    || type == typeof(Guid)
                    || type == typeof(Uri)
                    || type == typeof(Version);
            }
        }
    }
}
