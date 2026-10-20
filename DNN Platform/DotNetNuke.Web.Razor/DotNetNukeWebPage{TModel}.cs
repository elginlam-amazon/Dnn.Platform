// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Web.Razor
{
    using System;

    using DotNetNuke.Common;
    using DotNetNuke.Internal.SourceGenerators;

    using Microsoft.Extensions.DependencyInjection;

    /// <summary>A razor web page model.</summary>
    /// <typeparam name="TModel">The model type.</typeparam>
    [DnnDeprecated(9, 3, 2, "Use Razor Pages instead")]
    public abstract partial class DotNetNukeWebPage<TModel> : DotNetNukeWebPage
        where TModel : class
    {
        private TModel model;

        /// <summary>Initializes a new instance of the <see cref="DotNetNukeWebPage{TModel}"/> class.</summary>
        public DotNetNukeWebPage()
        {
            var resolvedModel = Globals.GetCurrentServiceProvider().GetService<TModel>();
            this.Model = resolvedModel ?? Activator.CreateInstance<TModel>();
        }

        /// <summary>Gets or sets the strongly typed model for the page.</summary>
        public new TModel Model
        {
            get { return this.ViewContext?.ViewData?.Model as TModel ?? this.model; }
            set { this.model = value; }
        }
    }
}
