using Xunit;
using TubeArr.Backend.Data;
using TubeArr.Backend.QualityProfile;

namespace TubeArr.Backend.Tests.QualityProfile;

public class QualityProfileValidationTests
{
	[Fact]
	public void Validates_minHeight_greater_than_maxHeight()
	{
		var profile = new QualityProfileEntity
		{
			Name = "Bad",
			MinHeight = 1080,
			MaxHeight = 720
		};
		var errors = QualityProfileValidation.Validate(profile);
		Assert.Contains(errors, e => e.Contains("minHeight") && e.Contains("maxHeight"));
	}

	[Fact]
	public void Validates_minFps_greater_than_maxFps()
	{
		var profile = new QualityProfileEntity
		{
			Name = "Bad",
			MinFps = 60,
			MaxFps = 24
		};
		var errors = QualityProfileValidation.Validate(profile);
		Assert.Contains(errors, e => e.Contains("minFps") && e.Contains("maxFps"));
	}

	[Fact]
	public void Validates_both_hdr_and_sdr_disallowed()
	{
		var profile = new QualityProfileEntity
		{
			Name = "Bad",
			AllowHdr = false,
			AllowSdr = false
		};
		var errors = QualityProfileValidation.Validate(profile);
		Assert.Contains(errors, e => e.Contains("allowHdr") || e.Contains("allowSdr"));
	}

	[Fact]
	public void Accepts_valid_profile()
	{
		var profile = new QualityProfileEntity
		{
			Name = "1080p",
			MaxHeight = 1080,
			MinHeight = 720,
			AllowHdr = true,
			AllowSdr = true,
			PreferSeparateStreams = true,
			AllowMuxedFallback = true
		};
		var errors = QualityProfileValidation.Validate(profile);
		Assert.Empty(errors);
	}
}
