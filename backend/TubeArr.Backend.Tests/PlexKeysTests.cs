using TubeArr.Backend.Plex;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class PlexKeysTests
{
	[Fact]
	public void LibraryMetadata_is_relative_to_provider_root()
	{
		Assert.Equal("/library/metadata/ch_UCabc", PlexKeys.LibraryMetadata("ch_UCabc"));
	}

	[Fact]
	public void LibraryMetadataChildren_matches_Plex_show_season_key_shape()
	{
		Assert.Equal("/library/metadata/pl_PL123/children", PlexKeys.LibraryMetadataChildren("pl_PL123"));
	}
}
