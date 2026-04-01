namespace TubeArr.Backend;

internal static class RootFolderPathProbe
{
	public static (bool Accessible, long? FreeSpace) GetStats(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return (false, null);

		try
		{
			var full = Path.GetFullPath(path.Trim());
			if (!Directory.Exists(full))
				return (false, null);

			var root = Path.GetPathRoot(full);
			if (string.IsNullOrEmpty(root))
				return (true, null);

			try
			{
				var di = new DriveInfo(root);
				if (!di.IsReady)
					return (true, null);
				return (true, di.AvailableFreeSpace);
			}
			catch
			{
				return (true, null);
			}
		}
		catch
		{
			return (false, null);
		}
	}
}
