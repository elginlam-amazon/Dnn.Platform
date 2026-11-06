// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvp
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;

    using DotNetNuke.Common;
    using DotNetNuke.Internal.SourceGenerators;
    using DotNetNuke.Services.Exceptions;
    using DotNetNuke.UI.Modules;
    using DotNetNuke.UI.Skins.Controls;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Represents a class that is a view for a module in a Model-View-Presenter application.</summary>
    /// <remarks>
    /// <para>
    /// This was a <c>System.Web.UI.UserControl</c> hosted by WebFormsMvp's <c>PageViewHost</c>. It
    /// is now a DNN skinning component (<see cref="ModuleUserControlBase"/>, i.e.
    /// <see cref="DotNetNuke.UI.DnnComponentBase"/>) and binds its own presenters through
    /// <see cref="MvpPresenterBinder"/>. The MVP separation is unchanged: the presenter is created
    /// while the component initializes, and it populates the model in response to
    /// <see cref="Initialize"/> and <see cref="Load"/>.
    /// </para>
    /// <para>
    /// The WebForms events map onto the component lifecycle like this:
    /// <c>Page.InitComplete</c> -&gt; <c>OnInitialized</c> (raises <see cref="Initialize"/>),
    /// <c>Page.Load</c> -&gt; <c>OnParametersSet</c> (raises <see cref="Load"/> once, as the page
    /// did once per request), <c>Page.PreRenderComplete</c> + <c>DataBind()</c> -&gt;
    /// <see cref="DataBind"/>, which asks the renderer for another pass now that the model is
    /// populated.
    /// </para>
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public abstract partial class ModuleViewBase : ModuleUserControlBase, IModuleViewBase
    {
        private readonly MvpStateBag state = new MvpStateBag();
        private MvpPresenterBinding presenterBinding;
        private PersistingComponentStateSubscription persistingSubscription;
        private bool persistingSubscribed;
        private bool loadRaised;

        /// <summary>Initializes a new instance of the <see cref="ModuleViewBase"/> class.</summary>
        protected ModuleViewBase()
        {
            this.AutoDataBind = true;
        }

        /// <inheritdoc />
        public event EventHandler Initialize;

        /// <inheritdoc />
        public event EventHandler Load;

        /// <inheritdoc />
        public bool ThrowExceptionIfNoPresenterBound
        {
            get { return true; }
        }

        /// <inheritdoc />
        public bool AutoDataBind { get; set; }

        /// <summary>Gets the explicit component state of this view.</summary>
        /// <remarks>
        /// <b>This is the replacement for <c>Control.ViewState</c></b>, which does not exist in
        /// Blazor. It is a plain dictionary owned by this component; it is handed to the interactive
        /// circuit through Blazor's <c>PersistentComponentState</c> so that the values a view or its
        /// model marked with <see cref="ViewStateAttribute"/> survive the pre-render boundary the
        /// way they used to survive a post-back.
        /// </remarks>
        public MvpStateBag State => this.state;

        /// <summary>
        /// Gets or sets the item this view is currently bound to, when it is rendered inside a
        /// data-bound container (a grid row, a repeater item).
        /// </summary>
        /// <remarks>
        /// Replaces the WebForms data-binding context that <c>Page.GetDataItem()</c> read. The
        /// container pushes the current item down as a cascading value named <c>DnnDataItem</c>,
        /// which is what <see cref="DataItem{T}"/> and <see cref="DataValue{T}()"/> read.
        /// </remarks>
        [CascadingParameter(Name = "DnnDataItem")]
        public object CurrentDataItem { get; set; }

        /// <summary>Gets or sets the Blazor component state used to persist this view's <c>[ViewState]</c> properties.</summary>
        /// <remarks>
        /// Injected rather than resolved from <c>Globals.GetCurrentServiceProvider()</c>:
        /// <see cref="PersistentComponentState"/> is scoped to the circuit, so resolving it from the ROOT
        /// provider throws <see cref="InvalidOperationException"/> when scope validation is on (Development)
        /// and otherwise hands back a different instance than the one the circuit persists from — which would
        /// make the state silently fail to round-trip. <see cref="InjectAttribute"/> is satisfied by the
        /// component's own scope. It stays nullable so non-Blazor hosts (unit tests, a plain HTTP render)
        /// still work with an empty bag, which is what the null check in <c>RestorePersistedState</c> handles.
        /// </remarks>
        [Inject]
        protected PersistentComponentState PersistentState { get; set; }

        /// <inheritdoc />
        public void ProcessModuleLoadException(Exception ex)
        {
            Exceptions.ProcessModuleLoadException(this, ex);
        }

        /// <inheritdoc />
        public void ShowMessage(string messageHeader, string message, ModuleMessage.ModuleMessageType messageType)
        {
            DotNetNuke.UI.Skins.Skin.AddModuleMessage(this, messageHeader, message, messageType);
        }

        /// <summary>Requests another render pass now that the model is populated.</summary>
        /// <remarks>
        /// Replaces <c>Control.DataBind()</c>. There is no server-side data-binding step in Blazor:
        /// the markup reads the model directly, so "data binding" is simply re-rendering once the
        /// presenter has filled the model in.
        /// </remarks>
        public virtual void DataBind()
        {
            this.NotifyTreeChanged();
        }

        /// <summary>Gets the data item.</summary>
        /// <typeparam name="T">The type of item.</typeparam>
        /// <returns>The item or a new <typeparamref name="T"/>.</returns>
        protected T DataItem<T>()
            where T : class, new()
        {
            var t = this.CurrentDataItem as T ?? new T();
            return t;
        }

        /// <summary>Gets the data value.</summary>
        /// <typeparam name="T">The type of value.</typeparam>
        /// <returns>The value.</returns>
        protected T DataValue<T>()
        {
            return this.CurrentDataItem is T value ? value : default;
        }

        /// <summary>Gets the formatted value.</summary>
        /// <param name="format">The format string.</param>
        /// <typeparam name="T">The type of value.</typeparam>
        /// <returns>The formatted value.</returns>
        protected string DataValue<T>(string format)
        {
            return string.Format(CultureInfo.CurrentCulture, format, this.DataValue<T>());
        }

        /// <summary>Restores the state of this view from <see cref="State"/>.</summary>
        /// <remarks>
        /// Replaces <c>Control.LoadViewState(object)</c>. It runs after the persisted state has been
        /// read back, before <see cref="Initialize"/> is raised.
        /// </remarks>
        protected virtual void LoadComponentState()
        {
        }

        /// <summary>Writes the state of this view into <see cref="State"/>.</summary>
        /// <remarks>
        /// Replaces <c>Control.SaveViewState()</c>. It runs just before the state is persisted for
        /// the interactive circuit.
        /// </remarks>
        protected virtual void SaveComponentState()
        {
        }

        /// <inheritdoc />
        protected override void OnInit(EventArgs e)
        {
            // Replaces PageViewHost.Register(this, this.Context, false): create the presenters
            // declared for this view and let them subscribe to its events.
            this.presenterBinding = MvpPresenterBinder.Bind(this);

            base.OnInit(e);

            this.RestorePersistedState();
            this.LoadComponentState();

            // Replaces Page.InitComplete, which was the point at which every control had been
            // initialized and the presenter could safely populate the model.
            this.Initialize?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (this.loadRaised)
            {
                return;
            }

            this.loadRaised = true;

            // Replaces Page.Load. The presenter populates the model here.
            this.Load?.Invoke(this, e);

            // Replaces Page.PreRenderComplete + DataBind(): now that the model is populated the
            // view renders it.
            if (this.AutoDataBind)
            {
                this.DataBind();
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.persistingSubscribed)
                {
                    this.persistingSubscription.Dispose();
                    this.persistingSubscribed = false;
                }

                this.presenterBinding?.Dispose();
                this.presenterBinding = null;
            }

            base.Dispose(disposing);
        }

        /// <summary>Gets the key this view's state is persisted under.</summary>
        /// <returns>A key unique to this view type and module instance.</returns>
        private string GetStateKey()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "DotNetNuke.Web.Mvp:{0}:{1}:{2}",
                this.GetType().FullName,
                this.ModuleContext?.ModuleId ?? -1,
                this.ID ?? string.Empty);
        }

        private void RestorePersistedState()
        {
            var persistentState = this.PersistentState;
            if (persistentState == null)
            {
                // no Blazor persistence in this host (unit tests, a plain HTTP render): the state
                // bag still works, it just starts empty.
                return;
            }

            var key = this.GetStateKey();
            if (persistentState.TryTakeFromJson<Dictionary<string, MvpStateEntry>>(key, out var persisted))
            {
                this.state.Restore(persisted);
            }

            if (!this.persistingSubscribed)
            {
                this.persistingSubscription = persistentState.RegisterOnPersisting(() =>
                {
                    this.SaveComponentState();
                    persistentState.PersistAsJson(key, this.state.Persist());
                    return Task.CompletedTask;
                });

                this.persistingSubscribed = true;
            }
        }
    }
}
