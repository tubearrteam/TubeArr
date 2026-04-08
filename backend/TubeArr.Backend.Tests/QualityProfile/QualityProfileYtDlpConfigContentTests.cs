using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using TubeArr.Backend.Data;
using TubeArr.Backend.QualityProfile;

namespace TubeArr.Backend.Tests.QualityProfile;

public class QualityProfileYtDlpConfigContentTests
{
	[Fact]
	public void RemoveYoutubeExtractorArgsFromArgv_strips_two_token_pair()
	{
		var argv = new List<string> { "-f", "best", "--extractor-args", "youtube:fetch_pot=always;player_client=tv", "URL" };
		QualityProfileYtDlpConfigContent.RemoveYoutubeExtractorArgsFromArgv(argv);
		Assert.Equal(new[] { "-f", "best", "URL" }, argv);
	}

	[Fact]
	public void RemoveYoutubeExtractorArgsFromArgv_strips_inline_form()
	{
		var argv = new List<string> { "--extractor-args=youtube:player_client=web" };
		QualityProfileYtDlpConfigContent.RemoveYoutubeExtractorArgsFromArgv(argv);
		Assert.Empty(argv);
	}

	[Fact]
	public void RemoveYoutubeExtractorArgsFromArgv_keeps_non_youtube_extractor_args()
	{
		var argv = new List<string> { "--extractor-args", "something-else:value" };
		QualityProfileYtDlpConfigContent.RemoveYoutubeExtractorArgsFromArgv(argv);
		Assert.Equal(new[] { "--extractor-args", "something-else:value" }, argv);
	}

	[Fact]
	public void BuildMergedDownloadConfigBody_appends_o_and_ffmpeg()
	{
		var merged = QualityProfileYtDlpConfigContent.BuildMergedDownloadConfigBody("-f\nbest", @"D:\out\%(title)s.%(ext)s", @"C:\ffmpeg\bin\ffmpeg.exe");
		Assert.Contains("-f", merged, StringComparison.Ordinal);
		Assert.Contains("-o ", merged, StringComparison.Ordinal);
		Assert.Contains("D:/out/%(title)s.%(ext)s", merged, StringComparison.Ordinal);
		Assert.Contains("--ffmpeg-location", merged, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("C:/ffmpeg/bin/ffmpeg.exe", merged, StringComparison.Ordinal);
	}

	[Fact]
	public void BuildMergedDownloadConfigBody_quotes_output_template_with_spaces_so_ytdlp_does_not_split_urls()
	{
		var merged = QualityProfileYtDlpConfigContent.BuildMergedDownloadConfigBody(
			"-f\nbest",
			@"D:\videos\ch\%(upload_date)s - %(title)s [%(id)s].%(ext)s",
			null);
		Assert.Contains("-o \"D:/videos/ch/%(upload_date)s - %(title)s [%(id)s].%(ext)s\"", merged, StringComparison.Ordinal);
	}

	[Fact]
	public void BuildMergedDownloadConfigBody_appends_cookies_last_when_file_exists()
	{
		var temp = Path.Combine(Path.GetTempPath(), "tubearr-merged-cookies-" + Guid.NewGuid().ToString("N") + ".txt");
		File.WriteAllText(temp, "# Netscape\n");

		try
		{
			var merged = QualityProfileYtDlpConfigContent.BuildMergedDownloadConfigBody("-f\nbest", @"D:\out\%(title)s.%(ext)s", null, temp);
			Assert.StartsWith("-f", merged.TrimStart(), StringComparison.Ordinal);
			var lastCookies = merged.LastIndexOf("--cookies", StringComparison.Ordinal);
			Assert.True(lastCookies >= 0);
			Assert.True(merged.IndexOf("-o ", StringComparison.Ordinal) < lastCookies);
			Assert.Contains(Path.GetFullPath(temp).Replace('\\', '/'), merged, StringComparison.OrdinalIgnoreCase);
		}
		finally
		{
			try
			{
				File.Delete(temp);
			}
			catch
			{
				/* best-effort */
			}
		}
	}

	[Fact]
	public void RemoveCookiesDirectivesFromConfigText_strips_cookies_and_no_cookies()
	{
		var raw = "--no-cookies\n--cookies\nc:\\bad.txt\n-f\nbest\n";
		var got = QualityProfileYtDlpConfigContent.RemoveCookiesDirectivesFromConfigText(raw);
		Assert.DoesNotContain("cookies", got, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("-f", got, StringComparison.Ordinal);
	}

	[Fact]
	public void SanitizeConfigTextForYtDlp_removes_youtube_extractor_block()
	{
		var raw = "--no-warnings\n--extractor-args\nyoutube:fetch_pot=always\n-f\nbest\n";
		var got = QualityProfileYtDlpConfigContent.SanitizeConfigTextForYtDlp(raw);
		Assert.DoesNotContain("extractor-args", got, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("youtube:", got, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("-f", got, StringComparison.Ordinal);
	}

	[Fact]
	public void BuildConfigFileBodyFromEntity_includes_advanced_arg_buckets_in_config_text()
	{
		var profile = new QualityProfileEntity
		{
			Id = 42,
			Name = "test",
			FallbackMode = 1,
			PreferSeparateStreams = true,
			AllowMuxedFallback = true,
			SelectionArgs = "--no-cache-dir",
			MuxArgs = "--embed-metadata"
		};
		var body = QualityProfileYtDlpConfigContent.BuildConfigFileBodyFromEntity(profile, ffmpegConfigured: false, logger: null, logContextId: profile.Id);
		Assert.Contains("--no-cache-dir", body, StringComparison.Ordinal);
		Assert.Contains("--embed-metadata", body, StringComparison.Ordinal);
	}
}
