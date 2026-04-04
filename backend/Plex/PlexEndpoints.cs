using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media;
using TubeArr.Backend.Media.Nfo;

namespace TubeArr.Backend.Plex;

internal static class PlexEndpoints
{
	static readonly JsonSerializerOptions PlexJson = new()
	{
		PropertyNamingPolicy = null
	};

	internal static void Map(WebApplication app)
	{
		MapRoutes(app, prefix: "");

		// If configured, also expose the provider under a mounted base path.
		// This is intentionally best-effort; the provider remains reachable at /tv for local use.
		try
		{
			using var scope = app.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<TubeArrDbContext>();
			var cfg = PlexProviderConfig.GetAsync(db, CancellationToken.None).GetAwaiter().GetResult();
			var bp = (cfg.BasePath ?? "").Trim();
			if (!string.IsNullOrWhiteSpace(bp))
				MapRoutes(app, prefix: bp);
		}
		catch
		{
			// Best-effort; do not block server startup if config can't be read here.
		}
	}

	static void MapRoutes(WebApplication app, string prefix)
	{
		IEndpointRouteBuilder group = app;
		if (!string.IsNullOrWhiteSpace(prefix))
			group = app.MapGroup(NormalizePrefix(prefix));

		group.MapGet("/tv", async (TubeArrDbContext db, ILogger<PlexProviderLog> logger, CancellationToken ct) =>
		{
			var cfg = await PlexProviderConfig.GetAsync(db, ct);
			logger.LogWarning("Plex tv: GET /tv enabled={Enabled}", cfg.Enabled);
			if (!cfg.Enabled)
			{
				logger.LogWarning("Plex tv: GET /tv returning 404 (set plex provider enabled=true via API or DB)");
				logger.LogDebug("GET /tv disabled (plex provider off)");
				return Results.NotFound();
			}

			logger.LogWarning("Plex tv: GET /tv (MediaProvider definition)");
			logger.LogDebug("GET /tv MediaProvider definition");

			// Plex requires numeric metadata types (2/3/4 for show/season/episode), Scheme[] per type, and Feature entries with `key` paths.
			// See: https://github.com/plexinc/tmdb-example-provider/blob/main/docs/MediaProvider.md
			var schemeEntry = new { scheme = PlexConstants.ProviderIdentifier };
			return Results.Json(new
			{
				MediaProvider = new
				{
					identifier = PlexConstants.ProviderIdentifier,
					title = PlexConstants.ProviderTitle,
					version = PlexConstants.ProviderVersion,
					Types = new object[]
					{
						new { type = 2, Scheme = new[] { schemeEntry } },
						new { type = 3, Scheme = new[] { schemeEntry } },
						new { type = 4, Scheme = new[] { schemeEntry } }
					},
					Feature = new[]
					{
						new { type = "metadata", key = "/library/metadata" },
						new { type = "match", key = "/library/metadata/matches" }
					}
				}
			}, PlexJson);
		});

		group.MapGet("/tv/health", async (TubeArrDbContext db, ILogger<PlexProviderLog> logger, CancellationToken ct) =>
		{
			var cfg = await PlexProviderConfig.GetAsync(db, ct);
			logger.LogDebug("GET /tv/health enabled={Enabled}", cfg.Enabled);
			return Results.Json(new
			{
				ok = true,
				enabled = cfg.Enabled,
				identifier = PlexConstants.ProviderIdentifier
			});
		});

		group.MapGet("/tv/artwork/episode-thumb", async (HttpRequest request, TubeArrDbContext db, ILogger<PlexProviderLog> logger, CancellationToken ct) =>
		{
			var cfg = await PlexProviderConfig.GetAsync(db, ct);
			if (!cfg.Enabled)
			{
				logger.LogDebug("GET /tv/artwork/episode-thumb disabled (provider off)");
				return Results.NotFound();
			}

			var yt = (request.Query["youtubeVideoId"].ToString() ?? "").Trim();
			if (yt.Length == 0)
				return Results.BadRequest();

			var video = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.YoutubeVideoId == yt, ct);
			if (video is null)
				return Results.NotFound();

			var paths = await LoadPrimaryVideoFilePathsByVideoIdsAsync(db, [video.Id], ct);
			if (!paths.TryGetValue(video.Id, out var mediaPath) || string.IsNullOrWhiteSpace(mediaPath))
				return Results.NotFound();

			if (!File.Exists(mediaPath))
				return Results.NotFound();

			var thumb = PlexEpisodeSidecarPaths.TryGetExistingSidecarPath(mediaPath);
			if (thumb is null)
				return Results.NotFound();

			logger.LogDebug("GET /tv/artwork/episode-thumb youtubeVideoId={YoutubeVideoId}", yt);

			var lastWrite = File.GetLastWriteTimeUtc(thumb);
			var etag = $"\"{lastWrite.Ticks:x}\"";
			request.HttpContext.Response.Headers["Cache-Control"] = "no-cache";
			request.HttpContext.Response.Headers["ETag"] = etag;

			if (request.Headers.IfNoneMatch.ToString() == etag)
				return Results.StatusCode(304);

			return Results.File(thumb, "image/jpeg");
		});

		group.MapPost("/tv/library/metadata/matches", async (HttpRequest req, TubeArrDbContext db, ILogger<PlexProviderLog> logger, CancellationToken ct) =>
		{
			var cfg = await PlexProviderConfig.GetAsync(db, ct);
			logger.LogWarning("Plex tv: POST /library/metadata/matches enabled={Enabled}", cfg.Enabled);
			if (!cfg.Enabled)
			{
				logger.LogWarning("Plex tv: POST matches returning 404 (provider disabled)");
				logger.LogDebug("POST /tv/library/metadata/matches disabled");
				return Results.NotFound();
			}

			logger.LogWarning("Plex tv: POST /library/metadata/matches");
			using var doc = await JsonDocument.ParseAsync(req.Body, cancellationToken: ct);
			var root = doc.RootElement;
			var fields = BuildCaseInsensitivePropertyMap(root);
			var type = GetIntFromPropertyMap(fields, "type");
			var includeManual = GetIntFromPropertyMap(fields, "manual") == 1;
			var path = ExtractMatchPath(fields);
			var title = CoalesceStringFromPropertyMap(fields, "title", "name", "originalTitle", "sortTitle");
			var guid = CoalesceStringFromPropertyMap(fields, "guid") ?? "";

			logger.LogDebug("POST /tv/library/metadata/matches type={Type} title={Title} guid={Guid} path={Path}", type, title, guid, path);
			if (cfg.VerboseRequestLogging)
				logger.LogInformation("Plex match request type={Type} title={Title} guid={Guid} path={Path}", type, title, guid, path);

			var matches = type switch
			{
				2 => await MatchShowAsync(db, logger, title, guid, path, ct),
				3 => await MatchSeasonAsync(db, logger, root, fields, guid, path, includeManual, ct),
				4 => await MatchEpisodeAsync(db, logger, root, fields, guid, path, includeManual, req, ct),
				_ => new List<object>()
			};

			if (matches.Count == 0 && type >= 2 && type <= 4)
				logger.LogWarning("Plex match returned no results type={Type} title={Title} pathLen={PathLen} (check JSON field names, title vs folder, or Channel.Path)", type, title, path.Length);

			return Results.Json(new
			{
				MediaContainer = new
				{
					offset = 0,
					totalSize = matches.Count,
					identifier = PlexConstants.ProviderIdentifier,
					size = matches.Count,
					Metadata = matches
				}
			}, PlexJson);
		});

		// Plex follows each show/season item's `key` (…/library/metadata/{id}/children) to list seasons or episodes.
		group.MapGet("/tv/library/metadata/{ratingKey}/children", async (string ratingKey, HttpRequest req, TubeArrDbContext db, ILogger<PlexProviderLog> logger, CancellationToken ct) =>
		{
			var cfg = await PlexProviderConfig.GetAsync(db, ct);
			ratingKey = Uri.UnescapeDataString(ratingKey.Trim());
			logger.LogWarning("Plex tv: GET children ratingKey={RatingKey} enabled={Enabled}", ratingKey, cfg.Enabled);
			if (!cfg.Enabled)
			{
				logger.LogWarning("Plex tv: GET children returning 404 (provider disabled)");
				logger.LogDebug("GET /tv/library/metadata/.../children disabled (provider off)");
				return Results.NotFound();
			}

			var paging = ParsePlexPaging(req);

			logger.LogWarning("Plex tv: GET /library/metadata/{RatingKey}/children", ratingKey);
			logger.LogDebug("GET /tv/library/metadata/{RatingKey}/children start={Start} size={Size}", ratingKey, paging.Start, paging.Size);
			if (cfg.VerboseRequestLogging)
				logger.LogInformation("Plex metadata children request ratingKey={RatingKey} start={Start} size={Size}", ratingKey, paging.Start, paging.Size);

			if (TryParseChannelOnlySeasonKey(ratingKey, out var channelIdOnly))
			{
				var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.YoutubeChannelId == channelIdOnly, ct);
				if (channel is null)
					return Results.NotFound();

				var episodes = await LoadChannelOnlyEpisodeChildrenAsync(db, channel, includeChildren: true, req, ct);
				return Results.Json(WrapPagedMetadata(episodes, paging), PlexJson);
			}

			if (!PlexIdentifier.TryParseRatingKey(ratingKey, out var kind, out var ytId))
				return Results.NotFound();

			switch (kind)
			{
				case PlexIdentifier.PlexItemKind.Show:
				{
					var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.YoutubeChannelId == ytId, ct);
					if (channel is null)
						return Results.NotFound();
					var seasons = await LoadSeasonChildrenAsync(db, channel, ct);
					return Results.Json(WrapPagedMetadata(seasons, paging), PlexJson);
				}
				case PlexIdentifier.PlexItemKind.Season:
				{
					var playlist = await db.Playlists.FirstOrDefaultAsync(p => p.YoutubePlaylistId == ytId, ct);
					if (playlist is null)
						return Results.NotFound();
					var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == playlist.ChannelId, ct);
					if (channel is null)
						return Results.NotFound();
					var seasonIndex = playlist.SeasonIndex.HasValue && playlist.SeasonIndex.Value > 0
						? playlist.SeasonIndex.Value
						: await StableTvNumbering.EnsurePlaylistSeasonIndexAsync(db, playlist.Id, ct);
					var episodes = await LoadEpisodeChildrenForPlaylistAsync(db, channel, playlist, seasonIndex, req, ct);
					return Results.Json(WrapPagedMetadata(episodes, paging), PlexJson);
				}
				default:
					return Results.Json(WrapPagedMetadata(Array.Empty<object>(), paging), PlexJson);
			}
		});

		// Required for TV libraries: flat list of all episodes under a show (see tmdb-example-provider MetadataService.getGrandchildren).
		group.MapGet("/tv/library/metadata/{ratingKey}/grandchildren", async (string ratingKey, HttpRequest req, TubeArrDbContext db, ILogger<PlexProviderLog> logger, CancellationToken ct) =>
		{
			var cfg = await PlexProviderConfig.GetAsync(db, ct);
			ratingKey = Uri.UnescapeDataString(ratingKey.Trim());
			logger.LogWarning("Plex tv: GET grandchildren ratingKey={RatingKey} enabled={Enabled}", ratingKey, cfg.Enabled);
			if (!cfg.Enabled)
			{
				logger.LogWarning("Plex tv: GET grandchildren returning 404 (provider disabled)");
				logger.LogDebug("GET /tv/library/metadata/.../grandchildren disabled (provider off)");
				return Results.NotFound();
			}

			var paging = ParsePlexPaging(req);
			logger.LogWarning("Plex tv: GET /library/metadata/{RatingKey}/grandchildren", ratingKey);
			logger.LogDebug("GET /tv/library/metadata/{RatingKey}/grandchildren start={Start} size={Size}", ratingKey, paging.Start, paging.Size);

			if (!PlexIdentifier.TryParseRatingKey(ratingKey, out var gKind, out var gYt) || gKind != PlexIdentifier.PlexItemKind.Show)
				return Results.NotFound();

			var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.YoutubeChannelId == gYt, ct);
			if (channel is null)
				return Results.NotFound();

			var allEpisodes = await LoadAllEpisodesForShowAsync(db, channel, req, ct);
			return Results.Json(WrapPagedMetadata(allEpisodes, paging), PlexJson);
		});

		group.MapGet("/tv/library/metadata/{ratingKey}", async (string ratingKey, HttpRequest req, TubeArrDbContext db, ILogger<PlexProviderLog> logger, CancellationToken ct) =>
		{
			var cfg = await PlexProviderConfig.GetAsync(db, ct);
			var rkDecoded = Uri.UnescapeDataString((ratingKey ?? "").Trim());
			logger.LogWarning("Plex tv: GET metadata ratingKey={RatingKey} enabled={Enabled}", rkDecoded, cfg.Enabled);
			if (!cfg.Enabled)
			{
				logger.LogWarning("Plex tv: GET metadata returning 404 (provider disabled)");
				logger.LogDebug("GET /tv/library/metadata/{RatingKey} disabled (provider off)", ratingKey);
				return Results.NotFound();
			}

			var includeChildren = QueryFlag(req, "includeChildren") ?? cfg.IncludeChildrenByDefault;

			ratingKey = rkDecoded;

			logger.LogWarning("Plex tv: GET /library/metadata/{RatingKey} includeChildren={IncludeChildren}", ratingKey, includeChildren);
			logger.LogDebug("GET /tv/library/metadata/{RatingKey} includeChildren={IncludeChildren}", ratingKey, includeChildren);
			if (cfg.VerboseRequestLogging)
				logger.LogInformation("Plex metadata request ratingKey={RatingKey} includeChildren={IncludeChildren}", ratingKey, includeChildren);

			if (TryParseChannelOnlySeasonKey(ratingKey, out var showChannelId))
			{
				var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.YoutubeChannelId == showChannelId, ct);
				if (channel is null)
					return Results.NotFound();

				var seasonMeta = BuildChannelOnlySeasonMetadata(channel, includeChildren, await LoadChannelOnlyEpisodeChildrenAsync(db, channel, includeChildren, req, ct));
				return Results.Json(WrapSingle(seasonMeta), PlexJson);
			}

			if (!PlexIdentifier.TryParseRatingKey(ratingKey, out var kind, out var ytId))
			{
				if (LooksLikePlexInternalNumericRatingKey(ratingKey))
					logger.LogWarning(
						"Plex metadata GET used Plex-internal numeric ratingKey={RatingKey}; TubeArr only serves ch_/pl_/v_ keys from match results. Item may still be agent tv.plex.agents.none — run Fix Match / refresh until the library item picks up the custom agent.",
						ratingKey);
				else
					logger.LogWarning("Plex metadata ratingKey parse failed: {RatingKey}", ratingKey);
				return Results.NotFound();
			}

			object? meta = kind switch
			{
				PlexIdentifier.PlexItemKind.Show => await BuildShowAsync(db, ytId, includeChildren, ct),
				PlexIdentifier.PlexItemKind.Season => await BuildSeasonAsync(db, ytId, includeChildren, req, ct),
				PlexIdentifier.PlexItemKind.Episode => await BuildEpisodeAsync(db, ytId, includeChildren, req, ct),
				_ => null
			};

			return meta is null ? Results.NotFound() : Results.Json(WrapSingle(meta), PlexJson);
		});
	}

	static string NormalizePrefix(string prefix)
	{
		var p = (prefix ?? "").Trim();
		p = p.Trim('/');
		return "/" + p;
	}

	static object WrapSingle(object meta) => new
	{
		MediaContainer = new
		{
			offset = 0,
			totalSize = 1,
			identifier = PlexConstants.ProviderIdentifier,
			size = 1,
			Metadata = new[] { meta }
		}
	};

	readonly record struct PlexPaging(int Size, int Start);

	static PlexPaging ParsePlexPaging(HttpRequest req)
	{
		var size = 20;
		var start = 1;
		if (TryGetIntHeaderOrQuery(req, "X-Plex-Container-Size", out var s) && s > 0)
			size = s;
		if (TryGetIntHeaderOrQuery(req, "X-Plex-Container-Start", out var st))
			start = st < 1 ? 1 : st;
		return new PlexPaging(size, start);
	}

	static bool TryGetIntHeaderOrQuery(HttpRequest req, string name, out int value)
	{
		value = 0;
		if (req.Headers.TryGetValue(name, out var hv) && int.TryParse(hv.ToString(), out var h))
		{
			value = h;
			return true;
		}
		if (req.Query.TryGetValue(name, out var qv) && int.TryParse(qv.ToString(), out var q))
		{
			value = q;
			return true;
		}
		return false;
	}

	static object WrapPagedMetadata(IReadOnlyList<object> all, PlexPaging paging)
	{
		var startIndex = paging.Start - 1;
		if (startIndex < 0)
			startIndex = 0;
		var page = new List<object>(Math.Min(paging.Size, Math.Max(0, all.Count - startIndex)));
		for (var i = startIndex; i < all.Count && page.Count < paging.Size; i++)
			page.Add(all[i]!);

		return new
		{
			MediaContainer = new
			{
				offset = startIndex,
				totalSize = all.Count,
				identifier = PlexConstants.ProviderIdentifier,
				size = page.Count,
				Metadata = page
			}
		};
	}

	static async Task<Dictionary<int, string>> LoadPrimaryVideoFilePathsByVideoIdsAsync(TubeArrDbContext db, IReadOnlyCollection<int> videoIds, CancellationToken ct)
	{
		var d = new Dictionary<int, string>();
		if (videoIds.Count == 0)
			return d;
		var rows = await db.VideoFiles.AsNoTracking()
			.Where(vf => videoIds.Contains(vf.VideoId))
			.OrderBy(vf => vf.Id)
			.Select(vf => new { vf.VideoId, vf.Path })
			.ToListAsync(ct);
		foreach (var r in rows)
		{
			if (!d.ContainsKey(r.VideoId) && !string.IsNullOrWhiteSpace(r.Path))
				d[r.VideoId] = r.Path!;
		}

		return d;
	}

	static async Task<Dictionary<int, string>> LoadEpisodeNfoTitlesByVideoIdsAsync(
		IReadOnlyDictionary<int, string> pathsByVideo,
		CancellationToken ct)
	{
		var result = new Dictionary<int, string>();
		foreach (var kv in pathsByVideo)
		{
			var title = await EpisodeNfoReader.TryReadEpisodeTitleAsync(kv.Value, ct).ConfigureAwait(false);
			if (!string.IsNullOrWhiteSpace(title))
				result[kv.Key] = title!;
		}

		return result;
	}

	static async Task<List<object>> LoadAllEpisodesForShowAsync(TubeArrDbContext db, ChannelEntity channel, HttpRequest httpRequest, CancellationToken ct)
	{
		var channelId = channel.Id;
		var videoIds = await db.Videos.AsNoTracking()
			.Where(v => v.ChannelId == channelId)
			.Select(v => v.Id)
			.ToListAsync(ct);

		await StableTvNumbering.EnsureVideoPlexIndicesAsync(db, channelId, videoIds, ct);

		var videos = await db.Videos.AsNoTracking()
			.Where(v => v.ChannelId == channelId)
			.OrderBy(v => v.PlexSeasonIndex ?? int.MaxValue)
			.ThenBy(v => v.PlexEpisodeIndex ?? int.MaxValue)
			.ThenBy(v => v.Id)
			.ToListAsync(ct);

		var playlistIds = videos
			.Where(v => v.PlexPrimaryPlaylistId is > 0)
			.Select(v => v.PlexPrimaryPlaylistId!.Value)
			.Distinct()
			.ToList();

		var playlists = playlistIds.Count == 0
			? new Dictionary<int, PlaylistEntity>()
			: await db.Playlists.AsNoTracking()
				.Where(p => playlistIds.Contains(p.Id))
				.ToDictionaryAsync(p => p.Id, ct);

		var pathsByVideo = await LoadPrimaryVideoFilePathsByVideoIdsAsync(db, videos.Select(v => v.Id).ToList(), ct);
		var nfoTitles = await LoadEpisodeNfoTitlesByVideoIdsAsync(pathsByVideo, ct);
		var (grandThumb, grandArt) = PlexArtworkResolver.GetShowArtwork(channel);
		var seasonPosterCache = new Dictionary<(int playlistId, int seasonIdx), string?>();

		var list = new List<object>(videos.Count);
		foreach (var v in videos)
		{
			PlaylistEntity? playlist = null;
			if (v.PlexPrimaryPlaylistId is > 0 && playlists.TryGetValue(v.PlexPrimaryPlaylistId.Value, out var pl))
				playlist = pl;
			var seasonIndex = v.PlexSeasonIndex.GetValueOrDefault(StableTvNumbering.ChannelOnlySeasonIndex);
			var epIndex = v.PlexEpisodeIndex.GetValueOrDefault(1);
			pathsByVideo.TryGetValue(v.Id, out var filePath);
			nfoTitles.TryGetValue(v.Id, out var nfoTitle);
			string? parentThumb = null;
			if (playlist is not null)
			{
				var key = (playlist.Id, seasonIndex);
				if (!seasonPosterCache.TryGetValue(key, out parentThumb))
				{
					parentThumb = PlexArtworkResolver.GetSeasonPoster(playlist);
					seasonPosterCache[key] = parentThumb;
				}
			}

			var epThumb = PlexArtworkResolver.ResolveEpisodeThumbForPlex(httpRequest, v, filePath);
			list.Add(PlexPayloadBuilder.BuildEpisodeMetadata(channel, playlist, v, seasonIndex, epIndex, filePath, nfoTitle, epThumb, parentThumb, grandThumb, grandArt));
		}

		return list;
	}

	static async Task<object?> BuildShowAsync(TubeArrDbContext db, string youtubeChannelId, bool includeChildren, CancellationToken ct)
	{
		var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.YoutubeChannelId == youtubeChannelId, ct);
		if (channel is null)
			return null;

		var (thumb, art) = PlexArtworkResolver.GetShowArtwork(channel);
		IReadOnlyList<object>? children = null;
		if (includeChildren)
			children = await LoadSeasonChildrenAsync(db, channel, ct);

		return PlexPayloadBuilder.BuildShowMetadata(channel, includeChildren, children, thumb, art);
	}

	static async Task<IReadOnlyList<object>> LoadSeasonChildrenAsync(TubeArrDbContext db, ChannelEntity channel, CancellationToken ct)
	{
		var playlists = await db.Playlists.AsNoTracking()
			.Where(p => p.ChannelId == channel.Id)
			.OrderBy(p => p.SeasonIndex ?? int.MaxValue)
			.ThenBy(p => p.Id)
			.ToListAsync(ct);

		foreach (var p in playlists.Where(p => !p.SeasonIndex.HasValue || p.SeasonIndex.Value <= 0))
		{
			await StableTvNumbering.EnsurePlaylistSeasonIndexAsync(db, p.Id, ct);
		}

		playlists = await db.Playlists.AsNoTracking()
			.Where(p => p.ChannelId == channel.Id)
			.OrderBy(p => p.SeasonIndex ?? int.MaxValue)
			.ThenBy(p => p.Id)
			.ToListAsync(ct);

		var (parentThumb, parentArt) = PlexArtworkResolver.GetShowArtwork(channel);
		var list = new List<object>();

		list.Add(BuildChannelOnlySeasonStub(channel, parentThumb, parentArt));

		foreach (var p in playlists)
		{
			var seasonIndex = p.SeasonIndex.GetValueOrDefault(StableTvNumbering.FirstPlaylistSeasonIndex);
			var seasonThumb = PlexArtworkResolver.GetSeasonPoster(p);
			list.Add(PlexPayloadBuilder.BuildSeasonMetadata(channel, p, seasonIndex, includeChildren: false, children: null, seasonThumb, parentThumb, parentArt));
		}

		return list;
	}

	static async Task<object?> BuildSeasonAsync(TubeArrDbContext db, string youtubePlaylistId, bool includeChildren, HttpRequest httpRequest, CancellationToken ct)
	{
		var playlist = await db.Playlists.FirstOrDefaultAsync(p => p.YoutubePlaylistId == youtubePlaylistId, ct);
		if (playlist is null)
			return null;

		var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == playlist.ChannelId, ct);
		if (channel is null)
			return null;

		var seasonIndex = playlist.SeasonIndex.HasValue && playlist.SeasonIndex.Value > 0
			? playlist.SeasonIndex.Value
			: await StableTvNumbering.EnsurePlaylistSeasonIndexAsync(db, playlist.Id, ct);

		var (showThumb, showArt) = PlexArtworkResolver.GetShowArtwork(channel);
		var seasonThumb = PlexArtworkResolver.GetSeasonPoster(playlist);
		IReadOnlyList<object>? children = null;
		if (includeChildren)
			children = await LoadEpisodeChildrenForPlaylistAsync(db, channel, playlist, seasonIndex, httpRequest, ct);

		return PlexPayloadBuilder.BuildSeasonMetadata(channel, playlist, seasonIndex, includeChildren, children, seasonThumb, showThumb, showArt);
	}

	static async Task<IReadOnlyList<object>> LoadEpisodeChildrenForPlaylistAsync(
		TubeArrDbContext db,
		ChannelEntity channel,
		PlaylistEntity playlist,
		int seasonIndex,
		HttpRequest httpRequest,
		CancellationToken ct)
	{
		var videoIds = await db.PlaylistVideos.AsNoTracking()
			.Where(pv => pv.PlaylistId == playlist.Id)
			.Select(pv => pv.VideoId)
			.Distinct()
			.ToListAsync(ct);

		await StableTvNumbering.EnsureVideoPlexIndicesAsync(db, channel.Id, videoIds, ct);

		var videos = await db.Videos.AsNoTracking()
			.Where(v => v.ChannelId == channel.Id && v.PlexSeasonIndex == seasonIndex && videoIds.Contains(v.Id))
			.OrderBy(v => v.PlexEpisodeIndex ?? int.MaxValue)
			.ThenBy(v => v.Id)
			.ToListAsync(ct);

		var pathsByVideo = await LoadPrimaryVideoFilePathsByVideoIdsAsync(db, videos.Select(v => v.Id).ToList(), ct);
		var nfoTitles = await LoadEpisodeNfoTitlesByVideoIdsAsync(pathsByVideo, ct);
		var (grandThumb, grandArt) = PlexArtworkResolver.GetShowArtwork(channel);
		var seasonPoster = PlexArtworkResolver.GetSeasonPoster(playlist);

		var list = new List<object>(videos.Count);
		foreach (var v in videos)
		{
			var ep = v.PlexEpisodeIndex.GetValueOrDefault(1);
			pathsByVideo.TryGetValue(v.Id, out var filePath);
			nfoTitles.TryGetValue(v.Id, out var nfoTitle);
			var epThumb = PlexArtworkResolver.ResolveEpisodeThumbForPlex(httpRequest, v, filePath);
			list.Add(PlexPayloadBuilder.BuildEpisodeMetadata(channel, playlist, v, seasonIndex, ep, filePath, nfoTitle, epThumb, seasonPoster, grandThumb, grandArt));
		}

		return list;
	}

	static async Task<IReadOnlyList<object>> LoadChannelOnlyEpisodeChildrenAsync(
		TubeArrDbContext db,
		ChannelEntity channel,
		bool includeChildren,
		HttpRequest httpRequest,
		CancellationToken ct)
	{
		if (!includeChildren)
			return Array.Empty<object>();

		var channelId = channel.Id;
		var videoIds = await db.Videos.AsNoTracking()
			.Where(v => v.ChannelId == channelId)
			.Select(v => v.Id)
			.ToListAsync(ct);

		await StableTvNumbering.EnsureVideoPlexIndicesAsync(db, channelId, videoIds, ct);

		var videos = await db.Videos.AsNoTracking()
			.Where(v => v.ChannelId == channelId && v.PlexSeasonIndex == StableTvNumbering.ChannelOnlySeasonIndex)
			.OrderBy(v => v.PlexEpisodeIndex ?? int.MaxValue)
			.ThenBy(v => v.Id)
			.ToListAsync(ct);

		var pathsByVideo = await LoadPrimaryVideoFilePathsByVideoIdsAsync(db, videos.Select(v => v.Id).ToList(), ct);
		var nfoTitles = await LoadEpisodeNfoTitlesByVideoIdsAsync(pathsByVideo, ct);
		var (grandThumb, grandArt) = PlexArtworkResolver.GetShowArtwork(channel);

		var list = new List<object>(videos.Count);
		foreach (var v in videos)
		{
			var ep = v.PlexEpisodeIndex.GetValueOrDefault(1);
			pathsByVideo.TryGetValue(v.Id, out var filePath);
			nfoTitles.TryGetValue(v.Id, out var nfoTitle);
			var epThumb = PlexArtworkResolver.ResolveEpisodeThumbForPlex(httpRequest, v, filePath);
			list.Add(PlexPayloadBuilder.BuildEpisodeMetadata(channel, playlist: null, v, StableTvNumbering.ChannelOnlySeasonIndex, ep, filePath, nfoTitle, epThumb, null, grandThumb, grandArt));
		}

		return list;
	}

	static async Task<object?> BuildEpisodeAsync(TubeArrDbContext db, string youtubeVideoId, bool includeChildren, HttpRequest httpRequest, CancellationToken ct)
	{
		var video = await db.Videos.FirstOrDefaultAsync(v => v.YoutubeVideoId == youtubeVideoId, ct);
		if (video is null)
			return null;

		await StableTvNumbering.EnsureVideoPlexIndicesAsync(db, video.ChannelId, [video.Id], ct);

		video = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == video.Id, ct);
		if (video is null)
			return null;

		var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == video.ChannelId, ct);
		if (channel is null)
			return null;

		var seasonIndex = video.PlexSeasonIndex.GetValueOrDefault(StableTvNumbering.ChannelOnlySeasonIndex);
		var episodeIndex = video.PlexEpisodeIndex.GetValueOrDefault(1);

		PlaylistEntity? playlist = null;
		if (video.PlexPrimaryPlaylistId.HasValue && video.PlexPrimaryPlaylistId.Value > 0)
		{
			playlist = await db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.Id == video.PlexPrimaryPlaylistId.Value, ct);
		}

		var pathsByVideo = await LoadPrimaryVideoFilePathsByVideoIdsAsync(db, [video.Id], ct);
		pathsByVideo.TryGetValue(video.Id, out var filePath);
		var nfoTitles = await LoadEpisodeNfoTitlesByVideoIdsAsync(pathsByVideo, ct);
		nfoTitles.TryGetValue(video.Id, out var nfoTitle);
		var (grandThumb, grandArt) = PlexArtworkResolver.GetShowArtwork(channel);
		string? parentThumb = null;
		if (playlist is not null)
			parentThumb = PlexArtworkResolver.GetSeasonPoster(playlist);
		var epThumb = PlexArtworkResolver.ResolveEpisodeThumbForPlex(httpRequest, video, filePath);

		return PlexPayloadBuilder.BuildEpisodeMetadata(channel, playlist, video, seasonIndex, episodeIndex, filePath, nfoTitle, epThumb, parentThumb, grandThumb, grandArt);
	}

	static async Task<List<object>> MatchShowAsync(TubeArrDbContext db, ILogger logger, string title, string guid, string path, CancellationToken ct)
	{
		var channel = await ResolveChannelForShowMatchAsync(db, logger, title, guid, path, ct);
		if (channel is null)
		{
			logger.LogWarning("Plex show match failed: no channel found for path={Path}, title={Title}, guid={Guid}", path, title, guid);
			return new List<object>();
		}
		return [BuildShowMatchStub(channel)];
	}

	static async Task<ChannelEntity?> TryResolveChannelFromFilenamePathAsync(TubeArrDbContext db, ILogger logger, string path, CancellationToken ct)
	{
		if (PlexFilenameParser.TryParseYoutubeChannelIdFromPath(path, out var channelIdFromPath))
		{
			var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.YoutubeChannelId == channelIdFromPath, ct);
			if (channel is not null)
			{
				logger.LogInformation("Plex show match: channelId from filename/path [{ChannelId}]", channelIdFromPath);
				return channel;
			}
		}

		if (!string.IsNullOrWhiteSpace(path))
		{
			var candidates = await db.Channels.AsNoTracking()
				.Where(c => c.Path != null && c.Path != "")
				.OrderBy(c => c.Id)
				.ToListAsync(ct);
			var channel = candidates.FirstOrDefault(c => path.StartsWith(c.Path!, StringComparison.OrdinalIgnoreCase));
			if (channel is not null)
			{
				logger.LogInformation("Plex show match: matched by configured channel path (filename) channelId={ChannelId}", channel.YoutubeChannelId);
				return channel;
			}
		}

		if (PlexFilenameParser.TryGetShowFolderNameFromPath(path, out var folderFromPath))
		{
			var folder = folderFromPath.Trim();
			if (folder.Length > 0)
			{
				var folderLower = folder.ToLowerInvariant();
				var channel = await db.Channels.AsNoTracking()
					.Where(c => c.Title != null && c.Title.ToLower() == folderLower)
					.OrderBy(c => c.Id)
					.FirstOrDefaultAsync(ct);
				if (channel is null)
				{
					var slugFromFolder = SlugHelper.Slugify(folder);
					if (slugFromFolder.Length > 0)
						channel = await db.Channels.AsNoTracking()
							.Where(c => c.TitleSlug != null && c.TitleSlug.ToLower() == slugFromFolder)
							.OrderBy(c => c.Id)
							.FirstOrDefaultAsync(ct);
				}
				if (channel is not null)
				{
					logger.LogInformation("Plex show match: folder from filename path folder={Folder} channelId={ChannelId}", folder, channel.YoutubeChannelId);
					return channel;
				}
			}
		}

		return null;
	}

	static async Task<ChannelEntity?> ResolveChannelFromGuidAndTitleAsync(TubeArrDbContext db, ILogger logger, string title, string guid, CancellationToken ct)
	{
		if (!string.IsNullOrWhiteSpace(guid) && guid.StartsWith(PlexConstants.Scheme + "://show/", StringComparison.OrdinalIgnoreCase))
		{
			var rk = guid.Substring((PlexConstants.Scheme + "://show/").Length);
			if (PlexIdentifier.TryParseRatingKey(rk, out var kind, out var yt) && kind == PlexIdentifier.PlexItemKind.Show)
			{
				var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.YoutubeChannelId == yt, ct);
				if (channel is not null)
					return channel;
			}
		}

		if (!string.IsNullOrWhiteSpace(title))
		{
			var titleNorm = title.Trim();
			var titleLower = titleNorm.ToLowerInvariant();
			var channel = await db.Channels.AsNoTracking()
				.Where(c => c.Title != null && c.Title.ToLower() == titleLower)
				.OrderBy(c => c.Id)
				.FirstOrDefaultAsync(ct);
			if (channel is not null)
			{
				logger.LogInformation("Plex show match: title match (case-insensitive) channelId={ChannelId}", channel.YoutubeChannelId);
				return channel;
			}
		}

		return null;
	}

	static async Task<ChannelEntity?> ResolveChannelForShowMatchAsync(TubeArrDbContext db, ILogger logger, string title, string guid, string path, CancellationToken ct)
	{
		var fromPath = await TryResolveChannelFromFilenamePathAsync(db, logger, path, ct);
		if (fromPath is not null)
			return fromPath;

		return await ResolveChannelFromGuidAndTitleAsync(db, logger, title, guid, ct);
	}

	static async Task<List<object>> MatchSeasonAsync(TubeArrDbContext db, ILogger logger, JsonElement root, IReadOnlyDictionary<string, JsonElement> fields, string guid, string path, bool manual, CancellationToken ct)
	{
		var parentGuid = CoalesceStringFromPropertyMap(fields, "parentGuid") ?? "";
		var parentRatingKey = CoalesceStringFromPropertyMap(fields, "parentRatingKey") ?? "";
		var index = GetIntFromPropertyMap(fields, "index");

		ChannelEntity? channel = await TryResolveChannelFromFilenamePathAsync(db, logger, path, ct);
		if (channel is null && !string.IsNullOrWhiteSpace(parentRatingKey) && PlexIdentifier.TryParseRatingKey(parentRatingKey, out var kind, out var yt) && kind == PlexIdentifier.PlexItemKind.Show)
			channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.YoutubeChannelId == yt, ct);
		if (channel is null && !string.IsNullOrWhiteSpace(parentGuid) && parentGuid.StartsWith(PlexConstants.Scheme + "://show/", StringComparison.OrdinalIgnoreCase))
		{
			var rk = parentGuid.Substring((PlexConstants.Scheme + "://show/").Length);
			if (PlexIdentifier.TryParseRatingKey(rk, out var kind2, out var yt2) && kind2 == PlexIdentifier.PlexItemKind.Show)
				channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.YoutubeChannelId == yt2, ct);
		}

		if (channel is null)
		{
			var parentTitle = CoalesceStringFromPropertyMap(fields, "parentTitle");
			channel = await ResolveChannelFromGuidAndTitleAsync(db, logger, parentTitle, parentGuid, ct);
		}

		if (channel is null)
		{
			logger.LogWarning("Plex season match failed: no channel found for parentRatingKey={ParentRatingKey}, parentGuid={ParentGuid}, path={Path}", parentRatingKey, parentGuid, path);
			return new List<object>();
		}

		if (index == StableTvNumbering.ChannelOnlySeasonIndex)
		{
			logger.LogInformation("Plex season match: channel-only season for channelId={ChannelId}", channel.YoutubeChannelId);
			return [BuildChannelOnlySeasonMatchStub(channel)];
		}

		if (index > 0)
		{
			var playlist = await db.Playlists.FirstOrDefaultAsync(p => p.ChannelId == channel.Id && p.SeasonIndex == index, ct);
			if (playlist is null)
			{
				var playlists = await db.Playlists.Where(p => p.ChannelId == channel.Id && (!p.SeasonIndex.HasValue || p.SeasonIndex.Value <= 0)).ToListAsync(ct);
				foreach (var p in playlists)
					await StableTvNumbering.EnsurePlaylistSeasonIndexAsync(db, p.Id, ct);
				playlist = await db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.ChannelId == channel.Id && p.SeasonIndex == index, ct);
			}

			if (playlist is not null)
			{
				logger.LogInformation("Plex season match: channelId={ChannelId} playlistId={PlaylistId} seasonIndex={SeasonIndex}", channel.YoutubeChannelId, playlist.YoutubePlaylistId, index);
				return [BuildSeasonMatchStub(channel, playlist, index)];
			}
		}

		// Exact title match as a fallback within channel.
		var title = CoalesceStringFromPropertyMap(fields, "title", "name");
		if (!string.IsNullOrWhiteSpace(title))
		{
			var tl = title.Trim().ToLowerInvariant();
			var playlist = await db.Playlists.AsNoTracking()
				.Where(p => p.ChannelId == channel.Id && p.Title != null && p.Title.ToLower() == tl)
				.OrderBy(p => p.Id)
				.FirstOrDefaultAsync(ct);
			if (playlist is not null)
			{
				var seasonIndex = playlist.SeasonIndex ?? await StableTvNumbering.EnsurePlaylistSeasonIndexAsync(db, playlist.Id, ct);
				logger.LogInformation("Plex season match: exact title match playlistId={PlaylistId} seasonIndex={SeasonIndex}", playlist.YoutubePlaylistId, seasonIndex);
				return [BuildSeasonMatchStub(channel, playlist, seasonIndex)];
			}
		}

		logger.LogWarning("Plex season match exhausted all strategies: channelId={ChannelId}, parentRatingKey={ParentRatingKey}, parentGuid={ParentGuid}, index={Index}, title={Title}",
			channel.YoutubeChannelId, parentRatingKey, parentGuid, index, title);
		return new List<object>();
	}

	static async Task<List<object>> MatchEpisodeAsync(TubeArrDbContext db, ILogger logger, JsonElement root, IReadOnlyDictionary<string, JsonElement> fields, string guid, string path, bool manual, HttpRequest httpRequest, CancellationToken ct)
	{
		if (PlexFilenameParser.TryParseYoutubeVideoIdFromPath(path, out var ytVideoId))
		{
			var video = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.YoutubeVideoId == ytVideoId, ct);
			if (video is not null)
			{
				logger.LogInformation("Plex episode match: videoId from filename [{YoutubeVideoId}]", ytVideoId);
				var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == video.ChannelId, ct);
				if (channel is null)
					return new List<object>();
				await StableTvNumbering.EnsureVideoPlexIndicesAsync(db, channel.Id, [video.Id], ct);
				video = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == video.Id, ct);
				if (video is null)
					return new List<object>();
				return [await BuildEpisodeMatchStubAsync(db, httpRequest, channel, video, path, ct)];
			}
			logger.LogDebug("Plex episode match: parsed videoId={VideoId} from path but no matching video in DB", ytVideoId);
		}
		else if (!string.IsNullOrWhiteSpace(path))
		{
			logger.LogDebug("Plex episode match: could not extract YouTube video ID from path={Path}", path);
		}

		if (!string.IsNullOrWhiteSpace(path))
		{
			var vf = await db.VideoFiles.AsNoTracking()
				.OrderBy(x => x.Id)
				.FirstOrDefaultAsync(x => x.Path == path, ct);
			if (vf is null)
			{
				var fileName = Path.GetFileName(path.TrimEnd('/', '\\'));
				if (!string.IsNullOrEmpty(fileName))
				{
					vf = await db.VideoFiles.AsNoTracking()
						.OrderBy(x => x.Id)
						.FirstOrDefaultAsync(x => x.Path != null && x.Path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase), ct);
				}
			}

			if (vf is not null)
			{
				var video = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vf.VideoId, ct);
				if (video is not null)
				{
					logger.LogInformation("Plex episode match: VideoFiles path/filename videoId={VideoId}", video.YoutubeVideoId);
					var channel = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.Id == video.ChannelId, ct);
					if (channel is null)
						return new List<object>();
					await StableTvNumbering.EnsureVideoPlexIndicesAsync(db, channel.Id, [video.Id], ct);
					video = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == video.Id, ct);
					if (video is null)
						return new List<object>();
					return [await BuildEpisodeMatchStubAsync(db, httpRequest, channel, video, path, ct)];
				}
				logger.LogDebug("Plex episode match: VideoFile found but no matching video entity for videoFileId={VideoFileId}, path={Path}", vf.Id, path);
			}
			else
			{
				logger.LogDebug("Plex episode match: no VideoFile matched path={Path}", path);
			}
		}

		var parentIndex = GetIntFromPropertyMap(fields, "parentIndex");
		var index = GetIntFromPropertyMap(fields, "index");
		var grandparentRatingKey = CoalesceStringFromPropertyMap(fields, "grandparentRatingKey") ?? "";
		var grandparentGuid = CoalesceStringFromPropertyMap(fields, "grandparentGuid") ?? "";
		var grandparentTitle = CoalesceStringFromPropertyMap(fields, "grandparentTitle");

		ChannelEntity? channelForEpisode = await TryResolveChannelFromFilenamePathAsync(db, logger, path, ct);
		if (channelForEpisode is null &&
		    !string.IsNullOrWhiteSpace(grandparentRatingKey) &&
		    PlexIdentifier.TryParseRatingKey(grandparentRatingKey, out var gpKind, out var gpYt) &&
		    gpKind == PlexIdentifier.PlexItemKind.Show)
		{
			channelForEpisode = await db.Channels.AsNoTracking().FirstOrDefaultAsync(c => c.YoutubeChannelId == gpYt, ct);
		}

		if (channelForEpisode is null)
			channelForEpisode = await ResolveChannelFromGuidAndTitleAsync(db, logger, grandparentTitle, grandparentGuid, ct);

		if (channelForEpisode is null)
		{
			logger.LogWarning("Plex episode match failed: no channel resolved for path={Path}, grandparentRatingKey={GrandparentRatingKey}, grandparentGuid={GrandparentGuid}, grandparentTitle={GrandparentTitle}",
				path, grandparentRatingKey, grandparentGuid, grandparentTitle);
			return new List<object>();
		}

		if (parentIndex > 0 && index > 0)
		{
			var video = await db.Videos.AsNoTracking()
				.Where(v => v.ChannelId == channelForEpisode.Id && v.PlexSeasonIndex == parentIndex && v.PlexEpisodeIndex == index)
				.OrderBy(v => v.Id)
				.FirstOrDefaultAsync(ct);

			if (video is null)
			{
				var allIds = await db.Videos.AsNoTracking().Where(v => v.ChannelId == channelForEpisode.Id).Select(v => v.Id).ToListAsync(ct);
				await StableTvNumbering.EnsureVideoPlexIndicesAsync(db, channelForEpisode.Id, allIds, ct);
				video = await db.Videos.AsNoTracking()
					.Where(v => v.ChannelId == channelForEpisode.Id && v.PlexSeasonIndex == parentIndex && v.PlexEpisodeIndex == index)
					.OrderBy(v => v.Id)
					.FirstOrDefaultAsync(ct);
			}

			if (video is not null)
			{
				logger.LogInformation("Plex episode match: by numbering channelId={ChannelId} s{Season}e{Episode} -> {YoutubeVideoId}", channelForEpisode.YoutubeChannelId, parentIndex, index, video.YoutubeVideoId);
				return [await BuildEpisodeMatchStubAsync(db, httpRequest, channelForEpisode, video, path, ct)];
			}
			logger.LogDebug("Plex episode match: no video at s{Season}e{Episode} for channelId={ChannelId}", parentIndex, index, channelForEpisode.YoutubeChannelId);
		}

		if (parentIndex <= 0 || index <= 0)
		{
			var dateStr = CoalesceStringFromPropertyMap(fields, "date", "originallyAvailableAt");
			if (TryParsePlexMatchDate(dateStr, out var dayUtc))
			{
				var video = await FindSingleVideoByCalendarDayUtcAsync(db, channelForEpisode.Id, dayUtc, ct);
				if (video is not null)
				{
					logger.LogInformation("Plex episode match: by date channelId={ChannelId} day={Day} -> {YoutubeVideoId}", channelForEpisode.YoutubeChannelId, dayUtc.UtcDateTime.ToString("yyyy-MM-dd"), video.YoutubeVideoId);
					await StableTvNumbering.EnsureVideoPlexIndicesAsync(db, channelForEpisode.Id, [video.Id], ct);
					video = await db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == video.Id, ct);
					if (video is null)
						return new List<object>();
					return [await BuildEpisodeMatchStubAsync(db, httpRequest, channelForEpisode, video, path, ct)];
				}
				logger.LogDebug("Plex episode match: date fallback found no unique video for channelId={ChannelId}, date={Date}", channelForEpisode.YoutubeChannelId, dayUtc.UtcDateTime.ToString("yyyy-MM-dd"));
			}
			else
			{
				logger.LogDebug("Plex episode match: no valid date for date-based fallback, dateStr={DateStr}", dateStr);
			}
		}

		logger.LogWarning("Plex episode match exhausted all strategies: channelId={ChannelId}, path={Path}, guid={Guid}, parentIndex={ParentIndex}, index={Index}",
			channelForEpisode.YoutubeChannelId, path, guid, parentIndex, index);
		return new List<object>();
	}

	static DateTimeOffset EffectiveEpisodeMatchDate(VideoEntity v) =>
		v.AirDateUtc != DateTimeOffset.UnixEpoch ? v.AirDateUtc : v.UploadDateUtc;

	static async Task<VideoEntity?> FindSingleVideoByCalendarDayUtcAsync(TubeArrDbContext db, int channelId, DateTimeOffset dayUtc, CancellationToken ct)
	{
		var day = dayUtc.UtcDateTime.Date;
		var start = new DateTimeOffset(day, TimeSpan.Zero);
		var end = start.AddDays(1);

		var candidates = await db.Videos.AsNoTracking()
			.Where(v => v.ChannelId == channelId)
			.Where(v =>
				(v.AirDateUtc != DateTimeOffset.UnixEpoch && v.AirDateUtc >= start && v.AirDateUtc < end) ||
				(v.UploadDateUtc >= start && v.UploadDateUtc < end))
			.ToListAsync(ct);

		var targetDay = day;
		var matches = candidates
			.Where(v => EffectiveEpisodeMatchDate(v).UtcDateTime.Date == targetDay)
			.ToList();

		return matches.Count == 1 ? matches[0] : null;
	}

	static bool TryParsePlexMatchDate(string? s, out DateTimeOffset dayUtc)
	{
		dayUtc = default;
		if (string.IsNullOrWhiteSpace(s))
			return false;
		s = s.Trim();
		if (DateTimeOffset.TryParse(s, out var dto))
		{
			dayUtc = dto;
			return true;
		}
		if (DateTime.TryParse(s, out var dt))
		{
			dayUtc = new DateTimeOffset(DateTime.SpecifyKind(dt.Date, DateTimeKind.Utc));
			return true;
		}
		return false;
	}

	static object BuildShowMatchStub(ChannelEntity channel)
	{
		var ratingKey = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Show, channel.YoutubeChannelId);
		var guid = PlexIdentifier.BuildGuid(PlexIdentifier.PlexItemKind.Show, ratingKey);
		var (thumb, art) = PlexArtworkResolver.GetShowArtwork(channel);
		var meta = new Dictionary<string, object?>
		{
			["type"] = "show",
			["ratingKey"] = ratingKey,
			["guid"] = guid,
			["key"] = PlexKeys.LibraryMetadataChildren(ratingKey),
			["title"] = PlexDisplayTitles.Channel(channel)
		};
		if (!string.IsNullOrWhiteSpace(thumb))
			meta["thumb"] = thumb;
		if (!string.IsNullOrWhiteSpace(art))
			meta["art"] = art;
		return meta;
	}

	static object BuildSeasonMatchStub(ChannelEntity channel, PlaylistEntity playlist, int seasonIndex)
	{
		var ratingKey = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Season, playlist.YoutubePlaylistId);
		var guid = PlexIdentifier.BuildGuid(PlexIdentifier.PlexItemKind.Season, ratingKey);
		var (parentThumb, parentArt) = PlexArtworkResolver.GetShowArtwork(channel);
		var seasonThumb = PlexArtworkResolver.GetSeasonPoster(playlist);
		var meta = new Dictionary<string, object?>
		{
			["type"] = "season",
			["ratingKey"] = ratingKey,
			["guid"] = guid,
			["key"] = PlexKeys.LibraryMetadataChildren(ratingKey),
			["title"] = PlexDisplayTitles.Season(playlist, seasonIndex, channelOnlySeason: false),
			["index"] = seasonIndex,
			["parentRatingKey"] = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Show, channel.YoutubeChannelId),
			["parentTitle"] = PlexDisplayTitles.Channel(channel)
		};
		if (!string.IsNullOrWhiteSpace(seasonThumb))
			meta["thumb"] = seasonThumb;
		if (!string.IsNullOrWhiteSpace(parentThumb))
			meta["parentThumb"] = parentThumb;
		if (!string.IsNullOrWhiteSpace(parentArt))
			meta["parentArt"] = parentArt;
		return meta;
	}

	static Dictionary<string, object?> BuildChannelOnlySeasonStub(ChannelEntity channel, string? parentThumbUrl, string? parentArtUrl)
	{
		var rk = BuildChannelOnlySeasonRatingKey(channel.YoutubeChannelId);
		var meta = new Dictionary<string, object?>
		{
			["type"] = "season",
			["ratingKey"] = rk,
			["guid"] = PlexConstants.Scheme + "://season/" + rk,
			["key"] = PlexKeys.LibraryMetadataChildren(rk),
			["title"] = "Channel Uploads",
			["index"] = StableTvNumbering.ChannelOnlySeasonIndex,
			["parentRatingKey"] = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Show, channel.YoutubeChannelId),
			["parentTitle"] = PlexDisplayTitles.Channel(channel)
		};
		if (!string.IsNullOrWhiteSpace(parentThumbUrl))
			meta["parentThumb"] = parentThumbUrl;
		if (!string.IsNullOrWhiteSpace(parentArtUrl))
			meta["parentArt"] = parentArtUrl;
		return meta;
	}

	static object BuildChannelOnlySeasonMetadata(ChannelEntity channel, bool includeChildren, IReadOnlyList<object>? children)
	{
		var showRatingKey = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Show, channel.YoutubeChannelId);
		var showGuid = PlexIdentifier.BuildGuid(PlexIdentifier.PlexItemKind.Show, showRatingKey);
		var showKey = PlexKeys.LibraryMetadata(showRatingKey);

		var seasonRatingKey = BuildChannelOnlySeasonRatingKey(channel.YoutubeChannelId);
		var seasonGuid = PlexConstants.Scheme + "://season/" + seasonRatingKey;
		var seasonKey = PlexKeys.LibraryMetadataChildren(seasonRatingKey);
		var aired = channel.Added.ToString("yyyy-MM-dd");

		var channelTitle = PlexDisplayTitles.Channel(channel);
		var (parentThumb, parentArt) = PlexArtworkResolver.GetShowArtwork(channel);
		var meta = new Dictionary<string, object?>
		{
			["type"] = "season",
			["ratingKey"] = seasonRatingKey,
			["guid"] = seasonGuid,
			["key"] = seasonKey,
			["title"] = "Channel Uploads",
			["titleSort"] = "Channel Uploads",
			["summary"] = "",
			["index"] = StableTvNumbering.ChannelOnlySeasonIndex,
			["originallyAvailableAt"] = aired,
			["year"] = channel.Added.Year,
			["parentType"] = "show",
			["parentRatingKey"] = showRatingKey,
			["parentGuid"] = showGuid,
			["parentKey"] = showKey,
			["parentTitle"] = channelTitle
		};
		if (!string.IsNullOrWhiteSpace(parentThumb))
			meta["parentThumb"] = parentThumb;
		if (!string.IsNullOrWhiteSpace(parentArt))
			meta["parentArt"] = parentArt;

		if (includeChildren && children is not null)
		{
			meta["Children"] = new
			{
				size = children.Count,
				Metadata = children
			};
		}

		return meta;
	}

	static object BuildChannelOnlySeasonMatchStub(ChannelEntity channel)
	{
		var (parentThumb, parentArt) = PlexArtworkResolver.GetShowArtwork(channel);
		return BuildChannelOnlySeasonStub(channel, parentThumb, parentArt);
	}

	static async Task<object> BuildEpisodeMatchStubAsync(TubeArrDbContext db, HttpRequest httpRequest, ChannelEntity channel, VideoEntity video, string? pathHint, CancellationToken ct)
	{
		var ratingKey = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Episode, video.YoutubeVideoId);
		var guid = PlexIdentifier.BuildGuid(PlexIdentifier.PlexItemKind.Episode, ratingKey);
		var epIndex = video.PlexEpisodeIndex ?? 1;
		var nfoTitle = await EpisodeNfoReader.TryReadEpisodeTitleAsync(pathHint, ct).ConfigureAwait(false);
		string? mediaPath = null;
		if (!string.IsNullOrWhiteSpace(pathHint) && File.Exists(pathHint))
			mediaPath = pathHint;
		else
		{
			var paths = await LoadPrimaryVideoFilePathsByVideoIdsAsync(db, [video.Id], ct);
			paths.TryGetValue(video.Id, out mediaPath);
		}

		var epThumb = PlexArtworkResolver.ResolveEpisodeThumbForPlex(httpRequest, video, mediaPath);
		var meta = new Dictionary<string, object?>
		{
			["type"] = "episode",
			["ratingKey"] = ratingKey,
			["guid"] = guid,
			["key"] = PlexKeys.LibraryMetadata(ratingKey),
			["title"] = PlexDisplayTitles.Episode(video, epIndex, pathHint, nfoTitle)
		};
		if (!string.IsNullOrWhiteSpace(epThumb))
			meta["thumb"] = epThumb;
		return meta;
	}

	static bool LooksLikePlexInternalNumericRatingKey(string ratingKey)
	{
		var s = (ratingKey ?? "").Trim();
		if (s.Length == 0)
			return false;
		foreach (var ch in s)
		{
			if (!char.IsDigit(ch))
				return false;
		}
		return true;
	}

	static bool TryParseChannelOnlySeasonKey(string ratingKey, out string youtubeChannelId)
	{
		youtubeChannelId = "";
		var rk = (ratingKey ?? "").Trim();
		if (!rk.StartsWith("ch_", StringComparison.Ordinal))
			return false;
		if (!rk.EndsWith("_s1", StringComparison.Ordinal))
			return false;
		var inner = rk.Substring("ch_".Length, rk.Length - "ch_".Length - "_s1".Length);
		if (string.IsNullOrWhiteSpace(inner))
			return false;
		if (!PlexIdentifier.IsSafeRatingKey("ch_" + inner))
			return false;
		youtubeChannelId = inner;
		return true;
	}

	static string BuildChannelOnlySeasonRatingKey(string youtubeChannelId)
	{
		var show = PlexIdentifier.BuildRatingKey(PlexIdentifier.PlexItemKind.Show, youtubeChannelId);
		return show + "_s1";
	}

	static bool? QueryFlag(HttpRequest req, string key)
	{
		if (!req.Query.TryGetValue(key, out var v))
			return null;
		var s = v.ToString();
		if (string.IsNullOrWhiteSpace(s))
			return null;
		return s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
	}

	static Dictionary<string, JsonElement> BuildCaseInsensitivePropertyMap(JsonElement root)
	{
		var d = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
		if (root.ValueKind != JsonValueKind.Object)
			return d;
		foreach (var p in root.EnumerateObject())
		{
			if (!d.ContainsKey(p.Name))
				d[p.Name] = p.Value;
		}
		return d;
	}

	static int GetIntFromPropertyMap(IReadOnlyDictionary<string, JsonElement> map, string name)
	{
		if (!map.TryGetValue(name, out var el))
			return 0;
		if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i))
			return i;
		if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s))
			return s;
		return 0;
	}

	static string CoalesceStringFromPropertyMap(IReadOnlyDictionary<string, JsonElement> map, params string[] keys)
	{
		foreach (var key in keys)
		{
			if (!map.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.String)
				continue;
			var s = el.GetString();
			if (!string.IsNullOrWhiteSpace(s))
				return s.Trim();
		}
		return "";
	}

	/// <summary>
	/// Plex may send paths only under <c>Media[].Part[].file</c>; property names may differ in casing.
	/// </summary>
	static string ExtractMatchPath(IReadOnlyDictionary<string, JsonElement> map)
	{
		foreach (var key in new[] { "filename", "path", "file", "location", "url" })
		{
			if (!map.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.String)
				continue;
			var s = el.GetString();
			if (!string.IsNullOrWhiteSpace(s))
				return s.Trim();
		}
		if (!map.TryGetValue("Media", out var mediaEl) || mediaEl.ValueKind != JsonValueKind.Array)
			return "";
		foreach (var mediaItem in mediaEl.EnumerateArray())
		{
			if (mediaItem.ValueKind != JsonValueKind.Object)
				continue;
			var mediaMap = BuildCaseInsensitivePropertyMap(mediaItem);
			if (!mediaMap.TryGetValue("Part", out var partEl) || partEl.ValueKind != JsonValueKind.Array)
				continue;
			foreach (var part in partEl.EnumerateArray())
			{
				if (part.ValueKind != JsonValueKind.Object)
					continue;
				var partMap = BuildCaseInsensitivePropertyMap(part);
				if (!partMap.TryGetValue("file", out var fileEl) || fileEl.ValueKind != JsonValueKind.String)
					continue;
				var p = fileEl.GetString();
				if (!string.IsNullOrWhiteSpace(p))
					return p.Trim();
			}
		}
		return "";
	}

	static string? TryGetString(JsonElement root, string name)
	{
		return root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
	}

	static int TryGetInt(JsonElement root, string name)
	{
		if (!root.TryGetProperty(name, out var el))
			return 0;
		if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i))
			return i;
		if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s))
			return s;
		return 0;
	}

}

