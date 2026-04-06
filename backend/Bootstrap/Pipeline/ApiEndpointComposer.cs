using Microsoft.AspNetCore.Builder;
using TubeArr.Backend.Contracts;

namespace TubeArr.Backend;

internal static class ApiEndpointComposer
{
	/// <summary>
	/// Maps supported API routes. Some Sonarr-compat stubs remain unmounted where the UI does not call them.
	/// </summary>
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
