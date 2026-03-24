using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

public interface IYtDlpClient
{
	Task<string?> GetExecutablePathAsync(TubeArrDbContext db, CancellationToken ct);
	Task<IReadOnlyList<YtDlpChannelResultMapper.ChannelResultMap>> SearchChannelsAsync(string executablePath, string term, int maxResults, CancellationToken ct);
	Task<(IReadOnlyList<YtDlpChannelResultMapper.ChannelResultMap> Results, string? ResolutionMethod)> ResolveExactChannelAsync(string executablePath, string input, CancellationToken ct, int timeoutMs, ILogger logger);
	Task<(string? Title, string? Description, string? ThumbnailUrl, string? ChannelUrl, string? Handle)?> EnrichChannelForCreateAsync(string executablePath, string youtubeChannelId, CancellationToken ct);
}
