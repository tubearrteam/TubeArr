using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;
using TubeArr.Backend.Integrations.Slskd;

namespace TubeArr.Backend;

internal static class SlskdIntegrationEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		static async Task<SlskdConfigEntity> GetOrCreateAsync(TubeArrDbContext db)
		{
			var row = await db.SlskdConfig.OrderBy(x => x.Id).FirstOrDefaultAsync();
			if (row is null)
			{
				row = new SlskdConfigEntity { Id = 1 };
				db.SlskdConfig.Add(row);
				await db.SaveChangesAsync();
			}

			return row;
		}

		static object ToDto(SlskdConfigEntity c, bool maskKey) => new
		{
			enabled = c.Enabled,
			baseUrl = c.BaseUrl ?? "",
			apiKey = maskKey ? MaskApiKey(c.ApiKey) : (c.ApiKey ?? ""),
			localDownloadsPath = c.LocalDownloadsPath ?? "",
			searchTimeoutSeconds = c.SearchTimeoutSeconds,
			maxCandidatesStored = c.MaxCandidatesStored,
			autoPickMinScore = c.AutoPickMinScore,
			manualReviewOnly = c.ManualReviewOnly,
			retryAttempts = c.RetryAttempts,
			acquisitionOrder = AcquisitionOrderKindExtensions.ParseOrDefault(c.AcquisitionOrder).ToApiString(),
			fallbackToSlskdOnYtDlpFailure = c.FallbackToSlskdOnYtDlpFailure,
			fallbackToYtDlpOnSlskdFailure = c.FallbackToYtDlpOnSlskdFailure,
			higherQualityHandling = c.HigherQualityHandling,
			requireManualReviewOnTranscode = c.RequireManualReviewOnTranscode,
			keepOriginalAfterTranscode = c.KeepOriginalAfterTranscode
		};

		api.MapGet("/config/slskd", async (TubeArrDbContext db) =>
		{
			var c = await GetOrCreateAsync(db);
			return Results.Json(ToDto(c, maskKey: true));
		});

		api.MapPut("/config/slskd", async (SlskdConfigUpdateRequest request, TubeArrDbContext db) =>
		{
			var c = await GetOrCreateAsync(db);
			if (request.Enabled.HasValue)
				c.Enabled = request.Enabled.Value;
			if (request.BaseUrl is not null)
				c.BaseUrl = request.BaseUrl.Trim();
			if (request.ApiKey is not null && !string.IsNullOrWhiteSpace(request.ApiKey))
				c.ApiKey = request.ApiKey.Trim();
			if (request.LocalDownloadsPath is not null)
				c.LocalDownloadsPath = request.LocalDownloadsPath.Trim();
			if (request.SearchTimeoutSeconds.HasValue)
				c.SearchTimeoutSeconds = Math.Clamp(request.SearchTimeoutSeconds.Value, 5, 600);
			if (request.MaxCandidatesStored.HasValue)
				c.MaxCandidatesStored = Math.Clamp(request.MaxCandidatesStored.Value, 1, 500);
			if (request.AutoPickMinScore.HasValue)
				c.AutoPickMinScore = Math.Clamp(request.AutoPickMinScore.Value, 0, 500);
			if (request.ManualReviewOnly.HasValue)
				c.ManualReviewOnly = request.ManualReviewOnly.Value;
			if (request.RetryAttempts.HasValue)
				c.RetryAttempts = Math.Clamp(request.RetryAttempts.Value, 0, 10);
			if (request.AcquisitionOrder is not null
				&& AcquisitionOrderKindExtensions.TryParseApi(request.AcquisitionOrder, out var ord))
				c.AcquisitionOrder = (int)ord;
			if (request.FallbackToSlskdOnYtDlpFailure.HasValue)
				c.FallbackToSlskdOnYtDlpFailure = request.FallbackToSlskdOnYtDlpFailure.Value;
			if (request.FallbackToYtDlpOnSlskdFailure.HasValue)
				c.FallbackToYtDlpOnSlskdFailure = request.FallbackToYtDlpOnSlskdFailure.Value;
			if (request.HigherQualityHandling.HasValue)
				c.HigherQualityHandling = Math.Clamp(request.HigherQualityHandling.Value, 0, 1);
			if (request.RequireManualReviewOnTranscode.HasValue)
				c.RequireManualReviewOnTranscode = request.RequireManualReviewOnTranscode.Value;
			if (request.KeepOriginalAfterTranscode.HasValue)
				c.KeepOriginalAfterTranscode = request.KeepOriginalAfterTranscode.Value;

			if (c.Enabled && (string.IsNullOrWhiteSpace(c.BaseUrl) || string.IsNullOrWhiteSpace(c.ApiKey)))
				return Results.Json(new { message = "Base URL and API key are required when slskd is enabled." }, statusCode: 400);

			await db.SaveChangesAsync();
			return Results.Json(ToDto(c, maskKey: true));
		});

		api.MapPost("/config/slskd/test", async (TubeArrDbContext db, IHttpClientFactory httpFactory, SlskdHttpClient slskd) =>
		{
			var c = await GetOrCreateAsync(db);
			if (string.IsNullOrWhiteSpace(c.BaseUrl) || string.IsNullOrWhiteSpace(c.ApiKey))
				return Results.Json(new { success = false, message = "Configure base URL and API key first." }, statusCode: 400);

			using var http = slskd.CreateClient(c.BaseUrl.Trim(), c.ApiKey);
			var (ok, code, body, err) = await SlskdHttpClient.GetAsync(http, "api/v0/application/version", CancellationToken.None);
			if (ok)
				return Results.Json(new { success = true, message = (body ?? "").Trim(), statusCode = code });
			return Results.Json(new { success = false, message = err ?? body ?? $"HTTP {code}" }, statusCode: 400);
		});
	}

	static string MaskApiKey(string? key)
	{
		if (string.IsNullOrEmpty(key))
			return "";
		return key.Length <= 4 ? "****" : "****" + key[^4..];
	}

}
