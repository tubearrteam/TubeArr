using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TubeArr.Backend.Serialization;

/// <summary>
/// Minimal API and SignalR JSON settings. SQLite maps <see cref="DateTime"/> columns as <see cref="DateTimeKind.Unspecified"/>
/// even when values are UTC; the default serializer omits an offset so browsers parse the string as <b>local</b> wall time
/// (wrong calendar day). We always write UTC with <c>Z</c> and read ambiguous strings as UTC.
/// </summary>
public static class TubeArrJsonSerializer
{
	public static void ApplyApiDefaults(JsonSerializerOptions options)
	{
		options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
		options.PropertyNameCaseInsensitive = true;
		options.Converters.Add(new UtcInstantDateTimeJsonConverter());
	}
}

internal sealed class UtcInstantDateTimeJsonConverter : JsonConverter<DateTime>
{
	public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.String)
			throw new JsonException("Expected string token for DateTime.");

		var s = reader.GetString();
		if (string.IsNullOrWhiteSpace(s))
			throw new JsonException("Empty DateTime string.");

		if (!DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
			throw new JsonException($"Unrecognized DateTime: {s}");

		return dt.Kind switch
		{
			DateTimeKind.Utc => dt,
			DateTimeKind.Local => dt.ToUniversalTime(),
			_ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
		};
	}

	public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
	{
		var utc = value.Kind switch
		{
			DateTimeKind.Utc => value,
			DateTimeKind.Local => value.ToUniversalTime(),
			_ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
		};
		writer.WriteStringValue(utc.ToString("O", CultureInfo.InvariantCulture));
	}
}
