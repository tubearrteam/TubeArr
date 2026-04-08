using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class UnmappedVideoFileMappingRunnerTests
{
	[Fact]
	public void TitleLooksLikeFileName_returns_true_when_most_title_words_present()
	{
		var ok = UnmappedVideoFileMappingRunner.TitleLooksLikeFileName(
			"My Cool Video",
			@"C:\media\Sample Channel\2026-01-01 - My Cool Video [dQw4w9WgXcQ].mp4");

		Assert.True(ok);
	}

	[Fact]
	public void TitleLooksLikeFileName_ignores_bracketed_segments()
	{
		var ok = UnmappedVideoFileMappingRunner.TitleLooksLikeFileName(
			"After Show Austrian Audio Hi-X65",
			@"D:\Youtube\DankPods\Season 10001\2024-01-01 - After Show Austrian Audio Hi-X65 [K-5AYeleTU].mp4");

		Assert.True(ok);
	}

	[Fact]
	public void TitleLooksLikeFileName_returns_false_when_title_is_unrelated()
	{
		var ok = UnmappedVideoFileMappingRunner.TitleLooksLikeFileName(
			"Completely Different Thing",
			@"C:\media\Some Folder\After Show Austrian Audio Hi-X65.mp4");

		Assert.False(ok);
	}

	[Fact]
	public void ChannelLooksLikePath_returns_true_when_channel_word_is_in_path()
	{
		var ok = UnmappedVideoFileMappingRunner.ChannelLooksLikePath(
			"DankPods",
			"UCxxxxxxxxxxxxxxxxxxxx",
			@"D:\Youtube\DankPods\Season 10001\video.mp4");

		Assert.True(ok);
	}

	[Fact]
	public void ChannelLooksLikePath_returns_true_when_channel_id_is_in_path()
	{
		var ok = UnmappedVideoFileMappingRunner.ChannelLooksLikePath(
			"Some Channel",
			"UC1234567890abcdefghiJ",
			@"D:\Youtube\UC1234567890abcdefghiJ\video.mp4");

		Assert.True(ok);
	}

	[Fact]
	public void ChannelLooksLikePath_returns_false_when_no_channel_hint_in_path()
	{
		var ok = UnmappedVideoFileMappingRunner.ChannelLooksLikePath(
			"DankPods",
			"UCxxxxxxxxxxxxxxxxxxxx",
			@"D:\Youtube\OtherChannel\Season 10001\video.mp4");

		Assert.False(ok);
	}

	[Fact]
	public void TitleFallback_normalization_is_resilient_to_separators()
	{
		var ok = UnmappedVideoFileMappingRunner.TitleLooksLikeFileName(
			"I Joined The Minecraft Multiverse",
			@"D:\Youtube\Kolanii\Kolanii - S02E16 - I Joined The Minecraft Multiverse.mp4");

		Assert.True(ok);
	}

	[Fact]
	public void TitleLooksLikeFileName_strips_common_quality_tokens_in_filename()
	{
		var ok = UnmappedVideoFileMappingRunner.TitleLooksLikeFileName(
			"My Cool Video",
			@"C:\media\ch\My Cool Video 1080p WEBRip x264.mkv");

		Assert.True(ok);
	}
}

