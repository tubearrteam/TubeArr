namespace TubeArr.Backend.QualityProfile;


/// <summary>
/// On-disk layout for yt-dlp quality profiles: one folder per profile id, single config.txt (yt-dlp config format).
/// </summary>
public static class QualityProfileConfigPaths
{
	public const string ConfigFileName = "config.txt";

	public static string GetProfileDirectory(string contentRoot, int profileId) =>
		Path.Combine(contentRoot, "quality-profiles", profileId.ToString());

	public static string GetConfigFilePath(string contentRoot, int profileId) =>
		Path.Combine(GetProfileDirectory(contentRoot, profileId), ConfigFileName);
}
