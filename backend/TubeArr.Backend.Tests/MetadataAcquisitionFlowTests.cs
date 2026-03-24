using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TubeArr.Backend;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.Tests;

public sealed class MetadataAcquisitionFlowTests
{
	const string YoutubeChannelId = "UC1234567890123456789012";

	[Fact]
	public void ChannelPageMetadataService_parses_direct_channel_metadata()
	{
		var metadata = ChannelPageMetadataService.ParseFromHtml(BuildChannelPageHtml("Channel Title", "Channel Description"));

		Assert.NotNull(metadata);
		Assert.Equal(YoutubeChannelId, metadata!.YoutubeChannelId);
		Assert.Equal("Channel Title", metadata.Title);
		Assert.Equal("Channel Description", metadata.Description);
		Assert.Equal("https://img.example/avatar.jpg", metadata.ThumbnailUrl);
		Assert.Equal("https://img.example/banner.jpg", metadata.BannerUrl);
	}

	[Fact]
	public void ChannelVideoDiscoveryService_parses_video_listing_rows()
	{
		var items = ChannelVideoDiscoveryService.ParseListingHtml(BuildListingHtml());

		Assert.Equal(2, items.Count);
		Assert.Equal("video-1", items[0].YoutubeVideoId);
		Assert.Equal("Discovery One", items[0].Title);
		Assert.Equal(754, items[0].Runtime);
		Assert.Equal("video-2", items[1].YoutubeVideoId);
		Assert.Null(items[1].Title);
		Assert.Equal("https://img.example/video-2.jpg", items[1].ThumbnailUrl);
	}

	[Fact]
	public void ChannelVideoDiscoveryService_parses_shorts_with_top_level_reel_watch_endpoint()
	{
		var items = ChannelVideoDiscoveryService.ParseListingHtml(BuildShortsListingHtmlWithReelOnRenderer());

		Assert.Single(items);
		Assert.Equal("shortDirect11", items[0].YoutubeVideoId);
	}

	[Fact]
	public void ChannelVideoDiscoveryService_parses_shorts_lockup_listing_rows()
	{
		var items = ChannelVideoDiscoveryService.ParseListingHtml(BuildShortsListingHtml());

		Assert.Equal(2, items.Count);
		Assert.Equal("shortA1111111", items[0].YoutubeVideoId);
		Assert.Contains("Short title one", items[0].Title, StringComparison.Ordinal);
		Assert.Equal("https://i.ytimg.com/vi/shortA1111111/frame0.jpg", items[0].ThumbnailUrl);
		Assert.Equal("shortB2222222", items[1].YoutubeVideoId);
		Assert.Contains("Short title two", items[1].Title, StringComparison.Ordinal);
	}

	[Fact]
	public void VideoWatchPageMetadataService_parses_watch_page_metadata()
	{
		var metadata = VideoWatchPageMetadataService.ParseFromHtml("video-1", BuildWatchPageHtml("Watch One", "Watch Description One", "2024-01-02", 754));

		Assert.NotNull(metadata);
		Assert.Equal("Watch One", metadata!.Title);
		Assert.Equal("Watch Description One", metadata.Description);
		Assert.Equal("2024-01-02", metadata.AirDate);
		Assert.Equal(754, metadata.Runtime);
	}

	[Fact]
	public async Task YouTubeDataApiMetadataService_discovers_complete_uploads_playlist_across_pages()
	{
		await using var db = await CreateDbContextAsync();
		db.YouTubeConfig.Add(new YouTubeConfigEntity
		{
			ApiKey = "api-key",
			UseYouTubeApi = true,
			ApiPriorityMetadataItemsJson = YouTubeDataApiMetadataService.SerializePriorityItems(
			[
				YouTubeApiMetadataPriorityItems.VideoListing
			])
		});
		await db.SaveChangesAsync();

		var httpClient = CreateHttpClient(request =>
		{
			var url = request.RequestUri!.ToString();
			return url switch
			{
				var value when value.Contains("channels?part=contentDetails", StringComparison.Ordinal) =>
					Ok("""
					{"items":[{"contentDetails":{"relatedPlaylists":{"uploads":"UU123"}}}]}
					"""),
				var value when value.Contains("playlistItems?part=snippet,contentDetails", StringComparison.Ordinal) && !value.Contains("pageToken=page-2", StringComparison.Ordinal) =>
					Ok("""
					{
					  "nextPageToken":"page-2",
					  "items":[
					    {
					      "snippet":{
					        "title":"Video One",
					        "publishedAt":"2024-01-02T00:00:00Z",
					        "thumbnails":{"high":{"url":"https://img.example/video-1.jpg"}}
					      },
					      "contentDetails":{
					        "videoId":"video-1",
					        "videoPublishedAt":"2024-01-02T00:00:00Z"
					      }
					    },
					    {
					      "snippet":{
					        "title":"Video Two",
					        "publishedAt":"2024-01-03T00:00:00Z",
					        "thumbnails":{"high":{"url":"https://img.example/video-2.jpg"}}
					      },
					      "contentDetails":{
					        "videoId":"video-2",
					        "videoPublishedAt":"2024-01-03T00:00:00Z"
					      }
					    }
					  ]
					}
					"""),
				var value when value.Contains("playlistItems?part=snippet,contentDetails", StringComparison.Ordinal) && value.Contains("pageToken=page-2", StringComparison.Ordinal) =>
					Ok("""
					{
					  "items":[
					    {
					      "snippet":{
					        "title":"Video Three",
					        "publishedAt":"2024-01-04T00:00:00Z",
					        "thumbnails":{"high":{"url":"https://img.example/video-3.jpg"}}
					      },
					      "contentDetails":{
					        "videoId":"video-3",
					        "videoPublishedAt":"2024-01-04T00:00:00Z"
					      }
					    }
					  ]
					}
					"""),
				_ => NotFound()
			};
		});

		var apiService = new YouTubeDataApiMetadataService(new SingleClientHttpClientFactory(httpClient), NullLogger<YouTubeDataApiMetadataService>.Instance);

		var result = await apiService.TryDiscoverChannelVideosAsync(db, YoutubeChannelId);
		var items = result.Items;

		Assert.Equal(new[] { "video-1", "video-2", "video-3" }, items.Select(x => x.YoutubeVideoId).ToArray());
		Assert.Equal(2, result.PlaylistItemsPageCount);
		Assert.Equal("Video One", items[0].Title);
		Assert.Equal("https://img.example/video-3.jpg", items[2].ThumbnailUrl);
		Assert.Equal(new DateTimeOffset(2024, 1, 4, 0, 0, 0, TimeSpan.Zero), items[2].PublishedUtc);
	}

	[Fact]
	public async Task YouTubeDataApiMetadataService_batches_video_metadata_requests_with_deduplicated_ids()
	{
		await using var db = await CreateDbContextAsync();
		db.YouTubeConfig.Add(new YouTubeConfigEntity
		{
			ApiKey = "api-key",
			UseYouTubeApi = true
		});
		await db.SaveChangesAsync();

		var batchSizes = new List<int>();
		var httpClient = CreateHttpClient(request =>
		{
			var url = request.RequestUri!.ToString();
			if (!url.Contains("videos?part=snippet,contentDetails", StringComparison.Ordinal))
				return NotFound();

			var ids = ExtractVideoIdsFromUrl(url);
			batchSizes.Add(ids.Count);
			return Ok(BuildVideosListApiResponse(ids));
		});

		var apiService = new YouTubeDataApiMetadataService(new SingleClientHttpClientFactory(httpClient), NullLogger<YouTubeDataApiMetadataService>.Instance);
		var ids = Enumerable.Range(1, 51).Select(i => $"video-{i}").Concat(["video-1"]);

		var result = await apiService.TryGetVideoMetadataBatchAsync(db, ids);

		Assert.Equal(2, result.BatchCallCount);
		Assert.Equal(51, result.MetadataByYoutubeId.Count);
		Assert.All(batchSizes, size => Assert.InRange(size, 1, 50));
	}

	[Fact]
	public async Task PopulateChannelDetailsAsync_uses_direct_sources_and_only_falls_back_per_failed_video()
	{
		await using var db = await CreateDbContextAsync();
		db.Channels.Add(new ChannelEntity
		{
			YoutubeChannelId = YoutubeChannelId,
			Title = "Old Channel",
			TitleSlug = "old-channel",
			Description = "Old Description",
			ThumbnailUrl = "https://old.example/avatar.jpg",
			BannerUrl = "https://old.example/banner.jpg",
			Monitored = true,
			QualityProfileId = 7,
			Path = @"C:\Media\Old Channel",
			RootFolderPath = @"C:\Media",
			Tags = "11,12",
			MonitorNewItems = 1,
			PlaylistFolder = true,
			ChannelType = "standard"
		});
		await db.SaveChangesAsync();

		var httpClient = CreateHttpClient(request =>
		{
			var url = request.RequestUri!.ToString();
			return url switch
			{
				var value when IsChannelPageUrl(value) =>
					Ok(BuildChannelPageHtml("Sample Channel", "Sample channel description")),
				var value when IsChannelVideosUrl(value) =>
					Ok(BuildListingHtml()),
				var value when value == "https://www.youtube.com/watch?v=video-1" =>
					Ok(BuildWatchPageHtml("Watch One", "Watch Description One", "2024-01-02", 754)),
				var value when value == "https://www.youtube.com/watch?v=video-2" =>
					Ok("<html><body>broken</body></html>"),
				_ => NotFound()
			};
		});

		var fallbackChannelCount = 0;
		var fallbackDiscoveryCount = 0;
		var fallbackVideoCount = 0;
		var service = CreateAcquisitionService(
			httpClient,
			channelMetadataFallbackAsync: (_, _, _) =>
			{
				fallbackChannelCount++;
				return Task.FromResult<ChannelPageMetadata?>(null);
			},
			videoDiscoveryFallbackAsync: (_, _, _) =>
			{
				fallbackDiscoveryCount++;
				return Task.FromResult<IReadOnlyList<ChannelVideoDiscoveryItem>>(Array.Empty<ChannelVideoDiscoveryItem>());
			},
			videoMetadataFallbackAsync: (_, youtubeVideoId, _) =>
			{
				fallbackVideoCount++;
				return Task.FromResult<VideoWatchPageMetadata?>(new VideoWatchPageMetadata(
					YoutubeVideoId: youtubeVideoId,
					Title: "Fallback Two",
					Description: "Fallback Description Two",
					ThumbnailUrl: "https://img.example/video-2-full.jpg",
					UploadDateUtc: new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero),
					AirDateUtc: new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero),
					AirDate: "2024-01-03",
					Overview: "Fallback Description Two",
					Runtime: 601));
			});

		var message = await service.PopulateChannelDetailsAsync(db, db.Channels.Single().Id);
		var channel = await db.Channels.SingleAsync();
		var videos = await db.Videos.OrderBy(v => v.YoutubeVideoId).ToListAsync();

		Assert.Null(message);
		Assert.Equal(0, fallbackChannelCount);
		Assert.Equal(1, fallbackDiscoveryCount);
		Assert.Equal(1, fallbackVideoCount);
		Assert.Equal("Sample Channel", channel.Title);
		Assert.Equal("Sample channel description", channel.Description);
		Assert.Equal("https://img.example/avatar.jpg", channel.ThumbnailUrl);
		Assert.Equal("https://img.example/banner.jpg", channel.BannerUrl);
		Assert.Equal(7, channel.QualityProfileId);
		Assert.Equal(@"C:\Media\Old Channel", channel.Path);
		Assert.Equal(@"C:\Media", channel.RootFolderPath);
		Assert.Equal("11,12", channel.Tags);
		Assert.Equal(2, videos.Count);
		Assert.Equal("Watch One", videos[0].Title);
		Assert.Equal("Watch Description One", videos[0].Description);
		Assert.Equal("2024-01-02", videos[0].AirDate);
		Assert.Equal(754, videos[0].Runtime);
		Assert.Equal("Fallback Two", videos[1].Title);
		Assert.Equal("Fallback Description Two", videos[1].Overview);
		Assert.Equal(601, videos[1].Runtime);
	}

	[Fact]
	public async Task PopulateChannelDetailsAsync_uses_youtube_data_api_video_listing_and_paginates_uploads_playlist()
	{
		await using var db = await CreateDbContextAsync();
		db.YouTubeConfig.Add(new YouTubeConfigEntity
		{
			ApiKey = "api-key",
			UseYouTubeApi = true,
			ApiPriorityMetadataItemsJson = YouTubeDataApiMetadataService.SerializePriorityItems(
			[
				YouTubeApiMetadataPriorityItems.VideoListing,
				YouTubeApiMetadataPriorityItems.VideoDetails
			])
		});
		db.Channels.Add(new ChannelEntity
		{
			YoutubeChannelId = YoutubeChannelId,
			Title = "Old Channel",
			TitleSlug = "old-channel",
			Monitored = true
		});
		await db.SaveChangesAsync();

		var uploadsResolveCount = 0;
		var playlistItemsRequestCount = 0;
		var videosListRequestCount = 0;
		var watchPageRequestCount = 0;

		var httpClient = CreateHttpClient(request =>
		{
			var url = request.RequestUri!.ToString();
			return url switch
			{
				var value when value.Contains("channels?part=contentDetails", StringComparison.Ordinal) =>
					CountAndReturn(ref uploadsResolveCount,
					Ok("""
					{"items":[{"contentDetails":{"relatedPlaylists":{"uploads":"UU123"}}}]}
					""")),
				var value when value.Contains("playlistItems?part=snippet,contentDetails", StringComparison.Ordinal) && !value.Contains("pageToken=page-2", StringComparison.Ordinal) =>
					CountAndReturn(ref playlistItemsRequestCount,
					Ok("""
					{
					  "nextPageToken":"page-2",
					  "items":[
					    {
					      "snippet":{
					        "title":"Video One",
					        "description":"Playlist Description One",
					        "publishedAt":"2024-01-02T00:00:00Z",
					        "thumbnails":{"high":{"url":"https://img.example/video-1.jpg"}}
					      },
					      "contentDetails":{
					        "videoId":"video-1",
					        "videoPublishedAt":"2024-01-02T00:00:00Z"
					      }
					    },
					    {
					      "snippet":{
					        "title":"Video Two",
					        "description":"Playlist Description Two",
					        "publishedAt":"2024-01-03T00:00:00Z",
					        "thumbnails":{"high":{"url":"https://img.example/video-2.jpg"}}
					      },
					      "contentDetails":{
					        "videoId":"video-2",
					        "videoPublishedAt":"2024-01-03T00:00:00Z"
					      }
					    }
					  ]
					}
					""")),
				var value when value.Contains("playlistItems?part=snippet,contentDetails", StringComparison.Ordinal) && value.Contains("pageToken=page-2", StringComparison.Ordinal) =>
					CountAndReturn(ref playlistItemsRequestCount,
					Ok("""
					{
					  "items":[
					    {
					      "snippet":{
					        "title":"Video Three",
					        "description":"Playlist Description Three",
					        "publishedAt":"2024-01-04T00:00:00Z",
					        "thumbnails":{"high":{"url":"https://img.example/video-3.jpg"}}
					      },
					      "contentDetails":{
					        "videoId":"video-3",
					        "videoPublishedAt":"2024-01-04T00:00:00Z"
					      }
					    }
					  ]
					}
					""")),
				var value when value.Contains("videos?part=snippet,contentDetails", StringComparison.Ordinal) =>
					CountAndReturn(ref videosListRequestCount, NotFound()),
				var value when value == "https://www.youtube.com/watch?v=video-1" =>
					CountAndReturn(ref watchPageRequestCount, Ok(BuildWatchPageHtml("Watch One", "Watch Description One", "2024-01-02", 754))),
				var value when value == "https://www.youtube.com/watch?v=video-2" =>
					CountAndReturn(ref watchPageRequestCount, Ok(BuildWatchPageHtml("Watch Two", "Watch Description Two", "2024-01-03", 601))),
				var value when value == "https://www.youtube.com/watch?v=video-3" =>
					CountAndReturn(ref watchPageRequestCount, Ok(BuildWatchPageHtml("Watch Three", "Watch Description Three", "2024-01-04", 420))),
				_ => NotFound()
			};
		});

		var apiService = new YouTubeDataApiMetadataService(new SingleClientHttpClientFactory(httpClient), NullLogger<YouTubeDataApiMetadataService>.Instance);
		var service = CreateAcquisitionService(
			httpClient,
			channelMetadataFallbackAsync: (_, youtubeChannelId, _) => Task.FromResult<ChannelPageMetadata?>(new ChannelPageMetadata(
				YoutubeChannelId: youtubeChannelId,
				Title: "Recovered Channel",
				Description: "Recovered channel description",
				ThumbnailUrl: "https://img.example/recovered-avatar.jpg",
				BannerUrl: "https://img.example/recovered-banner.jpg",
				CanonicalUrl: $"https://www.youtube.com/channel/{youtubeChannelId}")),
			videoDiscoveryFallbackAsync: (_, _, _) => Task.FromResult<IReadOnlyList<ChannelVideoDiscoveryItem>>(Array.Empty<ChannelVideoDiscoveryItem>()),
			videoMetadataFallbackAsync: (_, _, _) => Task.FromResult<VideoWatchPageMetadata?>(null),
			youTubeDataApiMetadataService: apiService);

		var message = await service.PopulateChannelDetailsAsync(db, db.Channels.Single().Id);
		var videos = await db.Videos.OrderBy(v => v.YoutubeVideoId).ToListAsync();

		Assert.Null(message);
		Assert.Equal(1, uploadsResolveCount);
		Assert.Equal(2, playlistItemsRequestCount);
		Assert.Equal(0, videosListRequestCount);
		Assert.Equal(0, watchPageRequestCount);
		Assert.Equal(new[] { "video-1", "video-2", "video-3" }, videos.Select(v => v.YoutubeVideoId).ToArray());
		Assert.Equal("Video Three", videos[2].Title);
		Assert.Equal("Playlist Description Three", videos[2].Description);
		Assert.Equal(0, videos[2].Runtime);
	}

	[Fact]
	public async Task PopulateChannelDetailsAsync_supplements_partial_direct_video_discovery_with_fallback_results()
	{
		await using var db = await CreateDbContextAsync();
		db.Channels.Add(new ChannelEntity
		{
			YoutubeChannelId = YoutubeChannelId,
			Title = "Old Channel",
			TitleSlug = "old-channel",
			Monitored = true
		});
		await db.SaveChangesAsync();

		var httpClient = CreateHttpClient(request =>
		{
			var url = request.RequestUri!.ToString();
			return url switch
			{
				var value when IsChannelPageUrl(value) =>
					Ok(BuildChannelPageHtml("Sample Channel", "Sample channel description")),
				var value when IsChannelVideosUrl(value) =>
					Ok(BuildListingHtml()),
				var value when value == "https://www.youtube.com/watch?v=video-1" =>
					Ok(BuildWatchPageHtml("Watch One", "Watch Description One", "2024-01-02", 754)),
				var value when value == "https://www.youtube.com/watch?v=video-2" =>
					Ok(BuildWatchPageHtml("Watch Two", "Watch Description Two", "2024-01-03", 601)),
				var value when value == "https://www.youtube.com/watch?v=video-3" =>
					Ok(BuildWatchPageHtml("Watch Three", "Watch Description Three", "2024-01-04", 420)),
				_ => NotFound()
			};
		});

		var fallbackDiscoveryCount = 0;
		var service = CreateAcquisitionService(
			httpClient,
			channelMetadataFallbackAsync: (_, _, _) => Task.FromResult<ChannelPageMetadata?>(null),
			videoDiscoveryFallbackAsync: (_, _, _) =>
			{
				fallbackDiscoveryCount++;
				return Task.FromResult<IReadOnlyList<ChannelVideoDiscoveryItem>>(
				[
					new ChannelVideoDiscoveryItem("video-1", "Discovery One", "https://img.example/video-1.jpg", 754),
					new ChannelVideoDiscoveryItem("video-2", "Discovery Two", "https://img.example/video-2.jpg", 601),
					new ChannelVideoDiscoveryItem("video-3", "Discovery Three", "https://img.example/video-3.jpg", 420)
				]);
			},
			videoMetadataFallbackAsync: (_, _, _) => Task.FromResult<VideoWatchPageMetadata?>(null));

		var message = await service.PopulateChannelDetailsAsync(db, db.Channels.Single().Id);
		var videos = await db.Videos
			.OrderBy(v => v.YoutubeVideoId)
			.ToListAsync();

		Assert.Null(message);
		Assert.Equal(1, fallbackDiscoveryCount);
		Assert.Equal(3, videos.Count);
		Assert.Equal(new[] { "video-1", "video-2", "video-3" }, videos.Select(v => v.YoutubeVideoId).ToArray());
		Assert.Equal("Watch Three", videos[2].Title);
	}

	[Fact]
	public async Task PopulateChannelDetailsAsync_keeps_skeleton_rows_when_hydrate_fails_and_upserts_duplicates()
	{
		await using var db = await CreateDbContextAsync();
		db.Channels.Add(new ChannelEntity
		{
			YoutubeChannelId = YoutubeChannelId,
			Title = "Seed",
			TitleSlug = "seed",
			Monitored = true
		});
		await db.SaveChangesAsync();

		var httpClient = CreateHttpClient(request =>
		{
			var url = request.RequestUri!.ToString();
			return url switch
			{
				var value when IsChannelPageUrl(value) =>
					Ok(BuildChannelPageHtml("Sample Channel", "Sample channel description")),
				var value when IsChannelVideosUrl(value) =>
					Ok(BuildListingHtml()),
				var value when value.StartsWith("https://www.youtube.com/watch?v=", StringComparison.Ordinal) =>
					Ok("<html><body>broken</body></html>"),
				_ => NotFound()
			};
		});

		var fallbackVideoCount = 0;
		var service = CreateAcquisitionService(
			httpClient,
			channelMetadataFallbackAsync: (_, _, _) => Task.FromResult<ChannelPageMetadata?>(null),
			videoDiscoveryFallbackAsync: (_, _, _) => Task.FromResult<IReadOnlyList<ChannelVideoDiscoveryItem>>(Array.Empty<ChannelVideoDiscoveryItem>()),
			videoMetadataFallbackAsync: (_, _, _) =>
			{
				fallbackVideoCount++;
				return Task.FromResult<VideoWatchPageMetadata?>(null);
			});

		var channelId = db.Channels.Single().Id;
		var firstMessage = await service.PopulateChannelDetailsAsync(db, channelId);
		var secondMessage = await service.PopulateChannelDetailsAsync(db, channelId);
		var videos = await db.Videos.OrderBy(v => v.YoutubeVideoId).ToListAsync();

		Assert.Null(firstMessage);
		Assert.Null(secondMessage);
		Assert.Equal(4, fallbackVideoCount);
		Assert.Equal(2, videos.Count);
		Assert.Equal("Discovery One", videos[0].Title);
		Assert.Equal("https://img.example/video-2.jpg", videos[1].ThumbnailUrl);
		Assert.Equal(DateTimeOffset.UnixEpoch, videos[0].UploadDateUtc);
	}

	[Fact]
	public async Task PopulateChannelDetailsAsync_recovers_orphaned_videos_after_channel_re_add()
	{
		await using var db = await CreateDbContextAsync();
		var originalChannel = new ChannelEntity
		{
			YoutubeChannelId = YoutubeChannelId,
			Title = "Original Channel",
			TitleSlug = "original-channel",
			Monitored = true
		};
		db.Channels.Add(originalChannel);
		await db.SaveChangesAsync();

		var orphanedVideo = new VideoEntity
		{
			ChannelId = originalChannel.Id,
			PlaylistId = null,
			YoutubeVideoId = "video-1",
			Title = "Existing Video",
			Description = "Existing Description",
			ThumbnailUrl = "https://img.example/existing-video.jpg",
			UploadDateUtc = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero),
			AirDateUtc = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero),
			AirDate = "2024-01-02",
			Overview = "Existing Description",
			Runtime = 754,
			Monitored = true,
			Added = DateTimeOffset.UtcNow
		};
		db.Videos.Add(orphanedVideo);
		await db.SaveChangesAsync();

		db.Channels.Remove(originalChannel);
		await db.SaveChangesAsync();

		var replacementChannel = new ChannelEntity
		{
			YoutubeChannelId = YoutubeChannelId,
			Title = "Replacement Channel",
			TitleSlug = "replacement-channel",
			Monitored = true
		};
		db.Channels.Add(replacementChannel);
		await db.SaveChangesAsync();

		var httpClient = CreateHttpClient(request =>
		{
			var url = request.RequestUri!.ToString();
			return url switch
			{
				var value when IsChannelPageUrl(value) =>
					Ok(BuildChannelPageHtml("Sample Channel", "Sample channel description")),
				var value when IsChannelVideosUrl(value) =>
					Ok(BuildListingHtml()),
				var value when value == "https://www.youtube.com/watch?v=video-1" =>
					Ok(BuildWatchPageHtml("Watch One", "Watch Description One", "2024-01-02", 754)),
				var value when value == "https://www.youtube.com/watch?v=video-2" =>
					Ok(BuildWatchPageHtml("Watch Two", "Watch Description Two", "2024-01-03", 601)),
				_ => NotFound()
			};
		});

		var service = CreateAcquisitionService(
			httpClient,
			channelMetadataFallbackAsync: (_, youtubeChannelId, _) => Task.FromResult<ChannelPageMetadata?>(new ChannelPageMetadata(
				YoutubeChannelId: youtubeChannelId,
				Title: "Recovered Channel",
				Description: "Recovered channel description",
				ThumbnailUrl: "https://img.example/recovered-avatar.jpg",
				BannerUrl: "https://img.example/recovered-banner.jpg",
				CanonicalUrl: $"https://www.youtube.com/channel/{youtubeChannelId}")),
			videoDiscoveryFallbackAsync: (_, _, _) => Task.FromResult<IReadOnlyList<ChannelVideoDiscoveryItem>>(Array.Empty<ChannelVideoDiscoveryItem>()),
			videoMetadataFallbackAsync: (_, _, _) => Task.FromResult<VideoWatchPageMetadata?>(null));

		var message = await service.PopulateChannelDetailsAsync(db, replacementChannel.Id);
		var videos = await db.Videos
			.OrderBy(v => v.YoutubeVideoId)
			.ToListAsync();

		Assert.Null(message);
		Assert.Equal(2, videos.Count);
		Assert.All(videos, video => Assert.Equal(replacementChannel.Id, video.ChannelId));
		Assert.Equal(new[] { "video-1", "video-2" }, videos.Select(v => v.YoutubeVideoId).ToArray());
	}

	[Fact]
	public async Task PopulateVideoMetadataAsync_hydrates_missing_placeholder_videos()
	{
		await using var db = await CreateDbContextAsync();
		var channel = new ChannelEntity
		{
			YoutubeChannelId = YoutubeChannelId,
			Title = "Sample Channel",
			TitleSlug = "sample-channel",
			Monitored = true
		};
		db.Channels.Add(channel);
		await db.SaveChangesAsync();

		db.Videos.Add(new VideoEntity
		{
			ChannelId = channel.Id,
			YoutubeVideoId = "video-1",
			Title = "",
			Description = null,
			ThumbnailUrl = null,
			UploadDateUtc = DateTimeOffset.UnixEpoch,
			AirDateUtc = DateTimeOffset.UnixEpoch,
			AirDate = "",
			Overview = null,
			Runtime = 0,
			Monitored = true,
			Added = DateTimeOffset.UtcNow
		});
		await db.SaveChangesAsync();

		var httpClient = CreateHttpClient(request =>
		{
			var url = request.RequestUri!.ToString();
			return url switch
			{
				var value when value == "https://www.youtube.com/watch?v=video-1" =>
					Ok(BuildWatchPageHtml("Watch One", "Watch Description One", "2024-01-02", 754)),
				_ => NotFound()
			};
		});

		var service = CreateAcquisitionService(
			httpClient,
			channelMetadataFallbackAsync: (_, _, _) => Task.FromResult<ChannelPageMetadata?>(null),
			videoDiscoveryFallbackAsync: (_, _, _) => Task.FromResult<IReadOnlyList<ChannelVideoDiscoveryItem>>(Array.Empty<ChannelVideoDiscoveryItem>()),
			videoMetadataFallbackAsync: (_, _, _) => Task.FromResult<VideoWatchPageMetadata?>(null));

		var message = await service.PopulateVideoMetadataAsync(db, channel.Id);
		var video = await db.Videos.SingleAsync();

		Assert.Equal("Updated metadata for 1 video(s).", message);
		Assert.Equal("Watch One", video.Title);
		Assert.Equal("Watch Description One", video.Description);
		Assert.Equal("2024-01-02", video.AirDate);
		Assert.Equal(754, video.Runtime);
	}

	[Fact]
	public async Task PopulateVideoMetadataAsync_uses_batched_youtube_api_video_details_requests()
	{
		await using var db = await CreateDbContextAsync();
		db.YouTubeConfig.Add(new YouTubeConfigEntity
		{
			ApiKey = "api-key",
			UseYouTubeApi = true,
			ApiPriorityMetadataItemsJson = YouTubeDataApiMetadataService.SerializePriorityItems(
			[
				YouTubeApiMetadataPriorityItems.VideoDetails
			])
		});

		var channel = new ChannelEntity
		{
			YoutubeChannelId = YoutubeChannelId,
			Title = "Sample Channel",
			TitleSlug = "sample-channel",
			Monitored = true
		};
		db.Channels.Add(channel);
		await db.SaveChangesAsync();

		for (var i = 1; i <= 51; i++)
		{
			db.Videos.Add(new VideoEntity
			{
				ChannelId = channel.Id,
				YoutubeVideoId = $"video-{i}",
				Title = string.Empty,
				Description = null,
				ThumbnailUrl = null,
				UploadDateUtc = DateTimeOffset.UnixEpoch,
				AirDateUtc = DateTimeOffset.UnixEpoch,
				AirDate = string.Empty,
				Overview = null,
				Runtime = 0,
				Monitored = true,
				Added = DateTimeOffset.UtcNow
			});
		}
		await db.SaveChangesAsync();

		var batchSizes = new List<int>();
		var videosListRequestCount = 0;
		var watchPageRequestCount = 0;
		var httpClient = CreateHttpClient(request =>
		{
			var url = request.RequestUri!.ToString();
			return url switch
			{
				var value when value.Contains("videos?part=snippet,contentDetails", StringComparison.Ordinal) =>
					CountAndReturn(ref videosListRequestCount, Ok(BuildVideosListApiResponse(ExtractVideoIdsFromUrl(value), batchSizes))),
				var value when value.StartsWith("https://www.youtube.com/watch?v=", StringComparison.Ordinal) =>
					CountAndReturn(ref watchPageRequestCount, NotFound()),
				_ => NotFound()
			};
		});

		var apiService = new YouTubeDataApiMetadataService(new SingleClientHttpClientFactory(httpClient), NullLogger<YouTubeDataApiMetadataService>.Instance);
		var service = CreateAcquisitionService(
			httpClient,
			channelMetadataFallbackAsync: (_, _, _) => Task.FromResult<ChannelPageMetadata?>(null),
			videoDiscoveryFallbackAsync: (_, _, _) => Task.FromResult<IReadOnlyList<ChannelVideoDiscoveryItem>>(Array.Empty<ChannelVideoDiscoveryItem>()),
			videoMetadataFallbackAsync: (_, _, _) => Task.FromResult<VideoWatchPageMetadata?>(null),
			youTubeDataApiMetadataService: apiService);

		var message = await service.PopulateVideoMetadataAsync(db, channel.Id);
		var updatedVideos = await db.Videos.OrderBy(v => v.YoutubeVideoId).ToListAsync();

		Assert.Equal("Updated metadata for 51 video(s).", message);
		Assert.Equal(2, videosListRequestCount);
		Assert.Equal(0, watchPageRequestCount);
		Assert.Equal(2, batchSizes.Count);
		Assert.All(batchSizes, size => Assert.InRange(size, 1, 50));
		Assert.Equal("Watch video-1", updatedVideos[0].Title);
		Assert.Equal(754, updatedVideos[0].Runtime);
	}

	static ChannelMetadataAcquisitionService CreateAcquisitionService(
		HttpClient httpClient,
		Func<TubeArrDbContext, string, CancellationToken, Task<ChannelPageMetadata?>> channelMetadataFallbackAsync,
		Func<TubeArrDbContext, string, CancellationToken, Task<IReadOnlyList<ChannelVideoDiscoveryItem>>> videoDiscoveryFallbackAsync,
		Func<TubeArrDbContext, string, CancellationToken, Task<VideoWatchPageMetadata?>> videoMetadataFallbackAsync,
		YouTubeDataApiMetadataService? youTubeDataApiMetadataService = null)
	{
		var factory = new SingleClientHttpClientFactory(httpClient);
		var channelPageMetadataService = new ChannelPageMetadataService(factory, NullLogger<ChannelPageMetadataService>.Instance);
		var channelVideoDiscoveryService = new ChannelVideoDiscoveryService(factory, NullLogger<ChannelVideoDiscoveryService>.Instance);
		var videoWatchPageMetadataService = new VideoWatchPageMetadataService(factory, NullLogger<VideoWatchPageMetadataService>.Instance);
		return new ChannelMetadataAcquisitionService(
			channelPageMetadataService,
			channelVideoDiscoveryService,
			videoWatchPageMetadataService,
			NullLogger<ChannelMetadataAcquisitionService>.Instance,
			channelMetadataFallbackAsync,
			videoDiscoveryFallbackAsync,
			videoMetadataFallbackAsync,
			youTubeDataApiMetadataService);
	}

	static async Task<TubeArrDbContext> CreateDbContextAsync()
	{
		var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();

		var options = new DbContextOptionsBuilder<TubeArrDbContext>()
			.UseSqlite(connection)
			.Options;
		var db = new TubeArrDbContext(options);
		await db.Database.MigrateAsync();
		return db;
	}

	static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
	{
		var client = new HttpClient(new RoutingHttpMessageHandler(responder))
		{
			BaseAddress = new Uri("https://www.youtube.com/")
		};
		return client;
	}

	static HttpResponseMessage Ok(string content) => new(HttpStatusCode.OK)
	{
		Content = new StringContent(content)
	};

	static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound)
	{
		Content = new StringContent(string.Empty)
	};

	static bool IsChannelPageUrl(string url)
	{
		return string.Equals(url.TrimEnd('/'), $"https://www.youtube.com/channel/{YoutubeChannelId}", StringComparison.Ordinal);
	}

	static bool IsChannelVideosUrl(string url)
	{
		return string.Equals(url.TrimEnd('/'), $"https://www.youtube.com/channel/{YoutubeChannelId}/videos", StringComparison.Ordinal);
	}

	static HttpResponseMessage CountAndReturn(ref int counter, HttpResponseMessage response)
	{
		counter++;
		return response;
	}

	static IReadOnlyList<string> ExtractVideoIdsFromUrl(string url)
	{
		var query = new Uri(url).Query.TrimStart('?');
		var idPart = query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.FirstOrDefault(part => part.StartsWith("id=", StringComparison.Ordinal));
		if (string.IsNullOrWhiteSpace(idPart))
			return Array.Empty<string>();

		return idPart[3..]
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(id => WebUtility.UrlDecode(id) ?? id)
			.ToArray();
	}

	static string BuildVideosListApiResponse(IEnumerable<string> videoIds, List<int>? batchSizes = null)
	{
		var ids = videoIds.ToArray();
		batchSizes?.Add(ids.Length);

		var items = ids.Select(id => new
		{
			id,
			snippet = new
			{
				title = $"Watch {id}",
				description = $"Watch Description {id}",
				publishedAt = "2024-01-02T00:00:00Z",
				thumbnails = new
				{
					high = new { url = $"https://img.example/{id}.jpg" }
				}
			},
			contentDetails = new
			{
				duration = "PT12M34S"
			}
		});

		return JsonSerializer.Serialize(new { items });
	}

	static string BuildChannelPageHtml(string title, string description)
	{
		return $$"""
		<html>
		<head>
		  <meta property="og:title" content="{{title}}" />
		  <meta property="og:description" content="{{description}}" />
		  <meta property="og:image" content="https://img.example/avatar.jpg" />
		</head>
		<body>
		  <script>
		    var ytInitialData = {
		      "externalId":"{{YoutubeChannelId}}",
		      "metadata":{
		        "channelMetadataRenderer":{
		          "title":{"simpleText":"{{title}}"},
		          "description":"{{description}}",
		          "avatar":{"thumbnails":[{"url":"https://img.example/avatar.jpg"}],"url":"https://img.example/avatar.jpg"},
		          "banner":{"thumbnails":[{"url":"https://img.example/banner.jpg"}],"url":"https://img.example/banner.jpg"}
		        }
		      }
		    };
		  </script>
		</body>
		</html>
		""";
	}

	static string BuildListingHtml()
	{
		return """
		<html><body><script>
		var ytInitialData = {
		  "contents": {
		    "twoColumnBrowseResultsRenderer": {
		      "tabs": [
		        {
		          "tabRenderer": {
		            "content": {
		              "richGridRenderer": {
		                "contents": [
		                  {
		                    "richItemRenderer": {
		                      "content": {
		                        "videoRenderer": {
		                          "videoId": "video-1",
		                          "title": { "runs": [ { "text": "Discovery One" } ] },
		                          "thumbnail": {
		                            "thumbnails": [
		                              { "url": "https://img.example/video-1.jpg" }
		                            ]
		                          },
		                          "thumbnailOverlays": [
		                            {
		                              "thumbnailOverlayTimeStatusRenderer": {
		                                "text": { "simpleText": "12:34" }
		                              }
		                            }
		                          ]
		                        }
		                      }
		                    }
		                  },
		                  {
		                    "richItemRenderer": {
		                      "content": {
		                        "videoRenderer": {
		                          "videoId": "video-2",
		                          "thumbnail": {
		                            "thumbnails": [
		                              { "url": "https://img.example/video-2.jpg" }
		                            ]
		                          }
		                        }
		                      }
		                    }
		                  }
		                ]
		              }
		            }
		          }
		        }
		      ]
		    }
		  }
		};
		</script></body></html>
		""";
	}

	static string BuildShortsListingHtmlWithReelOnRenderer()
	{
		return """
		<html><body><script>
		var ytInitialData = {
		  "contents": {
		    "twoColumnBrowseResultsRenderer": {
		      "tabs": [
		        {
		          "tabRenderer": {
		            "content": {
		              "richGridRenderer": {
		                "contents": [
		                  {
		                    "richItemRenderer": {
		                      "content": {
		                        "reelWatchEndpoint": {
		                          "videoId": "shortDirect11",
		                          "thumbnail": {
		                            "thumbnails": [
		                              { "url": "https://i.ytimg.com/short.jpg" }
		                            ]
		                          }
		                        }
		                      }
		                    }
		                  }
		                ]
		              }
		            }
		          }
		        }
		      ]
		    }
		  }
		};
		</script></body></html>
		""";
	}

	static string BuildShortsListingHtml()
	{
		return """
		<html><body><script>
		var ytInitialData = {
		  "contents": {
		    "twoColumnBrowseResultsRenderer": {
		      "tabs": [
		        {
		          "tabRenderer": {
		            "content": {
		              "richGridRenderer": {
		                "contents": [
		                  {
		                    "richItemRenderer": {
		                      "content": {
		                        "shortsLockupViewModel": {
		                          "accessibilityText": "Short title one, 12 views - play Short",
		                          "onTap": {
		                            "innertubeCommand": {
		                              "reelWatchEndpoint": {
		                                "videoId": "shortA1111111",
		                                "thumbnail": {
		                                  "thumbnails": [
		                                    { "url": "https://i.ytimg.com/vi/shortA1111111/frame0.jpg", "width": 1080, "height": 1920 }
		                                  ]
		                                }
		                              }
		                            }
		                          }
		                        }
		                      }
		                    }
		                  },
		                  {
		                    "richItemRenderer": {
		                      "content": {
		                        "shortsLockupViewModel": {
		                          "accessibilityText": "Short title two, 34 views - play Short",
		                          "onTap": {
		                            "innertubeCommand": {
		                              "reelWatchEndpoint": {
		                                "videoId": "shortB2222222",
		                                "thumbnail": {
		                                  "thumbnails": [
		                                    { "url": "https://i.ytimg.com/vi/shortB2222222/frame0.jpg", "width": 1080, "height": 1920 }
		                                  ]
		                                }
		                              }
		                            }
		                          }
		                        }
		                      }
		                    }
		                  }
		                ]
		              }
		            }
		          }
		        }
		      ]
		    }
		  }
		};
		</script></body></html>
		""";
	}

	static string BuildWatchPageHtml(string title, string description, string uploadDate, int runtime)
	{
		return $$"""
		<html>
		<head>
		  <meta property="og:title" content="{{title}}" />
		  <meta property="og:description" content="{{description}}" />
		  <meta property="og:image" content="https://img.example/{{title.Replace(" ", "-").ToLowerInvariant()}}.jpg" />
		  <meta itemprop="uploadDate" content="{{uploadDate}}" />
		</head>
		<body>
		  <script>
		    var ytInitialPlayerResponse = {
		      "videoDetails": {
		        "title": "{{title}}",
		        "shortDescription": "{{description}}",
		        "lengthSeconds": "{{runtime}}",
		        "thumbnail": {
		          "thumbnails": [
		            { "url": "https://img.example/{{title.Replace(" ", "-").ToLowerInvariant()}}.jpg" }
		          ]
		        }
		      },
		      "microformat": {
		        "playerMicroformatRenderer": {
		          "uploadDate": "{{uploadDate}}",
		          "publishDate": "{{uploadDate}}",
		          "thumbnail": {
		            "thumbnails": [
		              { "url": "https://img.example/{{title.Replace(" ", "-").ToLowerInvariant()}}.jpg" }
		            ]
		          }
		        }
		      }
		    };
		  </script>
		</body>
		</html>
		""";
	}

	sealed class SingleClientHttpClientFactory : IHttpClientFactory
	{
		readonly HttpClient _client;

		public SingleClientHttpClientFactory(HttpClient client)
		{
			_client = client;
		}

		public HttpClient CreateClient(string name)
		{
			return _client;
		}
	}

	sealed class RoutingHttpMessageHandler : HttpMessageHandler
	{
		readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

		public RoutingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
		{
			_responder = responder;
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return Task.FromResult(_responder(request));
		}
	}
}
