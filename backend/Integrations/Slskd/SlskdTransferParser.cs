using System.Text.Json;

namespace TubeArr.Backend.Integrations.Slskd;

public static class SlskdTransferParser
{
	public static bool TryParseEnqueueResponse(string json, out Guid transferId)
	{
		transferId = default;
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (root.TryGetProperty("enqueued", out var en) && en.ValueKind == JsonValueKind.Array)
			{
				foreach (var item in en.EnumerateArray())
				{
					if (TryReadGuid(item, out transferId))
						return true;
				}
			}

			// Some versions may return transfers directly
			if (TryReadGuid(root, out transferId))
				return true;
		}
		catch
		{
			/* ignore */
		}

		return false;
	}

	static bool TryReadGuid(JsonElement el, out Guid id)
	{
		id = default;
		if (el.ValueKind == JsonValueKind.String)
			return Guid.TryParse(el.GetString(), out id);
		if (el.TryGetProperty("id", out var idProp))
		{
			if (idProp.ValueKind == JsonValueKind.String && Guid.TryParse(idProp.GetString(), out id))
				return true;
		}

		return false;
	}

	public static bool IsTransferCompleted(string json, out double? percent, out string? filename, out string? error)
	{
		percent = null;
		filename = null;
		error = null;
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (root.TryGetProperty("percentComplete", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var pd))
				percent = pd;
			if (root.TryGetProperty("filename", out var fn) && fn.ValueKind == JsonValueKind.String)
				filename = fn.GetString();
			if (root.TryGetProperty("exception", out var ex) && ex.ValueKind == JsonValueKind.String)
				error = ex.GetString();

			if (!root.TryGetProperty("state", out var st))
				return false;

			var stateStr = "";
			if (st.ValueKind == JsonValueKind.String)
				stateStr = st.GetString() ?? "";
			else if (st.ValueKind == JsonValueKind.Number && st.TryGetInt32(out var sn))
				stateStr = sn.ToString();

			return IsCompletedState(stateStr);
		}
		catch
		{
			return false;
		}
	}

	public static bool IsTerminalFailure(string json, out string? message)
	{
		message = null;
		try
		{
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;
			if (!root.TryGetProperty("state", out var st))
				return false;
			var stateStr = st.ValueKind == JsonValueKind.String ? st.GetString() ?? "" : "";
			if (st.ValueKind == JsonValueKind.Number && st.TryGetInt32(out var n))
				stateStr = n.ToString();

			if (stateStr.Contains("abort", StringComparison.OrdinalIgnoreCase)
			    || stateStr.Contains("reject", StringComparison.OrdinalIgnoreCase)
			    || stateStr.Contains("error", StringComparison.OrdinalIgnoreCase)
			    || stateStr.Contains("fail", StringComparison.OrdinalIgnoreCase))
			{
				if (root.TryGetProperty("exception", out var ex) && ex.ValueKind == JsonValueKind.String)
					message = ex.GetString();
				return true;
			}
		}
		catch
		{
			/* ignore */
		}

		return false;
	}

	static bool IsCompletedState(string stateStr)
	{
		if (string.IsNullOrEmpty(stateStr))
			return false;
		if (stateStr.Equals("Completed", StringComparison.OrdinalIgnoreCase))
			return true;
		if (stateStr.Contains("complete", StringComparison.OrdinalIgnoreCase)
		    && !stateStr.Contains("incomplete", StringComparison.OrdinalIgnoreCase))
			return true;
		// Soulseek.TransferStates.Completed is often numeric 3 in older APIs — treat 3 as completed
		if (stateStr == "3")
			return true;
		return false;
	}
}
