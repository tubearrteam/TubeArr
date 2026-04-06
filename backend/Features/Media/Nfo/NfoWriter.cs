using System.Text;

namespace TubeArr.Backend.Media.Nfo;

/// <summary>Writes minimal UTF-8 NFO files for Plex/Kodi local metadata (TubeArr-managed paths only).</summary>
internal static class NfoWriter
{
	public static Task WriteTvShowNfoAsync(string showRootDirectory, TvShowNfoContent content, CancellationToken cancellationToken = default)
	{
		Directory.CreateDirectory(showRootDirectory);
		var path = Path.Combine(showRootDirectory, "tvshow.nfo");
		var xml = BuildTvShowDocument(content);
		return File.WriteAllTextAsync(path, xml, NfoXmlText.Utf8Encoding, cancellationToken);
	}

	public static Task WriteSeasonNfoAsync(string seasonDirectory, SeasonNfoContent content, CancellationToken cancellationToken = default)
	{
		Directory.CreateDirectory(seasonDirectory);
		var path = Path.Combine(seasonDirectory, "season.nfo");
		var xml = BuildSeasonDocument(content);
		return File.WriteAllTextAsync(path, xml, NfoXmlText.Utf8Encoding, cancellationToken);
	}

	public static Task WriteEpisodeNfoAsync(string mediaFilePath, EpisodeNfoContent content, CancellationToken cancellationToken = default)
	{
		var dir = Path.GetDirectoryName(mediaFilePath);
		if (string.IsNullOrEmpty(dir))
			throw new ArgumentException("Media path has no directory.", nameof(mediaFilePath));

		Directory.CreateDirectory(dir);
		var baseName = Path.GetFileNameWithoutExtension(mediaFilePath);
		var path = Path.Combine(dir, baseName + ".nfo");
		var xml = BuildEpisodeDocument(content);
		return File.WriteAllTextAsync(path, xml, NfoXmlText.Utf8Encoding, cancellationToken);
	}

	internal static string BuildTvShowDocument(TvShowNfoContent content)
	{
		var sb = new StringBuilder(256);
		sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
		sb.Append("<tvshow>\n");
		sb.Append("  <title>").Append(NfoXmlText.EscapeElementText(content.Title)).Append("</title>\n");
		if (content.Year.HasValue)
			sb.Append("  <year>").Append(content.Year.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("</year>\n");
		if (content.Plot is not null)
			sb.Append("  <plot>").Append(NfoXmlText.EscapeElementText(content.Plot)).Append("</plot>\n");
		sb.Append("</tvshow>\n");
		return sb.ToString();
	}

	internal static string BuildSeasonDocument(SeasonNfoContent content)
	{
		var sb = new StringBuilder(200);
		sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
		sb.Append("<season>\n");
		sb.Append("  <seasonnumber>").Append(content.SeasonNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("</seasonnumber>\n");
		sb.Append("  <title>").Append(NfoXmlText.EscapeElementText(content.Title)).Append("</title>\n");
		if (content.Year.HasValue)
			sb.Append("  <year>").Append(content.Year.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("</year>\n");
		sb.Append("</season>\n");
		return sb.ToString();
	}

	internal static string BuildEpisodeDocument(EpisodeNfoContent content)
	{
		var sb = new StringBuilder(280);
		sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n");
		sb.Append("<episodedetails>\n");
		sb.Append("  <title>").Append(NfoXmlText.EscapeElementText(content.Title)).Append("</title>\n");
		sb.Append("  <season>").Append(content.Season.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("</season>\n");
		sb.Append("  <episode>").Append(content.Episode.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("</episode>\n");
		if (content.Plot is not null)
			sb.Append("  <plot>").Append(NfoXmlText.EscapeElementText(content.Plot)).Append("</plot>\n");
		if (content.Aired is not null)
			sb.Append("  <aired>").Append(NfoXmlText.EscapeElementText(content.Aired)).Append("</aired>\n");
		if (!string.IsNullOrWhiteSpace(content.YoutubeVideoId))
			sb.Append("  <uniqueid type=\"youtube\" default=\"true\">")
				.Append(NfoXmlText.EscapeElementText(content.YoutubeVideoId.Trim()))
				.Append("</uniqueid>\n");
		sb.Append("</episodedetails>\n");
		return sb.ToString();
	}
}

internal readonly record struct TvShowNfoContent(string Title, int? Year, string? Plot);

internal readonly record struct SeasonNfoContent(int SeasonNumber, string Title, int? Year);

internal readonly record struct EpisodeNfoContent(string Title, int Season, int Episode, string? Plot, string? Aired, string? YoutubeVideoId = null);
