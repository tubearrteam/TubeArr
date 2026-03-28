namespace TubeArr.Backend.DownloadBackends;

public enum DownloadFailureStage
{
	None = 0,
	BrowserLaunchFailed = 1,
	CookieLoadFailed = 2,
	NavigationFailed = 3,
	AuthNotEstablished = 4,
	NoMediaCandidatesFound = 5,
	CandidateSelectionFailed = 6,
	StreamDownloadFailed = 7,
	MuxFailed = 8,
	YtDlpProcessFailed = 9,
	OutputNotFound = 10,
	InvalidConfiguration = 11,
	Unknown = 99
}
