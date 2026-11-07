// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

namespace DotNetNuke.Providers.Caching.SimpleWebFarmCachingProvider;

using Microsoft.Extensions.Logging;

/// <summary>Extension methods for <see cref="ILogger"/> for pre-defined logging messages.</summary>
/// <remarks>The DotNetNuke.Providers.Caching.SimpleWebFarmCachingProvider project has been assigned event IDs from 3,700,000 to 3,799,999.</remarks>
internal static partial class LoggerMessages
{
    [LoggerMessage(EventId = 3_700_000, Level = LogLevel.Debug, Message = "Cache server notification timed out. Url: {NotificationUrl}")]
    public static partial void CacheServerNotificationTimedOut(this ILogger logger, string notificationUrl);

    [LoggerMessage(EventId = 3_700_001, Level = LogLevel.Warning, Message = "Unable to notify the other web farm servers: dependency injection is not initialized yet.")]
    public static partial void DependencyInjectionNotInitialized(this ILogger logger);
}
