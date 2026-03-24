using System.Text.Json;
using System.Text.Json.Serialization;

namespace TubeArr.Backend.Contracts;

[JsonConverter(typeof(OptionalValueJsonConverterFactory))]
public readonly struct OptionalValue<T>
{
	public OptionalValue(T value)
	{
		IsSpecified = true;
		Value = value;
	}

	public bool IsSpecified { get; }

	public T Value { get; }

	public static implicit operator OptionalValue<T>(T value) => new(value);
}

internal sealed class OptionalValueJsonConverterFactory : JsonConverterFactory
{
	public override bool CanConvert(Type typeToConvert)
	{
		return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(OptionalValue<>);
	}

	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
	{
		var valueType = typeToConvert.GetGenericArguments()[0];
		var converterType = typeof(OptionalValueJsonConverter<>).MakeGenericType(valueType);
		return (JsonConverter)Activator.CreateInstance(converterType)!;
	}

	private sealed class OptionalValueJsonConverter<T> : JsonConverter<OptionalValue<T>>
	{
		public override OptionalValue<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
			{
				return new OptionalValue<T>(default!);
			}

			var value = JsonSerializer.Deserialize<T>(ref reader, options);
			return new OptionalValue<T>(value!);
		}

		public override void Write(Utf8JsonWriter writer, OptionalValue<T> value, JsonSerializerOptions options)
		{
			if (!value.IsSpecified)
			{
				writer.WriteNullValue();
				return;
			}

			JsonSerializer.Serialize(writer, value.Value, options);
		}
	}
}