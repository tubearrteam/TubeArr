namespace TubeArr.Backend.Data;

public sealed class PlexProviderConfigEntity
{
	public int Id { get; set; } = 1;

	public bool Enabled { get; set; } = false;
	public string BasePath { get; set; } = "";
	public bool ExposeArtworkUrls { get; set; } = false;
	public bool IncludeChildrenByDefault { get; set; } = true;
	public bool VerboseRequestLogging { get; set; } = false;
}

