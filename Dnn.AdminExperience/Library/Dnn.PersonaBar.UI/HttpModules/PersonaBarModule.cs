// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace Dnn.PersonaBar.UI.HttpModules
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Dnn.PersonaBar.Library.AppEvents;
    using Dnn.PersonaBar.Library.Common;
    using DotNetNuke.Application;
    using DotNetNuke.Collections;
    using DotNetNuke.Instrumentation;
    using DotNetNuke.UI.Skins.EventListeners;

    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>An <see cref="IHostedService"/> which registers components for the Persona Bar.</summary>
    /// <remarks>
    /// Was an <c>IHttpModule</c> whose <c>Init(HttpApplication)</c> ran the application-start work
    /// once (behind a double-checked lock, because ASP.NET creates one module instance per
    /// <c>HttpApplication</c>) and whose <c>Dispose</c> raised the application-end event. ASP.NET
    /// Core has no <c>IHttpModule</c> pipeline; a hosted service is the application-lifetime
    /// equivalent, so <see cref="StartAsync"/> is the former <c>Init</c> and
    /// <see cref="StopAsync"/> the former <c>Dispose</c>. The once-only guard is kept: although
    /// the host resolves a hosted service a single time, <c>ApplicationStart</c> registers global
    /// skin-event listeners that must never be added twice.
    /// </remarks>
    public class PersonaBarModule : IHostedService
    {
        private static readonly ILogger Logger = DnnLoggingController.GetLogger<PersonaBarModule>();

        private static readonly object LockAppStarted = new object();
        private static bool hasAppStarted;

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (hasAppStarted)
            {
                return Task.CompletedTask;
            }

            lock (LockAppStarted)
            {
                if (hasAppStarted)
                {
                    return Task.CompletedTask;
                }

                ApplicationStart();
                hasAppStarted = true;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            EventsController.Instance.ApplicationEndEvent();

            return Task.CompletedTask;
        }

        private static void ApplicationStart()
        {
            EventsController.Instance.ApplicationStartEvent();

            DotNetNukeContext.Current.SkinEventListeners.Add(new SkinEventListener(SkinEventType.OnSkinInit, OnSkinInit));
            DotNetNukeContext.Current.SkinEventListeners.Add(new SkinEventListener(SkinEventType.OnSkinLoad, OnSkinLoad));
            DotNetNukeContext.Current.SkinEventListeners.Add(new SkinEventListener(SkinEventType.OnSkinPreRender, OnSkinPreRender));
            DotNetNukeContext.Current.SkinEventListeners.Add(new SkinEventListener(SkinEventType.OnSkinUnLoad, OnSkinUnLoad));
        }

        private static void OnSkinInit(object sender, SkinEventArgs e)
        {
            IocUtil.GetInstanceContracts<ISkinEvents>().ForEach(instance =>
            {
                try
                {
                    instance.Init(e);
                }
                catch (Exception ex)
                {
                    Logger.PersonaBarModuleSkinEventsInitThrewAnException(ex, instance.GetType().FullName);
                }
            });
        }

        private static void OnSkinLoad(object sender, SkinEventArgs e)
        {
            IocUtil.GetInstanceContracts<ISkinEvents>().ForEach(instance =>
            {
                try
                {
                    instance.Load(e);
                }
                catch (Exception ex)
                {
                    Logger.PersonaBarModuleSkinEventsLoadThrewAnException(ex, instance.GetType().FullName);
                }
            });
        }

        private static void OnSkinPreRender(object sender, SkinEventArgs e)
        {
            IocUtil.GetInstanceContracts<ISkinEvents>().ForEach(instance =>
            {
                try
                {
                    instance.PreRender(e);
                }
                catch (Exception ex)
                {
                    Logger.PersonaBarModuleSkinEventsPreRenderThrewAnException(ex, instance.GetType().FullName);
                }
            });
        }

        private static void OnSkinUnLoad(object sender, SkinEventArgs e)
        {
            IocUtil.GetInstanceContracts<ISkinEvents>().ForEach(instance =>
            {
                try
                {
                    instance.UnLoad(e);
                }
                catch (Exception ex)
                {
                    Logger.PersonaBarModuleSkinEventsUnLoadThrewAnException(ex, instance.GetType().FullName);
                }
            });
        }
    }
}
