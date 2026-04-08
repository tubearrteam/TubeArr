using Microsoft.Extensions.Configuration;

namespace TubeArr.Backend;

/// <summary>Runtime feature switches from configuration (<c>TubeArr:Features:*</c>), also exposed via initialize.json.</summary>
public sealed class TubeArrRuntimeFeatures
{
	public TubeArrRuntimeFeatures(IConfiguration configuration)
	{
		ExperimentalMetadataDebug = configuration.GetValue("TubeArr:Features:ExperimentalMetadataDebug", false);
	}

	public bool ExperimentalMetadataDebug { get; }
}
