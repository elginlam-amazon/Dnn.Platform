// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace System.Web.UI
{
    /// <summary>Members of the System.Web <see cref="TemplateControl"/> which are not provided by CoreWebForms.</summary>
    public static class TemplateControlExtensions
    {
        /// <summary>Loads a control based on its type and constructor parameters.</summary>
        /// <param name="templateControl">The template control which loads the control.</param>
        /// <param name="t">The type of the control.</param>
        /// <param name="parameters">The constructor parameters, or <see langword="null"/> for the parameterless constructor.</param>
        /// <returns>The control.</returns>
        public static Control LoadControl(this TemplateControl templateControl, Type t, object[] parameters)
        {
            if (templateControl == null)
            {
                throw new ArgumentNullException(nameof(templateControl));
            }

            if (t == null)
            {
                throw new ArgumentNullException(nameof(t));
            }

            if (!typeof(Control).IsAssignableFrom(t))
            {
                throw new ArgumentException($"The type '{t.FullName}' does not derive from Control.", nameof(t));
            }

            var control = (Control)Activator.CreateInstance(t, parameters ?? []);
            if (control is UserControl userControl)
            {
                userControl.InitializeAsUserControl(templateControl.Page);
            }

            return control;
        }
    }
}
