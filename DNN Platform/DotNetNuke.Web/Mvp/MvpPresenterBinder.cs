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

    /// <summary>
    /// Creates and owns the presenters of a view, which is what makes the Model-View-Presenter
    /// separation work without the WebForms page lifecycle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the replacement for <c>WebFormsMvp.Web.PageViewHost.Register</c> and the
    /// <c>PresenterBinder</c> behind it. It is deliberately not a service locator: it reads the
    /// <see cref="PresenterBindingAttribute"/> declarations of the view, constructs each presenter
    /// with the view (which is when the presenter subscribes to the view's events), and initializes
    /// the view's <c>Model</c> so that the model is never <see langword="null"/> while the presenter
    /// runs — the two pieces of behaviour view and presenter code depend on.
    /// </para>
    /// <para>
    /// The returned <see cref="MvpPresenterBinding"/> keeps the presenters alive for as long as the
    /// view lives, and releases them when the view is torn down.
    /// </para>
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public static partial class MvpPresenterBinder
    {
        /// <summary>Creates the presenters declared for the given view and wires them to it.</summary>
        /// <param name="view">The view to bind.</param>
        /// <returns>The binding, which owns the created presenters.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="view"/> is <see langword="null"/>.</exception>
        /// <exception cref="MvpPresenterBindingException">
        /// No presenter is declared for a view whose <see cref="IMvpView.ThrowExceptionIfNoPresenterBound"/>
        /// is <see langword="true"/>, or a declared presenter cannot be constructed with the view.
        /// </exception>
        public static MvpPresenterBinding Bind(IMvpView view)
        {
            ArgumentNullException.ThrowIfNull(view);

            EnsureModel(view);

            var presenters = new List<object>();
            foreach (var binding in GetBindings(view.GetType()))
            {
                presenters.Add(CreatePresenter(binding, view));
            }

            if (presenters.Count == 0 && view.ThrowExceptionIfNoPresenterBound)
            {
                throw new MvpPresenterBindingException(string.Format(
                    CultureInfo.CurrentCulture,
                    "No presenter was bound to the view '{0}'. Decorate the view (or the view interface it implements) with [PresenterBinding(typeof(YourPresenter))].",
                    view.GetType().FullName));
            }

            return new MvpPresenterBinding(presenters);
        }

        /// <summary>Sets the <c>Model</c> of the view to a new instance when it has none yet.</summary>
        /// <param name="view">The view whose model to initialize.</param>
        /// <remarks>
        /// Reproduces the WebFormsMvp behaviour where the binder created the model, which is why
        /// view code can read <c>this.Model</c> from its first lifecycle method.
        /// </remarks>
        internal static void EnsureModel(IMvpView view)
        {
            var modelProperty = view.GetType()
                .GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMvpView<>))
                .Select(i => i.GetProperty("Model"))
                .FirstOrDefault(p => p != null);

            if (modelProperty == null || !modelProperty.CanWrite)
            {
                return;
            }

            object current = null;
            try
            {
                current = modelProperty.GetValue(view, null);
            }
            catch (TargetInvocationException)
            {
                // the view throws while the model is null (see ModuleView{TModel}.Model), which is
                // exactly the case this method exists to fix.
            }

            if (current == null)
            {
                modelProperty.SetValue(view, Activator.CreateInstance(modelProperty.PropertyType), null);
            }
        }

        private static IEnumerable<PresenterBindingAttribute> GetBindings(Type viewType)
        {
            var bindings = viewType
                .GetCustomAttributes(typeof(PresenterBindingAttribute), true)
                .OfType<PresenterBindingAttribute>()
                .ToList();

            foreach (var viewInterface in viewType.GetInterfaces())
            {
                bindings.AddRange(viewInterface
                    .GetCustomAttributes(typeof(PresenterBindingAttribute), true)
                    .OfType<PresenterBindingAttribute>());
            }

            return bindings
                .GroupBy(binding => binding.PresenterType)
                .Select(group => group.First());
        }

        private static object CreatePresenter(PresenterBindingAttribute binding, IMvpView view)
        {
            var constructor = binding.PresenterType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .Where(candidate => candidate.GetParameters().Length == 1)
                .FirstOrDefault(candidate => candidate.GetParameters()[0].ParameterType.IsInstanceOfType(view));

            if (constructor == null)
            {
                throw new MvpPresenterBindingException(string.Format(
                    CultureInfo.CurrentCulture,
                    "The presenter '{0}' has no public single argument constructor accepting the view '{1}'.",
                    binding.PresenterType.FullName,
                    view.GetType().FullName));
            }

            return constructor.Invoke(new object[] { view });
        }
    }
}
