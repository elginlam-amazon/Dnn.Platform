// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using DotNetNuke.UI.Utilities;

    using Microsoft.AspNetCore.Components;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>Helpers for navigating and dynamically loading DNN's skinning component tree.</summary>
    /// <remarks>
    /// The WebForms signatures took <c>System.Web.UI.Control</c>/<c>TemplateControl</c>. Blazor has
    /// no server-side control tree, so the navigation helpers now walk the explicit DNN component
    /// tree modelled by <see cref="IDnnControl"/>.
    /// </remarks>
    public class ControlUtilities
    {
        /// <summary>Walks up the component tree looking for the closest ancestor of type <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">The type of ancestor to find.</typeparam>
        /// <param name="control">The control to start from.</param>
        /// <returns>The ancestor, or <see langword="null"/> when there is none.</returns>
        public static T FindParentControl<T>(IClientApiControl control)
            where T : class
        {
            if (control?.Parent == null)
            {
                return null;
            }

            return control.Parent as T ?? FindParentControl<T>(control.Parent);
        }

        /// <summary>Finds a sibling (or, recursively, an ancestor's descendant) control by its identifier.</summary>
        /// <typeparam name="T">The type of control to find.</typeparam>
        /// <param name="control">The control to start from.</param>
        /// <param name="id">The identifier of the control to find.</param>
        /// <param name="recursive">Whether to keep searching up the tree.</param>
        /// <returns>The control, or <see langword="null"/> when it was not found.</returns>
        public static T FindControl<T>(IClientApiControl control, string id, bool recursive)
            where T : class
        {
            T target = null;
            if (control?.Parent != null)
            {
                target = control.Parent.FindControl(id) as T;

                if (target == null && recursive)
                {
                    target = FindControl<T>(control.Parent, id, true);
                }
            }

            return target;
        }

        /// <summary>Finds the first descendant (or the control itself) of type <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">The type of control to find.</typeparam>
        /// <param name="control">The control to start from.</param>
        /// <returns>The control, or <see langword="null"/> when it was not found.</returns>
        public static T FindFirstDescendent<T>(IClientApiControl control)
            where T : class
        {
            return FindFirstDescendent<T>(control, idx => idx is T);
        }

        /// <summary>Finds the first descendant (or the control itself) matching the given predicate.</summary>
        /// <typeparam name="T">The type of control to find.</typeparam>
        /// <param name="control">The control to start from.</param>
        /// <param name="predicate">The predicate the control must match.</param>
        /// <returns>The control, or <see langword="null"/> when it was not found.</returns>
        public static T FindFirstDescendent<T>(IClientApiControl control, Predicate<IClientApiControl> predicate)
            where T : class
        {
            if (control == null)
            {
                return null;
            }

            if (predicate(control))
            {
                return control as T;
            }

            foreach (var childControl in control.Children)
            {
                var descendent = FindFirstDescendent<T>(childControl, predicate);
                if (descendent != null)
                {
                    return descendent;
                }
            }

            return null;
        }

        /// <summary>LoadControl loads a control and returns a reference to the control.</summary>
        /// <typeparam name="T">The type of control to Load.</typeparam>
        /// <param name="containerControl">The parent Container Control.</param>
        /// <param name="controlSrc">The source for the control. This can either be a skin/container
        /// or module markup path (originally a <c>.ascx</c> user control) or a compiled control's
        /// type name.</param>
        /// <returns>A Control of type T.</returns>
        /// <remarks>
        /// <para>
        /// The WebForms implementation called <c>TemplateControl.LoadControl</c>, which compiled the
        /// <c>.ascx</c> file on the fly. ASP.NET Core has no runtime markup compiler, so a markup
        /// path is resolved to the Blazor component type that replaced that file through
        /// <see cref="IDnnControlTypeResolver"/>, and the component is created from the request's
        /// service provider so it can take constructor dependencies.
        /// </para>
        /// <para>
        /// This never returns <see langword="null"/> silently: when the source cannot be resolved it
        /// throws, because a skin/container/module loader that quietly produces nothing renders a
        /// blank site.
        /// </para>
        /// </remarks>
        public static T LoadControl<T>(IComponent containerControl, string controlSrc)
            where T : class, IComponent
        {
            if (string.IsNullOrWhiteSpace(controlSrc))
            {
                throw new ArgumentException("A control source is required to load a control.", nameof(controlSrc));
            }

            var controlType = ResolveControlType(controlSrc);
            var serviceProvider = Common.Globals.GetCurrentServiceProvider();
            var control = ActivatorUtilities.CreateInstance(serviceProvider, controlType);

            if (control is not T typedControl)
            {
                throw new InvalidOperationException(
                    $"The control '{controlSrc}' resolved to '{controlType.FullName}', which is not a '{typeof(T).FullName}'.");
            }

            if (control is DnnComponentBase dnnControl)
            {
                dnnControl.ControlSrc = controlSrc;
                if (containerControl is IClientApiControl parentControl)
                {
                    dnnControl.SetParent(parentControl);
                }
            }

            return typedControl;
        }

        /// <summary>Resolves the component type that implements the given control source.</summary>
        /// <param name="controlSrc">The control source (a markup path or a type name).</param>
        /// <returns>The component type.</returns>
        internal static Type ResolveControlType(string controlSrc)
        {
            var isMarkupPath = controlSrc.EndsWith(".ascx", StringComparison.OrdinalIgnoreCase)
                               || controlSrc.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                               || controlSrc.Contains('/')
                               || controlSrc.Contains('\\');

            if (!isMarkupPath)
            {
                // load from a typename in an assembly ( ie. server control )
                var objType = Framework.Reflection.CreateType(controlSrc, true);
                if (objType == null)
                {
                    throw new InvalidOperationException($"The control type '{controlSrc}' could not be found.");
                }

                return objType;
            }

            var resolver = Common.Globals.GetCurrentServiceProvider().GetService<IDnnControlTypeResolver>()
                           ?? DnnControlTypeResolver.Instance;
            var resolved = resolver.Resolve(controlSrc);
            if (resolved == null)
            {
                throw new InvalidOperationException(
                    $"No component was found for the skin/container/module source '{controlSrc}'. " +
                    "Register the component that replaced this markup file with IDnnControlTypeResolver.");
            }

            return resolved;
        }
    }

    /// <summary>
    /// Maps a DNN skin, container or module markup source (historically an <c>.ascx</c> path) onto
    /// the Blazor component type that implements it.
    /// </summary>
    /// <remarks>
    /// Themes and third party modules ship markup on disk; on ASP.NET Core the markup is compiled
    /// into components ahead of time, so the mapping from the stored path (the value of
    /// <c>TabInfo.SkinSrc</c>, <c>ModuleInfo.ContainerSrc</c>, <c>ModuleControlInfo.ControlSrc</c>)
    /// to the component type has to be registered. Register an implementation in DI to override the
    /// default convention based resolution.
    /// </remarks>
    public interface IDnnControlTypeResolver
    {
        /// <summary>Resolves the component type implementing the given markup source.</summary>
        /// <param name="controlSrc">The markup source, e.g. <c>Portals/_default/Skins/Xcillion/Index.ascx</c>.</param>
        /// <returns>The component type, or <see langword="null"/> when it is unknown.</returns>
        Type Resolve(string controlSrc);

        /// <summary>Registers the component type implementing the given markup source.</summary>
        /// <param name="controlSrc">The markup source.</param>
        /// <param name="componentType">The component type.</param>
        void Register(string controlSrc, Type componentType);
    }

    /// <summary>
    /// The default <see cref="IDnnControlTypeResolver"/>: an explicit registry, falling back to
    /// locating a component whose type name matches the markup file name.
    /// </summary>
    public class DnnControlTypeResolver : IDnnControlTypeResolver
    {
        private static readonly DnnControlTypeResolver SharedInstance = new DnnControlTypeResolver();

        private readonly Dictionary<string, Type> registrations =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets the process wide resolver used when none is registered in DI.</summary>
        public static DnnControlTypeResolver Instance => SharedInstance;

        /// <inheritdoc />
        public Type Resolve(string controlSrc)
        {
            if (string.IsNullOrWhiteSpace(controlSrc))
            {
                return null;
            }

            var key = Normalize(controlSrc);
            lock (this.registrations)
            {
                if (this.registrations.TryGetValue(key, out var registered))
                {
                    return registered;
                }
            }

            var byConvention = FindByConvention(key);
            if (byConvention != null)
            {
                this.Register(controlSrc, byConvention);
            }

            return byConvention;
        }

        /// <inheritdoc />
        public void Register(string controlSrc, Type componentType)
        {
            if (string.IsNullOrWhiteSpace(controlSrc) || componentType == null)
            {
                return;
            }

            lock (this.registrations)
            {
                this.registrations[Normalize(controlSrc)] = componentType;
            }
        }

        private static string Normalize(string controlSrc)
        {
            var normalized = controlSrc.Replace('\\', '/').TrimStart('~', '/');
            return normalized;
        }

        private static Type FindByConvention(string normalizedSrc)
        {
            var fileName = normalizedSrc;
            var directoryName = string.Empty;
            var lastSlash = fileName.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                directoryName = fileName.Substring(0, lastSlash);
                fileName = fileName.Substring(lastSlash + 1);

                var secondLastSlash = directoryName.LastIndexOf('/');
                if (secondLastSlash >= 0)
                {
                    directoryName = directoryName.Substring(secondLastSlash + 1);
                }
            }

            var dot = fileName.LastIndexOf('.');
            if (dot > 0)
            {
                fileName = fileName.Substring(0, dot);
            }

            // Skin and container file names may contain spaces and dashes; the generated component
            // name cannot, so both the raw and the sanitized name are considered.
            var candidates = new[]
            {
                fileName,
                fileName.Replace(" ", string.Empty).Replace("-", string.Empty),
            };

            var matches = new Framework.Reflections.TypeLocator()
                .GetAllMatchingTypes(type => type != null
                                             && !type.IsAbstract
                                             && typeof(IComponent).IsAssignableFrom(type)
                                             && candidates.Any(candidate => string.Equals(type.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (matches.Count <= 1)
            {
                return matches.FirstOrDefault();
            }

            // The file name alone is not unique across the installation. Generic names such as
            // Login.razor and Settings.razor are used by every authentication provider, so the
            // bare-name match below would bind, say, the Google provider's
            // DesktopModules/AuthenticationServices/Google/Login.razor to
            // DotNetNuke.Authentication.Facebook.Login - with no error, because both types are
            // named "Login". The containing directory is what disambiguated these on the
            // WebForms side, so it is used here too: prefer a type whose namespace ends with the
            // directory segment (".../Google/Login.razor" -> "DotNetNuke.Authentication.Google").
            if (!string.IsNullOrEmpty(directoryName))
            {
                var sanitizedDirectory = directoryName.Replace(" ", string.Empty).Replace("-", string.Empty);
                var byDirectory = matches
                    .Where(type => type.Namespace != null
                                   && (type.Namespace.EndsWith("." + sanitizedDirectory, StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(type.Namespace, sanitizedDirectory, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (byDirectory.Count == 1)
                {
                    return byDirectory[0];
                }

                if (byDirectory.Count > 1)
                {
                    matches = byDirectory;
                }
            }

            // Still ambiguous: fail loudly rather than silently binding the wrong component,
            // which for an authentication control would mean authenticating against the wrong
            // provider's configuration. An explicit IDnnControlTypeResolver.Register call
            // resolves this deterministically.
            throw new InvalidOperationException(
                $"The control source '{normalizedSrc}' matches more than one component type " +
                $"({string.Join(", ", matches.Select(type => type.FullName))}). " +
                "Register the intended component with IDnnControlTypeResolver to resolve the ambiguity.");
        }
    }
}
