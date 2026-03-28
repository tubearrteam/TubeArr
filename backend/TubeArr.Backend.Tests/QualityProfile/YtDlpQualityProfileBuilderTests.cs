using System.Linq;
using Xunit;
using TubeArr.Backend.Data;
using TubeArr.Backend.QualityProfile;

namespace TubeArr.Backend.Tests.QualityProfile;

public class YtDlpQualityProfileBuilderTests
{
	static QualityProfileEntity Profile(int id, string name, int? maxHeight = 1080, int? minHeight = 720,
		string? preferredVideoCodecsJson = null, string? preferredAudioCodecsJson = null,
		bool allowMuxedFallback = true, int fallbackMode = 1)
	{
		return new QualityProfileEntity
		{
			Id = id,
			Name = name,
			Enabled = true,
			MaxHeight = maxHeight,
			MinHeight = minHeight,
			MinFps = 0,
			MaxFps = 60,
			AllowHdr = true,
			AllowSdr = true,
			PreferredVideoCodecsJson = preferredVideoCodecsJson ?? "[\"AV1\",\"VP9\",\"AVC\"]",
			PreferredAudioCodecsJson = preferredAudioCodecsJson ?? "[\"OPUS\",\"MP4A\"]",
			PreferSeparateStreams = true,
			AllowMuxedFallback = allowMuxedFallback,
			FallbackMode = fallbackMode
		};
	}

	[Fact]
	public void Builds_selector_for_1080p_max_profile()
	{
		var profile = Profile(1, "1080p", maxHeight: 1080, minHeight: 720);
		var builder = new YtDlpQualityProfileBuilder();
		var result = builder.Build(profile);

		Assert.Contains("height<=1080", result.Selector);
		Assert.Contains("height>=720", result.Selector);
		Assert.Contains("bv*", result.Selector);
		Assert.Contains("+ba", result.Selector);
	}

	[Fact]
	public void Prefers_AV1_over_VP9_over_AVC_in_sort()
	{
		var profile = Profile(1, "Default", preferredVideoCodecsJson: "[\"AV1\",\"VP9\",\"AVC\"]");
		var builder = new YtDlpQualityProfileBuilder();
		var result = builder.Build(profile);

		Assert.Contains("vcodec", result.Sort);
	}

	[Fact]
	public void Respects_max_height_ceiling()
	{
		var profile = Profile(1, "720p", maxHeight: 720, minHeight: 360);
		var builder = new YtDlpQualityProfileBuilder();
		var result = builder.Build(profile);

		Assert.Contains("height<=720", result.Selector);
		Assert.DoesNotContain("height<=1080", result.Selector);
	}

	[Fact]
	public void Respects_min_height_floor()
	{
		var profile = Profile(1, "Min720", maxHeight: 1080, minHeight: 720);
		var builder = new YtDlpQualityProfileBuilder();
		var result = builder.Build(profile);

		Assert.Contains("height>=720", result.Selector);
	}

	[Fact]
	public void Muxed_fallback_included_only_when_enabled()
	{
		var withMuxed = Profile(1, "With", allowMuxedFallback: true);
		var withoutMuxed = Profile(2, "Without", allowMuxedFallback: false);
		var builder = new YtDlpQualityProfileBuilder();

		var resultWith = builder.Build(withMuxed);
		var resultWithout = builder.Build(withoutMuxed);

		Assert.Contains("/b", resultWith.Selector);
		Assert.DoesNotContain("/b", resultWithout.Selector);
	}

	[Fact]
	public void Strict_mode_has_no_fallback_alternatives()
	{
		var profile = Profile(1, "Strict", fallbackMode: 0); // Strict
		var builder = new YtDlpQualityProfileBuilder();
		var result = builder.Build(profile);

		Assert.Contains("Strict", result.FallbackPlanSummary);
		Assert.Single(result.Selector.Split('/'));
	}

	[Fact]
	public void Generated_selector_uses_only_supported_ytdlp_fields()
	{
		var profile = Profile(1, "Full");
		var builder = new YtDlpQualityProfileBuilder();
		var result = builder.Build(profile);

		var selector = result.Selector;
		// Must not contain unsupported abstractions
		Assert.DoesNotContain("format_id", selector);
		Assert.DoesNotContain("itag", selector);
		// Should contain supported
		Assert.Contains("height", selector);
		Assert.Contains("bv*", selector);
	}

	[Fact]
	public void Sort_uses_only_supported_keys()
	{
		var profile = Profile(1, "Default");
		var builder = new YtDlpQualityProfileBuilder();
		var result = builder.Build(profile);

		Assert.Contains("vcodec", result.Sort);
		Assert.Contains("res", result.Sort);
		Assert.Contains("acodec", result.Sort);
	}

	[Fact]
	public void Codec_filters_avoid_single_quotes_for_ytdlp_config_compat()
	{
		var profile = Profile(1, "AVC only");
		profile.AllowedVideoCodecsJson = "[\"AVC\"]";
		profile.AllowedAudioCodecsJson = "[\"MP4A\"]";
		var builder = new YtDlpQualityProfileBuilder();
		var result = builder.Build(profile);

		Assert.Contains("vcodec^=avc", result.Selector);
		Assert.Contains("acodec^=mp4a", result.Selector);
	}

	[Fact]
	public void Multiple_allowed_codecs_use_double_quoted_regex()
	{
		var profile = Profile(1, "Balanced");
		profile.AllowedVideoCodecsJson = "[\"AVC\",\"VP9\",\"AV1\"]";
		profile.AllowedAudioCodecsJson = "[\"MP4A\",\"OPUS\"]";
		var builder = new YtDlpQualityProfileBuilder();
		var result = builder.Build(profile);

		Assert.Contains("vcodec~=\"^(avc|vp9|av01)\"", result.Selector);
		Assert.Contains("acodec~=\"^(mp4a|opus)\"", result.Selector);
	}

	[Fact]
	public void YtDlpArgs_contains_f_and_S()
	{
		var profile = Profile(1, "Args");
		var builder = new YtDlpQualityProfileBuilder();
		var result = builder.Build(profile);

		Assert.Contains("-f", result.YtDlpArgs);
		Assert.Contains("-S", result.YtDlpArgs);
		var list = result.YtDlpArgs.ToList();
		var fIdx = list.IndexOf("-f");
		Assert.True(fIdx >= 0 && fIdx + 1 < list.Count);
		Assert.Equal(result.Selector, list[fIdx + 1]);
	}
}
