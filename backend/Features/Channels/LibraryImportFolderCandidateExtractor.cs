using System.Text.RegularExpressions;
using TubeArr.Backend.Data;
using TubeArr.Backend.Media;

namespace TubeArr.Backend;

/// <summary>Derive ordered resolve/search strings from an on-disk library folder name and a shallow media file scan.</summary>
internal static class LibraryImportFolderCandidateExtractor
{
	static readonly Regex UcToken = new(@"\b(UC[0-9A-Za-z_-]{22})\b", RegexOptions.Compiled);
	static readonly Regex PlaylistId = new(@"\b(PL[a-zA-Z0-9_-]{16,})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	static readonly Regex BracketedVideoId = new(@"\[([a-zA-Z0-9_-]{11})\]", RegexOptions.Compiled);
	static readonly Regex YoutubeUrl = new(@"https?://[^\s""'<>]+?(?:youtube\.com|youtu\.be)[^\s""'<>]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	static readonly Regex AtHandle = new(@"(?<![\w])@([A-Za-z0-9_.-]{3,64})(?![\w])", RegexOptions.Compiled);

	static readonly Regex FileNameVideoId = new(@"(?<![A-Za-z0-9_-])([A-Za-z0-9_-]{11})(?=\.|$)", RegexOptions.Compiled);

	/// <summary>Distinct candidates in priority order (channel id / URL / handle / video-derived / folder title search).</summary>
	internal static IReadOnlyList<string> CollectCandidates(string folderFullPath, string folderName)
	{
		var ordered = new List<string>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		void Add(string? s)
		{
			var t = (s ?? "").Trim();
			if (t.Length < 2 || !seen.Add(t))
				return;
			ordered.Add(t);
		}

		foreach (Match m in UcToken.Matches(folderName))
			Add(m.Groups[1].Value);

		foreach (Match m in PlaylistId.Matches(folderName))
			Add("https://www.youtube.com/playlist?list=" + Uri.EscapeDataString(m.Groups[1].Value));

		foreach (Match m in YoutubeUrl.Matches(folderName))
			Add(m.Value.TrimEnd('.', ',', ';'));

		foreach (Match m in AtHandle.Matches(folderName))
			Add("@" + m.Groups[1].Value);

		if (Directory.Exists(folderFullPath))
		{
			foreach (Match m in UcToken.Matches(folderFullPath))
				Add(m.Groups[1].Value);

			var videoIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var fileCount = 0;
			try
			{
				foreach (var file in Directory.EnumerateFiles(folderFullPath, "*", SearchOption.AllDirectories))
				{
					if (++fileCount > 150)
						break;
					if (!MediaFileKnownExtensions.All.Contains(Path.GetExtension(file)))
						continue;

					var fn = Path.GetFileName(file);
					foreach (Match bm in BracketedVideoId.Matches(fn))
					{
						var id = bm.Groups[1].Value;
						if (videoIds.Add(id))
							Add("https://www.youtube.com/watch?v=" + Uri.EscapeDataString(id));
					}

					var fm = FileNameVideoId.Match(fn);
					if (fm.Success && videoIds.Add(fm.Groups[1].Value))
						Add("https://www.youtube.com/watch?v=" + Uri.EscapeDataString(fm.Groups[1].Value));
				}
			}
			catch
			{
				/* ignore inaccessible trees */
			}
		}

		var title = folderName.Trim();
		if (title.Length >= 3)
			Add(title);

		return ordered;
	}

	/// <param name="configuredRootFolderCount">How many library roots exist; used so we do not reserve the same folder name under every root when <see cref="ChannelEntity.RootFolderPath"/> is unset.</param>
	internal static HashSet<string> BuildMappedNormalizedPaths(
		RootFolderEntity root,
		IReadOnlyList<ChannelEntity> channels,
		NamingConfigEntity naming,
		int configuredRootFolderCount = 1)
	{
		var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var rootPath = Path.GetFullPath((root.Path ?? "").Trim());
		if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
			return set;

		foreach (var channel in channels)
		{
			foreach (var p in BuildScanRootsForRoot(channel, naming, root, rootPath, configuredRootFolderCount))
			{
				try
				{
					if (string.IsNullOrWhiteSpace(p) || !Directory.Exists(p))
						continue;
					set.Add(Path.GetFullPath(p));
				}
				catch
				{
					/* skip */
				}
			}
		}

		return set;
	}

	static bool RootFolderPathsMatch(string? channelRootFolderPath, string libraryRootFull)
	{
		if (string.IsNullOrWhiteSpace(channelRootFolderPath))
			return false;
		try
		{
			return string.Equals(
				Path.GetFullPath(channelRootFolderPath.Trim()),
				libraryRootFull,
				StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	/// <summary>True if <paramref name="pathFull"/> is this root or a subdirectory (same volume / full path semantics).</summary>
	static bool IsUnderLibraryRoot(string pathFull, string rootFull)
	{
		try
		{
			var p = Path.GetFullPath(pathFull).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			var r = Path.GetFullPath(rootFull).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			if (p.Equals(r, StringComparison.OrdinalIgnoreCase))
				return true;
			var sep = Path.DirectorySeparatorChar;
			return p.StartsWith(r + sep, StringComparison.OrdinalIgnoreCase)
			       || p.StartsWith(r + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	static IEnumerable<string> BuildScanRootsForRoot(
		ChannelEntity channel,
		NamingConfigEntity naming,
		RootFolderEntity root,
		string libraryRootFull,
		int configuredRootFolderCount)
	{
		var rootPath = (root.Path ?? "").Trim();
		if (string.IsNullOrWhiteSpace(rootPath))
			yield break;

		if (!string.IsNullOrWhiteSpace(channel.Path))
		{
			string resolved = Path.IsPathRooted(channel.Path)
				? channel.Path.Trim()
				: Path.Combine(rootPath, channel.Path.Trim());

			if (Path.IsPathRooted(channel.Path))
			{
				string fullChannelPath;
				try
				{
					fullChannelPath = Path.GetFullPath(resolved);
				}
				catch
				{
					yield break;
				}

				if (!IsUnderLibraryRoot(fullChannelPath, libraryRootFull))
					yield break;
			}
			else
			{
				if (configuredRootFolderCount > 1 && !RootFolderPathsMatch(channel.RootFolderPath, libraryRootFull))
					yield break;
			}

			if (!string.IsNullOrWhiteSpace(resolved))
				yield return resolved;
			yield break;
		}

		if (configuredRootFolderCount > 1 && !RootFolderPathsMatch(channel.RootFolderPath, libraryRootFull))
			yield break;

		var dummyVideo = new VideoEntity { Title = "", YoutubeVideoId = "", UploadDateUtc = DateTimeOffset.UtcNow };
		var context = new VideoFileNaming.NamingContext(Channel: channel, Video: dummyVideo, Playlist: null, PlaylistIndex: null, QualityFull: null, Resolution: null, Extension: null);
		var channelFolderName = VideoFileNaming.BuildFolderName(naming.ChannelFolderFormat, context, naming);
		if (!string.IsNullOrWhiteSpace(channelFolderName))
			yield return Path.Combine(rootPath, channelFolderName);
	}
}
