// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Services.Localization
{
    using System;

    /// <summary>Produces the design-time representation of a <c>dnnLoc</c> localization expression.</summary>
    /// <remarks>
    /// Was a <c>System.Web.UI.Design.ExpressionEditor</c>, referenced from
    /// <see cref="LocalizationExpressionBuilder"/> by an <c>[ExpressionEditor]</c> attribute. The
    /// WebForms designer and its <c>System.Web.UI.Design</c> assembly do not exist on modern .NET,
    /// so the base class is gone; the type and its behaviour — echoing the expression back as the
    /// <c>[dnnLoc:Key]</c> token a designer would display in place of the resolved value — are kept
    /// because they are public API and are what <see cref="LocalizationExpressionBuilder.ReplaceExpressions"/>
    /// consumes as its token syntax.
    /// </remarks>
    public class LocalizationExpressionBuilderEditor
    {
        /// <summary>Gets the token that represents the given expression when it is not resolved.</summary>
        /// <param name="expression">The resource key.</param>
        /// <param name="parseTimeData">Data produced while parsing the expression; unused, kept for signature compatibility.</param>
        /// <param name="propertyType">The type of the property the expression is bound to; unused, kept for signature compatibility.</param>
        /// <param name="serviceProvider">The host's service provider; unused, kept for signature compatibility.</param>
        /// <returns>The <c>[dnnLoc:<paramref name="expression"/>]</c> token.</returns>
        public virtual object EvaluateExpression(string expression, object parseTimeData, Type propertyType, IServiceProvider serviceProvider)
        {
            return string.Concat("[", LocalizationExpressionBuilder.ExpressionPrefix, ":", expression, "]");
        }
    }
}
