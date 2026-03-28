using Microsoft.AspNetCore.Builder;

namespace TubeArr.Backend;

public static class SystemUpdateEndpoints
{
	public static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/update", async (IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken) =>
		{
			try
			{
				var items = await RemoteUpdateCatalog.FetchAsync(httpClientFactory, configuration, cancellationToken);
				return Results.Json(items);
			}
			catch
			{
				return Results.Json(Array.Empty<UpdateItemDto>());
			}
		});
	}
}
