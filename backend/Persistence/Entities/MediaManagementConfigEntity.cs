namespace TubeArr.Backend.Data;

public sealed class MediaManagementConfigEntity
{
	public int Id { get; set; } = 1;

	public bool CreateEmptyChannelFolders { get; set; } = false;
	public bool DeleteEmptyFolders { get; set; } = false;

	public string VideoTitleRequired { get; set; } = "always";
	public bool SkipFreeSpaceCheckWhenImporting { get; set; } = false;
	public int MinimumFreeSpaceWhenImporting { get; set; } = 100;

	public bool CopyUsingHardlinks { get; set; } = false;
	public bool UseScriptImport { get; set; } = false;
	public string ScriptImportPath { get; set; } = "";

	public bool ImportExtraFiles { get; set; } = false;
	public string ExtraFileExtensions { get; set; } = "";

	public bool AutoUnmonitorPreviouslyDownloadedVideos { get; set; } = false;
	public string DownloadPropersAndRepacks { get; set; } = "preferAndUpgrade";
	public bool EnableMediaInfo { get; set; } = true;

	/// <summary>When true, write tvshow/season/episode NFO files next to completed downloads (Kodi/Plex-style).</summary>
	public bool UseCustomNfos { get; set; } = true;

	/// <summary>When true (or when the Plex metadata provider is enabled), write episode thumbnail sidecars next to library files.</summary>
	public bool DownloadLibraryThumbnails { get; set; } = false;

	public string RescanAfterRefresh { get; set; } = "afterManual";
	public string FileDate { get; set; } = "none";

	public string RecycleBin { get; set; } = "";
	public int RecycleBinCleanupDays { get; set; } = 7;

	public bool SetPermissionsLinux { get; set; } = false;
	public string ChmodFolder { get; set; } = "775";
	public string ChownGroup { get; set; } = "";
}

