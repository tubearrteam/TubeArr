using System;
using TubeArr.Backend.Media.Nfo;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class NfoXmlTextTests
{
	[Fact]
	public void EscapeElementText_escapes_ampersand_lt_gt()
	{
		Assert.Equal("a &amp; b &lt; c &gt;", NfoXmlText.EscapeElementText("a & b < c >"));
	}

	[Fact]
	public void EscapeElementText_preserves_emoji_after_strip()
	{
		var s = NfoXmlText.EscapeElementText("🔥 & <tag>");
		Assert.Contains("🔥", s, StringComparison.Ordinal);
		Assert.Contains("&amp;", s, StringComparison.Ordinal);
	}

	[Fact]
	public void StripInvalidXmlChar_removes_surrogates_and_control_chars()
	{
		var raw = "a\u0001b\ud800c"; // lone surrogate
		var t = NfoXmlText.StripInvalidXmlChars(raw);
		Assert.DoesNotContain("\u0001", t, StringComparison.Ordinal);
		Assert.DoesNotContain("\ud800", t, StringComparison.Ordinal);
	}

	[Fact]
	public void NormalizeOptionalPlot_null_for_whitespace_only()
	{
		Assert.Null(NfoXmlText.NormalizeOptionalPlot(null));
		Assert.Null(NfoXmlText.NormalizeOptionalPlot("   "));
		Assert.Null(NfoXmlText.NormalizeOptionalPlot("\n\t  \n"));
	}

	[Fact]
	public void NormalizeOptionalPlot_collapses_single_line_spaces()
	{
		var n = NfoXmlText.NormalizeOptionalPlot("  hello   world  ");
		Assert.Equal("hello world", n);
	}

	[Fact]
	public void NormalizeOptionalPlot_preserves_newlines_in_multiline()
	{
		var n = NfoXmlText.NormalizeOptionalPlot("line1\n\nline2");
		Assert.NotNull(n);
		Assert.Contains("line1", n, StringComparison.Ordinal);
		Assert.Contains("line2", n, StringComparison.Ordinal);
		Assert.Contains('\n', n);
	}

	[Fact]
	public void TryGetCalendarYear_false_for_unix_epoch()
	{
		Assert.False(NfoXmlText.TryGetCalendarYear(default, out _));
		Assert.False(NfoXmlText.TryGetCalendarYear(DateTimeOffset.UnixEpoch, out _));
	}

	[Fact]
	public void TryGetCalendarYear_uses_utc_year()
	{
		var dto = new DateTimeOffset(2024, 6, 18, 23, 0, 0, TimeSpan.Zero);
		Assert.True(NfoXmlText.TryGetCalendarYear(dto, out var y));
		Assert.Equal(2024, y);
	}

	[Fact]
	public void FormatAiredDate_yyyy_MM_dd_utc_date()
	{
		var dto = new DateTimeOffset(2024, 6, 18, 15, 30, 0, TimeSpan.Zero);
		Assert.Equal("2024-06-18", NfoXmlText.FormatAiredDate(dto));
	}

	[Fact]
	public void FormatAiredDate_empty_for_invalid()
	{
		Assert.Equal("", NfoXmlText.FormatAiredDate(default));
		Assert.Equal("", NfoXmlText.FormatAiredDate(DateTimeOffset.UnixEpoch));
	}
}
