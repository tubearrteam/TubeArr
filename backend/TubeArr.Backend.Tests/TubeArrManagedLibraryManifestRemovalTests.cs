using System.Xml.Linq;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class TubeArrManagedLibraryManifestRemovalTests
{
	[Fact]
	public void RemoveManagedNfoFiles_deletes_nfos_and_drops_entries_keeps_artwork()
	{
		var lib = Path.Combine(Path.GetTempPath(), "tubearr-nfo-rem-" + Guid.NewGuid().ToString("N"));
		var ch = Path.Combine(lib, "Ch");
		Directory.CreateDirectory(ch);
		var manifest = Path.Combine(lib, TubeArrManagedLibraryManifest.ManifestFileName);
		var nfo = Path.Combine(ch, "tvshow.nfo");
		var jpg = Path.Combine(ch, "poster.jpg");
		try
		{
			File.WriteAllText(nfo, "<tvshow/>");
			File.WriteAllText(jpg, "");
			new XDocument(
				new XElement("tubearr",
					new XAttribute("version", "1"),
					new XElement("managed",
						new XAttribute("path", "Ch/tvshow.nfo"),
						new XAttribute("kind", TubeArrManagedLibraryManifest.KindNfo)),
					new XElement("managed",
						new XAttribute("path", "Ch/poster.jpg"),
						new XAttribute("kind", TubeArrManagedLibraryManifest.KindArtwork))))
				.Save(manifest);

			var (del, miss) = TubeArrManagedLibraryManifest.RemoveManagedNfoFiles(lib);
			Assert.Equal(1, del);
			Assert.Equal(0, miss);
			Assert.False(File.Exists(nfo));
			Assert.True(File.Exists(jpg));
			Assert.True(File.Exists(manifest));
			var doc = XDocument.Load(manifest);
			var paths = doc.Root!.Elements("managed").Select(e => e.Attribute("path")!.Value).ToList();
			Assert.Single(paths);
			Assert.Equal("Ch/poster.jpg", paths[0]);
		}
		finally
		{
			try { if (File.Exists(nfo)) File.Delete(nfo); } catch { /* ignore */ }
			try { if (File.Exists(jpg)) File.Delete(jpg); } catch { /* ignore */ }
			try { if (File.Exists(manifest)) File.Delete(manifest); } catch { /* ignore */ }
			try { if (Directory.Exists(lib)) Directory.Delete(lib, recursive: true); } catch { /* ignore */ }
		}
	}

	[Fact]
	public void RemoveManagedNfoFiles_deletes_manifest_when_only_nfo_entries()
	{
		var lib = Path.Combine(Path.GetTempPath(), "tubearr-nfo-rem2-" + Guid.NewGuid().ToString("N"));
		var ch = Path.Combine(lib, "X");
		Directory.CreateDirectory(ch);
		var manifest = Path.Combine(lib, TubeArrManagedLibraryManifest.ManifestFileName);
		var nfo = Path.Combine(ch, "a.nfo");
		try
		{
			File.WriteAllText(nfo, "<ep/>");
			new XDocument(
				new XElement("tubearr",
					new XAttribute("version", "1"),
					new XElement("managed",
						new XAttribute("path", "X/a.nfo"),
						new XAttribute("kind", TubeArrManagedLibraryManifest.KindNfo))))
				.Save(manifest);

			var (del, miss) = TubeArrManagedLibraryManifest.RemoveManagedNfoFiles(lib);
			Assert.Equal(1, del);
			Assert.Equal(0, miss);
			Assert.False(File.Exists(nfo));
			Assert.False(File.Exists(manifest));
		}
		finally
		{
			try { if (File.Exists(nfo)) File.Delete(nfo); } catch { /* ignore */ }
			try { if (File.Exists(manifest)) File.Delete(manifest); } catch { /* ignore */ }
			try { if (Directory.Exists(lib)) Directory.Delete(lib, recursive: true); } catch { /* ignore */ }
		}
	}
}
