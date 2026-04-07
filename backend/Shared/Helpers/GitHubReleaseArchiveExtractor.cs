using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace TubeArr.Backend;

/// <summary>Extract GitHub release downloads (zip, tar.gz, tar.xz) with zip-slip protection for tar.</summary>
internal static class GitHubReleaseArchiveExtractor
{
	enum ArchiveKind
	{
		None,
		Zip,
		TarGz,
		TarXz
	}

	internal static bool IsArchiveAssetName(string assetName)
	{
		var n = assetName.ToLowerInvariant();
		return n.EndsWith(".zip", StringComparison.Ordinal)
			|| n.EndsWith(".tar.gz", StringComparison.Ordinal)
			|| n.EndsWith(".tgz", StringComparison.Ordinal)
			|| n.EndsWith(".tar.xz", StringComparison.Ordinal)
			|| n.EndsWith(".txz", StringComparison.Ordinal);
	}

	internal static string BuildSanitizedExtractFolderName(string? releaseTag, string assetName)
	{
		var tagName = string.IsNullOrWhiteSpace(releaseTag)
			? Path.GetFileNameWithoutExtension(assetName)
			: releaseTag.Trim();
		// Strip a second extension for *.tar.gz / *.tar.xz so folder is not "release.tar".
		tagName = Path.GetFileNameWithoutExtension(tagName);
		var sanitized = string.Join("_", Regex.Replace(tagName, @"[^a-zA-Z0-9.-]", "_").Split(['_'], StringSplitOptions.RemoveEmptyEntries));
		return string.IsNullOrWhiteSpace(sanitized) ? "build" : sanitized;
	}

	internal static async Task<(bool Ok, string? Error)> TryExtractToDirectoryAsync(string archivePath, string extractDir, CancellationToken ct)
	{
		Directory.CreateDirectory(extractDir);
		var kind = ClassifyArchive(Path.GetFileName(archivePath));
		try
		{
			switch (kind)
			{
				case ArchiveKind.Zip:
					await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, extractDir, overwriteFiles: true), ct).ConfigureAwait(false);
					return (true, null);
				case ArchiveKind.TarGz:
				{
					var (tarOk, tarErr) = await TryExtractWithTarCliAsync(archivePath, extractDir, ct).ConfigureAwait(false);
					if (tarOk)
						return (true, null);
					try
					{
						await ExtractTarGzManagedAsync(archivePath, extractDir, ct).ConfigureAwait(false);
						return (true, null);
					}
					catch (Exception ex)
					{
						return (false, $"tar: {tarErr ?? "failed"}; fallback: {ex.Message}");
					}
				}
				case ArchiveKind.TarXz:
				{
					var (tarOk, tarErr) = await TryExtractWithTarCliAsync(archivePath, extractDir, ct).ConfigureAwait(false);
					if (!tarOk)
						return (false, tarErr ?? "tar extraction failed (install tar with xz support, or use a .zip/.tar.gz asset)");
					return (true, null);
				}
				default:
					return (false, "Unsupported archive format.");
			}
		}
		catch (Exception ex)
		{
			return (false, ex.Message);
		}
	}

	static ArchiveKind ClassifyArchive(string fileName)
	{
		var n = fileName.ToLowerInvariant();
		if (n.EndsWith(".zip", StringComparison.Ordinal))
			return ArchiveKind.Zip;
		if (n.EndsWith(".tar.gz", StringComparison.Ordinal) || n.EndsWith(".tgz", StringComparison.Ordinal))
			return ArchiveKind.TarGz;
		if (n.EndsWith(".tar.xz", StringComparison.Ordinal) || n.EndsWith(".txz", StringComparison.Ordinal))
			return ArchiveKind.TarXz;
		return ArchiveKind.None;
	}

	static async Task<(bool Ok, string? Err)> TryExtractWithTarCliAsync(string archivePath, string extractDir, CancellationToken ct)
	{
		try
		{
			var tar = OperatingSystem.IsWindows() ? "tar.exe" : "tar";
			using var p = Process.Start(new ProcessStartInfo
			{
				FileName = tar,
				Arguments = $"-xf \"{archivePath}\" -C \"{extractDir}\"",
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = false,
				CreateNoWindow = true
			});
			if (p is null)
				return (false, "Could not start tar");
			await p.WaitForExitAsync(ct).ConfigureAwait(false);
			if (p.ExitCode != 0)
			{
				var err = await p.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
				return (false, string.IsNullOrWhiteSpace(err) ? $"tar exit {p.ExitCode}" : err.Trim());
			}
			return (true, null);
		}
		catch (Exception ex)
		{
			return (false, ex.Message);
		}
	}

	static async Task ExtractTarGzManagedAsync(string archivePath, string extractRoot, CancellationToken ct)
	{
		await using var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536, useAsync: true);
		await using var gz = new GZipStream(fs, CompressionMode.Decompress);
		await using var reader = new TarReader(gz);
		var rootFull = Path.GetFullPath(extractRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

		while (await reader.GetNextEntryAsync(copyData: true, cancellationToken: ct).ConfigureAwait(false) is { } entry)
		{
			switch (entry.EntryType)
			{
				case TarEntryType.Directory:
				{
					var dirPath = ResolveSafePath(rootFull, entry.Name);
					Directory.CreateDirectory(dirPath);
					entry.DataStream?.Dispose();
					break;
				}
				case TarEntryType.RegularFile:
				{
					var dest = ResolveSafePath(rootFull, entry.Name);
					var parent = Path.GetDirectoryName(dest);
					if (!string.IsNullOrEmpty(parent))
						Directory.CreateDirectory(parent);
					if (entry.DataStream is null)
						break;
					await using (entry.DataStream)
					{
						await using var outFs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536, useAsync: true);
						await entry.DataStream.CopyToAsync(outFs, ct).ConfigureAwait(false);
					}
					break;
				}
				default:
					entry.DataStream?.Dispose();
					break;
			}
		}
	}

	/// <summary>Resolves a tar entry name under <paramref name="rootFull"/>; rejects absolute paths and <c>..</c>.</summary>
	static string ResolveSafePath(string rootFull, string entryName)
	{
		var relative = (entryName ?? "").Replace('\\', '/').TrimStart('/');
		if (string.IsNullOrEmpty(relative))
			throw new InvalidDataException("Empty path in archive");
		var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (segments.Length == 0)
			throw new InvalidDataException("Invalid path in archive");
		foreach (var seg in segments)
		{
			if (seg == "..")
				throw new InvalidDataException("Path traversal in archive");
		}

		var combined = rootFull;
		foreach (var seg in segments)
			combined = Path.Combine(combined, seg);
		var full = Path.GetFullPath(combined);
		var rootPrefix = rootFull.EndsWith(Path.DirectorySeparatorChar) ? rootFull : rootFull + Path.DirectorySeparatorChar;
		var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		if (!full.StartsWith(rootPrefix, cmp) && !string.Equals(full, rootFull, cmp))
			throw new InvalidDataException("Path escapes extract directory");
		return full;
	}

	internal static string? FindDescendantFile(string extractRoot, string exactFileName) =>
		Directory.EnumerateFiles(extractRoot, exactFileName, SearchOption.AllDirectories)
			.Select(Path.GetFullPath)
			.FirstOrDefault();

	/// <summary>Locate ffmpeg in a release tree: prefer <c>bin/ffmpeg</c>, then root, then any matching file name under the extract root.</summary>
	internal static string? FindFfmpegExecutable(string extractDir, string authorizedAncestorFull, bool isWindows)
	{
		var ffmpegFileName = isWindows ? "ffmpeg.exe" : "ffmpeg";
		var auth = Path.GetFullPath(authorizedAncestorFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		var root = Path.GetFullPath(extractDir);
		var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		var authPrefix = auth.EndsWith(Path.DirectorySeparatorChar) ? auth : auth + Path.DirectorySeparatorChar;

		bool UnderAuth(string candidate)
		{
			var f = Path.GetFullPath(candidate);
			return f.StartsWith(authPrefix, cmp) || string.Equals(f, auth, cmp);
		}

		try
		{
			// 1) Standard layout: extractRoot/bin/ffmpeg
			var directBin = Path.GetFullPath(Path.Combine(root, "bin", ffmpegFileName));
			if (UnderAuth(directBin) && File.Exists(directBin))
				return directBin;

			// 2) Top-level folder then bin (e.g. release-name/bin/ffmpeg)
			foreach (var sub in Directory.EnumerateDirectories(root))
			{
				var nested = Path.GetFullPath(Path.Combine(sub, "bin", ffmpegFileName));
				if (UnderAuth(nested) && File.Exists(nested))
					return nested;
			}

			// 3) Executable at extract root
			var atRoot = Path.GetFullPath(Path.Combine(root, ffmpegFileName));
			if (UnderAuth(atRoot) && File.Exists(atRoot))
				return atRoot;

			// 4) Any ffmpeg(.exe) under the tree (flat or custom layouts)
			foreach (var path in Directory.EnumerateFiles(root, ffmpegFileName, SearchOption.AllDirectories))
			{
				var full = Path.GetFullPath(path);
				if (!UnderAuth(full))
					continue;
				if (!string.Equals(Path.GetFileName(full), ffmpegFileName, StringComparison.OrdinalIgnoreCase))
					continue;
				return full;
			}
		}
		catch
		{
			return null;
		}

		return null;
	}

	internal static void TryEnsureUnixExecutable(string path)
	{
		if (OperatingSystem.IsWindows() || !File.Exists(path))
			return;
		try
		{
			using var p = Process.Start(new ProcessStartInfo
			{
				FileName = "chmod",
				Arguments = $"+x \"{path}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardError = true
			});
			p?.WaitForExit(10_000);
		}
		catch
		{
			// Best-effort; tar-based extracts usually preserve mode.
		}
	}
}
