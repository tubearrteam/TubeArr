using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;
using TubeArr.Backend.Realtime;

namespace TubeArr.Backend;

public sealed class ChannelIngestionOrchestrator
{
	readonly ChannelPageMetadataService _channelPageMetadataService;
	readonly IYtDlpClient _ytDlpClient;
	readonly IServiceScopeFactory _scopeFactory;
	readonly CommandDispatcher _commandDispatcher;
	readonly IRealtimeEventBroadcaster _realtime;
	readonly ILogger<ChannelIngestionOrchestrator> _logger;

	public ChannelIngestionOrchestrator(
		ChannelPageMetadataService channelPageMetadataService,
		IYtDlpClient ytDlpClient,
		IServiceScopeFactory scopeFactory,
		CommandDispatcher commandDispatcher,
		IRealtimeEventBroadcaster realtime,
		ILogger<ChannelIngestionOrchestrator> logger)
	{
		_channelPageMetadataService = channelPageMetadataService;
		_ytDlpClient = ytDlpClient;
		_scopeFactory = scopeFactory;
		_commandDispatcher = commandDispatcher;
		_realtime = realtime;
		_logger = logger;
	}

	public async Task<(ChannelEntity? Channel, bool WasNew, string? ErrorMessage)> CreateOrUpdateAsync(
		CreateChannelRequest request,
		TubeArrDbContext db,
		CancellationToken ct = default)
	{
		var youtubeChannelId = (request.YoutubeChannelId ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(youtubeChannelId))
			return (null, false, "youtubeChannelId is required");

		var title = (request.Title ?? string.Empty).Trim();
		var description = request.Description;
		var thumbnailUrl = (string?)null;
		var bannerUrl = (string?)null;
		var monitored = request.Monitored;
		var qualityProfileId = request.QualityProfileId.HasValue && request.QualityProfileId.Value > 0
			? request.QualityProfileId
			: null;
		var rootFolderPath = request.RootFolderPath;
		var channelType = request.ChannelType;
		var playlistFolder = request.PlaylistFolder;
		var playlistMultiMatchStrategy = request.PlaylistMultiMatchStrategy is int pms && pms is >= 0 and <= 3
			? pms
			: 0;
		var playlistMultiMatchStrategyOrder = ChannelDtoMapper.NormalizePlaylistMultiMatchStrategyOrder(request.PlaylistMultiMatchStrategyOrder?.Trim())
			?? ChannelDtoMapper.DerivePlaylistMultiMatchStrategyOrderFromLegacy(playlistMultiMatchStrategy);
		var monitorNewItems = request.MonitorNewItems ?? (monitored ? 1 : 0);
		var roundRobinCap = request.RoundRobinLatestVideoCount is int rr && rr > 0 ? rr : (int?)null;
		var path = request.Path;
		var filterOutShorts = request.FilterOutShorts;
		var filterOutLivestreams = request.FilterOutLivestreams;
		var monitorPreset = NormalizeMonitorPreset(request.MonitorPreset);
		bool? mergedHasShortsTab = null;
		bool? mergedHasStreamsTab = null;

		// UI (add / import) always sends title from search or resolve; skip slow inline fetches so POST returns
		// as soon as the row is saved. Background refresh still hydrates playlists, uploads, and richer metadata.
		if (string.IsNullOrWhiteSpace(title) &&
		    ChannelResolveHelper.LooksLikeYouTubeChannelId(youtubeChannelId))
		{
			ChannelPageMetadata? directChannelMetadata = null;
			try
			{
				directChannelMetadata = await _channelPageMetadataService.GetMetadataByYoutubeChannelIdAsync(youtubeChannelId, ct);
			}
			catch (Exception ex)
			{
				_logger.LogDebug(ex, "Channel metadata direct parse threw for {YoutubeChannelId}", youtubeChannelId);
			}

			ChannelPageMetadata? fallbackChannelMetadata = null;
			if (!HasCompleteChannelMetadata(directChannelMetadata))
			{
				var ytDlpPath = await _ytDlpClient.GetExecutablePathAsync(db, ct);
				if (!string.IsNullOrWhiteSpace(ytDlpPath))
				{
					try
					{
						var ytDlpCookiesPath = await _ytDlpClient.GetCookiesPathAsync(db, ct);
						var enriched = await _ytDlpClient.EnrichChannelForCreateAsync(ytDlpPath, youtubeChannelId, ct, ytDlpCookiesPath);
						if (enriched.HasValue)
						{
							var (ytTitle, ytDesc, ytThumb, _, _) = enriched.Value;
							fallbackChannelMetadata = new ChannelPageMetadata(
								YoutubeChannelId: youtubeChannelId,
								Title: ytTitle,
								Description: ytDesc,
								ThumbnailUrl: ytThumb,
								BannerUrl: null,
								CanonicalUrl: $"https://www.youtube.com/channel/{youtubeChannelId}");
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Channel metadata yt-dlp fallback failed for {YoutubeChannelId}", youtubeChannelId);
					}
				}
				else
				{
					_logger.LogDebug("Channel metadata yt-dlp fallback unavailable (yt-dlp not configured) for {YoutubeChannelId}", youtubeChannelId);
				}
			}

			var mergedTitle = directChannelMetadata?.Title ?? fallbackChannelMetadata?.Title;
			var mergedDescription = directChannelMetadata?.Description ?? fallbackChannelMetadata?.Description;
			var mergedThumbnailUrl = directChannelMetadata?.ThumbnailUrl ?? fallbackChannelMetadata?.ThumbnailUrl;
			var mergedBannerUrl = directChannelMetadata?.BannerUrl ?? fallbackChannelMetadata?.BannerUrl;
			mergedHasShortsTab = directChannelMetadata?.HasShortsTab == true || fallbackChannelMetadata?.HasShortsTab == true
				? true
				: (bool?)null;
			mergedHasStreamsTab = directChannelMetadata?.HasStreamsTab == true || fallbackChannelMetadata?.HasStreamsTab == true
				? true
				: (bool?)null;
			if (string.IsNullOrWhiteSpace(mergedTitle) ||
				string.IsNullOrWhiteSpace(mergedDescription) ||
				string.IsNullOrWhiteSpace(mergedThumbnailUrl) ||
				string.IsNullOrWhiteSpace(mergedBannerUrl))
			{
				_logger.LogWarning("Channel metadata remained incomplete after direct parse and fallback for {YoutubeChannelId}", youtubeChannelId);
			}

			if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(mergedTitle))
				title = mergedTitle.Trim();
			if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(mergedDescription))
				description = mergedDescription;
			if (string.IsNullOrWhiteSpace(thumbnailUrl) && !string.IsNullOrWhiteSpace(mergedThumbnailUrl))
				thumbnailUrl = mergedThumbnailUrl.Trim();
			if (string.IsNullOrWhiteSpace(bannerUrl) && !string.IsNullOrWhiteSpace(mergedBannerUrl))
				bannerUrl = mergedBannerUrl.Trim();
		}

		if (string.IsNullOrWhiteSpace(title))
			return (null, false, "title is required after metadata extraction.");

		var titleSlug = SlugHelper.Slugify(title);
		var hadExplicitImportPath = !string.IsNullOrWhiteSpace((path ?? "").Trim());
		var storage = await ResolveChannelStorageAsync(
			db,
			youtubeChannelId,
			title,
			titleSlug,
			rootFolderPath,
			path,
			ct);
		rootFolderPath = storage.RootFolderPath;
		path = storage.ChannelPath;

		var wasNew = false;
		var existing = await db.Channels.FirstOrDefaultAsync(x => x.YoutubeChannelId == youtubeChannelId, ct);
		if (existing is null &&
		    hadExplicitImportPath &&
		    !string.IsNullOrWhiteSpace(rootFolderPath) &&
		    !string.IsNullOrWhiteSpace(path))
		{
			var normalized = await TryNormalizeImportedChannelFolderAsync(
				db,
				youtubeChannelId,
				title,
				titleSlug,
				rootFolderPath,
				path,
				ct);
			if (!string.IsNullOrWhiteSpace(normalized))
				path = normalized;
		}
		if (existing is null)
		{
			wasNew = true;
			existing = new ChannelEntity
			{
				YoutubeChannelId = youtubeChannelId,
				Title = title,
				Description = description,
				ThumbnailUrl = string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl,
				BannerUrl = string.IsNullOrWhiteSpace(bannerUrl) ? null : bannerUrl,
				TitleSlug = titleSlug,
				Monitored = monitored,
				Added = DateTimeOffset.UtcNow,
				QualityProfileId = qualityProfileId,
				Path = path,
				RootFolderPath = rootFolderPath,
				MonitorNewItems = monitorNewItems,
				ChannelType = channelType,
				PlaylistFolder = playlistFolder,
				PlaylistMultiMatchStrategy = playlistMultiMatchStrategy,
				PlaylistMultiMatchStrategyOrder = playlistMultiMatchStrategyOrder,
				RoundRobinLatestVideoCount = roundRobinCap,
				FilterOutShorts = filterOutShorts,
				FilterOutLivestreams = filterOutLivestreams,
				HasShortsTab = mergedHasShortsTab,
				HasStreamsTab = mergedHasStreamsTab,
				MonitorPreset = monitorPreset
			};

			db.Channels.Add(existing);
		}
		else
		{
			existing.Title = title;
			existing.TitleSlug = titleSlug;
			existing.Description = description;
			existing.ThumbnailUrl = string.IsNullOrWhiteSpace(thumbnailUrl) ? existing.ThumbnailUrl : thumbnailUrl;
			existing.BannerUrl = string.IsNullOrWhiteSpace(bannerUrl) ? existing.BannerUrl : bannerUrl;
			existing.Monitored = monitored;
			existing.QualityProfileId = qualityProfileId;
			if (!string.IsNullOrWhiteSpace(path))
				existing.Path = path;
			if (!string.IsNullOrWhiteSpace(rootFolderPath))
				existing.RootFolderPath = rootFolderPath;
			if (request.MonitorNewItems.HasValue || !existing.MonitorNewItems.HasValue)
				existing.MonitorNewItems = monitorNewItems;
			if (channelType is not null)
				existing.ChannelType = channelType;
			if (playlistFolder.HasValue)
				existing.PlaylistFolder = playlistFolder;
			if (request.PlaylistMultiMatchStrategyOrder is not null)
			{
				var normalized = ChannelDtoMapper.NormalizePlaylistMultiMatchStrategyOrder(request.PlaylistMultiMatchStrategyOrder.Trim());
				if (normalized is not null)
				{
					existing.PlaylistMultiMatchStrategyOrder = normalized;
					existing.PlaylistMultiMatchStrategy = normalized[0] - '0';
				}
			}
			else if (request.PlaylistMultiMatchStrategy is int pms2 && pms2 is >= 0 and <= 3)
			{
				existing.PlaylistMultiMatchStrategy = pms2;
				existing.PlaylistMultiMatchStrategyOrder = ChannelDtoMapper.DerivePlaylistMultiMatchStrategyOrderFromLegacy(pms2);
			}
			if (request.RoundRobinLatestVideoCount.HasValue)
				existing.RoundRobinLatestVideoCount = request.RoundRobinLatestVideoCount.Value <= 0 ? null : request.RoundRobinLatestVideoCount.Value;
			existing.FilterOutShorts = filterOutShorts;
			existing.FilterOutLivestreams = filterOutLivestreams;
			if (mergedHasShortsTab == true)
				existing.HasShortsTab = true;
			if (mergedHasStreamsTab == true)
				existing.HasStreamsTab = true;
			existing.MonitorPreset = monitorPreset;
		}

		await db.SaveChangesAsync(ct);

		if (wasNew)
			await ChannelTagHelper.ReplaceChannelTagsAsync(db, existing.Id, request.Tags, ct);
		else if (request.Tags is not null)
			await ChannelTagHelper.ReplaceChannelTagsAsync(db, existing.Id, request.Tags, ct);

		if (wasNew || request.Tags is not null)
			await db.SaveChangesAsync(ct);

		if (!wasNew)
			await RoundRobinMonitoringHelper.ApplyForChannelAsync(db, existing.Id, ct);
		else
		{
			try
			{
				await _commandDispatcher.QueueRefreshChannelAsync(existing.Id, "auto", _realtime);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Queueing channel metadata acquisition failed for {ChannelId}", existing.Id);
			}
		}

		return (existing, wasNew, null);
	}

	private static bool HasCompleteChannelMetadata(ChannelPageMetadata? metadata)
	{
		return metadata is not null &&
			!string.IsNullOrWhiteSpace(metadata.Title) &&
			!string.IsNullOrWhiteSpace(metadata.Description) &&
			!string.IsNullOrWhiteSpace(metadata.ThumbnailUrl) &&
			!string.IsNullOrWhiteSpace(metadata.BannerUrl);
	}

	private static string? NormalizeMonitorPreset(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return null;
		var s = raw.Trim();
		if (string.Equals(s, "specificVideos", StringComparison.OrdinalIgnoreCase))
			return "specificVideos";
		if (string.Equals(s, "specificPlaylists", StringComparison.OrdinalIgnoreCase))
			return "specificPlaylists";
		return null;
	}

	private static async Task<(string? RootFolderPath, string? ChannelPath)> ResolveChannelStorageAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		string title,
		string titleSlug,
		string? requestedRootFolderPath,
		string? requestedPath,
		CancellationToken ct = default)
	{
		var normalizedRequestedPath = string.IsNullOrWhiteSpace(requestedPath) ? null : requestedPath.Trim();
		var normalizedRootFolderPath = string.IsNullOrWhiteSpace(requestedRootFolderPath)
			? await db.RootFolders.AsNoTracking().OrderBy(x => x.Path).Select(x => x.Path).FirstOrDefaultAsync(ct)
			: requestedRootFolderPath.Trim();

		if (!string.IsNullOrWhiteSpace(normalizedRequestedPath))
			return (normalizedRootFolderPath, normalizedRequestedPath);

		if (string.IsNullOrWhiteSpace(normalizedRootFolderPath))
		{
			var folderOnly = await ComputeCanonicalChannelFolderNameAsync(db, youtubeChannelId, title, titleSlug, ct);
			return (null, folderOnly);
		}

		var combined = await BuildCanonicalChannelPathUnderRootAsync(db, youtubeChannelId, title, titleSlug, normalizedRootFolderPath, ct);
		return (normalizedRootFolderPath, combined);
	}

	static async Task<string?> ComputeCanonicalChannelFolderNameAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		string title,
		string titleSlug,
		CancellationToken ct)
	{
		var naming = await db.NamingConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct) ?? new NamingConfigEntity { Id = 1 };
		var previewChannel = new ChannelEntity
		{
			YoutubeChannelId = youtubeChannelId,
			Title = title,
			TitleSlug = titleSlug
		};
		var dummyVideo = new VideoEntity
		{
			Title = title,
			YoutubeVideoId = youtubeChannelId,
			UploadDateUtc = DateTimeOffset.UtcNow
		};
		var context = new VideoFileNaming.NamingContext(
			Channel: previewChannel,
			Video: dummyVideo,
			Playlist: null,
			PlaylistIndex: null,
			QualityFull: null,
			Resolution: null,
			Extension: null);
		var folderName = VideoFileNaming.BuildFolderName(naming.ChannelFolderFormat, context, naming);
		if (string.IsNullOrWhiteSpace(folderName))
			folderName = titleSlug;

		return string.IsNullOrWhiteSpace(folderName) ? null : folderName;
	}

	/// <summary>Channel folder path under the root using Settings → Media Management → Channel Folder Format (same as a non-import create).</summary>
	static async Task<string?> BuildCanonicalChannelPathUnderRootAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		string title,
		string titleSlug,
		string normalizedRootFolderPath,
		CancellationToken ct)
	{
		var folderName = await ComputeCanonicalChannelFolderNameAsync(db, youtubeChannelId, title, titleSlug, ct);
		if (folderName is null)
			return null;

		return Path.Combine(normalizedRootFolderPath.Trim(), folderName);
	}

	static string? ToFullLibraryPath(string? rootFolderPath, string? channelPath)
	{
		if (string.IsNullOrWhiteSpace(channelPath))
			return null;
		var p = channelPath.Trim();
		try
		{
			if (Path.IsPathRooted(p))
				return Path.GetFullPath(p);
			if (string.IsNullOrWhiteSpace(rootFolderPath))
				return null;
			return Path.GetFullPath(Path.Combine(rootFolderPath.Trim(), p));
		}
		catch
		{
			return null;
		}
	}

	static bool FullPathsAreEqual(string a, string b)
	{
		try
		{
			var fa = Path.GetFullPath(a);
			var fb = Path.GetFullPath(b);
			return OperatingSystem.IsWindows()
				? string.Equals(fa, fb, StringComparison.OrdinalIgnoreCase)
				: string.Equals(fa, fb, StringComparison.Ordinal);
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// When the user picked an on-disk folder for import, rename it to the canonical channel folder from naming settings if needed (e.g. <c>somefolder</c> → <c>{Channel Name}</c>).
	/// </summary>
	async Task<string?> TryNormalizeImportedChannelFolderAsync(
		TubeArrDbContext db,
		string youtubeChannelId,
		string title,
		string titleSlug,
		string rootFolderPath,
		string currentPathFromRequest,
		CancellationToken ct)
	{
		var canonicalCombined = await BuildCanonicalChannelPathUnderRootAsync(db, youtubeChannelId, title, titleSlug, rootFolderPath, ct);
		if (string.IsNullOrWhiteSpace(canonicalCombined))
			return null;

		string? canonicalFull;
		string? currentFull;
		try
		{
			canonicalFull = Path.GetFullPath(canonicalCombined);
			currentFull = ToFullLibraryPath(rootFolderPath, currentPathFromRequest);
		}
		catch (Exception ex)
		{
			_logger.LogDebug(ex, "Import folder normalize: could not resolve paths for {YoutubeChannelId}", youtubeChannelId);
			return null;
		}

		if (currentFull is null || FullPathsAreEqual(currentFull, canonicalFull))
			return null;

		if (!Directory.Exists(currentFull))
		{
			_logger.LogDebug("Import folder normalize skipped: source does not exist {Path}", currentFull);
			return null;
		}

		if (Directory.Exists(canonicalFull))
		{
			_logger.LogWarning(
				"Import folder normalize skipped: target folder already exists (leaving import path unchanged). source={Source} target={Target}",
				currentFull,
				canonicalFull);
			return null;
		}

		try
		{
			var destParent = Path.GetDirectoryName(canonicalFull);
			if (!string.IsNullOrEmpty(destParent))
				Directory.CreateDirectory(destParent);
			Directory.Move(currentFull, canonicalFull);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Import folder normalize failed for {YoutubeChannelId}", youtubeChannelId);
			return null;
		}

		_logger.LogInformation(
			"Normalized imported channel folder to media-management path: {Source} → {Target}",
			currentFull,
			canonicalFull);

		return canonicalCombined;
	}

}
