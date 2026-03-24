using Microsoft.AspNetCore.SignalR;

namespace TubeArr.Backend.Realtime;

public sealed class SignalRRealtimeEventBroadcaster : IRealtimeEventBroadcaster
{
	private readonly IHubContext<MessagesHub> _hubContext;

	public SignalRRealtimeEventBroadcaster(IHubContext<MessagesHub> hubContext)
	{
		_hubContext = hubContext;
	}

	public Task BroadcastAsync(string name, object body, CancellationToken ct = default)
	{
		return _hubContext.Clients.All.SendAsync("receiveMessage", new
		{
			name,
			body
		}, ct);
	}
}

