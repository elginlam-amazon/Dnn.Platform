// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Api.Internal
{
    using System;
    using System.ComponentModel;
    using System.Threading;

    using Microsoft.AspNetCore.Mvc.ApplicationModels;
    using Microsoft.AspNetCore.Mvc.ModelBinding;

    /// <summary>
    /// Applies the ASP.NET Web API parameter binding rules to the services framework controllers: parameters of complex types
    /// without an explicit binding source are read from the request body, simple types from the route and query string.
    /// </summary>
    internal sealed class DnnApiControllerConvention : IApplicationModelConvention
    {
        /// <inheritdoc />
        public void Apply(ApplicationModel application)
        {
            if (application == null)
            {
                return;
            }

            foreach (var controller in application.Controllers)
            {
                if (typeof(DnnApiController).IsAssignableFrom(controller.ControllerType))
                {
                    Apply(controller);
                }
            }
        }

        private static void Apply(ControllerModel controller)
        {
            foreach (var action in controller.Actions)
            {
                foreach (var parameter in action.Parameters)
                {
                    if (parameter.BindingInfo?.BindingSource != null || IsSimpleType(parameter.ParameterType))
                    {
                        continue;
                    }

                    parameter.BindingInfo ??= new BindingInfo();
                    parameter.BindingInfo.BindingSource = BindingSource.Body;
                }
            }
        }

        private static bool IsSimpleType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime)
                || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid) || type == typeof(Uri)
                || type == typeof(CancellationToken))
            {
                return true;
            }

            // like ASP.NET Web API, types which can be converted from a string are bound from the URI
            return TypeDescriptor.GetConverter(type).CanConvertFrom(typeof(string));
        }
    }
}
