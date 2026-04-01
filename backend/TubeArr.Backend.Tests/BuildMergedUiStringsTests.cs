using TubeArr.Backend;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class BuildMergedUiStringsTests
{
	[Fact]
	public void Merges_overlay_onto_english_and_keeps_missing_keys()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-loc-merge-" + Guid.NewGuid().ToString("N"));
		var loc = Path.Combine(dir, "Shared", "Localization");
		Directory.CreateDirectory(loc);
		try
		{
			File.WriteAllText(
				Path.Combine(loc, "languages.json"),
				"""
				[
				  {"id":0,"name":"English","code":"en","dictionaryFile":"en.json","enabled":true},
				  {"id":1,"name":"Spanish","code":"es","dictionaryFile":"es.json","enabled":true}
				]
				""");
			File.WriteAllText(Path.Combine(loc, "en.json"), """{"A":"one","B":"two"}""");
			File.WriteAllText(Path.Combine(loc, "es.json"), """{"A":"uno"}""");

			var merged = ProgramStartupHelpers.BuildMergedUiStrings(dir, 1);
			Assert.Equal("uno", merged["A"]);
			Assert.Equal("two", merged["B"]);
		}
		finally
		{
			try { Directory.Delete(dir, true); } catch { /* best-effort */ }
		}
	}

	[Fact]
	public void Unknown_language_id_falls_back_to_english_only()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-loc-merge-" + Guid.NewGuid().ToString("N"));
		var loc = Path.Combine(dir, "Shared", "Localization");
		Directory.CreateDirectory(loc);
		try
		{
			File.WriteAllText(
				Path.Combine(loc, "languages.json"),
				"""[{"id":0,"name":"English","code":"en","dictionaryFile":"en.json","enabled":true}]""");
			File.WriteAllText(Path.Combine(loc, "en.json"), """{"X":"en"}""");

			var merged = ProgramStartupHelpers.BuildMergedUiStrings(dir, 99);
			Assert.Equal("en", merged["X"]);
		}
		finally
		{
			try { Directory.Delete(dir, true); } catch { /* best-effort */ }
		}
	}
}
