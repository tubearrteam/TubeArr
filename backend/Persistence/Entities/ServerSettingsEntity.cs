namespace TubeArr.Backend.Data;

public sealed class ServerSettingsEntity
{
	public int Id { get; set; } = 1;

	public string BindAddress { get; set; } = "*";
	public int Port { get; set; } = 5075;
	public string UrlBase { get; set; } = "";
	public string InstanceName { get; set; } = "TubeArr";
	public string ApplicationUrl { get; set; } = "";

	public bool EnableSsl { get; set; } = false;
	public int SslPort { get; set; } = 9898;
	public string SslCertPath { get; set; } = "";
	public string SslCertPassword { get; set; } = "";
	public bool LaunchBrowser { get; set; } = true;

	public string AuthenticationMethod { get; set; } = "none";
	public string AuthenticationRequired { get; set; } = "enabled";
	public string Username { get; set; } = "";
	public string Password { get; set; } = "";
	public string ApiKey { get; set; } = "";
	public string CertificateValidation { get; set; } = "enabled";

	public bool ProxyEnabled { get; set; } = false;
	public string ProxyType { get; set; } = "http";
	public string ProxyHostname { get; set; } = "";
	public int ProxyPort { get; set; } = 8080;
	public string ProxyUsername { get; set; } = "";
	public string ProxyPassword { get; set; } = "";
	public string ProxyBypassFilter { get; set; } = "";
	public bool ProxyBypassLocalAddresses { get; set; } = true;

	public string LogLevel { get; set; } = "info";
	public string ConsoleLogLevel { get; set; } = "info";
	public int LogSizeLimit { get; set; } = 1;

	public bool AnalyticsEnabled { get; set; } = false;

	public string Branch { get; set; } = "main";
	public bool UpdateAutomatically { get; set; } = false;
	public string UpdateMechanism { get; set; } = "builtIn";
	public string UpdateScriptPath { get; set; } = "";

	public string BackupFolder { get; set; } = "";
	public int BackupInterval { get; set; } = 7;
	public int BackupRetention { get; set; } = 28;
}