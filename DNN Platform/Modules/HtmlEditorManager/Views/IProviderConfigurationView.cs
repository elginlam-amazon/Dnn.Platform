// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Modules.HtmlEditorManager.Views
{
    using System;

    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Modules.HtmlEditorManager.ViewModels;
    using DotNetNuke.UI;
    using DotNetNuke.Web.Mvp;

    /// <summary>Interface for the Provider Configuration View.</summary>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public partial interface IProviderConfigurationView : IModuleView<ProviderConfigurationViewModel>
    {
        /// <summary>Occurs when [save editor choice].</summary>
        event EventHandler<EditorEventArgs> SaveEditorChoice;

        /// <summary>Occurs when the editor changed.</summary>
        event EventHandler<EditorEventArgs> EditorChanged;

        /// <summary>Gets or sets the editor panel.</summary>
        /// <value>The editor panel.</value>
        /// <remarks>
        /// Was a <c>System.Web.UI.WebControls.PlaceHolder</c>; <see cref="DnnPlaceHolder"/> is the
        /// platform's replacement for it, and it keeps the <c>Controls.Add</c>/<c>Controls.Clear</c>
        /// surface the presenter drives the panel through.
        /// </remarks>
        DnnPlaceHolder Editor { get; set; }

        /// <summary>Refresh the provider configuration view.</summary>
        void Refresh();
    }
}
