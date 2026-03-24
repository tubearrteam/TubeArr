namespace TubeArr.Backend.Data;

public sealed class CustomFilterEntity
{
	public int Id { get; set; }
	public string Type { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string FiltersJson { get; set; } = "[]";
}
