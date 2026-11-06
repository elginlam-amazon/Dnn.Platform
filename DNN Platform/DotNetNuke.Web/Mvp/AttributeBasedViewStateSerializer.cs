// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Web.Mvp
{
    using System;
    using System.Linq;
    using System.Reflection;

    using DotNetNuke.Internal.SourceGenerators;

    /// <summary>
    /// Moves the properties marked with <see cref="ViewStateAttribute"/> between an object and an
    /// <see cref="MvpStateBag"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reflection contract is unchanged; what changed is where the state goes. It used to go
    /// into <c>System.Web.UI.StateBag</c>, i.e. into the page's <c>__VIEWSTATE</c> hidden field, and
    /// it now goes into an <see cref="MvpStateBag"/> owned by the component
    /// (<see cref="ModuleViewBase.State"/>), which is explicit component state that Blazor's
    /// <c>PersistentComponentState</c> carries across the pre-render boundary.
    /// </para>
    /// <para>
    /// Only writable properties are restored; a read-only <c>[ViewState]</c> property is still saved
    /// (so that it can be inspected) but cannot be assigned back, which is also what reflection
    /// against view state did.
    /// </para>
    /// </remarks>
    [DnnDeprecated(9, 2, 0, "Replace WebFormsMvp and DotNetNuke.Web.Mvp with MVC or SPA patterns instead")]
    public partial class AttributeBasedViewStateSerializer
    {
        private const BindingFlags MemberBindingFlags = BindingFlags.Instance | BindingFlags.Public;

        /// <summary>Deserializes the <paramref name="state"/> into the <paramref name="value"/>.</summary>
        /// <param name="value">The value.</param>
        /// <param name="state">The state.</param>
        public static void DeSerialize(object value, MvpStateBag state)
        {
            if (value == null || state == null)
            {
                return;
            }

            Type typ = value.GetType();

            // Parse all the Public instance properties
            foreach (PropertyInfo member in typ.GetProperties(MemberBindingFlags))
            {
                // Determine if they are attributed with a ViewState Attribute
                ViewStateAttribute attr = member.GetCustomAttributes(typeof(ViewStateAttribute), true).OfType<ViewStateAttribute>().FirstOrDefault();
                if (attr == null || !member.CanWrite)
                {
                    continue;
                }

                // Get object from the state bag
                var stateKey = GetStateKey(attr, member);
                if (!state.ContainsKey(stateKey))
                {
                    // nothing was stored for this property; leave its current value alone rather
                    // than overwriting it with null.
                    continue;
                }

                member.SetValue(value, state[stateKey], null);
            }
        }

        /// <summary>Serializes the <paramref name="value"/> into the <paramref name="state"/>.</summary>
        /// <param name="value">The value.</param>
        /// <param name="state">The state.</param>
        public static void Serialize(object value, MvpStateBag state)
        {
            if (value == null || state == null)
            {
                return;
            }

            Type typ = value.GetType();

            // Parse all the Public instance properties
            foreach (PropertyInfo member in typ.GetProperties(MemberBindingFlags))
            {
                // Determine if they are attributed with a ViewState Attribute
                ViewStateAttribute attr = member.GetCustomAttributes(typeof(ViewStateAttribute), true).OfType<ViewStateAttribute>().FirstOrDefault();
                if (attr == null || !member.CanRead)
                {
                    continue;
                }

                // Add the property to the state bag
                state[GetStateKey(attr, member)] = member.GetValue(value, null);
            }
        }

        private static string GetStateKey(ViewStateAttribute attr, PropertyInfo member)
        {
            // Use the class member's name when no explicit key is declared
            return string.IsNullOrEmpty(attr.ViewStateKey) ? member.Name : attr.ViewStateKey;
        }
    }
}
