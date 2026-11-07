// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvp
{
    using System;

    using DotNetNuke.Internal.SourceGenerators;

    /// <summary>An attribute marking a model or presenter property as persisted component state.</summary>
    /// <remarks>
    /// <para>
    /// The name is kept for compatibility, but the storage is not view state any more: there is no
    /// view state in Blazor. A property marked with this attribute is moved by
    /// <see cref="AttributeBasedViewStateSerializer"/> into the <see cref="MvpStateBag"/> the view
    /// component owns (<see cref="ModuleViewBase.State"/>), which the component persists across the
    /// pre-render to interactive transition with Blazor's <c>PersistentComponentState</c>.
    /// </para>
    /// <para>
    /// Values must therefore be serializable with <c>System.Text.Json</c> — the same practical
    /// constraint view state imposed with its own serializer.
    /// </para>
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public partial class ViewStateAttribute : Attribute
    {
        /// <summary>Gets or sets the key the property's value is stored under.</summary>
        /// <remarks>Defaults to the name of the property when not set.</remarks>
        public string ViewStateKey { get; set; }
    }
}
