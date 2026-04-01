namespace TubeArr.Backend.Data;

/// <summary>
/// Stored Connect / notification definition (Servarr-style JSON payload).
/// </summary>
public sealed class NotificationConnectionEntity
{
	public int Id { get; set; }

	/// <summary>Full API resource JSON (merged with schema fields on read/write).</summary>
	public string PayloadJson { get; set; } = "{}";
}
