// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.UI.Utilities
{
    using System;
    using System.Collections;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Control used to register post-back events.
    /// </summary>
    /// <remarks>
    /// In order for a post-back event to be trapped we need to associate a control to handle the
    /// event.
    /// </remarks>
    public class ClientAPIPostBackControl : ClientApiControlBase, IClientApiPostBackEventHandler
    {
        private readonly Hashtable eventHandlers = new Hashtable();

        /// <summary>Initializes a new instance of the <see cref="ClientAPIPostBackControl"/> class.</summary>
        /// <param name="objPage">The page the control lives on.</param>
        /// <param name="strEventName">The name of the event to handle.</param>
        /// <param name="objDelegate">The handler of the event.</param>
        public ClientAPIPostBackControl(IClientApiPage objPage, string strEventName, PostBackEvent objDelegate)
        {
            this.AssignedPage = objPage;
            ClientAPI.GetPostBackClientEvent(objPage, this, string.Empty);
            this.AddEventHandler(strEventName, objDelegate);
        }

        /// <summary>Initializes a new instance of the <see cref="ClientAPIPostBackControl"/> class.</summary>
        public ClientAPIPostBackControl()
        {
        }

        /// <summary>Handles a client side post back event raised through the Client API.</summary>
        /// <param name="args">The arguments of the event.</param>
        public delegate void PostBackEvent(ClientAPIPostBackEventArgs args);

        /// <summary>
        /// Gets the individual post back event handler registered for the given event name.
        /// </summary>
        /// <param name="strEventName">The name of the event.</param>
        /// <returns>The handler of the event, or <see langword="null"/> when none is registered.</returns>
        public PostBackEvent EventHandlers(string strEventName)
        {
            if (this.eventHandlers.ContainsKey(strEventName))
            {
                return (PostBackEvent)this.eventHandlers[strEventName];
            }

            return null;
        }

        /// <summary>Adds a postback event handler to the control.</summary>
        /// <param name="strEventName">The name of the event.</param>
        /// <param name="objDelegate">The handler of the event.</param>
        public void AddEventHandler(string strEventName, PostBackEvent objDelegate)
        {
            if (this.eventHandlers.ContainsKey(strEventName) == false)
            {
                this.eventHandlers.Add(strEventName, objDelegate);
            }
            else
            {
                this.eventHandlers[strEventName] = (PostBackEvent)Delegate.Combine(
                    (PostBackEvent)this.eventHandlers[strEventName], objDelegate);
            }
        }

        /// <summary>
        /// Allows the hosting page to invoke the control's events, mirroring the WebForms
        /// <c>IPostBackEventHandler</c> implementation of the original control.
        /// </summary>
        /// <param name="strEventArgument">The raw post back argument sent by the client.</param>
        [SuppressMessage("Microsoft.Naming", "CA1725:ParameterNamesShouldMatchBaseDeclaration", Justification = "Breaking change")]
        public void RaisePostBackEvent(string strEventArgument)
        {
            var objArg = new ClientAPIPostBackEventArgs(strEventArgument);
            var handler = this.EventHandlers(objArg.EventName);
            if (handler is not null)
            {
                handler(objArg);
            }
        }
    }
}
