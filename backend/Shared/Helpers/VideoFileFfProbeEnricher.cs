using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media;
using System.IO;

namespace TubeArr.Backend;

/// <summary>Populates <see cref="VideoFileEntity.MediaInfoJson"/> using ffprobe for files that have not been probed yet.</summary>
internal static class VideoFileFfProbeEnricher
{
	static readonly JsonSerializerOptions SerializeOpts = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	/// <param name="channelId">When set, only rows for this channel are probed.</param>
	/// <param name="reportFileProgress">Invoked as each file is started: (1-based index, total, file label). Before the first file, (0, total, "") is sent when total &gt; 0.</param>
	public static async Task<(int Probed, string Message)> RunAsync(
		TubeArrDbContext db,
		ILogger logger,
		CancellationToken cancellationToken,
		Func<string, Task>? reportProgress = null,
		int? channelId = null,
		Func<int, int, string, Task>? reportFileProgress = null)
	{
		var ffmpeg = await db.FFmpegConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
		if (ffmpeg is null || !ffmpeg.Enabled || string.IsNullOrWhiteSpace(ffmpeg.ExecutablePath))
		{
			logger.LogInformation("ffprobe skipped: FFmpeg disabled or executable path not set.");
			return (0, "ffprobe skipped (FFmpeg disabled or path not set).");
		}

		var pendingQuery = db.VideoFiles
			.Where(vf => vf.MediaInfoJson == null || vf.MediaInfoJson == "");
		if (channelId is not null)
			pendingQuery = pendingQuery.Where(vf => vf.ChannelId == channelId.Value);

		var pending = await pendingQuery.ToListAsync(cancellationToken);

		if (pending.Count == 0)
			return (0, "No video files pending ffprobe.");

		logger.LogInformation("ffprobe: {Count} video file row(s) pending metadata.", pending.Count);
		if (reportFileProgress is not null)
			await reportFileProgress(0, pending.Count, string.Empty);
		else if (reportProgress is not null)
			await reportProgress($"ffprobe: {pending.Count} file(s) queued (this may take a while)…");

		var probed = 0;
		for (var i = 0; i < pending.Count; i++)
		{
			var row = pending[i];
			cancellationToken.ThrowIfCancellationRequested();

			var label = string.IsNullOrWhiteSpace(row.Path)
				? "(no path)"
				: Path.GetFileName(row.Path);

			if (reportFileProgress is not null)
				await reportFileProgress(i + 1, pending.Count, label);
			else if (reportProgress is not null && i > 0 && (i % 25 == 0 || i == pending.Count - 1))
				await reportProgress($"ffprobe: {i}/{pending.Count} file(s) processed…");

			if (string.IsNullOrWhiteSpace(row.Path) || !File.Exists(row.Path))
				continue;

			var payload = FfProbeMediaProbe.Probe(row.Path, ffmpeg.ExecutablePath);
			if (payload is null)
				continue;

			try
			{
				row.MediaInfoJson = JsonSerializer.Serialize(payload, SerializeOpts);
				probed++;
			}
			catch (Exception ex)
			{
				logger.LogDebug(ex, "ffprobe serialize failed videoFileId={VideoFileId}", row.Id);
			}
		}

		if (probed > 0)
			await db.SaveChangesAsync(cancellationToken);

		return (probed, probed == 0
			? "ffprobe produced no new metadata (files missing or probe failed)."
			: $"ffprobe enriched {probed} video file(s).");
	}
}
