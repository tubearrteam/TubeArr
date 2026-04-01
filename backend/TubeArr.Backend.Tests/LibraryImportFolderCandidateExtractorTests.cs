using TubeArr.Backend.Data;
using Xunit;

namespace TubeArr.Backend.Tests;

public sealed class LibraryImportFolderCandidateExtractorTests
{
	[Fact]
	public void CollectCandidates_finds_UC_in_folder_name()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-libimp-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			var uc = "UC" + new string('x', 22);
			var c = LibraryImportFolderCandidateExtractor.CollectCandidates(dir, $"My {uc} Stuff");
			Assert.Contains(uc, c);
		}
		finally
		{
			try { Directory.Delete(dir); } catch { /* ignore */ }
		}
	}

	[Fact]
	public void CollectCandidates_finds_bracketed_video_id_in_file()
	{
		var dir = Path.Combine(Path.GetTempPath(), "tubearr-libimp-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		var vid = "dQw4w9WgXcQ";
		try
		{
			File.WriteAllText(Path.Combine(dir, $"Episode [{vid}].mkv"), "");
			var c = LibraryImportFolderCandidateExtractor.CollectCandidates(dir, "Unknown");
			Assert.Contains($"https://www.youtube.com/watch?v={vid}", c);
		}
		finally
		{
			try { Directory.Delete(dir, true); } catch { /* ignore */ }
		}
	}

	[Fact]
	public void BuildMappedNormalizedPaths_includes_default_channel_folder_under_root()
	{
		var root = Path.Combine(Path.GetTempPath(), "tubearr-libimp-" + Guid.NewGuid().ToString("N"));
		var channelFolder = Path.Combine(root, "Test Ch");
		Directory.CreateDirectory(channelFolder);
		try
		{
			var rf = new RootFolderEntity { Id = 1, Path = root };
			var naming = new NamingConfigEntity { Id = 1, ChannelFolderFormat = "{ Channel Name }" };
			var ch = new ChannelEntity
			{
				Id = 1,
				YoutubeChannelId = "UCxxxxxxxxxxxxxxxxxxxxxxxxxx",
				Title = "Test Ch",
				TitleSlug = "test-ch"
			};

			var mapped = LibraryImportFolderCandidateExtractor.BuildMappedNormalizedPaths(rf, [ch], naming, 1);
			Assert.Contains(Path.GetFullPath(channelFolder), mapped);
		}
		finally
		{
			try { Directory.Delete(root, true); } catch { /* ignore */ }
		}
	}

	[Fact]
	public void BuildMappedNormalizedPaths_does_not_reserve_same_folder_name_on_a_different_library_root()
	{
		var root1 = Path.Combine(Path.GetTempPath(), "tubearr-libimp-a-" + Guid.NewGuid().ToString("N"));
		var root2 = Path.Combine(Path.GetTempPath(), "tubearr-libimp-b-" + Guid.NewGuid().ToString("N"));
		var folderOn1 = Path.Combine(root1, "Linus Tech Tips");
		var folderOn2 = Path.Combine(root2, "Linus Tech Tips");
		Directory.CreateDirectory(folderOn1);
		Directory.CreateDirectory(folderOn2);
		try
		{
			var rf2 = new RootFolderEntity { Id = 2, Path = root2 };
			var naming = new NamingConfigEntity { Id = 1, ChannelFolderFormat = "{ Channel Name }" };
			var ch = new ChannelEntity
			{
				Id = 1,
				YoutubeChannelId = "UCxxxxxxxxxxxxxxxxxxxxxxxxxx",
				Title = "Someone Else",
				TitleSlug = "someone-else",
				Path = "Linus Tech Tips",
				RootFolderPath = root1
			};

			var mappedOn2 = LibraryImportFolderCandidateExtractor.BuildMappedNormalizedPaths(rf2, [ch], naming, 2);
			Assert.DoesNotContain(Path.GetFullPath(folderOn2), mappedOn2);
		}
		finally
		{
			try { Directory.Delete(root1, true); } catch { /* ignore */ }
			try { Directory.Delete(root2, true); } catch { /* ignore */ }
		}
	}

	[Fact]
	public void BuildMappedNormalizedPaths_absolute_channel_path_ignored_when_not_under_scanned_root()
	{
		var libRoot = Path.Combine(Path.GetTempPath(), "tubearr-libimp-lib-" + Guid.NewGuid().ToString("N"));
		var otherTree = Path.Combine(Path.GetTempPath(), "tubearr-libimp-oth-" + Guid.NewGuid().ToString("N"));
		var channelDir = Path.Combine(otherTree, "Ch");
		Directory.CreateDirectory(libRoot);
		Directory.CreateDirectory(channelDir);
		try
		{
			var rf = new RootFolderEntity { Id = 1, Path = libRoot };
			var naming = new NamingConfigEntity { Id = 1, ChannelFolderFormat = "{ Channel Name }" };
			var ch = new ChannelEntity
			{
				Id = 1,
				YoutubeChannelId = "UCxxxxxxxxxxxxxxxxxxxxxxxxxx",
				Title = "Far Away",
				TitleSlug = "far-away",
				Path = channelDir
			};

			var mapped = LibraryImportFolderCandidateExtractor.BuildMappedNormalizedPaths(rf, [ch], naming, 1);
			Assert.Empty(mapped);
		}
		finally
		{
			try { Directory.Delete(libRoot, true); } catch { /* ignore */ }
			try { Directory.Delete(otherTree, true); } catch { /* ignore */ }
		}
	}
}
