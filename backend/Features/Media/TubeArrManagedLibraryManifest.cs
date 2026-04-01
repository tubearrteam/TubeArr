using System.Collections.Concurrent;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using TubeArr.Backend.Data;

namespace TubeArr.Backend;

/// <summary>
/// One manifest per library root folder (<c>.tubearr</c>): UTF-8 XML listing TubeArr-managed artwork and NFO paths (relative to that root, all channels under it).
/// Migrates per-show <c>tubearr-managed.tubearr</c>, <c>tubearr-managed-artwork.xml</c>, and per-file <c>*.tubearr</c> sidecars on first use.
/// </summary>
internal static class TubeArrManagedLibraryManifest
{
	internal const string ManifestFileName = ".tubearr";
	internal const string LegacyShowManifestFileName = "tubearr-managed.tubearr";
	internal const string LegacyArtworkXmlFileName = "tubearr-managed-artwork.xml";

	internal const string KindArtwork = "artwork";
	internal const string KindNfo = "nfo";

	static readonly ConcurrentDictionary<string, object> LibraryRootLocks = new(StringComparer.OrdinalIgnoreCase);

	static StringComparer PathComparer =>
		OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

	static StringComparison PathPrefixComparison =>
		OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

	internal static string CanonicalRelativePath(string relativePath)
	{
		if (string.IsNullOrWhiteSpace(relativePath))
			return "";
		return relativePath.Replace('\\', '/').Trim();
	}

	/// <summary>Longest configured root folder path that contains <paramref name="absolutePath"/>.</summary>
	internal static string? TryResolveLibraryRootForPath(string absolutePath, IReadOnlyList<RootFolderEntity> rootFolders)
	{
		if (rootFolders.Count == 0)
			return null;
		try
		{
			var full = Path.GetFullPath(absolutePath.Trim());
			string? best = null;
			var bestLen = -1;
			foreach (var r in rootFolders)
			{
				var rp = (r.Path ?? "").Trim();
				if (string.IsNullOrWhiteSpace(rp))
					continue;
				try
				{
					var fr = Path.GetFullPath(rp).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
					if (full.Length >= fr.Length && full.StartsWith(fr, PathPrefixComparison))
					{
						if (fr.Length > bestLen)
						{
							bestLen = fr.Length;
							best = fr;
						}
					}
				}
				catch
				{
					/* skip bad root */
				}
			}

			return best;
		}
		catch
		{
			return null;
		}
	}

	internal static string? TryGetManagedRelativePath(string libraryRoot, string absolutePath)
	{
		try
		{
			var root = Path.GetFullPath(libraryRoot);
			var dest = Path.GetFullPath(absolutePath);
			var rel = Path.GetRelativePath(root, dest);
			if (Path.IsPathFullyQualified(rel))
				return null;
			if (rel.StartsWith("..", StringComparison.Ordinal))
				return null;
			return rel;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>NFOs: write when missing, or when path is already listed (sync/repair may update TubeArr-owned files).</summary>
	internal static bool CanWriteManagedNfo(IReadOnlyList<RootFolderEntity> rootFolders, string absoluteNfoPath)
	{
		var lib = TryResolveLibraryRootForPath(absoluteNfoPath, rootFolders);
		if (string.IsNullOrWhiteSpace(lib))
			return false;

		var rel = TryGetManagedRelativePath(lib, absoluteNfoPath);
		if (rel is null)
			return false;

		if (!File.Exists(absoluteNfoPath))
			return true;

		lock (GetLibraryLock(lib))
		{
			EnsureMigratedOnceUnlocked(lib);
			return LoadManagedKindsUnlocked(lib).ContainsKey(CanonicalRelativePath(rel));
		}
	}

	internal static void RegisterManagedAsset(IReadOnlyList<RootFolderEntity> rootFolders, string absolutePath, string kind)
	{
		var lib = TryResolveLibraryRootForPath(absolutePath, rootFolders);
		if (string.IsNullOrWhiteSpace(lib))
			return;

		var rel = TryGetManagedRelativePath(lib, absolutePath);
		if (rel is null)
			return;

		var canonical = CanonicalRelativePath(rel);
		if (string.IsNullOrEmpty(canonical))
			return;

		kind = string.IsNullOrWhiteSpace(kind) ? KindArtwork : kind.Trim().ToLowerInvariant();
		if (kind != KindArtwork && kind != KindNfo)
			kind = KindArtwork;

		lock (GetLibraryLock(lib))
		{
			EnsureMigratedOnceUnlocked(lib);
			var map = LoadManagedKindsUnlocked(lib);
			map[canonical] = kind;
			SaveManifestUnlocked(lib, map);
		}
	}

	/// <summary>Delete files listed as <see cref="KindNfo"/> in the manifest, remove those entries, save or delete the manifest.</summary>
	internal static (int FilesDeleted, int FilesMissing) RemoveManagedNfoFiles(string libraryRoot)
	{
		lock (GetLibraryLock(libraryRoot))
		{
			EnsureMigratedOnceUnlocked(libraryRoot);
			var map = LoadManagedKindsUnlocked(libraryRoot);
			var nfoRelPaths = map
				.Where(kv => string.Equals(kv.Value, KindNfo, StringComparison.OrdinalIgnoreCase))
				.Select(kv => kv.Key)
				.ToList();

			if (nfoRelPaths.Count == 0)
				return (0, 0);

			var root = Path.GetFullPath(libraryRoot);
			var deleted = 0;
			var missing = 0;

			foreach (var rel in nfoRelPaths)
			{
				var relOs = rel.Replace('/', Path.DirectorySeparatorChar);
				string abs;
				try
				{
					abs = Path.GetFullPath(Path.Combine(root, relOs));
				}
				catch
				{
					map.Remove(rel);
					continue;
				}

				try
				{
					var relCheck = Path.GetRelativePath(root, abs);
					if (relCheck.StartsWith("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(relCheck))
					{
						map.Remove(rel);
						continue;
					}
				}
				catch
				{
					map.Remove(rel);
					continue;
				}

				var dropEntry = false;
				try
				{
					if (File.Exists(abs))
					{
						File.Delete(abs);
						deleted++;
						dropEntry = true;
					}
					else
					{
						missing++;
						dropEntry = true;
					}
				}
				catch
				{
					/* leave entry so a later run can retry (e.g. file locked) */
				}

				if (dropEntry)
					map.Remove(rel);
			}

			var manifestPath = Path.Combine(libraryRoot, ManifestFileName);
			if (map.Count == 0)
			{
				try
				{
					if (File.Exists(manifestPath))
						File.Delete(manifestPath);
				}
				catch
				{
					/* best-effort */
				}
			}
			else
			{
				SaveManifestUnlocked(libraryRoot, map);
			}

			return (deleted, missing);
		}
	}

	static object GetLibraryLock(string libraryRoot) =>
		LibraryRootLocks.GetOrAdd(Path.GetFullPath(libraryRoot), _ => new object());

	static readonly ConcurrentDictionary<string, byte> MigrationDone = new(StringComparer.OrdinalIgnoreCase);

	static void EnsureMigratedOnceUnlocked(string libraryRoot)
	{
		var key = Path.GetFullPath(libraryRoot);
		if (MigrationDone.ContainsKey(key))
			return;

		var manifestPath = Path.Combine(libraryRoot, ManifestFileName);
		var hadNewManifest = File.Exists(manifestPath);
		var map = LoadManagedKindsUnlocked(libraryRoot);

		var legacyShowManifests = SafeGetFiles(libraryRoot, LegacyShowManifestFileName);
		foreach (var oldPath in legacyShowManifests)
		{
			var showDir = Path.GetDirectoryName(oldPath);
			if (string.IsNullOrEmpty(showDir))
				continue;
			MergeManagedDocumentIntoMap(libraryRoot, showDir, oldPath, map);
		}

		foreach (var xmlPath in SafeGetFiles(libraryRoot, LegacyArtworkXmlFileName))
		{
			var showDir = Path.GetDirectoryName(xmlPath);
			if (string.IsNullOrEmpty(showDir))
				continue;
			MergeManagedDocumentIntoMap(libraryRoot, showDir, xmlPath, map);
			try
			{
				File.Delete(xmlPath);
			}
			catch
			{
				/* best-effort */
			}
		}

		try
		{
			var rootManifestFull = Path.GetFullPath(manifestPath);
			foreach (var marker in Directory.EnumerateFiles(libraryRoot, "*.tubearr", SearchOption.AllDirectories))
			{
				if (Path.GetFullPath(marker).Equals(rootManifestFull, StringComparison.OrdinalIgnoreCase))
					continue;
				if (string.Equals(Path.GetFileName(marker), LegacyShowManifestFileName, StringComparison.OrdinalIgnoreCase))
					continue;

				const string suffix = ".tubearr";
				if (marker.Length <= suffix.Length || !marker.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
					continue;

				var baseMediaPath = marker[..^suffix.Length];
				if (!File.Exists(baseMediaPath))
					continue;

				var br = TryGetManagedRelativePath(libraryRoot, baseMediaPath);
				if (br is null)
					continue;

				var bc = CanonicalRelativePath(br);
				if (string.IsNullOrEmpty(bc))
					continue;

				map.TryAdd(bc, KindArtwork);
				try
				{
					File.Delete(marker);
				}
				catch
				{
					/* best-effort */
				}
			}
		}
		catch
		{
			/* ignore enumeration failures */
		}

		foreach (var oldPath in legacyShowManifests)
		{
			try
			{
				if (File.Exists(oldPath))
					File.Delete(oldPath);
			}
			catch
			{
				/* best-effort */
			}
		}

		if (map.Count > 0 || hadNewManifest || legacyShowManifests.Count > 0)
			SaveManifestUnlocked(libraryRoot, map);

		MigrationDone.TryAdd(key, 0);
	}

	static List<string> SafeGetFiles(string libraryRoot, string fileName)
	{
		try
		{
			return Directory.GetFiles(libraryRoot, fileName, SearchOption.AllDirectories).ToList();
		}
		catch
		{
			return [];
		}
	}

	static void MergeManagedDocumentIntoMap(string libraryRoot, string showDir, string xmlPath, Dictionary<string, string> map)
	{
		try
		{
			var doc = XDocument.Load(xmlPath);
			foreach (var el in doc.Root?.Elements("managed") ?? Enumerable.Empty<XElement>())
			{
				var p = (string?)el.Attribute("path");
				if (string.IsNullOrWhiteSpace(p))
					continue;
				var relToShow = p!.Replace('/', Path.DirectorySeparatorChar);
				string abs;
				try
				{
					abs = Path.GetFullPath(Path.Combine(showDir, relToShow));
				}
				catch
				{
					continue;
				}

				var relLib = TryGetManagedRelativePath(libraryRoot, abs);
				if (relLib is null)
					continue;
				var c = CanonicalRelativePath(relLib);
				if (string.IsNullOrEmpty(c))
					continue;
				var k = ((string?)el.Attribute("kind") ?? KindArtwork).Trim().ToLowerInvariant();
				if (k != KindArtwork && k != KindNfo)
					k = KindArtwork;
				map[c] = k;
			}
		}
		catch
		{
			/* corrupt */
		}
	}

	static Dictionary<string, string> LoadManagedKindsUnlocked(string libraryRoot)
	{
		var map = new Dictionary<string, string>(PathComparer);
		var manifestPath = Path.Combine(libraryRoot, ManifestFileName);
		if (!File.Exists(manifestPath))
			return map;

		try
		{
			var doc = XDocument.Load(manifestPath);
			foreach (var el in doc.Root?.Elements("managed") ?? Enumerable.Empty<XElement>())
			{
				var p = (string?)el.Attribute("path");
				if (string.IsNullOrWhiteSpace(p))
					continue;
				var c = CanonicalRelativePath(p!);
				if (string.IsNullOrEmpty(c))
					continue;
				var k = ((string?)el.Attribute("kind") ?? KindArtwork).Trim().ToLowerInvariant();
				if (k != KindArtwork && k != KindNfo)
					k = KindArtwork;
				map[c] = k;
			}
		}
		catch
		{
			/* corrupt: treat as empty */
		}

		return map;
	}

	static void SaveManifestUnlocked(string libraryRoot, Dictionary<string, string> map)
	{
		Directory.CreateDirectory(libraryRoot);
		var manifestPath = Path.Combine(libraryRoot, ManifestFileName);

		var root = new XElement("tubearr", new XAttribute("version", "1"));
		foreach (var kv in map.OrderBy(x => x.Key, PathComparer))
		{
			root.Add(new XElement(
				"managed",
				new XAttribute("path", kv.Key),
				new XAttribute("kind", kv.Value)));
		}

		var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
		var settings = new XmlWriterSettings
		{
			Indent = true,
			Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
			OmitXmlDeclaration = false,
			NewLineHandling = NewLineHandling.Replace,
			NewLineChars = "\n"
		};
		using (var writer = XmlWriter.Create(manifestPath, settings))
			doc.Save(writer);
	}
}
