// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Collections;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Event arguments passed to a delegate associated to a client postback event.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "Breaking change")]
    public class ClientAPIPostBackEventArgs
    {
        /// <summary>The name of the event being raised.</summary>
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        public string EventName;

        /// <summary>The arguments of the event, keyed by name.</summary>
        [SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields", Justification = "Breaking change")]
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:FieldsMustBePrivate", Justification = "Breaking change")]
        public Hashtable EventArguments = new Hashtable();

        /// <summary>Initializes a new instance of the <see cref="ClientAPIPostBackEventArgs"/> class.</summary>
        public ClientAPIPostBackEventArgs()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ClientAPIPostBackEventArgs"/> class.</summary>
        /// <param name="strEventArgument">The raw post back argument sent by the client, which holds
        /// the event name followed by name/value pairs separated by the column delimiter.</param>
        public ClientAPIPostBackEventArgs(string strEventArgument)
        {
            var aryArgs = (strEventArgument ?? string.Empty).Split(new[] { ClientAPI.COLUMN_DELIMITER }, StringSplitOptions.None);

            if (aryArgs.Length > 0)
            {
                this.EventName = aryArgs[0];
            }

            for (var i = 1; i <= aryArgs.Length - 1; i += 2)
            {
                this.EventArguments.Add(aryArgs[i], aryArgs[i + 1]);
            }
        }
    }
}
