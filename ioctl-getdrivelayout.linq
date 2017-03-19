<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Management.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Configuration.Install.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\Microsoft.JScript.dll</Reference>
  <NuGetReference>BetterWin32Errors</NuGetReference>
  <Namespace>BetterWin32Errors</Namespace>
  <Namespace>Microsoft.Win32.SafeHandles</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Management</Namespace>
  <Namespace>System.Dynamic</Namespace>
</Query>

// Note: Requires Administrator access

void Main()
{
	Environment.CurrentDirectory = Path.GetDirectoryName(Util.CurrentQueryPath);

	foreach (var disk in GetInstances("Win32_DiskDrive"))
	{
		string deviceId = disk.DeviceID;
		PrintHeader($"{deviceId} ({disk.Caption})");
		GetDiskLayout(deviceId).Dump();
	}
}

IEnumerable<IoctlDiskGetDriveLayout_PartitionInfo> GetDiskLayout(string path)
{
	using (var deviceHandle = OpenPath(path))
	{
		var enumerator = default(IoctlVolumeGetVolumeDiskExtentsEnumerator);
		if (!IoctlDiskGetDriveLayout_Enumerate(deviceHandle, ref enumerator))
			throw new Win32Exception();

		try
		{
			while (IoctlDiskGetDriveLayout_Next(ref enumerator))
				yield return enumerator.Current;
		}
		finally
		{
			IoctlDiskGetDriveLayout_Done(ref enumerator);
		}
	}
}

static void PrintHeader(string text, string tag = "h3")
{
	new XElement("LINQPad.HTML", new XElement(tag, text)).Dump();
}

static dynamic[] GetInstances(string className, string scope = null)
{
	using (var mgmtClass = new ManagementClass(scope, className, new ObjectGetOptions()))
	using (var instances = mgmtClass.GetInstances())
	{
		return instances.OfType<ManagementObject>()
			.Select(x => PropertiesToObject(x.Properties))
			.ToArray();
	}
}

static dynamic PropertiesToObject(PropertyDataCollection properties)
{
	var result = (IDictionary<string, object>)new ExpandoObject();
	foreach (var p in properties)
		result[p.Name] = p.Value;
	return result;
}

static SafeFileHandle OpenPath(
	string path,
	FileMode mode = FileMode.Open,
	FileAccess access = FileAccess.Read,
	FileShare fileShare = FileShare.ReadWrite,
	FileAttributes attributes = FileAttributes.Normal)
{
    var fileHandle = CreateFile(
        fileName:				path,
        fileAccess: 			access,
        fileShare: 				fileShare,
        securityAttributes: 	IntPtr.Zero,
        creationDisposition: 	mode,
        flags: 					FileAttributes.Normal,
        template: 				IntPtr.Zero);

	if (fileHandle.IsInvalid)
		throw new Win32Exception();

	return fileHandle;
}

[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
static extern SafeFileHandle CreateFile(
	string fileName,
	[MarshalAs(UnmanagedType.U4)] FileAccess fileAccess,
	[MarshalAs(UnmanagedType.U4)] FileShare fileShare,
	IntPtr securityAttributes,
	[MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
	[MarshalAs(UnmanagedType.U4)] FileAttributes flags,
	IntPtr template);

[DllImport(@".\Debug\Win32Adapter.dll", SetLastError = true)]
static extern bool IoctlDiskGetDriveLayout_Enumerate(SafeFileHandle device, ref IoctlVolumeGetVolumeDiskExtentsEnumerator enumerator);

[DllImport(@".\Debug\Win32Adapter.dll", SetLastError = true)]
static extern bool IoctlDiskGetDriveLayout_Next(ref IoctlVolumeGetVolumeDiskExtentsEnumerator enumerator);

[DllImport(@".\Debug\Win32Adapter.dll")]
static extern void IoctlDiskGetDriveLayout_Done(ref IoctlVolumeGetVolumeDiskExtentsEnumerator enumerator);

[StructLayout(LayoutKind.Sequential)]
struct IoctlVolumeGetVolumeDiskExtentsEnumerator
{
	public uint PartitionStyle;
	public int i;
	public int Total;
	public IoctlDiskGetDriveLayout_PartitionInfo Current;
	public IntPtr Handle;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct IoctlDiskGetDriveLayout_PartitionInfo
{
	public long StartingOffset;
	public long PartitionLength;
	public uint PartitionNumber;
	public MbrPartitionType MbrPartitionType;
	public bool MbrIsRecognizedType;
	public bool MbrIsBootable;
	public uint MbrHiddenSectorCount;
	public Guid PartitionType;
	public Guid PartitionId;
	public ulong Attributes;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 36)]
	public string Name;
}

// Source: <ntddisk.h>
enum MbrPartitionType : byte
{
    PARTITION_ENTRY_UNUSED          = 0x00, // Entry unused
    PARTITION_FAT_12                = 0x01, // 12-bit FAT entries
    PARTITION_XENIX_1               = 0x02, // Xenix
    PARTITION_XENIX_2               = 0x03, // Xenix
    PARTITION_FAT_16                = 0x04, // 16-bit FAT entries
    PARTITION_EXTENDED              = 0x05, // Extended partition entry
    PARTITION_HUGE                  = 0x06, // Huge partition MS-DOS V4
    PARTITION_IFS                   = 0x07, // IFS Partition
    PARTITION_OS2BOOTMGR            = 0x0A, // OS/2 Boot Manager/OPUS/Coherent swap
    PARTITION_FAT32                 = 0x0B, // FAT32
    PARTITION_FAT32_XINT13          = 0x0C, // FAT32 using extended int13 services
    PARTITION_XINT13                = 0x0E, // Win95 partition using extended int13 services
    PARTITION_XINT13_EXTENDED       = 0x0F, // Same as type 5 but uses extended int13 services
    PARTITION_PREP                  = 0x41, // PowerPC Reference Platform (PReP) Boot Partition
    PARTITION_LDM                   = 0x42, // Logical Disk Manager partition
    PARTITION_DM                    = 0x54, // OnTrack Disk Manager partition
    PARTITION_EZDRIVE               = 0x55, // EZ-Drive partition
    PARTITION_UNIX                  = 0x63, // Unix
    PARTITION_SPACES                = 0xE7, // Storage Spaces protective partition
    PARTITION_GPT                   = 0xEE, // Gpt protective partition
}