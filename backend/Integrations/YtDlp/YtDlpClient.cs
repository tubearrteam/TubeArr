using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public sealed class YtDlpClient : IYtDlpClient
{
	public Task<string?> GetExecutablePathAsync(TubeArrDbContext db, CancellationToken ct)
	{
		return YtDlpMetadataService.GetExecutablePathAsync(db, ct);
	}

	public Task<string?> GetCookiesPathAsync(TubeArrDbContext db, CancellationToken ct, string? contentRoot = null)
	{
		return YtDlpMetadataService.GetCookiesPathAsync(db, ct, contentRoot);
	}

	public async Task<IReadOnlyList<YtDlpChannelResultMapper.ChannelResultMap>> SearchChannelsAsync(string executablePath, string term, int maxResults, CancellationToken ct, string? cookiesPath = null)
	{
		var results = await YtDlpChannelLookupService.SearchChannelsAsync(executablePath, term, maxResults, ct, cookiesPath);
		return results;
	}

	public async Task<(IReadOnlyList<YtDlpChannelResultMapper.ChannelResultMap> Results, string? ResolutionMethod)> ResolveExactChannelAsync(string executablePath, string input, CancellationToken ct, int timeoutMs, ILogger logger, string? cookiesPath = null)
	{
		var lookup = await YtDlpChannelLookupService.ResolveExactChannelAsync(executablePath, input, ct, timeoutMs, logger, cookiesPath);
		IReadOnlyList<YtDlpChannelResultMapper.ChannelResultMap> normalizedResults = lookup.Results is null
			? Array.Empty<YtDlpChannelResultMapper.ChannelResultMap>()
			: lookup.Results;
		return (normalizedResults, lookup.ResolutionMethod);
	}

	public Task<(string? Title, string? Description, string? ThumbnailUrl, string? ChannelUrl, string? Handle)?> EnrichChannelForCreateAsync(string executablePath, string youtubeChannelId, CancellationToken ct, string? cookiesPath = null)
	{
		return YtDlpChannelLookupService.EnrichChannelForCreateAsync(executablePath, youtubeChannelId, ct, cookiesPath);
	}
}
