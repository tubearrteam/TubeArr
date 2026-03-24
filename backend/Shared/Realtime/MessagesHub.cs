using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.Realtime;

public sealed class MessagesHub : Hub
{
	private readonly TubeArrDbContext _db;

	public MessagesHub(TubeArrDbContext db)
	{
		_db = db;
	}

	public override async Task OnConnectedAsync()
	{
		var http = Context.GetHttpContext();
		var token = http?.Request.Query["access_token"].FirstOrDefault();
		var settings = await _db.ServerSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
		var expectedApiKey = settings?.ApiKey ?? string.Empty;

		if (string.IsNullOrWhiteSpace(expectedApiKey) ||
			!string.Equals(token, expectedApiKey, StringComparison.Ordinal))
		{
			Context.Abort();
			return;
		}

		await base.OnConnectedAsync();
	}
}

