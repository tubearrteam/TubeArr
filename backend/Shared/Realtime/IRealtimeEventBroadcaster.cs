namespace TubeArr.Backend.Realtime;

public interface IRealtimeEventBroadcaster
{
	Task BroadcastAsync(string name, object body, CancellationToken ct = default);
}

