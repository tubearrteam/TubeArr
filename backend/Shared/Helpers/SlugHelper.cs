namespace TubeArr.Backend;

public static class SlugHelper
{
	public static string Slugify(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return "";

		Span<char> buffer = stackalloc char[value.Length];
		var j = 0;
		var prevDash = false;

		foreach (var ch in value.Trim().ToLowerInvariant())
		{
			if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
			{
				buffer[j++] = ch;
				prevDash = false;
				continue;
			}

			if (!prevDash)
			{
				buffer[j++] = '-';
				prevDash = true;
			}
		}

		var result = new string(buffer[..j]);
		return result.Trim('-');
	}
}
