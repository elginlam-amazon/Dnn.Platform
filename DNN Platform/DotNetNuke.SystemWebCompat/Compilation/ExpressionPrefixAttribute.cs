// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Compilation
{
    /// <summary>Specifies the prefix of an <see cref="ExpressionBuilder"/> in a <c>&lt;%$ prefix: ... %&gt;</c> expression.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ExpressionPrefixAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="ExpressionPrefixAttribute"/> class.</summary>
        /// <param name="expressionPrefix">The expression prefix.</param>
        public ExpressionPrefixAttribute(string expressionPrefix)
        {
            this.ExpressionPrefix = expressionPrefix;
        }

        /// <summary>Gets the expression prefix.</summary>
        public string ExpressionPrefix { get; }
    }
}
