using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TubeArr.Backend.Contracts;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

internal static class NamingConfigEndpoints
{
	internal static void Map(RouteGroupBuilder api)
	{
		api.MapGet("/config/naming", async (TubeArrDbContext db) =>
		{
			var existing = await db.NamingConfig.OrderBy(x => x.Id).FirstOrDefaultAsync();
			if (existing is null)
			{
				existing = new NamingConfigEntity { Id = 1 };
				db.NamingConfig.Add(existing);
				await db.SaveChangesAsync();
			}

			return Results.Json(ToNamingConfigResource(existing));
		});

		api.MapPut("/config/naming", async (NamingConfigUpdateRequest request, TubeArrDbContext db) =>
		{
			var existing = await db.NamingConfig.OrderBy(x => x.Id).FirstOrDefaultAsync();
			if (existing is null)
			{
				existing = new NamingConfigEntity { Id = 1 };
				db.NamingConfig.Add(existing);
			}

			if (request.RenameVideos.HasValue)
				existing.RenameVideos = request.RenameVideos.Value;
			if (request.ReplaceIllegalCharacters.HasValue)
				existing.ReplaceIllegalCharacters = request.ReplaceIllegalCharacters.Value;
			if (request.ColonReplacementFormat.HasValue)
				existing.ColonReplacementFormat = request.ColonReplacementFormat.Value;
			if (request.CustomColonReplacementFormat is not null)
				existing.CustomColonReplacementFormat = request.CustomColonReplacementFormat;
			if (request.MultiVideoStyle.HasValue)
				existing.MultiVideoStyle = request.MultiVideoStyle.Value;
			if (request.StandardVideoFormat is not null)
				existing.StandardVideoFormat = request.StandardVideoFormat;
			if (request.DailyVideoFormat is not null)
				existing.DailyVideoFormat = request.DailyVideoFormat;
			if (request.EpisodicVideoFormat is not null)
				existing.EpisodicVideoFormat = request.EpisodicVideoFormat;
			if (request.StreamingVideoFormat is not null)
				existing.StreamingVideoFormat = request.StreamingVideoFormat;
			if (request.ChannelFolderFormat is not null)
				existing.ChannelFolderFormat = request.ChannelFolderFormat;
			if (request.PlaylistFolderFormat is not null)
				existing.PlaylistFolderFormat = request.PlaylistFolderFormat;
			if (request.SpecialsFolderFormat is not null)
				existing.SpecialsFolderFormat = request.SpecialsFolderFormat;

			var failures = ValidateNamingConfigPatterns(existing);
			if (failures.Count > 0)
			{
				return ApiErrorResults.BadRequest(TubeArrErrorCodes.ValidationFailed, "One or more naming patterns are invalid.", failures.ToArray());
			}

			await db.SaveChangesAsync();
			return Results.Json(ToNamingConfigResource(existing));
		});

		api.MapGet("/config/naming/examples", async (HttpContext context, TubeArrDbContext db) =>
		{
			var naming = await db.NamingConfig.OrderBy(x => x.Id).FirstOrDefaultAsync() ?? new NamingConfigEntity { Id = 1 };
			var media = await db.MediaManagementConfig.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync();
			var useCustomNfos = media?.UseCustomNfos != false;

			string GetPattern(string key, string current) =>
				context.Request.Query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
					? value.ToString()
					: current;

			naming.StandardVideoFormat = GetPattern("standardVideoFormat", naming.StandardVideoFormat);
			naming.DailyVideoFormat = GetPattern("dailyVideoFormat", naming.DailyVideoFormat);
			naming.EpisodicVideoFormat = GetPattern("episodicVideoFormat", naming.EpisodicVideoFormat);
			naming.StreamingVideoFormat = GetPattern("streamingVideoFormat", naming.StreamingVideoFormat);
			naming.ChannelFolderFormat = GetPattern("channelFolderFormat", naming.ChannelFolderFormat);
			naming.PlaylistFolderFormat = GetPattern("playlistFolderFormat", naming.PlaylistFolderFormat);
			naming.SpecialsFolderFormat = GetPattern("specialsFolderFormat", naming.SpecialsFolderFormat);

			var sampleChannel = new ChannelEntity
			{
				Id = 1,
				YoutubeChannelId = "UCxxxxxxxxxxxxxxxxxxxx",
				Title = "Sample Channel",
				TitleSlug = "sample-channel",
				Monitored = true,
				Added = DateTimeOffset.UtcNow
			};

			var samplePlaylist = new PlaylistEntity
			{
				Id = 1,
				ChannelId = 1,
				YoutubePlaylistId = "PL1234567890",
				Title = "Sample Playlist",
				Monitored = true,
				Added = DateTimeOffset.UtcNow
			};

			var sampleVideo = new VideoEntity
			{
				Id = 1,
				ChannelId = 1,
				YoutubeVideoId = "dQw4w9WgXcQ",
				Title = "My Cool Video",
				UploadDateUtc = new DateTimeOffset(2026, 3, 14, 12, 0, 0, TimeSpan.Zero),
				Monitored = true,
				Added = DateTimeOffset.UtcNow
			};

			var altChannel = new ChannelEntity
			{
				Id = 2,
				YoutubeChannelId = "UCyyyyyyyyyyyyyyyyyyyy",
				Title = "Live Channel",
				TitleSlug = "live-channel",
				ChannelType = "streaming",
				Monitored = true,
				Added = DateTimeOffset.UtcNow
			};

			var altVideo = new VideoEntity
			{
				Id = 2,
				ChannelId = 2,
				YoutubeVideoId = "abcdefghijk",
				Title = "Tuesday Stream Highlights",
				UploadDateUtc = new DateTimeOffset(2026, 4, 1, 20, 0, 0, TimeSpan.Zero),
				Monitored = true,
				Added = DateTimeOffset.UtcNow
			};

			var contextForExamples = new VideoFileNaming.NamingContext(
				sampleChannel,
				samplePlaylist,
				sampleVideo,
				PlaylistIndex: 1,
				QualityFull: "WEBRip-1080p",
				Resolution: "1080p",
				Extension: "mkv",
				PlaylistNumber: 2,
				MediaInfoCodec: "avc1",
				MediaInfoAudioCodec: "aac",
				MediaInfoResolution: "1080p",
				MediaInfoFramerate: "30",
				MediaInfoDynamicRange: "SDR",
				MediaInfoAudioChannels: "2",
				MediaInfoBitrate: "4500k",
				MediaInfoContainer: "mkv"
			);

			string BuildExample(string pattern) =>
				VideoFileNaming.BuildFileName(pattern, contextForExamples, naming);

			var altContext = new VideoFileNaming.NamingContext(
				altChannel,
				samplePlaylist,
				altVideo,
				PlaylistIndex: 3,
				QualityFull: "WEBRip-720p",
				Resolution: "720p",
				Extension: "mp4",
				PlaylistNumber: 2,
				MediaInfoCodec: "avc1",
				MediaInfoAudioCodec: "aac",
				MediaInfoResolution: "720p",
				MediaInfoFramerate: "60",
				MediaInfoDynamicRange: "SDR",
				MediaInfoAudioChannels: "2",
				MediaInfoBitrate: "2800k",
				MediaInfoContainer: "mp4"
			);

			string BuildAlt(string pattern) =>
				VideoFileNaming.BuildFileName(pattern, altContext, naming);

			var result = new
			{
				singleVideoExample = BuildExample(naming.StandardVideoFormat),
				multiVideoExample = BuildExample(naming.StandardVideoFormat),
				dailyVideoExample = BuildExample(naming.DailyVideoFormat),
				episodicVideoExample = BuildExample(naming.EpisodicVideoFormat),
				episodicMultiVideoExample = BuildExample(naming.EpisodicVideoFormat),
				streamingVideoExample = BuildAlt(naming.StreamingVideoFormat),
				alternateStandardVideoExample = BuildAlt(naming.StandardVideoFormat),
				channelFolderExample = VideoFileNaming.BuildFolderName(naming.ChannelFolderFormat, contextForExamples, naming),
				alternateChannelFolderExample = VideoFileNaming.BuildFolderName(naming.ChannelFolderFormat, altContext, naming),
				playlistFolderExample = useCustomNfos
					? NfoLibraryExporter.FormatSeasonPlaylistFolderName(2)
					: VideoFileNaming.BuildFolderName(naming.PlaylistFolderFormat, contextForExamples, naming),
				specialsFolderExample = VideoFileNaming.BuildFolderName(naming.SpecialsFolderFormat, contextForExamples, naming)
			};

			return Results.Json(result);
		});
	}

	private static object ToNamingConfigResource(NamingConfigEntity entity) =>
		new
		{
			renameVideos = entity.RenameVideos,
			replaceIllegalCharacters = entity.ReplaceIllegalCharacters,
			colonReplacementFormat = entity.ColonReplacementFormat,
			customColonReplacementFormat = entity.CustomColonReplacementFormat,
			multiVideoStyle = entity.MultiVideoStyle,
			standardVideoFormat = entity.StandardVideoFormat,
			dailyVideoFormat = entity.DailyVideoFormat,
			episodicVideoFormat = entity.EpisodicVideoFormat,
			streamingVideoFormat = entity.StreamingVideoFormat,
			channelFolderFormat = entity.ChannelFolderFormat,
			playlistFolderFormat = entity.PlaylistFolderFormat,
			specialsFolderFormat = entity.SpecialsFolderFormat
		};

	private static List<object> ValidateNamingConfigPatterns(NamingConfigEntity entity)
	{
		var failures = new List<object>();

		void AddFailures(string propertyName, string pattern)
		{
			foreach (var (token, error) in VideoFileNaming.ValidatePattern(pattern))
			{
				failures.Add(new
				{
					propertyName,
					errorMessage = error,
					isWarning = false,
					infoLink = (string?)null,
					detailedDescription = (string?)null
				});
			}
		}

		AddFailures("standardVideoFormat", entity.StandardVideoFormat);
		AddFailures("dailyVideoFormat", entity.DailyVideoFormat);
		AddFailures("episodicVideoFormat", entity.EpisodicVideoFormat);
		AddFailures("streamingVideoFormat", entity.StreamingVideoFormat);
		AddFailures("channelFolderFormat", entity.ChannelFolderFormat);
		AddFailures("playlistFolderFormat", entity.PlaylistFolderFormat);
		AddFailures("specialsFolderFormat", entity.SpecialsFolderFormat);

		return failures;
	}
}
