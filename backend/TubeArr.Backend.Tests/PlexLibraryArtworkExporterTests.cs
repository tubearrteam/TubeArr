using System.Reflection;
using System.Xml.Linq;
using TubeArr.Backend;
using TubeArr.Backend.Data;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class PlexLibraryArtworkExporterTests
{
	static List<RootFolderEntity> Roots(string path) =>
		[new RootFolderEntity { Id = 1, Path = path }];

	[Fact]
	public void Youtube_video_thumb_candidates_follow_spec_order()
	{
		var expected = new[]
		{
			"maxresdefault.jpg",
			"standard.jpg",
			"sddefault.jpg",
			"high.jpg",
			"hqdefault.jpg",
			"medium.jpg",
			"mqdefault.jpg",
			"default.jpg"
		};

		var field = typeof(PlexLibraryArtworkExporter).GetField(
			"YoutubeVideoThumbFileNames",
			BindingFlags.Static | BindingFlags.NonPublic);
		Assert.NotNull(field);
		var actual = (string[]?)field!.GetValue(null);
		Assert.NotNull(actual);
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void CanWriteTubeArrManagedFile_allows_new_file()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-art-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		var path = Path.Combine(dir, "thumb.jpg");
		try
		{
			Assert.False(File.Exists(path));
			Assert.True(PlexLibraryArtworkExporter.CanWriteTubeArrManagedFile(path, dir, Roots(dir)));
		}
		finally
		{
			try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
			try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
		}
	}

	[Fact]
	public void CanWriteTubeArrManagedFile_blocks_existing_without_listing_or_legacy_marker()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-art-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		var path = Path.Combine(dir, "thumb.jpg");
		try
		{
			File.WriteAllText(path, "user");
			Assert.False(PlexLibraryArtworkExporter.CanWriteTubeArrManagedFile(path, dir, Roots(dir)));
		}
		finally
		{
			try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
			try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
		}
	}

	[Fact]
	public void CanWriteTubeArrManagedFile_blocks_existing_even_when_listed_in_manifest()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-art-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		var path = Path.Combine(dir, "thumb.jpg");
		var manifestPath = Path.Combine(dir, TubeArrManagedLibraryManifest.ManifestFileName);
		try
		{
			File.WriteAllText(path, "old");
			new XDocument(
				new XElement("tubearr",
					new XAttribute("version", "1"),
					new XElement("managed",
						new XAttribute("path", "thumb.jpg"),
						new XAttribute("kind", TubeArrManagedLibraryManifest.KindArtwork))))
				.Save(manifestPath);
			Assert.False(PlexLibraryArtworkExporter.CanWriteTubeArrManagedFile(path, dir, Roots(dir)));
		}
		finally
		{
			try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
			try { if (File.Exists(manifestPath)) File.Delete(manifestPath); } catch { /* ignore */ }
			try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
		}
	}

	[Fact]
	public void CanWriteTubeArrManagedFile_blocks_existing_even_with_legacy_sidecar_marker()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-art-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		var path = Path.Combine(dir, "thumb.jpg");
		var marker = path + ".tubearr";
		try
		{
			File.WriteAllText(path, "old");
			File.WriteAllText(marker, "tubeart-v1\n");
			Assert.False(PlexLibraryArtworkExporter.CanWriteTubeArrManagedFile(path, dir, Roots(dir)));
		}
		finally
		{
			try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
			try { if (File.Exists(marker)) File.Delete(marker); } catch { /* ignore */ }
			try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
		}
	}
}
