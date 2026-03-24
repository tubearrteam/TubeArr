using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class TubeArrHealthCheckRunner
{
	public static async Task<List<Dictionary<string, object?>>> CollectAsync(
		TubeArrDbContext db,
		YouTubeDataApiMetadataService youTubeDataApi,
		CancellationToken ct = default)
	{
		var results = new List<Dictionary<string, object?>>();

		try
		{
			await db.Database.CanConnectAsync(ct);
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "Database",
				["status"] = "ok",
				["message"] = "Database connection succeeded."
			});
		}
		catch (Exception ex)
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "Database",
				["status"] = "error",
				["message"] = ex.Message
			});
		}

		var ytPath = await YtDlpMetadataService.GetExecutablePathAsync(db, ct);
		if (string.IsNullOrWhiteSpace(ytPath))
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "YtDlp",
				["status"] = "warn",
				["message"] = "yt-dlp is not configured or disabled; downloads may not work."
			});
		}
		else
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "YtDlp",
				["status"] = "ok",
				["message"] = ytPath
			});
		}

		var ffmpeg = await db.FFmpegConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
		var ffmpegPath = (ffmpeg?.ExecutablePath ?? "").Trim();
		if (string.IsNullOrWhiteSpace(ffmpegPath))
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "FFmpeg",
				["status"] = "warn",
				["message"] = "FFmpeg path is not configured."
			});
		}
		else
		{
			results.Add(new Dictionary<string, object?>
			{
				["type"] = "FFmpeg",
				["status"] = "ok",
				["message"] = ffmpegPath
			});
		}

		var youTubeCheck = await youTubeDataApi.TryBuildHealthCheckAsync(db, ct);
		if (youTubeCheck is not null)
			results.Add(youTubeCheck);

		return results;
	}

	public static string Summarize(IReadOnlyList<Dictionary<string, object?>> checks)
	{
		static string? StatusOf(Dictionary<string, object?> c) =>
			c.TryGetValue("status", out var s) ? s?.ToString() : null;

		var errors = checks.Count(c => string.Equals(StatusOf(c), "error", StringComparison.OrdinalIgnoreCase));
		var warns = checks.Count(c => string.Equals(StatusOf(c), "warn", StringComparison.OrdinalIgnoreCase));
		if (errors > 0)
			return $"{errors} error(s), {warns} warning(s). See command body or GET /health.";
		if (warns > 0)
			return $"Healthy with {warns} warning(s).";
		return "All checks passed.";
	}
}
