<Query Kind="Program">
  <NuGetReference>BetterWin32Errors</NuGetReference>
  <Namespace>BetterWin32Errors</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
</Query>

void Main()
{
	GetDeviceMap().Dump();
}

IEnumerable<DeviceMapping> GetDeviceMap()
{
	var size = 32 * 1024;
	var buffer = new byte[size];
	var actual = (int)QueryDosDevice(null, buffer, (uint)buffer.Length);
	if (actual == 0)
		throw new Win32Exception();

	var devices = Encoding.UTF8.GetString(buffer, 0, actual)
		.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
	return devices
		.Select(x => new DeviceMapping
		{
			Device = x,
			Path = LookupPath(x),
		})
		.OrderBy(x => x.Device);
}

class DeviceMapping
{
	public string Device { get; set; }
	public string Path { get; set; }
}

string LookupPath(string device)
{
	var buffer = new StringBuilder(4096, 4096);
	var result = QueryDosDevice_StringBuilder(device, buffer, (uint)buffer.Capacity);
	if (result == 0)
		throw new Win32Exception();

	return buffer.ToString();
}

[DllImport("kernel32.dll", SetLastError = true)]
static extern uint QueryDosDevice(
	string deviceName,
	[Out] byte[] targetPath,
	uint targetPathmax);

[DllImport("kernel32.dll", EntryPoint = "QueryDosDevice", SetLastError = true)]
static extern uint QueryDosDevice_StringBuilder(
	string deviceName,
	[Out] StringBuilder targetPath,
	uint targetPathmax);