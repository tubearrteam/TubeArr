using System.Runtime.InteropServices;

namespace TubeArr.Backend;

/// <summary>
/// Best-effort (volume serial, file index) for duplicate / hardlink detection during library scans.
/// Implemented for Windows (<see cref="GetFileInformationByHandle"/>). Other OSes: <see cref="TryGetIdentity"/> returns false
/// (hardlink duplicates are not detected there without a portable inode API).
/// </summary>
internal static class FileVolumeIdentityHelper
{
	internal readonly struct FileVolumeIdentity : IEquatable<FileVolumeIdentity>
	{
		public FileVolumeIdentity(ulong volumeSerial, ulong fileIndex)
		{
			VolumeSerial = volumeSerial;
			FileIndex = fileIndex;
		}

		public ulong VolumeSerial { get; }
		public ulong FileIndex { get; }

		public bool Equals(FileVolumeIdentity other) =>
			VolumeSerial == other.VolumeSerial && FileIndex == other.FileIndex;

		public override bool Equals(object? obj) => obj is FileVolumeIdentity o && Equals(o);

		public override int GetHashCode() => HashCode.Combine(VolumeSerial, FileIndex);
	}

	internal static bool TryGetIdentity(string path, out FileVolumeIdentity identity)
	{
		identity = default;
		if (!OperatingSystem.IsWindows())
			return false;
		if (string.IsNullOrWhiteSpace(path))
			return false;
		try
		{
			var full = Path.GetFullPath(path);
			if (!File.Exists(full))
				return false;
			return TryGetWindows(full, out identity);
		}
		catch
		{
			return false;
		}
	}

	static bool TryGetWindows(string fullPath, out FileVolumeIdentity identity)
	{
		identity = default;
		var handle = CreateFileW(
			fullPath,
			0x80, // FILE_READ_ATTRIBUTES
			FileShare.ReadWrite | FileShare.Delete,
			IntPtr.Zero,
			FileMode.Open,
			0x02000000, // FILE_FLAG_BACKUP_SEMANTICS
			IntPtr.Zero);
		if (handle == new IntPtr(-1))
			return false;
		try
		{
			if (!GetFileInformationByHandle(handle, out var info))
				return false;
			var idx = ((ulong)info.nFileIndexHigh << 32) | info.nFileIndexLow;
			identity = new FileVolumeIdentity(info.dwVolumeSerialNumber, idx);
			return true;
		}
		finally
		{
			CloseHandle(handle);
		}
	}

	[DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
	static extern IntPtr CreateFileW(
		string lpFileName,
		uint dwDesiredAccess,
		FileShare dwShareMode,
		IntPtr lpSecurityAttributes,
		FileMode dwCreationDisposition,
		uint dwFlagsAndAttributes,
		IntPtr hTemplateFile);

	[DllImport("kernel32", SetLastError = true)]
	static extern bool CloseHandle(IntPtr h);

	[DllImport("kernel32", SetLastError = true)]
	static extern bool GetFileInformationByHandle(IntPtr h, out BY_HANDLE_FILE_INFORMATION lp);

	[StructLayout(LayoutKind.Sequential)]
	struct BY_HANDLE_FILE_INFORMATION
	{
		public uint dwFileAttributes;
		public uint ftCreationTimeLow;
		public uint ftCreationTimeHigh;
		public uint ftLastAccessTimeLow;
		public uint ftLastAccessTimeHigh;
		public uint ftLastWriteTimeLow;
		public uint ftLastWriteTimeHigh;
		public uint dwVolumeSerialNumber;
		public uint nFileSizeHigh;
		public uint nFileSizeLow;
		public uint nNumberOfLinks;
		public uint nFileIndexHigh;
		public uint nFileIndexLow;
	}
}
