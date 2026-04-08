using Microsoft.AspNetCore.Builder;
using TubeArr.Backend.Plex;

namespace TubeArr.Backend;

internal static class DebugEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/debug/plex/match-traces", (PlexMatchTraceBuffer buffer) =>
			Results.Json(buffer.Snapshot()));
	}
}
