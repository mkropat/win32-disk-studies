<Query Kind="Program">
  <NuGetReference>BetterWin32Errors</NuGetReference>
  <Namespace>BetterWin32Errors</Namespace>
  <Namespace>Microsoft.Win32.SafeHandles</Namespace>
  <Namespace>System.Collections.Specialized</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
</Query>

void Main()
{
	GetVolumes().Dump();
}

IEnumerable<Volume> GetVolumes()
{
	foreach (var id in GetVolumeIds())
	{
		var volume = new Volume
		{
			Id = id
		};

		var nameBufferSize = MAX_PATH + 1;
		var names = new StringBuilder(nameBufferSize, nameBufferSize);

		uint serialNumber;
		uint maxComponentLength;
		FileSystemFlag flags;

		var fileSystemName = new StringBuilder(nameBufferSize, nameBufferSize);

		if (GetVolumeInformation(id, names, nameBufferSize, out serialNumber, out maxComponentLength, out flags, fileSystemName, (uint)nameBufferSize))
		{
			volume.FileSystemName = fileSystemName.ToString();
			volume.Flags = flags;
			volume.Name = names.ToString();
			volume.SerialNumber = serialNumber;
		}
		else
		{
			var ignoredErrors = new[]
			{
				Win32Error.ERROR_NOT_READY,
				Win32Error.ERROR_UNRECOGNIZED_VOLUME,
			};
			var error = Win32Exception.GetLastWin32Error();
			if (!ignoredErrors.Contains(error))
				throw new Win32Exception(error);
		}

		yield return volume;
	}
}

public static IEnumerable<string> GetVolumeIds()
{
	var bufferLength = @"\\?\Volume{00000000-0000-0000-0000-000000000000}\".Length + 1;
	var volume = new StringBuilder(bufferLength, bufferLength);

	using (var volumeHandle = FindFirstVolume(volume, (uint)volume.Capacity))
	{
		if (volumeHandle.IsInvalid)
			throw new Win32Exception();

		do
		{
			yield return volume.ToString();
		} while (FindNextVolume(volumeHandle, volume, (uint)volume.Capacity));
	}
}

class Volume
{
	public string Id { get; set; }
	public string Name { get; set; }
	public uint SerialNumber { get; set; }
	public string FileSystemName { get; set; }
	public FileSystemFlag Flags { get; set; }
}

class FindVolumeSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	private FindVolumeSafeHandle()
	: base(true)
	{
	}

	public FindVolumeSafeHandle(IntPtr preexistingHandle, bool ownsHandle)
		: base(ownsHandle)
	{
		SetHandle(preexistingHandle);
	}

	protected override bool ReleaseHandle()
	{
		return FindVolumeClose(handle);
	}
}

const int MAX_PATH = 260;

[Flags]
enum FileSystemFlag : uint  {
    FILE_CASE_SENSITIVE_SEARCH          = 0x00000001,
    FILE_CASE_PRESERVED_NAMES           = 0x00000002,
    FILE_UNICODE_ON_DISK                = 0x00000004,
    FILE_PERSISTENT_ACLS                = 0x00000008,
    FILE_FILE_COMPRESSION               = 0x00000010,
    FILE_VOLUME_QUOTAS                  = 0x00000020,
    FILE_SUPPORTS_SPARSE_FILES          = 0x00000040,
    FILE_SUPPORTS_REPARSE_POINTS        = 0x00000080,
    FILE_SUPPORTS_REMOTE_STORAGE        = 0x00000100,
    FILE_VOLUME_IS_COMPRESSED           = 0x00008000,
    FILE_SUPPORTS_OBJECT_IDS            = 0x00010000,
    FILE_SUPPORTS_ENCRYPTION            = 0x00020000,
    FILE_NAMED_STREAMS                  = 0x00040000,
    FILE_READ_ONLY_VOLUME               = 0x00080000,
    FILE_SEQUENTIAL_WRITE_ONCE          = 0x00100000,
    FILE_SUPPORTS_TRANSACTIONS          = 0x00200000,
    FILE_SUPPORTS_HARD_LINKS            = 0x00400000,
    FILE_SUPPORTS_EXTENDED_ATTRIBUTES   = 0x00800000,
    FILE_SUPPORTS_OPEN_BY_FILE_ID       = 0x01000000,
    FILE_SUPPORTS_USN_JOURNAL           = 0x02000000,
    FILE_SUPPORTS_INTEGRITY_STREAMS     = 0x04000000,
    FILE_SUPPORTS_BLOCK_REFCOUNTING     = 0x08000000,
    FILE_SUPPORTS_SPARSE_VDL            = 0x10000000,
    FILE_DAX_VOLUME                     = 0x20000000,
    FILE_SUPPORTS_GHOSTING              = 0x40000000,
}

[DllImport("kernel32.dll", SetLastError = true)]
static extern FindVolumeSafeHandle FindFirstVolume([Out] StringBuilder volumeName, uint bufferLength);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool FindNextVolume(FindVolumeSafeHandle volumeHandle, [Out] StringBuilder volumeName, uint bufferLength);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool FindVolumeClose(IntPtr volumeHandle);

[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
extern static bool GetVolumeInformation(
   string rootPathName,
   [Out] StringBuilder volumeNameBuffer,
   int volumeNameSize,
   out uint volumeSerialNumber,
   out uint maximumComponentLength,
   out FileSystemFlag fileSystemFlags,
   [Out] StringBuilder fileSystemNameBuffer,
   uint fileSystemNameSize);

[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
extern static bool GetVolumeInformationByHandleW(
  FindVolumeSafeHandle handle,
  [Out] StringBuilder volumeNameBuffer,
  uint volumeNameSize,
  out uint volumeSerialNumber,
  out uint maximumComponentLength,
  out FileSystemFlag fileSystemFlags,
  [Out] StringBuilder fileSystemNameBuffer,
  uint fileSystemNameSize);