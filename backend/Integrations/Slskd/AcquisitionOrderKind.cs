namespace TubeArr.Backend.Integrations.Slskd;

public enum AcquisitionOrderKind
{
	YtDlpFirst = 0,
	SlskdFirst = 1,
	SlskdOnly = 2,
	YtDlpOnly = 3
}

public static class AcquisitionOrderKindExtensions
{
	public static AcquisitionOrderKind ParseOrDefault(int value) =>
		Enum.IsDefined(typeof(AcquisitionOrderKind), value)
			? (AcquisitionOrderKind)value
			: AcquisitionOrderKind.YtDlpFirst;

	public static string ToApiString(this AcquisitionOrderKind k) => k switch
	{
		AcquisitionOrderKind.YtDlpFirst => "ytdlpFirst",
		AcquisitionOrderKind.SlskdFirst => "slskdFirst",
		AcquisitionOrderKind.SlskdOnly => "slskdOnly",
		AcquisitionOrderKind.YtDlpOnly => "ytdlpOnly",
		_ => "ytdlpFirst"
	};

	public static bool TryParseApi(string? s, out AcquisitionOrderKind kind)
	{
		kind = AcquisitionOrderKind.YtDlpFirst;
		if (string.IsNullOrWhiteSpace(s))
			return false;
		switch (s.Trim().ToLowerInvariant())
		{
			case "ytdlpfirst":
			case "yt-dlp-first":
				kind = AcquisitionOrderKind.YtDlpFirst;
				return true;
			case "slskdfirst":
			case "slskd-first":
				kind = AcquisitionOrderKind.SlskdFirst;
				return true;
			case "slskdonly":
			case "slskd-only":
				kind = AcquisitionOrderKind.SlskdOnly;
				return true;
			case "ytdlponly":
			case "yt-dlp-only":
				kind = AcquisitionOrderKind.YtDlpOnly;
				return true;
			default:
				return false;
		}
	}
}
