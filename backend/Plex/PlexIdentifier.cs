using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace TubeArr.Backend.Plex;

public static class PlexIdentifier
{
	internal const string CustomPlaylistSeasonPrefix = "cst_";

	static readonly Regex RatingKeySafe = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

	public enum PlexItemKind
	{
		Show,
		Season,
		Episode
	}

	internal static string BuildRatingKey(PlexItemKind kind, string youtubeId)
	{
		var trimmed = (youtubeId ?? "").Trim();
		var prefix = kind switch
		{
			PlexItemKind.Show => "ch_",
			PlexItemKind.Season => "pl_",
			PlexItemKind.Episode => "v_",
			_ => ""
		};

		var ratingKey = prefix + trimmed;
		if (!IsSafeRatingKey(ratingKey))
			throw new ArgumentException("Unsafe ratingKey", nameof(youtubeId));
		return ratingKey;
	}

	internal static bool IsSafeRatingKey(string? ratingKey)
	{
		var s = (ratingKey ?? "").Trim();
		if (string.IsNullOrWhiteSpace(s))
			return false;
		if (s.Contains('/'))
			return false;
		return RatingKeySafe.IsMatch(s);
	}

	internal static string BuildGuid(PlexItemKind kind, string ratingKey)
	{
		var type = kind switch
		{
			PlexItemKind.Show => "show",
			PlexItemKind.Season => "season",
			PlexItemKind.Episode => "episode",
			_ => "show"
		};

		if (!IsSafeRatingKey(ratingKey))
			throw new ArgumentException("Unsafe ratingKey", nameof(ratingKey));

		return PlexConstants.Scheme + "://" + type + "/" + ratingKey;
	}

	internal static string BuildCustomPlaylistSeasonRatingKey(int channelCustomPlaylistId)
	{
		var rk = CustomPlaylistSeasonPrefix + channelCustomPlaylistId.ToString(CultureInfo.InvariantCulture);
		if (!IsSafeRatingKey(rk))
			throw new ArgumentException("Unsafe ratingKey", nameof(channelCustomPlaylistId));
		return rk;
	}

	internal static bool TryParseRatingKey(string ratingKey, out PlexItemKind kind, out string youtubeId)
	{
		kind = PlexItemKind.Show;
		youtubeId = "";

		var rk = (ratingKey ?? "").Trim();
		if (!IsSafeRatingKey(rk))
			return false;

		if (rk.StartsWith("ch_", StringComparison.Ordinal))
		{
			kind = PlexItemKind.Show;
			youtubeId = rk["ch_".Length..];
			return youtubeId.Length > 0;
		}
		if (rk.StartsWith("pl_", StringComparison.Ordinal))
		{
			kind = PlexItemKind.Season;
			youtubeId = rk["pl_".Length..];
			return youtubeId.Length > 0;
		}
		if (rk.StartsWith(CustomPlaylistSeasonPrefix, StringComparison.Ordinal))
		{
			var rest = rk[CustomPlaylistSeasonPrefix.Length..];
			if (rest.Length == 0 || !rest.All(char.IsDigit))
				return false;
			kind = PlexItemKind.Season;
			youtubeId = rest;
			return true;
		}
		if (rk.StartsWith("v_", StringComparison.Ordinal))
		{
			kind = PlexItemKind.Episode;
			youtubeId = rk["v_".Length..];
			return youtubeId.Length > 0;
		}

		return false;
	}
}

