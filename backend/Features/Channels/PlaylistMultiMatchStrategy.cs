namespace TubeArr.Backend;

/// <summary>When a video belongs to multiple curated playlists, which playlist wins for on-disk folder layout and primary playlist id.</summary>
public enum PlaylistMultiMatchStrategy : int
{
	/// <summary>Highest playlist activity first (latest video upload in playlist), then title — matches historical TubeArr behavior.</summary>
	LatestPlaylistActivity = 0,

	/// <summary>Lexicographic by playlist title, then id.</summary>
	AlphabeticalByTitle = 1,

	/// <summary>Newest playlist row first (<see cref="Data.PlaylistEntity.Added"/> descending).</summary>
	NewestPlaylistAdded = 2,

	/// <summary>Oldest playlist row first (<see cref="Data.PlaylistEntity.Added"/> ascending).</summary>
	OldestPlaylistAdded = 3
}
