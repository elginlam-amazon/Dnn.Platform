// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.UI
{
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web.Compilation;
    using System.Web.Services;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Executes the ASP.NET AJAX page methods (public static methods of a page marked with <see cref="WebMethodAttribute"/>) which
    /// are called by the <c>PageMethods</c> client proxy rendered by the <see cref="ScriptManager"/> (<c>EnablePageMethods</c>):
    /// <c>POST ~/page.aspx/MethodName</c> with a JSON object of the arguments, the response is <c>{ "d": result }</c>.
    /// </summary>
    public static class PageMethodsHandler
    {
        private const string PageExtension = ".aspx/";
        private static readonly Regex InheritsRegex = new Regex("<%@\\s*Page\\b[^>]*?\\bInherits\\s*=\\s*\"(?<type>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private static readonly ConcurrentDictionary<string, Type> PageTypes = new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Adds the middleware which executes the page methods.</summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The <paramref name="app"/> instance.</returns>
        public static IApplicationBuilder UseWebFormsPageMethods(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                if (!await TryHandleAsync(context))
                {
                    await next(context);
                }
            });
        }

        /// <summary>Executes the page method of the request, if the request is a page method call.</summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns><see langword="true"/> if the request was handled.</returns>
        public static async Task<bool> TryHandleAsync(Microsoft.AspNetCore.Http.HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var path = context.Request.Path.Value ?? string.Empty;
            var index = path.IndexOf(PageExtension, StringComparison.OrdinalIgnoreCase);
            if (index < 0 || !HttpMethods.IsPost(context.Request.Method))
            {
                return false;
            }

            var pagePath = path.Substring(0, index + PageExtension.Length - 1);
            var methodName = path.Substring(index + PageExtension.Length);
            var method = FindPageMethod(pagePath, methodName);
            if (method == null)
            {
                return false;
            }

            string body;
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync(context.RequestAborted);
            }

            var arguments = string.IsNullOrWhiteSpace(body) ? new JObject() : JObject.Parse(body);
            var parameters = method.GetParameters();
            var values = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var token = arguments.Properties().FirstOrDefault(p => string.Equals(p.Name, parameters[i].Name, StringComparison.OrdinalIgnoreCase))?.Value;
                values[i] = token == null || token.Type == JTokenType.Null
                    ? (parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null)
                    : token.ToObject(parameters[i].ParameterType);
            }

            context.Response.ContentType = "application/json; charset=utf-8";
            object result;
            try
            {
                result = method.Invoke(null, values);
                if (result is Task task)
                {
                    await task;
                    result = task.GetType().IsGenericType ? task.GetType().GetProperty("Result")?.GetValue(task) : null;
                }
            }
            catch (TargetInvocationException ex)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { Message = ex.InnerException?.Message, ExceptionType = ex.InnerException?.GetType().FullName, }), context.RequestAborted);
                return true;
            }

            await context.Response.WriteAsync(JsonConvert.SerializeObject(new { d = result, }), context.RequestAborted);
            return true;
        }

        /// <summary>Gets the page methods of a page type.</summary>
        /// <param name="pageType">The page type.</param>
        /// <returns>The page methods.</returns>
        internal static MethodInfo[] GetPageMethods(Type pageType)
        {
            return pageType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(m => m.GetCustomAttribute<WebMethodAttribute>() != null)
                .ToArray();
        }

        private static MethodInfo FindPageMethod(string pagePath, string methodName)
        {
            var pageType = PageTypes.GetOrAdd(pagePath, ResolvePageType);
            return pageType == null
                ? null
                : GetPageMethods(pageType).FirstOrDefault(m => string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase));
        }

        private static Type ResolvePageType(string pagePath)
        {
            var physicalPath = Hosting.HostingEnvironment.MapPath(pagePath);
            if (string.IsNullOrEmpty(physicalPath) || !File.Exists(physicalPath))
            {
                return null;
            }

            var match = InheritsRegex.Match(File.ReadAllText(physicalPath));
            return match.Success ? BuildManager.GetType(match.Groups["type"].Value, false, true) : null;
        }
    }
}
