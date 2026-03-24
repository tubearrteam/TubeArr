namespace TubeArr.Backend.Data;

public sealed class ImportListOptionsConfigEntity
{
	public int Id { get; set; } = 1;

	public string ListSyncLevel { get; set; } = "disabled";
	public int ListSyncTag { get; set; } = 0;
}

