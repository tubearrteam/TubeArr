using System.Text.RegularExpressions;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>Derive ordered resolve strings from the import folder name and a filename-only tree walk (no file content probing).</summary>
internal static class LibraryImportFolderCandidateExtractor
{
	static readonly Regex UcToken = new(@"\b(UC[0-9A-Za-z_-]{22})\b", RegexOptions.Compiled);
	static readonly Regex PlaylistId = new(@"\b(PL[a-zA-Z0-9_-]{16,})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	static readonly Regex BracketedVideoId = new(@"\[([a-zA-Z0-9_-]{11})\]", RegexOptions.Compiled);
	static readonly Regex YoutubeUrl = new(@"https?://[^\s""'<>]+?(?:youtube\.com|youtu\.be)[^\s""'<>]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	static readonly Regex AtHandle = new(@"(?<![\w])@([A-Za-z0-9_.-]{3,64})(?![\w])", RegexOptions.Compiled);

	static readonly HashSet<string> MediaExts = new(StringComparer.OrdinalIgnoreCase)
	{
		".mp4", ".mkv", ".webm", ".avi", ".mov", ".m4v", ".flv", ".wmv", ".mpg", ".mpeg",
		".m4a", ".mp3", ".aac", ".opus", ".ogg", ".wav", ".flac"
	};

	static readonly Regex FileNameVideoId = new(@"(?<![A-Za-z0-9_-])([A-Za-z0-9_-]{11})(?=\.|$)", RegexOptions.Compiled);

	/// <summary>
	/// Distinct candidates in priority order: tokens from the import folder name, then the first channel/playlist/url/handle/video id
	/// found in a depth-first walk (subfolders sorted, then files sorted). If none found in the tree, folder names are added for search resolve.
	/// </summary>
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

		var afterRoot = ordered.Count;
		AddTokensFromFolderName(folderName, Add);
		var rootSuppliedTokens = ordered.Count > afterRoot;

		if (!Directory.Exists(folderFullPath))
		{
			if (!rootSuppliedTokens)
				AddFolderNamesForSearchResolve(folderFullPath, folderName, Add);
			return ordered;
		}

		var idFromTree = false;
		if (!rootSuppliedTokens)
			idFromTree = TryAddFirstIdFromTreeWalk(folderFullPath, Add, () => ordered.Count);

		if (!rootSuppliedTokens && !idFromTree)
			AddFolderNamesForSearchResolve(folderFullPath, folderName, Add);

		return ordered;
	}

	static void AddTokensFromFolderName(string name, Action<string?> add)
	{
		foreach (Match m in UcToken.Matches(name))
			add(m.Groups[1].Value);

		foreach (Match m in PlaylistId.Matches(name))
			add("https://www.youtube.com/playlist?list=" + Uri.EscapeDataString(m.Groups[1].Value));

		foreach (Match m in YoutubeUrl.Matches(name))
			add(m.Value.TrimEnd('.', ',', ';'));

		foreach (Match m in AtHandle.Matches(name))
			add("@" + m.Groups[1].Value);
	}

	/// <returns>True if at least one new candidate was added from the subtree.</returns>
	static bool TryAddFirstIdFromTreeWalk(string folderFullPath, Action<string?> add, Func<int> orderedCount)
	{
		try
		{
			return Walk(folderFullPath);
		}
		catch
		{
			return false;
		}

		bool Walk(string dir)
		{
			string[] subs;
			try
			{
				subs = Directory.GetDirectories(dir);
			}
			catch
			{
				subs = Array.Empty<string>();
			}

			Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
			foreach (var sub in subs)
			{
				string subName;
				try
				{
					subName = Path.GetFileName(sub.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
				}
				catch
				{
					continue;
				}

				if (TryAddFirstNewTokenFromFolderName(subName, add, orderedCount))
					return true;

				if (Walk(sub))
					return true;
			}

			string[] files;
			try
			{
				files = Directory.GetFiles(dir);
			}
			catch
			{
				files = Array.Empty<string>();
			}

			Array.Sort(files, StringComparer.OrdinalIgnoreCase);
			foreach (var file in files)
			{
				if (!MediaExts.Contains(Path.GetExtension(file)))
					continue;

				var fn = Path.GetFileName(file);
				if (TryAddFirstVideoIdFromFileName(fn, add, orderedCount))
					return true;
			}

			return false;
		}
	}

	/// <summary>First UC / playlist / URL / @ in <paramref name="name"/> that is not already a candidate.</summary>
	static bool TryAddFirstNewTokenFromFolderName(string name, Action<string?> add, Func<int> orderedCount)
	{
		foreach (Match m in UcToken.Matches(name))
		{
			var before = orderedCount();
			add(m.Groups[1].Value);
			if (orderedCount() > before)
				return true;
		}

		foreach (Match m in PlaylistId.Matches(name))
		{
			var before = orderedCount();
			add("https://www.youtube.com/playlist?list=" + Uri.EscapeDataString(m.Groups[1].Value));
			if (orderedCount() > before)
				return true;
		}

		foreach (Match m in YoutubeUrl.Matches(name))
		{
			var before = orderedCount();
			add(m.Value.TrimEnd('.', ',', ';'));
			if (orderedCount() > before)
				return true;
		}

		foreach (Match m in AtHandle.Matches(name))
		{
			var before = orderedCount();
			add("@" + m.Groups[1].Value);
			if (orderedCount() > before)
				return true;
		}

		return false;
	}

	static bool TryAddFirstVideoIdFromFileName(string fileName, Action<string?> add, Func<int> orderedCount)
	{
		foreach (Match bm in BracketedVideoId.Matches(fileName))
		{
			var before = orderedCount();
			add("https://www.youtube.com/watch?v=" + Uri.EscapeDataString(bm.Groups[1].Value));
			if (orderedCount() > before)
				return true;
		}

		var fm = FileNameVideoId.Match(fileName);
		if (fm.Success)
		{
			var before = orderedCount();
			add("https://www.youtube.com/watch?v=" + Uri.EscapeDataString(fm.Groups[1].Value));
			return orderedCount() > before;
		}

		return false;
	}

	static void AddFolderNamesForSearchResolve(string folderFullPath, string folderName, Action<string?> add)
	{
		var rootTitle = folderName.Trim();
		if (rootTitle.Length >= 3)
			add(rootTitle);

		if (!Directory.Exists(folderFullPath))
			return;

		var byName = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (rootTitle.Length >= 3)
			byName.Add(rootTitle);

		try
		{
			Walk(folderFullPath);
		}
		catch
		{
			/* ignore */
		}

		void Walk(string dir)
		{
			string[] subs;
			try
			{
				subs = Directory.GetDirectories(dir);
			}
			catch
			{
				return;
			}

			Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
			foreach (var sub in subs)
			{
				string name;
				try
				{
					name = Path.GetFileName(sub.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
				}
				catch
				{
					continue;
				}

				if (name.Length >= 3 && byName.Add(name))
					add(name);

				Walk(sub);
			}
		}
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
