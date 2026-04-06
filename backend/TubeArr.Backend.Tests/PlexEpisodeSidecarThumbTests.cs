using Microsoft.AspNetCore.Http;
using TubeArr.Backend.Data;
using TubeArr.Backend.Plex;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class PlexEpisodeSidecarThumbTests
{
	[Fact]
	public void TryGetExistingSidecarPath_returns_null_when_missing()
	{
		Assert.Null(PlexEpisodeSidecarPaths.TryGetExistingSidecarPath(null));
		Assert.Null(PlexEpisodeSidecarPaths.TryGetExistingSidecarPath(@"Z:\no\such\file.mp4"));
	}

	[Fact]
	public void TryGetExistingSidecarPath_finds_thumb_next_to_media()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-plex-thumb-test-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			var media = Path.Combine(dir, "Video [abc123].mkv");
			File.WriteAllText(media, "x");
			var thumb = Path.Combine(dir, "Video [abc123]-thumb.jpg");
			File.WriteAllBytes(thumb, [0xff, 0xd8, 0xff]);

			Assert.Equal(thumb, PlexEpisodeSidecarPaths.TryGetExistingSidecarPath(media));
		}
		finally
		{
			try { Directory.Delete(dir, true); } catch { /* best-effort */ }
		}
	}

	[Fact]
	public void BuildTvProviderRootUrl_strips_suffix_after_tv()
	{
		var ctx = new DefaultHttpContext();
		ctx.Request.Scheme = "https";
		ctx.Request.Host = new HostString("plex.example.org");
		ctx.Request.PathBase = "";
		ctx.Request.Path = "/tv/library/metadata/v_dQw4w9WgXcQ";
		Assert.Equal("https://plex.example.org/tv", PlexPublicUrls.BuildTvProviderRootUrl(ctx.Request));
	}

	[Fact]
	public void BuildTvProviderRootUrl_includes_pathbase_and_plex_prefix()
	{
		var ctx = new DefaultHttpContext();
		ctx.Request.Scheme = "http";
		ctx.Request.Host = new HostString("localhost:5075");
		ctx.Request.PathBase = "/tube";
		ctx.Request.Path = "/plex/tv/library/metadata/ch_UCxyz";
		Assert.Equal("http://localhost:5075/tube/plex/tv", PlexPublicUrls.BuildTvProviderRootUrl(ctx.Request));
	}

	[Fact]
	public void ResolveEpisodeThumbForPlex_uses_sidecar_url_when_file_exists()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-plex-resolve-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			var media = Path.Combine(dir, "ep.mkv");
			File.WriteAllText(media, "x");
			File.WriteAllBytes(Path.Combine(dir, "ep-thumb.jpg"), [0xff, 0xd8, 0xff]);

			var ctx = new DefaultHttpContext();
			ctx.Request.Scheme = "http";
			ctx.Request.Host = new HostString("host");
			ctx.Request.Path = "/tv/x";

			var v = new VideoEntity { YoutubeVideoId = "abcXYZ", ThumbnailUrl = "https://i.ytimg.com/vi/abcXYZ/hqdefault.jpg" };
			var url = PlexArtworkResolver.ResolveEpisodeThumbForPlex(ctx.Request, v, media);
			Assert.NotNull(url);
			Assert.StartsWith("http://host/tv/artwork/episode-thumb?youtubeVideoId=", url, StringComparison.Ordinal);
			Assert.Contains("abcXYZ", url, StringComparison.Ordinal);

			var urlExposeOff = PlexArtworkResolver.ResolveEpisodeThumbForPlex(ctx.Request, v, media, exposeRemoteArtworkUrls: false);
			Assert.Equal(url, urlExposeOff);
		}
		finally
		{
			try { Directory.Delete(dir, true); } catch { /* best-effort */ }
		}
	}

	[Fact]
	public void ResolveEpisodeThumbForPlex_remote_suppressed_when_expose_off_and_no_sidecar()
	{
		var ctx = new DefaultHttpContext();
		ctx.Request.Scheme = "http";
		ctx.Request.Host = new HostString("host");
		ctx.Request.Path = "/tv/x";

		var v = new VideoEntity { YoutubeVideoId = "abcXYZ", ThumbnailUrl = "https://i.ytimg.com/vi/abcXYZ/hqdefault.jpg" };
		Assert.Null(PlexArtworkResolver.ResolveEpisodeThumbForPlex(ctx.Request, v, @"Z:\no\sidecar.mkv", exposeRemoteArtworkUrls: false));
		Assert.NotNull(PlexArtworkResolver.ResolveEpisodeThumbForPlex(ctx.Request, v, @"Z:\no\sidecar.mkv", exposeRemoteArtworkUrls: true));
	}
}
