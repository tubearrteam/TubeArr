namespace TubeArr.Backend.QualityProfile;

/// <summary>
/// Controls how format selection degrades when the preferred quality is unavailable.
/// </summary>
public enum FallbackMode
{
	/// <summary>Fail if preferred constraints cannot be satisfied.</summary>
	Strict = 0,

	/// <summary>Keep resolution ceiling; relax codec/fps/container/audio preferences first.</summary>
	NextBestWithinCeiling = 1,

	/// <summary>Relax preferences, then step down resolution through allowed height steps.</summary>
	DegradeResolution = 2,

	/// <summary>Same as NextBestWithinCeiling but commonly used alias.</summary>
	NextBest = 3
}
