using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class LibraryThumbnailExportPolicyTests
{
	[Fact]
	public void ShouldExport_IsTrueWhenEitherFlagIsTrue()
	{
		Assert.True(LibraryThumbnailExportPolicy.ShouldExport(downloadLibraryThumbnails: true, plexProviderEnabled: false));
		Assert.True(LibraryThumbnailExportPolicy.ShouldExport(downloadLibraryThumbnails: false, plexProviderEnabled: true));
		Assert.True(LibraryThumbnailExportPolicy.ShouldExport(downloadLibraryThumbnails: true, plexProviderEnabled: true));
	}

	[Fact]
	public void ShouldExport_IsFalseWhenBothOff()
	{
		Assert.False(LibraryThumbnailExportPolicy.ShouldExport(downloadLibraryThumbnails: false, plexProviderEnabled: false));
	}
}
