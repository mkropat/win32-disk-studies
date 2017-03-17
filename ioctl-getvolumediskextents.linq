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

void Main()
{
	Environment.CurrentDirectory = Path.GetDirectoryName(Util.CurrentQueryPath);

	foreach (var volume in GetInstances("Win32_Volume"))
	{
		string deviceId = volume.DeviceID.TrimEnd('\\');
		PrintHeader(volume.Caption);
		try
		{
			GetDiskDeviceNumbersForVolume(deviceId).Dump();
		}
		catch (Exception ex)
		{
			ex.Dump();
		}
	}
}

public static IEnumerable<uint> GetDiskDeviceNumbersForVolume(string path)
{
	using (var deviceHandle = OpenPath(path))
	{
		var enumerator = default(IoctlVolumeGetVolumeDiskExtentsEnumerator);
		if (!IoctlVolumeGetVolumeDiskExtents_Enumerate(deviceHandle, ref enumerator))
			throw new Win32Exception();

		try
		{
			while (IoctlVolumeGetVolumeDiskExtents_Next(ref enumerator))
				yield return enumerator.current.diskNumber;
		}
		finally
		{
			IoctlVolumeGetVolumeDiskExtents_Done(ref enumerator);
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
static extern bool IoctlVolumeGetVolumeDiskExtents_Enumerate(SafeFileHandle device, ref IoctlVolumeGetVolumeDiskExtentsEnumerator enumerator);

[DllImport(@".\Debug\Win32Adapter.dll")]
static extern bool IoctlVolumeGetVolumeDiskExtents_Next(ref IoctlVolumeGetVolumeDiskExtentsEnumerator enumerator);

[DllImport(@".\Debug\Win32Adapter.dll")]
static extern void IoctlVolumeGetVolumeDiskExtents_Done(ref IoctlVolumeGetVolumeDiskExtentsEnumerator enumerator);

[StructLayout(LayoutKind.Sequential)]
struct IoctlVolumeGetVolumeDiskExtentsEnumerator
{
	public short i;
	public DISK_EXTENT current;
	public IntPtr handle;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISK_EXTENT
{
	public uint diskNumber;
	public long startingOffset;
	public long extentLength;
}