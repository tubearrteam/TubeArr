using System.Xml.Linq;
using TubeArr.Backend.Data;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class TubeArrManagedLibraryManifestTests
{
	static List<RootFolderEntity> Roots(string path) =>
		[new RootFolderEntity { Id = 1, Path = path }];

	[Fact]
	public void Register_migrates_legacy_artwork_xml_into_root_manifest_and_deletes_xml()
	{
		var lib = Path.Combine(Path.GetTempPath(), "tubearr-manifest-" + Guid.NewGuid().ToString("N"));
		var show = Path.Combine(lib, "MyShow");
		Directory.CreateDirectory(show);
		var legacyXml = Path.Combine(show, TubeArrManagedLibraryManifest.LegacyArtworkXmlFileName);
		var manifestPath = Path.Combine(lib, TubeArrManagedLibraryManifest.ManifestFileName);
		var extraJpeg = Path.Combine(show, "extra.jpg");
		try
		{
			new XDocument(
				new XElement("tubearr",
					new XAttribute("version", "1"),
					new XElement("managed", new XAttribute("path", "poster.jpg"))))
				.Save(legacyXml);
			File.WriteAllText(extraJpeg, "");

			TubeArrManagedLibraryManifest.RegisterManagedAsset(Roots(lib), extraJpeg, TubeArrManagedLibraryManifest.KindArtwork);

			Assert.False(File.Exists(legacyXml));
			Assert.True(File.Exists(manifestPath));
			var doc = XDocument.Load(manifestPath);
			Assert.NotNull(doc.Root);
			var paths = doc.Root.Elements("managed").Select(e => e.Attribute("path")!.Value).ToHashSet();
			Assert.Contains("MyShow/poster.jpg", paths);
			Assert.Contains("MyShow/extra.jpg", paths);
		}
		finally
		{
			try { if (File.Exists(legacyXml)) File.Delete(legacyXml); } catch { /* ignore */ }
			try { if (File.Exists(manifestPath)) File.Delete(manifestPath); } catch { /* ignore */ }
			try { if (File.Exists(extraJpeg)) File.Delete(extraJpeg); } catch { /* ignore */ }
			try { if (Directory.Exists(lib)) Directory.Delete(lib, recursive: true); } catch { /* ignore */ }
		}
	}

	[Fact]
	public void Register_migrates_sidecar_tubearr_marker_and_deletes_marker()
	{
		var lib = Path.Combine(Path.GetTempPath(), "tubearr-manifest-" + Guid.NewGuid().ToString("N"));
		var show = Path.Combine(lib, "S");
		Directory.CreateDirectory(show);
		var jpeg = Path.Combine(show, "a.jpg");
		var marker = jpeg + ".tubearr";
		var manifestPath = Path.Combine(lib, TubeArrManagedLibraryManifest.ManifestFileName);
		var other = Path.Combine(show, "b.jpg");
		try
		{
			File.WriteAllText(jpeg, "");
			File.WriteAllText(marker, "x");
			File.WriteAllText(other, "");

			TubeArrManagedLibraryManifest.RegisterManagedAsset(Roots(lib), other, TubeArrManagedLibraryManifest.KindArtwork);

			Assert.False(File.Exists(marker));
			Assert.True(File.Exists(manifestPath));
			var doc = XDocument.Load(manifestPath);
			Assert.NotNull(doc.Root);
			var paths = doc.Root.Elements("managed").Select(e => e.Attribute("path")!.Value).ToHashSet();
			Assert.Contains("S/a.jpg", paths);
			Assert.Contains("S/b.jpg", paths);
		}
		finally
		{
			try { if (File.Exists(jpeg)) File.Delete(jpeg); } catch { /* ignore */ }
			try { if (File.Exists(marker)) File.Delete(marker); } catch { /* ignore */ }
			try { if (File.Exists(other)) File.Delete(other); } catch { /* ignore */ }
			try { if (File.Exists(manifestPath)) File.Delete(manifestPath); } catch { /* ignore */ }
			try { if (Directory.Exists(lib)) Directory.Delete(lib, recursive: true); } catch { /* ignore */ }
		}
	}

	[Fact]
	public void Register_merges_legacy_per_show_manifest_into_root_dot_tubearr()
	{
		var lib = Path.Combine(Path.GetTempPath(), "tubearr-manifest-" + Guid.NewGuid().ToString("N"));
		var show = Path.Combine(lib, "ChannelA");
		Directory.CreateDirectory(show);
		var oldManifest = Path.Combine(show, TubeArrManagedLibraryManifest.LegacyShowManifestFileName);
		var rootManifest = Path.Combine(lib, TubeArrManagedLibraryManifest.ManifestFileName);
		var newJpeg = Path.Combine(show, "x.jpg");
		try
		{
			new XDocument(
				new XElement("tubearr",
					new XAttribute("version", "1"),
					new XElement("managed",
						new XAttribute("path", "fanart.jpg"),
						new XAttribute("kind", TubeArrManagedLibraryManifest.KindArtwork))))
				.Save(oldManifest);
			File.WriteAllText(newJpeg, "");

			TubeArrManagedLibraryManifest.RegisterManagedAsset(Roots(lib), newJpeg, TubeArrManagedLibraryManifest.KindArtwork);

			Assert.False(File.Exists(oldManifest));
			Assert.True(File.Exists(rootManifest));
			var doc = XDocument.Load(rootManifest);
			var paths = doc.Root!.Elements("managed").Select(e => e.Attribute("path")!.Value).ToHashSet();
			Assert.Contains("ChannelA/fanart.jpg", paths);
			Assert.Contains("ChannelA/x.jpg", paths);
		}
		finally
		{
			try { if (File.Exists(oldManifest)) File.Delete(oldManifest); } catch { /* ignore */ }
			try { if (File.Exists(rootManifest)) File.Delete(rootManifest); } catch { /* ignore */ }
			try { if (File.Exists(newJpeg)) File.Delete(newJpeg); } catch { /* ignore */ }
			try { if (Directory.Exists(lib)) Directory.Delete(lib, recursive: true); } catch { /* ignore */ }
		}
	}

	[Fact]
	public void CanWriteManagedNfo_allows_missing_file_without_manifest_entry()
	{
		var lib = Path.Combine(Path.GetTempPath(), "tubearr-manifest-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(lib);
		var nfo = Path.Combine(lib, "tvshow.nfo");
		try
		{
			Assert.True(TubeArrManagedLibraryManifest.CanWriteManagedNfo(Roots(lib), nfo));
		}
		finally
		{
			try { if (Directory.Exists(lib)) Directory.Delete(lib, recursive: true); } catch { /* ignore */ }
		}
	}

	[Fact]
	public void CanWriteManagedNfo_blocks_existing_file_not_listed()
	{
		var lib = Path.Combine(Path.GetTempPath(), "tubearr-manifest-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(lib);
		var nfo = Path.Combine(lib, "tvshow.nfo");
		try
		{
			File.WriteAllText(nfo, "<tvshow/>");
			Assert.False(TubeArrManagedLibraryManifest.CanWriteManagedNfo(Roots(lib), nfo));
		}
		finally
		{
			try { if (File.Exists(nfo)) File.Delete(nfo); } catch { /* ignore */ }
			try { if (Directory.Exists(lib)) Directory.Delete(lib, recursive: true); } catch { /* ignore */ }
		}
	}

	[Fact]
	public void CanWriteManagedNfo_allows_existing_file_when_listed_as_nfo()
	{
		var lib = Path.Combine(Path.GetTempPath(), "tubearr-manifest-" + Guid.NewGuid().ToString("N"));
		var show = Path.Combine(lib, "C");
		Directory.CreateDirectory(show);
		var nfo = Path.Combine(show, "tvshow.nfo");
		var manifestPath = Path.Combine(lib, TubeArrManagedLibraryManifest.ManifestFileName);
		try
		{
			File.WriteAllText(nfo, "<tvshow/>");
			new XDocument(
				new XElement("tubearr",
					new XAttribute("version", "1"),
					new XElement("managed",
						new XAttribute("path", "C/tvshow.nfo"),
						new XAttribute("kind", TubeArrManagedLibraryManifest.KindNfo))))
				.Save(manifestPath);

			Assert.True(TubeArrManagedLibraryManifest.CanWriteManagedNfo(Roots(lib), nfo));
		}
		finally
		{
			try { if (File.Exists(nfo)) File.Delete(nfo); } catch { /* ignore */ }
			try { if (File.Exists(manifestPath)) File.Delete(manifestPath); } catch { /* ignore */ }
			try { if (Directory.Exists(lib)) Directory.Delete(lib, recursive: true); } catch { /* ignore */ }
		}
	}
}
