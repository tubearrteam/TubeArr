using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json;
using TubeArr.Backend.Data;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

public static class CommandEndpoints
{
	public static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/command", (InMemoryCommandState state) =>
		{
			return Results.Json(state.GetCommandsSnapshot());
		});

		api.MapPost("/command", async (
			JsonElement payload,
			TubeArrDbContext db,
			IServiceScopeFactory scopeFactory,
			ILogger<Program> logger,
			IRealtimeEventBroadcaster realtime,
			ChannelMetadataAcquisitionService channelMetadataAcquisitionService,
			YouTubeDataApiMetadataService youTubeDataApiMetadataService,
			CommandDispatcher dispatcher) =>
		{
			var result = await dispatcher.DispatchAsync(
				payload,
				db,
				scopeFactory,
				logger,
				realtime,
				channelMetadataAcquisitionService,
				youTubeDataApiMetadataService);
			return Results.Json(result);
		});

		api.MapDelete("/command/{id:int}", async (int id, InMemoryCommandState state, ICommandExecutionQueue commandQueue, IRealtimeEventBroadcaster realtime) =>
		{
			Dictionary<string, object?>? removed = null;
			var canCancel = false;

			lock (state.CommandsGate)
			{
				var command = state.Commands.FirstOrDefault(c =>
					c.TryGetValue("id", out var idObj) &&
					idObj is int existingId &&
					existingId == id);

				if (command is null)
					return Results.NotFound();

				var status = command.TryGetValue("status", out var statusObj)
					? Convert.ToString(statusObj)
					: null;

				if (!IsCancellableCommandStatus(status))
				{
					return Results.Conflict(new { message = "Only queued or in-progress commands can be cancelled." });
				}

				canCancel = true;
			}

			if (!canCancel || !await commandQueue.TryCancelAsync(id))
				return Results.Conflict(new { message = "Command is no longer cancellable (already finished or not in the execution queue)." });

			lock (state.CommandsGate)
			{
				var command = state.Commands.FirstOrDefault(c =>
					c.TryGetValue("id", out var idObj) &&
					idObj is int existingId &&
					existingId == id);

				if (command is null)
					return Results.NotFound();

				removed = new Dictionary<string, object?>(command);
				if (removed.TryGetValue("body", out var bodyObj) && bodyObj is Dictionary<string, object?> body)
				{
					removed["body"] = new Dictionary<string, object?>(body);
				}

				state.Commands.Remove(command);
			}

			await realtime.BroadcastAsync("command", new { action = "deleted", resource = removed }, System.Threading.CancellationToken.None);
			return Results.Ok(removed);
		});
	}

	static bool IsCancellableCommandStatus(string? status) =>
		string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(status, "started", StringComparison.OrdinalIgnoreCase);
}

