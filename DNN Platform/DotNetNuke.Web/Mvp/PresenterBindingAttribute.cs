// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvp
{
    using System;

    using DotNetNuke.Internal.SourceGenerators;

    /// <summary>Declares the presenter which drives the decorated view.</summary>
    /// <remarks>
    /// Replaces <c>WebFormsMvp.PresenterBindingAttribute</c>. <see cref="MvpPresenterBinder"/> reads
    /// it when a view is bound, so a view keeps declaring its presenter exactly as before:
    /// <code>
    /// [PresenterBinding(typeof(MySettingsPresenter))]
    /// public partial class Settings : SettingsView&lt;MySettingsModel&gt;
    /// </code>
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
    public partial class PresenterBindingAttribute : Attribute
    {
        /// <summary>Initializes a new instance of the <see cref="PresenterBindingAttribute"/> class.</summary>
        /// <param name="presenterType">The type of the presenter to create for the view.</param>
        public PresenterBindingAttribute(Type presenterType)
        {
            this.PresenterType = presenterType ?? throw new ArgumentNullException(nameof(presenterType));
        }

        /// <summary>Gets the type of the presenter to create for the view.</summary>
        public Type PresenterType { get; }

        /// <summary>
        /// Gets or sets the type of the view the presenter is constructed with, when the view
        /// implements more than one view interface.
        /// </summary>
        public Type ViewType { get; set; }
    }
}
