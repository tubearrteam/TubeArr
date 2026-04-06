using Microsoft.AspNetCore.Builder;
using TubeArr.Backend.Contracts;

namespace TubeArr.Backend;

internal static class ApiEndpointComposer
{
	/// <summary>
	/// Maps supported API routes under /api/v1.
	/// </summary>
	/// <remarks>
	/// <para><b>Issue 10 (placeholder endpoints):</b> Sonarr-style routes that returned empty JSON (indexer, marketplace,
	/// downloadClient, etc.) are not mounted; unused stub code was removed. Notification routes stay mounted: they are
	/// persisted (see NotificationConnections) and Plex pin flows call notification action URLs.</para>
	/// <para><b>Issue 11 (empty or stub-shaped JSON):</b> Inventory of intentional empties vs real data:</para>
	/// <list type="bullet">
	/// <item><description><b>Sonarr-compat field shape:</b> GET /tag/detail uses empty int arrays for indexerIds,
	/// downloadClientIds, and similar keys TubeArr does not use. GET /history/channel and queue/history DTOs use
	/// Array.Empty for languages and customFormats. GET /autoTagging returns [].</description></item>
	/// <item><description><b>Legitimate no-data:</b> GET /rename and similar return [] when rename is off, channel missing,
	/// or no matching files. Channel search/resolve return [] when there are no hits. GET /history/channel returns []
	/// for invalid channelId.</description></item>
	/// <item><description><b>Provider / filesystem:</b> Plex JSON responses may use empty Metadata arrays (e.g. 404
	/// handler in hosting). Root folder and disk browse endpoints return empty directory or unmappedFolders lists when
	/// there is nothing to show or on error paths documented in those handlers.</description></item>
	/// </list>
	/// <para>When adding endpoints, prefer 404 or 400 with a message for invalid references; use [] or empty objects only
	/// when the Sonarr UI contract expects a list or “no items” is normal.</para>
	/// </remarks>
	internal static void MapTubeArrApiEndpoints(
		this WebApplication app,
		string preloadedUrlBase,
		Lazy<IReadOnlyDictionary<string, string>> englishStringsLazy)
	{
		var api = app.MapGroup("/api/v1");

		SystemMiscEndpoints.Map(
			api,
			preloadedUrlBase,
			englishStringsLazy);

		NotificationApiEndpoints.Map(api);

		SystemUpdateEndpoints.Map(api);

		QualityProfileAndConfigEndpoints.Map(api);
		TagEndpoints.Map(api);
		CustomFilterEndpoints.Map(api);
		ChannelListDetailEndpoints.Map(api);
		ChannelCrudEndpoints.Map(api);
		ChannelResolveEndpoints.Map(api);
		VideoFileEndpoints.Map(api);
		VideoEndpoints.Map(api);
		LogAndHistoryEndpoints.Map(api, englishStringsLazy);
		QueueAndHistoryEndpoints.Map(api);
		CommandEndpoints.Map(api);
		NamingConfigEndpoints.Map(api);
		SystemAdminEndpoints.Map(api);
	}
}
