using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class PlexTvNotificationClientTests
{
	[Fact]
	public void BuildAppAuthLink_includes_client_code_and_product_in_fragment()
	{
		var link = PlexTvNotificationClient.BuildAppAuthLink("abc123xyz");
		Assert.StartsWith("https://app.plex.tv/auth#?", link, StringComparison.Ordinal);
		Assert.Contains("clientID=" + Uri.EscapeDataString(PlexTvNotificationClient.PlexClientIdentifier), link, StringComparison.Ordinal);
		Assert.Contains("code=" + Uri.EscapeDataString("abc123xyz"), link, StringComparison.Ordinal);
		Assert.Contains("context%5Bdevice%5D%5Bproduct%5D=" + Uri.EscapeDataString(PlexTvNotificationClient.PlexProductName), link, StringComparison.Ordinal);
	}

	[Fact]
	public void ParseServerOptions_prefers_local_connection()
	{
		const string xml = """
			<?xml version="1.0"?>
			<MediaContainer>
				<Device name="Home" clientIdentifier="abc-123" provides="server">
					<Connection uri="https://192-168-1-10.plex.direct:32400" local="0"/>
					<Connection uri="http://192.168.1.10:32400" local="1"/>
				</Device>
			</MediaContainer>
			""";
		var list = PlexTvNotificationClient.ParseServerOptions(xml);
		Assert.Single(list);
		var s = list[0];
		Assert.Equal("abc-123", s.ClientIdentifier);
		Assert.Equal("192.168.1.10", s.Host);
		Assert.Equal(32400, s.Port);
		Assert.False(s.UseSsl);
	}
}
