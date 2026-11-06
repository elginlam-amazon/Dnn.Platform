// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvp
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;

    using DotNetNuke.Internal.SourceGenerators;

    using Microsoft.AspNetCore.Http;

    /// <summary>Represents a class that is a view for a web service in a Model-View-Presenter application.</summary>
    /// <remarks>
    /// <para>
    /// Replaces <c>WebFormsMvp.Web.MvpWebService</c>, which derived from
    /// <c>System.Web.Services.WebService</c> and exposed its <c>[WebMethod]</c> members over SOAP.
    /// Nothing in the solution consumes these services over SOAP, so they are exposed as REST: a
    /// request to <c>{route}/{operation}</c> invokes the method named <c>operation</c> that is marked
    /// with <see cref="MvpServiceOperationAttribute"/>, and its return value is written as JSON.
    /// </para>
    /// <para>
    /// The MVP lifecycle is preserved: the presenters declared with
    /// <see cref="PresenterBindingAttribute"/> are created and <see cref="Load"/> is raised before
    /// the operation runs, which is where <c>MvpWebService</c> raised it (from its constructor).
    /// </para>
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public abstract partial class WebServiceViewBase : IWebServiceViewBase
    {
        private static readonly JsonSerializerOptions BodyOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <inheritdoc />
        public event EventHandler Load;

        /// <inheritdoc />
        public bool ThrowExceptionIfNoPresenterBound => true;

        /// <summary>Gets the context of the request being served.</summary>
        public HttpContext HttpContext { get; private set; }

        /// <summary>Gets the request being served.</summary>
        public HttpRequest Request => this.HttpContext?.Request;

        /// <summary>Gets the response being written.</summary>
        public HttpResponse Response => this.HttpContext?.Response;

        /// <inheritdoc />
        public async Task<object> InvokeAsync(string operation, HttpContext context)
        {
            this.HttpContext = context ?? throw new ArgumentNullException(nameof(context));

            var method = FindOperation(this.GetType(), operation);
            if (method == null)
            {
                throw new MvpServiceOperationNotFoundException(string.Format(
                    CultureInfo.CurrentCulture,
                    "The service '{0}' has no operation named '{1}'. Mark the method with [MvpServiceOperation].",
                    this.GetType().FullName,
                    operation));
            }

            using (MvpPresenterBinder.Bind(this))
            {
                this.Load?.Invoke(this, EventArgs.Empty);

                var arguments = await this.BindArgumentsAsync(method, context);
                var result = method.Invoke(this, arguments);

                return await UnwrapAsync(result);
            }
        }

        /// <summary>Finds the operation of the given name on the given service type.</summary>
        /// <param name="serviceType">The service type.</param>
        /// <param name="operation">The operation name.</param>
        /// <returns>The method, or <see langword="null"/> when the service has no such operation.</returns>
        internal static MethodInfo FindOperation(Type serviceType, string operation)
        {
            if (string.IsNullOrEmpty(operation))
            {
                return null;
            }

            return GetOperations(serviceType).FirstOrDefault(candidate =>
                string.Equals(OperationName(candidate), operation, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Gets every operation the given service type exposes.</summary>
        /// <param name="serviceType">The service type.</param>
        /// <returns>The methods marked with <see cref="MvpServiceOperationAttribute"/>.</returns>
        internal static IEnumerable<MethodInfo> GetOperations(Type serviceType)
        {
            return serviceType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(candidate => candidate.IsDefined(typeof(MvpServiceOperationAttribute), true));
        }

        /// <summary>Gets the route name of the given operation.</summary>
        /// <param name="method">The operation.</param>
        /// <returns>The declared name, or the method name.</returns>
        internal static string OperationName(MethodInfo method)
        {
            var attribute = method
                .GetCustomAttributes(typeof(MvpServiceOperationAttribute), true)
                .OfType<MvpServiceOperationAttribute>()
                .FirstOrDefault();

            return string.IsNullOrEmpty(attribute?.Name) ? method.Name : attribute.Name;
        }

        private static async Task<object> UnwrapAsync(object result)
        {
            switch (result)
            {
                case null:
                    return null;
                case Task task:
                    await task;
                    var resultProperty = task.GetType().GetProperty("Result");
                    return resultProperty == null || resultProperty.PropertyType.Name == "VoidTaskResult"
                        ? null
                        : resultProperty.GetValue(task);
                default:
                    return result;
            }
        }

        private static bool TryConvert(string raw, Type targetType, out object value)
        {
            value = null;
            var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (raw == null)
            {
                return false;
            }

            try
            {
                value = type.IsEnum
                    ? Enum.Parse(type, raw, ignoreCase: true)
                    : Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (InvalidCastException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Binds the operation's parameters from the route values, the query string, the posted form
        /// and - for a single complex parameter - the JSON request body.
        /// </summary>
        /// <param name="method">The operation being invoked.</param>
        /// <param name="context">The request.</param>
        /// <returns>The argument values, positionally.</returns>
        /// <remarks>
        /// This replaces the SOAP envelope deserialization the ASMX handler performed; the parameter
        /// names of the operation are unchanged, so an existing caller only changes transport.
        /// </remarks>
        private async Task<object[]> BindArgumentsAsync(MethodInfo method, HttpContext context)
        {
            var parameters = method.GetParameters();
            var arguments = new object[parameters.Length];
            JsonDocument body = null;

            try
            {
                for (var index = 0; index < parameters.Length; index++)
                {
                    var parameter = parameters[index];

                    if (parameter.ParameterType == typeof(HttpContext))
                    {
                        arguments[index] = context;
                        continue;
                    }

                    var raw = this.ReadRawValue(context, parameter.Name);
                    if (raw != null && TryConvert(raw, parameter.ParameterType, out var converted))
                    {
                        arguments[index] = converted;
                        continue;
                    }

                    body ??= await this.ReadBodyAsync(context);
                    if (body != null && this.TryReadFromBody(body, parameter, parameters.Length, out var fromBody))
                    {
                        arguments[index] = fromBody;
                        continue;
                    }

                    arguments[index] = parameter.HasDefaultValue
                        ? parameter.DefaultValue
                        : (parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null);
                }
            }
            finally
            {
                body?.Dispose();
            }

            return arguments;
        }

        private string ReadRawValue(HttpContext context, string name)
        {
            if (context.Request.RouteValues.TryGetValue(name, out var routeValue) && routeValue != null)
            {
                return routeValue.ToString();
            }

            if (context.Request.Query.TryGetValue(name, out var queryValue))
            {
                return queryValue.ToString();
            }

            if (context.Request.HasFormContentType && context.Request.Form.TryGetValue(name, out var formValue))
            {
                return formValue.ToString();
            }

            return null;
        }

        private async Task<JsonDocument> ReadBodyAsync(HttpContext context)
        {
            if (context.Request.ContentLength is null or 0)
            {
                return null;
            }

            var contentType = context.Request.ContentType;
            if (contentType == null || !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                return await JsonDocument.ParseAsync(context.Request.Body, default, context.RequestAborted);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private bool TryReadFromBody(JsonDocument body, ParameterInfo parameter, int parameterCount, out object value)
        {
            value = null;

            // a single complex parameter is the whole body, which is how a JSON client posts a DTO.
            if (parameterCount == 1 && body.RootElement.ValueKind == JsonValueKind.Object
                && parameter.ParameterType != typeof(string) && !parameter.ParameterType.IsPrimitive)
            {
                var asWhole = body.RootElement.Deserialize(parameter.ParameterType, BodyOptions);
                if (asWhole != null)
                {
                    value = asWhole;
                    return true;
                }
            }

            if (body.RootElement.ValueKind != JsonValueKind.Object
                || !body.RootElement.TryGetProperty(parameter.Name, out var property))
            {
                return false;
            }

            value = property.Deserialize(parameter.ParameterType, BodyOptions);
            return true;
        }
    }
}
