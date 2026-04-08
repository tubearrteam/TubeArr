using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;
using TubeArr.Backend.Integrations.Slskd;

namespace TubeArr.Backend;

internal static class SlskdVideoEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/videos/{videoId:int}/external/slskd/candidates", async (int videoId, TubeArrDbContext db) =>
		{
			var q = await db.DownloadQueue
				.Where(x => x.VideoId == videoId && x.Status == QueueJobStatuses.Running)
				.OrderByDescending(x => x.Id)
				.FirstOrDefaultAsync();
			if (q is null || string.IsNullOrWhiteSpace(q.ExternalAcquisitionJson))
				return Results.Json(new { phase = "", candidates = Array.Empty<object>(), message = "No active slskd session for this video." });

			var ext = ExternalAcquisitionJsonSerializer.TryDeserialize(q.ExternalAcquisitionJson);
			if (ext is null)
				return Results.Json(new { phase = "", candidates = Array.Empty<object>() });

			return Results.Json(new
			{
				queueId = q.Id,
				phase = ext.Phase,
				candidates = ext.Candidates.Select(c => new
				{
					c.Id,
					c.Username,
					c.Filename,
					c.Size,
					c.Extension,
					c.DurationSeconds,
					c.BitrateKbps,
					c.MatchScore,
					c.Confidence,
					c.MatchedSignals,
					c.SearchQueryUsed
				}),
				chosenId = ext.ChosenCandidate?.Id,
				lastError = q.LastError,
				fallbackUsed = ext.FallbackUsed,
				primaryFailureSummary = ext.PrimaryFailureSummary
			});
		});

		api.MapPost("/videos/{videoId:int}/external/slskd/search", async (int videoId, TubeArrDbContext db) =>
		{
			var q = await db.DownloadQueue
				.Where(x => x.VideoId == videoId && x.Status == QueueJobStatuses.Running)
				.OrderByDescending(x => x.Id)
				.FirstOrDefaultAsync();
			if (q is null)
				return Results.Json(new { message = "No running download queue item for this video. Enqueue a download first." }, statusCode: 404);

			var ext = ExternalAcquisitionJsonSerializer.TryDeserialize(q.ExternalAcquisitionJson) ?? new ExternalAcquisitionState();
			if (ext.ActiveProvider != "slskd")
				return Results.Json(new { message = "Current acquisition is not slskd for this queue item." }, statusCode: 400);

			ext.Phase = ExternalAcquisitionPhases.PendingSearch;
			ext.SearchId = null;
			ext.Candidates = new List<ExternalDownloadCandidateDto>();
			ext.ChosenCandidate = null;
			ext.TransferId = null;
			ext.TransferUsername = null;
			ext.ResumeProcessor = true;
			q.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
			q.ExternalWorkPending = 1;
			q.LastError = null;
			await db.SaveChangesAsync();
			return Results.Json(new { ok = true, queueId = q.Id });
		});

		api.MapPost("/videos/{videoId:int}/external/slskd/select", async (int videoId, SlskdSelectCandidateRequest body, TubeArrDbContext db) =>
		{
			if (string.IsNullOrWhiteSpace(body.CandidateId))
				return Results.Json(new { message = "candidateId required." }, statusCode: 400);

			var q = await db.DownloadQueue
				.Where(x => x.VideoId == videoId && x.Status == QueueJobStatuses.Running)
				.OrderByDescending(x => x.Id)
				.FirstOrDefaultAsync();
			if (q is null || string.IsNullOrWhiteSpace(q.ExternalAcquisitionJson))
				return Results.Json(new { message = "No active slskd queue item for this video." }, statusCode: 400);

			var ext = ExternalAcquisitionJsonSerializer.TryDeserialize(q.ExternalAcquisitionJson);
			if (ext is null)
				return Results.Json(new { message = "Invalid acquisition state." }, statusCode: 400);

			if (ext.Phase != ExternalAcquisitionPhases.CandidatesReady && ext.Phase != ExternalAcquisitionPhases.AwaitingManualPick)
				return Results.Json(new { message = "No candidate list ready for selection (wrong phase)." }, statusCode: 400);

			var pick = ext.Candidates.FirstOrDefault(c => string.Equals(c.Id, body.CandidateId, StringComparison.Ordinal));
			if (pick is null)
				return Results.Json(new { message = "Candidate not found." }, statusCode: 404);

			ext.ChosenCandidate = pick;
			ext.Phase = ExternalAcquisitionPhases.QueuedTransfer;
			q.ExternalAcquisitionJson = ExternalAcquisitionJsonSerializer.Serialize(ext);
			q.ExternalWorkPending = 1;
			q.LastError = null;
			await db.SaveChangesAsync();
			return Results.Json(new { ok = true, queueId = q.Id });
		});

		api.MapPost("/queue/items/{queueId:int}/external/cancel", async (int queueId, TubeArrDbContext db, SlskdHttpClient slskd) =>
		{
			var q = await db.DownloadQueue.FirstOrDefaultAsync(x => x.Id == queueId);
			if (q is null)
				return Results.NotFound();

			var cfg = await db.SlskdConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
			if (cfg is null || !cfg.Enabled)
				return Results.BadRequest();

			var ext = ExternalAcquisitionJsonSerializer.TryDeserialize(q.ExternalAcquisitionJson);
			if (ext?.TransferId is { } tid && !string.IsNullOrEmpty(ext.TransferUsername))
			{
				using var http = slskd.CreateClient(cfg.BaseUrl.Trim(), cfg.ApiKey);
				var enc = Uri.EscapeDataString(ext.TransferUsername);
				await SlskdHttpClient.DeleteAsync(http, $"api/v0/transfers/downloads/{enc}/{tid:N}?remove=true", CancellationToken.None);
			}

			q.Status = QueueJobStatuses.Aborted;
			q.LastError = "slskd download cancelled.";
			q.ExternalWorkPending = 0;
			q.EndedAtUtc = DateTimeOffset.UtcNow;
			await db.SaveChangesAsync();
			return Results.NoContent();
		});
	}
}

public record SlskdSelectCandidateRequest(string? CandidateId);
