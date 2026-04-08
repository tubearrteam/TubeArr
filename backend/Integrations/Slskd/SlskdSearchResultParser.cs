using System.Text.Json;

namespace TubeArr.Backend.Integrations.Slskd;

public static class SlskdSearchResultParser
{
	public static bool TryParseSearchComplete(string json, out bool isComplete, out List<ParsedRemoteFile> files)
	{
		isComplete = false;
		files = new List<ParsedRemoteFile>();
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (root.TryGetProperty("isComplete", out var ic) && ic.ValueKind == JsonValueKind.True)
				isComplete = true;
			else if (root.TryGetProperty("state", out var st) && st.ValueKind == JsonValueKind.String)
			{
				var s = st.GetString() ?? "";
				if (s.Contains("complete", StringComparison.OrdinalIgnoreCase) && !s.Contains("incomplete", StringComparison.OrdinalIgnoreCase))
					isComplete = true;
			}

			if (!root.TryGetProperty("responses", out var responses) || responses.ValueKind != JsonValueKind.Array)
				return true;

			foreach (var resp in responses.EnumerateArray())
			{
				var username = "";
				if (resp.TryGetProperty("username", out var un) && un.ValueKind == JsonValueKind.String)
					username = un.GetString() ?? "";

				if (!resp.TryGetProperty("files", out var fileArr) || fileArr.ValueKind != JsonValueKind.Array)
					continue;

				foreach (var f in fileArr.EnumerateArray())
				{
					var filename = "";
					if (f.TryGetProperty("filename", out var fn) && fn.ValueKind == JsonValueKind.String)
						filename = fn.GetString() ?? "";
					if (string.IsNullOrWhiteSpace(filename))
						continue;

					long size = 0;
					if (f.TryGetProperty("size", out var sz))
					{
						if (sz.ValueKind == JsonValueKind.Number && sz.TryGetInt64(out var l))
							size = l;
					}

					int? length = null;
					if (f.TryGetProperty("length", out var len) && len.ValueKind == JsonValueKind.Number && len.TryGetInt32(out var li))
						length = li;

					int? bitrate = null;
					if (f.TryGetProperty("bitrate", out var br) && br.ValueKind == JsonValueKind.Number && br.TryGetInt32(out var bi))
						bitrate = bi;

					var ext = "";
					if (f.TryGetProperty("extension", out var ex) && ex.ValueKind == JsonValueKind.String)
						ext = ex.GetString() ?? "";

					files.Add(new ParsedRemoteFile(username, filename, size, ext, length, bitrate));
				}
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool TryParseSearchId(string json, out Guid id)
	{
		id = default;
		try
		{
			using var doc = JsonDocument.Parse(json);
			if (doc.RootElement.TryGetProperty("id", out var idEl))
			{
				if (idEl.ValueKind == JsonValueKind.String && Guid.TryParse(idEl.GetString(), out id))
					return true;
				if (idEl.ValueKind == JsonValueKind.String)
					return Guid.TryParse(idEl.GetString(), out id);
			}
		}
		catch
		{
			/* ignore */
		}

		return false;
	}
}

public sealed record ParsedRemoteFile(
	string Username,
	string Filename,
	long Size,
	string Extension,
	int? DurationSeconds,
	int? BitrateKbps);
