// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvp
{
    using System;

    using DotNetNuke.Common;
    using DotNetNuke.Internal.SourceGenerators;

    using Microsoft.AspNetCore.Http;

    /// <summary>The base class of a DNN Model-View-Presenter presenter.</summary>
    /// <typeparam name="TView">The type of the view this presenter drives.</typeparam>
    /// <remarks>
    /// <para>
    /// This is the ASP.NET Core replacement for <c>WebFormsMvp.Presenter&lt;TView&gt;</c>. It keeps
    /// the members presenter code is written against — <see cref="View"/>,
    /// <see cref="HttpContext"/>, <see cref="Request"/>, <see cref="Response"/> — with the
    /// <c>Microsoft.AspNetCore.Http</c> types in place of the <c>System.Web</c> ones.
    /// </para>
    /// <para>
    /// The context is resolved through <see cref="HttpContextSource"/>, which is non
    /// <see langword="null"/> while the component that owns the view renders server side. A
    /// presenter must therefore read what it needs from the request during
    /// <see cref="ModulePresenterBase{TView}.OnInit"/>/<see cref="ModulePresenterBase{TView}.OnLoad"/>
    /// rather than later from an interactive circuit.
    /// </para>
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public abstract partial class PresenterBase<TView> : IMvpPresenter
        where TView : class
    {
        private HttpContext capturedContext;

        /// <summary>Initializes a new instance of the <see cref="PresenterBase{TView}"/> class.</summary>
        /// <param name="view">The view this presenter drives.</param>
        protected PresenterBase(TView view)
        {
            this.View = view ?? throw new ArgumentNullException(nameof(view));
            this.capturedContext = HttpContextSource.Current;
        }

        /// <summary>Gets the view this presenter drives.</summary>
        public TView View { get; private set; }

        /// <summary>Gets the context of the request the view is being rendered for.</summary>
        /// <remarks>
        /// Replaces <c>Presenter.HttpContext</c>, which was a <c>System.Web.HttpContextBase</c>.
        /// </remarks>
        public HttpContext HttpContext => this.capturedContext ?? HttpContextSource.Current;

        /// <summary>Gets the request the view is being rendered for.</summary>
        public HttpRequest Request => this.HttpContext?.Request;

        /// <summary>Gets the response of the request the view is being rendered for.</summary>
        public HttpResponse Response => this.HttpContext?.Response;

        /// <summary>Detaches this presenter from its view.</summary>
        /// <remarks>
        /// Replaces <c>Presenter.ReleaseView()</c>. Derived presenters override it to unsubscribe
        /// from the view's events; the base implementation drops the references so neither the view
        /// nor the request context is kept alive by a presenter that outlives them.
        /// </remarks>
        public virtual void ReleaseView()
        {
            this.View = null;
            this.capturedContext = null;
        }
    }
}
