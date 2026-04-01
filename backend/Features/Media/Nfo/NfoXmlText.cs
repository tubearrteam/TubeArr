using System.Globalization;
using System.Text;

namespace TubeArr.Backend.Media.Nfo;

/// <summary>UTF-8 XML text helpers for minimal Plex/Kodi-style NFO files.</summary>
internal static class NfoXmlText
{
	static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

	internal static UTF8Encoding Utf8Encoding => Utf8NoBom;

	/// <summary>Strips characters that are not allowed in XML 1.0, then escapes &amp; &lt; &gt;.</summary>
	internal static string EscapeElementText(string? value)
	{
		if (string.IsNullOrEmpty(value))
			return "";

		var stripped = StripInvalidXmlChars(value);
		if (stripped.Length == 0)
			return "";

		return stripped
			.Replace("&", "&amp;", StringComparison.Ordinal)
			.Replace("<", "&lt;", StringComparison.Ordinal)
			.Replace(">", "&gt;", StringComparison.Ordinal);
	}

	/// <summary>
	/// Plot/optional body: strip invalid XML, trim; if empty return null to omit the node.
	/// Preserves single newlines when the text is multi-line; otherwise normalizes horizontal whitespace.
	/// </summary>
	internal static string? NormalizeOptionalPlot(string? raw)
	{
		if (string.IsNullOrEmpty(raw))
			return null;

		var stripped = StripInvalidXmlChars(raw);
		if (stripped.Length == 0)
			return null;

		var hasNewline = stripped.Contains('\n', StringComparison.Ordinal) || stripped.Contains('\r', StringComparison.Ordinal);
		if (hasNewline)
		{
			var unified = stripped.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
			var sb = new StringBuilder(unified.Length);
			var lines = unified.Split('\n');
			for (var i = 0; i < lines.Length; i++)
			{
				if (i > 0)
					sb.Append('\n');
				var line = lines[i].TrimEnd();
				line = CollapseHorizontalWhitespace(line);
				sb.Append(line);
			}

			var s = sb.ToString().Trim();
			return s.Length == 0 ? null : s;
		}

		var single = CollapseHorizontalWhitespace(stripped).Trim();
		return single.Length == 0 ? null : single;
	}

	static string CollapseHorizontalWhitespace(string line)
	{
		if (line.Length == 0)
			return line;

		var sb = new StringBuilder(line.Length);
		var space = false;
		foreach (var ch in line)
		{
			if (char.IsWhiteSpace(ch) && ch is not '\n' and not '\r')
			{
				space = true;
				continue;
			}

			if (space && sb.Length > 0)
				sb.Append(' ');
			space = false;
			sb.Append(ch);
		}

		return sb.ToString();
	}

	internal static string StripInvalidXmlChars(string text)
	{
		if (text.Length == 0)
			return text;

		var sb = new StringBuilder(text.Length);
		foreach (var rune in text.EnumerateRunes())
		{
			var v = rune.Value;
			if (IsXmlChar(v))
				sb.Append(rune);
		}

		return sb.ToString();
	}

	static bool IsXmlChar(int v) =>
		v is 0x9 or 0xA or 0xD
		|| (v >= 0x20 && v <= 0xD7FF)
		|| (v >= 0xE000 && v <= 0xFFFD)
		|| (v >= 0x10000 && v <= 0x10FFFF);

	internal static bool TryGetCalendarYear(DateTimeOffset dto, out int year)
	{
		year = 0;
		if (dto == default)
			return false;
		if (dto == DateTimeOffset.UnixEpoch)
			return false;

		try
		{
			year = dto.UtcDateTime.Year;
		}
		catch
		{
			return false;
		}

		return year is >= 1 and <= 9999;
	}

	internal static string FormatAiredDate(DateTimeOffset uploadDateUtc)
	{
		if (uploadDateUtc == default || uploadDateUtc == DateTimeOffset.UnixEpoch)
			return "";

		try
		{
			return uploadDateUtc.UtcDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		}
		catch
		{
			return "";
		}
	}
}
