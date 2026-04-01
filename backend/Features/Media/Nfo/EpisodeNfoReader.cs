using System.Xml.Linq;

namespace TubeArr.Backend.Media.Nfo;

/// <summary>Reads minimal fields from episode NFO sidecars written by <see cref="NfoWriter"/>.</summary>
internal static class EpisodeNfoReader
{
	/// <summary>Path is the media file path; looks for <c>{basename}.nfo</c> in the same directory.</summary>
	internal static async Task<string?> TryReadEpisodeTitleAsync(string? mediaFilePath, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(mediaFilePath))
			return null;

		var dir = Path.GetDirectoryName(mediaFilePath);
		if (string.IsNullOrEmpty(dir))
			return null;

		var baseName = Path.GetFileNameWithoutExtension(mediaFilePath);
		if (string.IsNullOrEmpty(baseName))
			return null;

		var nfoPath = Path.Combine(dir, baseName + ".nfo");
		if (!File.Exists(nfoPath))
			return null;

		string text;
		try
		{
			text = await File.ReadAllTextAsync(nfoPath, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			return null;
		}

		return TryParseEpisodeTitleFromXml(text);
	}

	internal static string? TryParseEpisodeTitleFromXml(string? xml)
	{
		if (string.IsNullOrWhiteSpace(xml))
			return null;

		try
		{
			var doc = XDocument.Parse(xml);
			var root = doc.Root;
			if (root is null)
				return null;

			XElement? titleEl = null;
			if (string.Equals(root.Name.LocalName, "episodedetails", StringComparison.OrdinalIgnoreCase))
			{
				titleEl = root.Elements()
					.FirstOrDefault(e => e.Name.LocalName.Equals("title", StringComparison.OrdinalIgnoreCase));
			}
			else
			{
				var ep = doc.Descendants()
					.FirstOrDefault(e => e.Name.LocalName.Equals("episodedetails", StringComparison.OrdinalIgnoreCase));
				if (ep is not null)
				{
					titleEl = ep.Elements()
						.FirstOrDefault(e => e.Name.LocalName.Equals("title", StringComparison.OrdinalIgnoreCase));
				}
			}

			titleEl ??= doc.Descendants()
				.FirstOrDefault(e => e.Name.LocalName.Equals("title", StringComparison.OrdinalIgnoreCase));

			var t = titleEl?.Value.Trim();
			return string.IsNullOrEmpty(t) ? null : t;
		}
		catch
		{
			return null;
		}
	}
}
