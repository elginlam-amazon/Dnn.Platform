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

    using DotNetNuke.Internal.SourceGenerators;

    /// <summary>The presenters created for a single view, which the view owns and releases.</summary>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public sealed partial class MvpPresenterBinding : IDisposable
    {
        private readonly List<object> presenters;

        /// <summary>Initializes a new instance of the <see cref="MvpPresenterBinding"/> class.</summary>
        /// <param name="presenters">The presenters bound to the view.</param>
        internal MvpPresenterBinding(List<object> presenters)
        {
            this.presenters = presenters ?? new List<object>();
        }

        /// <summary>Gets the presenters bound to the view.</summary>
        public IReadOnlyList<object> Presenters => this.presenters;

        /// <summary>Releases the views of the bound presenters.</summary>
        /// <remarks>Replaces <c>PresenterBinder.Release()</c>.</remarks>
        public void Dispose()
        {
            foreach (var presenter in this.presenters.OfType<IMvpPresenter>())
            {
                presenter.ReleaseView();
            }

            this.presenters.Clear();
        }
    }
}
