using System;
using System.IO;
using TubeArr.Backend.Data;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class YtDlpCookiesPathResolverTests
{
	[Fact]
	public void GetDefaultCookiesTxtPath_prefers_directory_next_to_executable()
	{
		var path = YtDlpCookiesPathResolver.GetDefaultCookiesTxtPath(@"C:\tools\bin\yt-dlp.exe", @"D:\app");
		Assert.Equal(Path.Combine(@"C:\tools\bin", "cookies.txt"), path);
	}

	[Fact]
	public void GetDefaultCookiesTxtPath_falls_back_to_contentRoot_ytdlp()
	{
		var path = YtDlpCookiesPathResolver.GetDefaultCookiesTxtPath(null, @"D:\app");
		Assert.Equal(Path.Combine(@"D:\app", "yt-dlp", "cookies.txt"), path);
	}

	[Fact]
	public void GetEffectiveCookiesFilePath_resolves_relative_path_next_to_executable()
	{
		var temp = Path.Combine(Path.GetTempPath(), "tubearr-cookies-" + Guid.NewGuid().ToString("N"));
		var exeDir = Path.Combine(temp, "bin");
		Directory.CreateDirectory(exeDir);
		var cookieFile = Path.Combine(exeDir, "mine.txt");
		File.WriteAllText(cookieFile, "# Netscape\n");

		var config = new YtDlpConfigEntity
		{
			ExecutablePath = Path.Combine(exeDir, "yt-dlp.exe"),
			CookiesPath = "mine.txt"
		};

		var found = YtDlpCookiesPathResolver.GetEffectiveCookiesFilePath(config, temp);
		Assert.Equal(Path.GetFullPath(cookieFile), found);

		try
		{
			Directory.Delete(temp, recursive: true);
		}
		catch
		{
			/* best-effort */
		}
	}

	[Fact]
	public void GetEffectiveCookiesFilePath_falls_back_to_default_when_rooted_cookiesPath_missing()
	{
		var temp = Path.Combine(Path.GetTempPath(), "tubearr-cookies-stale-" + Guid.NewGuid().ToString("N"));
		var exeDir = Path.Combine(temp, "bin");
		Directory.CreateDirectory(exeDir);
		var goodCookie = Path.Combine(exeDir, "cookies.txt");
		File.WriteAllText(goodCookie, "# Netscape\n");

		var config = new YtDlpConfigEntity
		{
			ExecutablePath = Path.Combine(exeDir, "yt-dlp.exe"),
			CookiesPath = @"C:\this\path\does\not\exist\cookies.txt"
		};

		var found = YtDlpCookiesPathResolver.GetEffectiveCookiesFilePath(config, temp);
		Assert.Equal(Path.GetFullPath(goodCookie), found);

		try
		{
			Directory.Delete(temp, recursive: true);
		}
		catch
		{
			/* best-effort */
		}
	}

	[Fact]
	public void GetEffectiveCookiesFilePath_uses_default_location_when_cookiesPath_empty()
	{
		var temp = Path.Combine(Path.GetTempPath(), "tubearr-cookies-def-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(temp, "yt-dlp"));
		var cookieFile = Path.Combine(temp, "yt-dlp", "cookies.txt");
		File.WriteAllText(cookieFile, "# Netscape\n");

		var config = new YtDlpConfigEntity
		{
			ExecutablePath = "",
			CookiesPath = ""
		};

		var found = YtDlpCookiesPathResolver.GetEffectiveCookiesFilePath(config, temp);
		Assert.Equal(Path.GetFullPath(cookieFile), found);

		try
		{
			Directory.Delete(temp, recursive: true);
		}
		catch
		{
			/* best-effort */
		}
	}
}
