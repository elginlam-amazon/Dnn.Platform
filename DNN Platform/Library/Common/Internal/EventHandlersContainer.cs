// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

// ReSharper disable ConvertPropertyToExpressionBody
namespace DotNetNuke.Common.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Composition;

    using DotNetNuke.Abstractions.Application;
    using DotNetNuke.ComponentModel;
    using DotNetNuke.ExtensionPoints;
    using DotNetNuke.Instrumentation;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    /// <summary>A container to hold event handlers.</summary>
    /// <typeparam name="T">The type of event handlers.</typeparam>
    internal class EventHandlersContainer<T> : ComponentBase<IEventHandlersContainer<T>, EventHandlersContainer<T>>, IEventHandlersContainer<T>
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<EventHandlersContainer<T>>();

        // This was a field. MEF 2's ImportManyAttribute is not valid on a field (only on a property
        // or parameter), so the import is now a private property with the same semantics.
        [ImportMany]
        private IEnumerable<Lazy<T>> ImportedEventHandlers { get; set; } = new List<Lazy<T>>();

        /// <summary>Initializes a new instance of the <see cref="EventHandlersContainer{T}"/> class.</summary>
        public EventHandlersContainer()
        {
            try
            {
                if (GetCurrentStatus() != UpgradeStatus.None)
                {
                    return;
                }

                ExtensionPointManager.ComposeParts(this);
            }
            catch (Exception ex)
            {
                Logger.EventHandlersContainerConstructorException(ex, ex.Message);
            }
        }

        /// <inheritdoc />
        public IEnumerable<Lazy<T>> EventHandlers
        {
            get
            {
                return this.ImportedEventHandlers;
            }
        }

        private static UpgradeStatus GetCurrentStatus()
        {
            try
            {
                return Globals.GetCurrentServiceProvider().GetRequiredService<IApplicationStatusInfo>().Status;
            }
            catch (NullReferenceException)
            {
                return UpgradeStatus.Unknown;
            }
        }
    }
}
