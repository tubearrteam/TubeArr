using Xunit;
using TubeArr.Backend;

namespace TubeArr.Backend.Tests;

public class RemoteUpdateCatalogTests
{
	[Theory]
	[InlineData("v0.8.5", "0.8.5")]
	[InlineData("0.8.5", "0.8.5")]
	[InlineData("V1.2.3", "1.2.3")]
	public void TryParseReleaseVersion_normalizes_tags(string tag, string expected)
	{
		Assert.True(RemoteUpdateCatalog.TryParseReleaseVersion(tag, out var norm, out _));
		Assert.Equal(expected, norm);
	}

	[Fact]
	public void TryParseReleaseVersion_rejects_prerelease_noise_after_strip()
	{
		Assert.True(RemoteUpdateCatalog.TryParseReleaseVersion("1.0.0-rc1", out var norm, out _));
		Assert.Equal("1.0.0", norm);
	}
}
