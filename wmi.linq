<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Management.dll</Reference>
  <Namespace>System.Management</Namespace>
  <Namespace>System.Dynamic</Namespace>
</Query>

void Main()
{
	var legacyClasses = new[]
	{
		"Win32_MountPoint",
		"Win32_DiskDrive",
		"Win32_Volume",
	};
	foreach (var c in legacyClasses)
	{
		PrintHeader(c);
		GetInstances(c).Dump();
	}

	var storageScope = @"\\.\ROOT\Microsoft\Windows\Storage";
	var newClasses = new[]
	{
		"MSFT_Disk",
		"MSFT_PhysicalDisk",
	};
	foreach (var c in newClasses)
	{
		PrintHeader(c);
		GetInstances(c, storageScope).Dump();
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

static dynamic[] Query(string scope, string query)
{
	using (var searcher = new ManagementObjectSearcher(scope, query))
	{
		return searcher.Get()
			.OfType<ManagementObject>()
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

// Source: WinBase.h
enum DriveType : uint
{
	DRIVE_UNKNOWN 		= 0,
	DRIVE_NO_ROOT_DIR 	= 1,
	DRIVE_REMOVABLE 	= 2,
	DRIVE_FIXED 		= 3,
	DRIVE_REMOTE 		= 4,
	DRIVE_CDROM 		= 5,
	DRIVE_RAMDISK 		= 6,
}