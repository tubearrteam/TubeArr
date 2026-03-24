using System.Text.Json;
using System.Text.Json.Serialization;

namespace TubeArr.Backend;

/// <summary>
/// Serializes/deserializes tags as either a comma-separated string or an array of numbers (tag ids).
/// </summary>
public sealed class TagsJsonConverter : JsonConverter<string?>
{
	public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Null)
			return null;
		if (reader.TokenType == JsonTokenType.String)
			return reader.GetString();
		if (reader.TokenType == JsonTokenType.StartArray)
		{
			var list = new List<int>();
			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndArray)
					break;
				if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n))
					list.Add(n);
			}
			return list.Count == 0 ? null : string.Join(",", list);
		}
		return null;
	}

	public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
	{
		if (value is null)
			writer.WriteNullValue();
		else
			writer.WriteStringValue(value);
	}
}
