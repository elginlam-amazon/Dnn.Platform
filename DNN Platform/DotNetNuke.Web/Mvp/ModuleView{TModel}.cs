// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvp
{
    using System;

    using DotNetNuke.Internal.SourceGenerators;

    /// <summary>Represents a class that is a view for a module with a strongly typed view model in a Web Forms Model-View-Presenter application.</summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public abstract partial class ModuleView<TModel> : ModuleViewBase, IMvpView<TModel>
        where TModel : class, new()
    {
        private TModel model;

        /// <inheritdoc />
        public TModel Model
        {
            get
            {
                if (this.model == null)
                {
                    throw new InvalidOperationException(
                        "The Model property is currently null, however it should have been automatically initialized by the presenter. This most likely indicates that no presenter was bound to the control. Check your presenter bindings.");
                }

                return this.model;
            }

            set
            {
                this.model = value;
            }
        }

        /// <inheritdoc />
        protected override void LoadComponentState()
        {
            // Call the base class to load any persisted state
            base.LoadComponentState();

            // Restore the model properties marked with [ViewState] from this component's state bag,
            // which has just been read back from Blazor's PersistentComponentState. This replaces
            // the former LoadViewState(object)/Control.ViewState round-trip.
            MvpPresenterBinder.EnsureModel(this);
            AttributeBasedViewStateSerializer.DeSerialize(this.model, this.State);
        }

        /// <inheritdoc />
        protected override void SaveComponentState()
        {
            // Write the model properties marked with [ViewState] into this component's state bag so
            // they are persisted for the interactive circuit. Replaces SaveViewState().
            AttributeBasedViewStateSerializer.Serialize(this.model, this.State);

            base.SaveComponentState();
        }
    }
}
