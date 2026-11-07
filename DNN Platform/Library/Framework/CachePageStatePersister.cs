// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information
namespace DotNetNuke.Framework
{
    using System;
    using System.Globalization;
    using System.Text;

    using DotNetNuke.Common.Extensions;
    using DotNetNuke.Common.Utilities;
    using DotNetNuke.Services.Cache;

    /// <summary>CachePageStatePersister provides a cache based page state persistence mechanism.</summary>
    /// <remarks>
    /// The state is held in the DNN memory cache (<c>IMemoryCache</c> via <see cref="DataCache"/>)
    /// under a generated key. The key used to be round-tripped through a WebForms hidden field; it is
    /// now kept on the page component itself (<see cref="PageBase.PageStateCacheKey"/>) for the
    /// lifetime of the circuit, and a key posted by a legacy form field of the same name is still
    /// honoured on <see cref="Load"/> so that an incoming hard post can be read.
    /// </remarks>
    public class CachePageStatePersister : PageStatePersister
    {
        /// <summary>The name of the field/parameter the cache key is read from on a hard post.</summary>
        public const string ViewStateCacheKey = "__VIEWSTATE_CACHEKEY";

        /// <summary>Initializes a new instance of the <see cref="CachePageStatePersister"/> class.</summary>
        /// <param name="page">The <see cref="PageBase"/> that the state persistence mechanism is created for.</param>
        public CachePageStatePersister(PageBase page)
            : this(page, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="CachePageStatePersister"/> class.</summary>
        /// <param name="page">The <see cref="PageBase"/> that the state persistence mechanism is created for.</param>
        /// <param name="stateFormatter">The state formatter, or <see langword="null"/> for the default.</param>
        public CachePageStatePersister(PageBase page, IPageStateFormatter stateFormatter)
            : base(page, stateFormatter)
        {
        }

        /// <summary>Loads the Page State from the Cache.</summary>
        public override void Load()
        {
            // Get the cache key, preferring one posted by the request over the one this page holds.
            var key = this.Page.Request?.GetValue(ViewStateCacheKey);
            if (string.IsNullOrEmpty(key))
            {
                key = this.Page.PageStateCacheKey;
            }

            // Abort if cache key is not available or valid
            if (string.IsNullOrEmpty(key) || !key.StartsWith("VS_", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidViewStateCacheKeyException("Missing valid " + ViewStateCacheKey);
            }

            var state = DataCache.GetCache<PageState>(key);
            if (state != null)
            {
                // Set view state and control state
                this.ViewState = state.ViewState;
                this.ControlState = state.ControlState;
            }

            // Remove this state from the cache as it has served its purpose
            DataCache.RemoveCache(key);
            this.Page.PageStateCacheKey = null;
        }

        /// <summary>Saves the Page State to the Cache.</summary>
        public override void Save()
        {
            // No processing needed if no states available
            if (this.ViewState == null && this.ControlState == null)
            {
                return;
            }

            // Generate a unique cache key
            var key = new StringBuilder();
            {
                key.Append("VS_");
                key.Append(this.Page.PageStateId);
                key.Append('_');
                key.Append(DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture));
            }

            // Save view state and control state together
            var state = new PageState { ViewState = this.ViewState, ControlState = this.ControlState };

            // Add view state and control state to cache
            DNNCacheDependency objDependency = null;
            DataCache.SetCache(
                key.ToString(),
                state,
                objDependency,
                DateTime.Now.AddMinutes(this.StateTimeoutMinutes),
                Cache.NoSlidingExpiration,
                CacheItemPriority.NotRemovable,
                null);

            // Keep the cache key on the page, which is what the hidden field used to carry.
            this.Page.PageStateCacheKey = key.ToString();
        }
    }
}
