// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvp
{
    using System;

    using DotNetNuke.Internal.SourceGenerators;

    /// <summary>The root contract of a DNN Model-View-Presenter view.</summary>
    /// <remarks>
    /// <para>
    /// This is the ASP.NET Core replacement for <c>WebFormsMvp.IView</c>. The MVP separation
    /// (view interface + presenter + model) is preserved; only the WebForms specific framework is
    /// gone. A view raises <see cref="Load"/> once its data is needed, which is what the presenter
    /// subscribes to in order to populate the model.
    /// </para>
    /// <para>
    /// For a Blazor component view (<see cref="ModuleViewBase"/>) <see cref="Load"/> is raised from
    /// the component lifecycle (<c>OnParametersSet</c>); for an endpoint view
    /// (<see cref="HttpHandlerView"/>, <see cref="WebServiceViewBase"/>) it is raised while the
    /// request is being handled.
    /// </para>
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public partial interface IMvpView
    {
        /// <summary>An event which triggers when the view is loaded and needs its model populated.</summary>
        /// <remarks>Replaces <c>WebFormsMvp.IView.Load</c>.</remarks>
        event EventHandler Load;

        /// <summary>
        /// Gets a value indicating whether binding the view must fail loudly when no presenter is
        /// declared for it.
        /// </summary>
        /// <remarks>Replaces <c>WebFormsMvp.IView.ThrowExceptionIfNoPresenterBound</c>.</remarks>
        bool ThrowExceptionIfNoPresenterBound { get; }
    }

    /// <summary>The contract of a DNN Model-View-Presenter view with a strongly typed model.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <remarks>Replaces <c>WebFormsMvp.IView&lt;TModel&gt;</c>.</remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public partial interface IMvpView<TModel> : IMvpView
        where TModel : class, new()
    {
        /// <summary>Gets or sets the model of this view, which the presenter owns.</summary>
        TModel Model { get; set; }
    }
}
