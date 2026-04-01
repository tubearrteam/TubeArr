using System;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public interface IYtDlpClient
{
	Task<string?> GetExecutablePathAsync(TubeArrDbContext db, CancellationToken ct);
	Task<string?> GetCookiesPathAsync(TubeArrDbContext db, CancellationToken ct, string? contentRoot = null);
	Task<IReadOnlyList<YtDlpChannelResultMapper.ChannelResultMap>> SearchChannelsAsync(string executablePath, string term, int maxResults, CancellationToken ct, string? cookiesPath = null);
	Task<(IReadOnlyList<YtDlpChannelResultMapper.ChannelResultMap> Results, string? ResolutionMethod)> ResolveExactChannelAsync(string executablePath, string input, CancellationToken ct, int timeoutMs, ILogger logger, string? cookiesPath = null);
	Task<(IReadOnlyList<YtDlpChannelResultMapper.ChannelResultMap> Results, string? ResolutionMethod)> ResolveUploaderFromYoutubeMediaUrlAsync(string executablePath, string mediaUrl, CancellationToken ct, int timeoutMs, ILogger logger, string? cookiesPath = null);
	Task<(string? Title, string? Description, string? ThumbnailUrl, string? ChannelUrl, string? Handle)?> EnrichChannelForCreateAsync(string executablePath, string youtubeChannelId, CancellationToken ct, string? cookiesPath = null);
}
