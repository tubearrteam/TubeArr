using System.Text;
using Microsoft.Extensions.Logging;
using TubeArr.Backend.Data;

namespace TubeArr.Backend.QualityProfile;

public static class QualityProfileConfigFileOperations
{
	static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

	public static string ReadConfigTextOrEmpty(string contentRoot, int profileId)
	{
		var path = QualityProfileConfigPaths.GetConfigFilePath(contentRoot, profileId);
		return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
	}

	public static async Task<string> ReadConfigTextOrEmptyAsync(string contentRoot, int profileId, CancellationToken cancellationToken = default)
	{
		var path = QualityProfileConfigPaths.GetConfigFilePath(contentRoot, profileId);
		if (!File.Exists(path))
			return string.Empty;
		return await File.ReadAllTextAsync(path, cancellationToken);
	}

	public static void WriteConfigText(string contentRoot, int profileId, string text)
	{
		var path = QualityProfileConfigPaths.GetConfigFilePath(contentRoot, profileId);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, text ?? string.Empty, Utf8NoBom);
	}

	public static void DeleteProfileDirectory(string contentRoot, int profileId)
	{
		var dir = QualityProfileConfigPaths.GetProfileDirectory(contentRoot, profileId);
		if (!Directory.Exists(dir))
			return;
		try
		{
			Directory.Delete(dir, recursive: true);
		}
		catch
		{
			/* best-effort */
		}
	}

	/// <summary>
	/// If config.txt is missing, create it from the structured DB entity (legacy compatibility / first run).
	/// </summary>
	public static async Task EnsureConfigFileExistsAsync(
		string contentRoot,
		QualityProfileEntity profile,
		bool ffmpegConfigured,
		ILogger? logger,
		CancellationToken cancellationToken)
	{
		var path = QualityProfileConfigPaths.GetConfigFilePath(contentRoot, profile.Id);
		if (File.Exists(path))
			return;
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		var body = QualityProfileYtDlpConfigContent.BuildConfigFileBodyFromEntity(profile, ffmpegConfigured, logger, profile.Id);
		await File.WriteAllTextAsync(path, body, Utf8NoBom, cancellationToken);
		logger?.LogInformation("Created default quality profile config profileId={ProfileId} path={Path}", profile.Id, path);
	}
}
