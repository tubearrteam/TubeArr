using System.Text.Json;
using TubeArr.Backend.Serialization;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class JsonDateTimeSerializationTests
{
	static JsonSerializerOptions Options()
	{
		var o = new JsonSerializerOptions();
		TubeArrJsonSerializer.ApplyApiDefaults(o);
		return o;
	}

	[Fact]
	public void Unspecified_DateTime_serializes_as_utc_z()
	{
		var wall = DateTime.SpecifyKind(new DateTime(2026, 4, 1, 2, 52, 0), DateTimeKind.Unspecified);
		var json = JsonSerializer.Serialize(new { date = wall }, Options());
		Assert.Contains("2026-04-01T02:52:00", json, StringComparison.Ordinal);
		Assert.Contains("Z", json, StringComparison.Ordinal);
	}

	[Fact]
	public void Nullable_unspecified_DateTime_serializes_as_utc_z()
	{
		DateTime? wall = DateTime.SpecifyKind(new DateTime(2026, 4, 1, 2, 52, 0), DateTimeKind.Unspecified);
		var json = JsonSerializer.Serialize(new { date = wall }, Options());
		Assert.Contains("Z", json, StringComparison.Ordinal);
	}

	[Fact]
	public void Roundtrip_preserves_instant()
	{
		var opts = Options();
		var original = DateTime.SpecifyKind(new DateTime(2026, 3, 31, 21, 52, 0), DateTimeKind.Unspecified);
		var json = JsonSerializer.Serialize(new { date = original }, opts);
		var back = JsonSerializer.Deserialize<Holder>(json, opts);
		Assert.NotNull(back);
		Assert.Equal(DateTimeKind.Utc, back!.Date.Kind);
		Assert.Equal(original.Ticks, back.Date.Ticks);
	}

	sealed class Holder
	{
		public DateTime Date { get; set; }
	}
}
