// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Mvc.Helpers
{
    using System;
    using System.Linq.Expressions;

    using DotNetNuke.Common;

    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Resolves ASP.NET Core model metadata for string and lambda expressions.</summary>
    /// <remarks>
    /// Replaces the MVC 5 <c>ModelMetadata.FromStringExpression</c>/<c>ModelMetadata.FromLambdaExpression</c>
    /// and <c>ExpressionHelper.GetExpressionText</c> APIs, which have no direct ASP.NET Core counterpart.
    /// </remarks>
    internal static class ModelMetadataHelper
    {
        /// <summary>Gets the model explorer of a string expression.</summary>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="expression">The string expression.</param>
        /// <returns>The model explorer.</returns>
        internal static ModelExplorer GetModelExplorer(IHtmlHelper htmlHelper, string expression)
        {
            Requires.NotNull("htmlHelper", htmlHelper);

            var metadataProvider = GetServices(htmlHelper).GetRequiredService<IModelMetadataProvider>();
            var container = htmlHelper.ViewData.ModelExplorer
                            ?? metadataProvider.GetModelExplorerForType(typeof(object), null);

            if (string.IsNullOrEmpty(expression))
            {
                return container;
            }

            var explorer = container;
            foreach (var part in expression.Split('.'))
            {
                var next = explorer.GetExplorerForProperty(part);
                if (next == null)
                {
                    return explorer;
                }

                explorer = next;
            }

            return explorer;
        }

        /// <summary>Gets the metadata of a string expression.</summary>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="expression">The string expression.</param>
        /// <returns>The model metadata.</returns>
        internal static ModelMetadata GetMetadata(IHtmlHelper htmlHelper, string expression)
            => GetModelExplorer(htmlHelper, expression).Metadata;

        /// <summary>Creates the model expression of a lambda expression.</summary>
        /// <typeparam name="TModel">The model type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="htmlHelper">The HTML helper.</param>
        /// <param name="expression">The lambda expression.</param>
        /// <returns>The model expression, carrying both the metadata and the expression text.</returns>
        internal static ModelExpression CreateModelExpression<TModel, TValue>(IHtmlHelper<TModel> htmlHelper, Expression<Func<TModel, TValue>> expression)
        {
            Requires.NotNull("htmlHelper", htmlHelper);
            Requires.NotNull("expression", expression);

            var provider = GetServices(htmlHelper).GetRequiredService<IModelExpressionProvider>();
            return provider.CreateModelExpression(htmlHelper.ViewData, expression);
        }

        private static IServiceProvider GetServices(IHtmlHelper htmlHelper)
            => htmlHelper.ViewContext?.HttpContext?.RequestServices ?? Globals.GetCurrentServiceProvider();
    }
}
