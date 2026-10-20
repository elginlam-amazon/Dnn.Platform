// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.Script.Serialization
{
    /// <summary>Specifies that a public property or field is ignored by the <see cref="JavaScriptSerializer"/>.</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class ScriptIgnoreAttribute : Attribute
    {
        /// <summary>Gets or sets a value indicating whether the member is only ignored when the <see cref="ApplyToOverrides"/> is set (kept for compatibility).</summary>
        public bool ApplyToOverrides { get; set; }
    }
}
