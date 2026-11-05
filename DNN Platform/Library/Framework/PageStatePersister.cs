// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    using System;

    /// <summary>
    /// Provides the base class for the component-state persistence mechanisms a <see cref="PageBase"/>
    /// can use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This replaces <c>System.Web.UI.PageStatePersister</c>. ASP.NET Core has no ViewState: a Blazor
    /// Server component keeps its state in the circuit for as long as the circuit lives. DNN's
    /// persisters existed so that state could survive <em>outside</em> the circuit — across a hard
    /// navigation, a server recycle or a farm node switch — and that requirement is unchanged, so the
    /// persisters are kept and re-implemented as a real keyed state store:
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="CachePageStatePersister"/> stores the state in the DNN memory cache (<c>IMemoryCache</c>) under a generated key.</description></item>
    /// <item><description><see cref="DiskPageStatePersister"/> stores the state as a file under the portal's cache directory.</description></item>
    /// </list>
    /// <para>
    /// The identity that scopes a key was the ASP.NET <c>Session.SessionID</c>; it is now
    /// <see cref="PageBase.PageStateId"/>, which is the identity of the page component instance (i.e.
    /// of the circuit's page) and therefore the direct Blazor equivalent.
    /// </para>
    /// </remarks>
    public abstract class PageStatePersister
    {
        private readonly IPageStateFormatter stateFormatter;

        /// <summary>Initializes a new instance of the <see cref="PageStatePersister"/> class.</summary>
        /// <param name="page">The page whose state this instance persists.</param>
        /// <param name="stateFormatter">
        /// The formatter used to turn the state into text and back, or <see langword="null"/> to use
        /// <see cref="PageStateFormatter"/>.
        /// </param>
        protected PageStatePersister(PageBase page, IPageStateFormatter stateFormatter)
        {
            this.Page = page ?? throw new ArgumentNullException(nameof(page));
            this.stateFormatter = stateFormatter ?? new PageStateFormatter();
        }

        /// <summary>Gets the page whose state this instance persists.</summary>
        public PageBase Page { get; }

        /// <summary>Gets or sets the state of the page itself (the former <c>ViewState</c>).</summary>
        public object ViewState { get; set; }

        /// <summary>Gets or sets the state of the page's controls (the former <c>ControlState</c>).</summary>
        public object ControlState { get; set; }

        /// <summary>
        /// Gets the number of minutes a persisted state stays valid.
        /// </summary>
        /// <remarks>
        /// Was <c>Page.Session.Timeout</c>. A Blazor Server circuit has no ASP.NET session, so the
        /// value is the page's own circuit-state lifetime, overridable by a derived page.
        /// </remarks>
        public virtual int StateTimeoutMinutes => 20;

        /// <summary>Gets the formatter used to turn the state into text and back.</summary>
        /// <remarks>Replaces <c>PageStatePersister.StateFormatter</c> (which returned an <c>IStateFormatter</c> backed by <c>LosFormatter</c>).</remarks>
        protected IPageStateFormatter StateFormatter => this.stateFormatter;

        /// <summary>Loads the page state, assigning <see cref="ViewState"/> and <see cref="ControlState"/>.</summary>
        public abstract void Load();

        /// <summary>Saves <see cref="ViewState"/> and <see cref="ControlState"/>.</summary>
        public abstract void Save();
    }
}
