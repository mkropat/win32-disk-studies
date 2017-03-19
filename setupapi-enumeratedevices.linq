<Query Kind="Program">
  <NuGetReference>BetterWin32Errors</NuGetReference>
  <Namespace>BetterWin32Errors</Namespace>
  <Namespace>Microsoft.Win32.SafeHandles</Namespace>
  <Namespace>System.Collections.Specialized</Namespace>
  <Namespace>System.Runtime.InteropServices</Namespace>
  <Namespace>System.Dynamic</Namespace>
</Query>

void Main()
{
	Environment.CurrentDirectory = Path.GetDirectoryName(Util.CurrentQueryPath);

	GetDevices(DevInterfaceIds.GUID_DEVINTERFACE_DISK, DiGetClassFlags.DIGCF_PRESENT).Dump();
}

public static IEnumerable<dynamic> GetDevices(Guid deviceClass, DiGetClassFlags flags = DiGetClassFlags.DIGCF_DEFAULT)
{
	var enumerator = default(SetupApiDeviceEnumerator);
	if (!SetupApi_Enumerate(deviceClass, flags, ref enumerator))
		throw new Win32Exception();

	try
	{
		while (SetupApi_Next(ref enumerator))
		{
			var device = (IDictionary<string, object>)new ExpandoObject();
			device["DevicePath"] = enumerator.Current;

			foreach (var kv in EnumerateEnum<DeviceRegistryCode>())
			{
				var property = ReadDeviceProperty(ref enumerator, kv.Value);
				if (property != null)
					device[kv.Key] = property;
			}
			
			yield return device;
		}

		if (enumerator.WasError)
			throw new Win32Exception();
	}
	finally
	{
		SetupApi_Done(ref enumerator);
	}
}

static object ReadDeviceProperty(ref SetupApiDeviceEnumerator enumerator, DeviceRegistryCode property)
{
	uint requiredSize;
	RegistryDataType dataType;

	SetupApi_ReadProperty(ref enumerator, property, out dataType, null, 0, out requiredSize);

	var propertyBuffer = new byte[requiredSize];
	if (!SetupApi_ReadProperty(ref enumerator, property, out dataType, propertyBuffer, propertyBuffer.Length, out requiredSize))
	{
		var error = Win32Exception.GetLastWin32Error();
		if (error == Win32Error.ERROR_INVALID_DATA)
			return null;

		throw new Win32Exception(error);
	}

	return ParseRegistryValue(propertyBuffer, dataType);
}

static object ParseRegistryValue(byte[] buffer, RegistryDataType type)
{
	switch (type)
	{
		case RegistryDataType.REG_BINARY:
			return buffer;

		case RegistryDataType.REG_DWORD:
			return BitConverter.ToUInt32(buffer, 0);

		case RegistryDataType.REG_MULTI_SZ:
			var str = Encoding.Unicode.GetString(buffer);
			return str.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);

		case RegistryDataType.REG_NONE:
		case RegistryDataType.REG_SZ:
		default:
			return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
	}
}

static IEnumerable<KeyValuePair<string, T>> EnumerateEnum<T>()
{
	foreach (T val in Enum.GetValues(typeof(T)))
	{
		yield return new KeyValuePair<string, T>(
			Enum.GetName(typeof(T), val),
			val);
	}
}

[DllImport(@".\Debug\Win32Adapter.dll", SetLastError = true)]
static extern bool SetupApi_Enumerate(Guid deviceClass, DiGetClassFlags flags, ref SetupApiDeviceEnumerator enumerator);

[DllImport(@".\Debug\Win32Adapter.dll", SetLastError = true)]
static extern bool SetupApi_Next(ref SetupApiDeviceEnumerator enumerator);

[DllImport(@".\Debug\Win32Adapter.dll", SetLastError = true)]
static extern bool SetupApi_ReadProperty(
	ref SetupApiDeviceEnumerator enumerator,
	DeviceRegistryCode property,
	out RegistryDataType propertyRegDataType,
	byte[] propertyBuffer,
	int propertyBufferSize,
	out uint requiredSize);

[DllImport(@".\Debug\Win32Adapter.dll")]
static extern void SetupApi_Done(ref SetupApiDeviceEnumerator enumerator);

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct SetupApiDeviceEnumerator
{
	public int i;
	public bool WasError;

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
	public string Current;

	public IntPtr Handle;
};

// Source: <SetupApi.h>
[Flags]
public enum DiGetClassFlags : uint
{
    DIGCF_DEFAULT 			= 0x00000001,  // only valid with DIGCF_DEVICEINTERFACE
    DIGCF_PRESENT 			= 0x00000002,
    DIGCF_ALLCLASSES 		= 0x00000004,
    DIGCF_PROFILE 			= 0x00000008,
    DIGCF_DEVICEINTERFACE 	= 0x00000010,
}

// Source: <SetupAPI.h>
enum DeviceRegistryCode : uint
{
    SPDRP_DEVICEDESC 			 	  = 0x00000000,  // DeviceDesc (R/W)
    SPDRP_HARDWAREID 				  = 0x00000001,  // HardwareID (R/W)
    SPDRP_COMPATIBLEIDS 			  = 0x00000002,  // CompatibleIDs (R/W)
    SPDRP_UNUSED0 					  = 0x00000003,  // unused
    SPDRP_SERVICE 					  = 0x00000004,  // Service (R/W)
    SPDRP_UNUSED1 					  = 0x00000005,  // unused
    SPDRP_UNUSED2 				      = 0x00000006,  // unused
    SPDRP_CLASS 					  = 0x00000007,  // Class (R--tied to ClassGUID)
    SPDRP_CLASSGUID 				  = 0x00000008,  // ClassGUID (R/W)
    SPDRP_DRIVER 					  = 0x00000009,  // Driver (R/W)
    SPDRP_CONFIGFLAGS                 = 0x0000000A,  // ConfigFlags (R/W)
    SPDRP_MFG                         = 0x0000000B,  // Mfg (R/W)
    SPDRP_FRIENDLYNAME                = 0x0000000C,  // FriendlyName (R/W)
    SPDRP_LOCATION_INFORMATION        = 0x0000000D,  // LocationInformation (R/W)
    SPDRP_PHYSICAL_DEVICE_OBJECT_NAME = 0x0000000E,  // PhysicalDeviceObjectName (R)
    SPDRP_CAPABILITIES                = 0x0000000F,  // Capabilities (R)
    SPDRP_UI_NUMBER                   = 0x00000010,  // UiNumber (R)
    SPDRP_UPPERFILTERS                = 0x00000011,  // UpperFilters (R/W)
    SPDRP_LOWERFILTERS                = 0x00000012,  // LowerFilters (R/W)
    SPDRP_BUSTYPEGUID                 = 0x00000013,  // BusTypeGUID (R)
    SPDRP_LEGACYBUSTYPE               = 0x00000014,  // LegacyBusType (R)
    SPDRP_BUSNUMBER                   = 0x00000015,  // BusNumber (R)
    SPDRP_ENUMERATOR_NAME             = 0x00000016,  // Enumerator Name (R)
    SPDRP_SECURITY                    = 0x00000017,  // Security (R/W, binary form)
    SPDRP_SECURITY_SDS                = 0x00000018,  // Security (W, SDS form)
    SPDRP_DEVTYPE                     = 0x00000019,  // Device Type (R/W)
    SPDRP_EXCLUSIVE                   = 0x0000001A,  // Device is exclusive-access (R/W)
    SPDRP_CHARACTERISTICS             = 0x0000001B,  // Device Characteristics (R/W)
    SPDRP_ADDRESS                     = 0x0000001C,  // Device Address (R)
    SPDRP_UI_NUMBER_DESC_FORMAT       = 0X0000001D,  // UiNumberDescFormat (R/W)
    SPDRP_DEVICE_POWER_DATA           = 0x0000001E,  // Device Power Data (R)
    SPDRP_REMOVAL_POLICY              = 0x0000001F,  // Removal Policy (R)
    SPDRP_REMOVAL_POLICY_HW_DEFAULT   = 0x00000020,  // Hardware Removal Policy (R)
    SPDRP_REMOVAL_POLICY_OVERRIDE     = 0x00000021,  // Removal Policy Override (RW)
    SPDRP_INSTALL_STATE               = 0x00000022,  // Device Install State (R)
    SPDRP_LOCATION_PATHS              = 0x00000023,  // Device Location Paths (R)
    SPDRP_BASE_CONTAINERID            = 0x00000024,  // Base ContainerID (R)
}

// Source: <winnt.h>
enum RegistryDataType : uint
{
    REG_NONE                    = 0, // No value type
    REG_SZ                      = 1, // Unicode nul terminated string
    REG_EXPAND_SZ               = 2, // Unicode nul terminated string
                                     // (with environment variable references)
    REG_BINARY                  = 3, // Free form binary
    REG_DWORD                   = 4, // 32-bit number
    REG_DWORD_LITTLE_ENDIAN     = 4, // 32-bit number (same as REG_DWORD)
    REG_DWORD_BIG_ENDIAN        = 5, // 32-bit number
    REG_LINK                    = 6, // Symbolic Link (unicode)
    REG_MULTI_SZ                = 7, // Multiple Unicode strings
    REG_RESOURCE_LIST           = 8, // Resource list in the resource map
    REG_FULL_RESOURCE_DESCRIPTOR = 9, // Resource list in the hardware description
    REG_RESOURCE_REQUIREMENTS_LIST = 10,
    REG_QWORD                   = 11, // 64-bit number
    REG_QWORD_LITTLE_ENDIAN     = 11, // 64-bit number (same as REG_QWORD)
}

// Source: http://msdn.microsoft.com/en-us/library/windows/hardware/ff553412(v=vs.85).aspx
public static class DevInterfaceIds
{
    public static readonly Guid BUS1394_CLASS_GUID                       = new Guid("6BDD1FC1-810F-11d0-BEC7-08002BE2092F");
    public static readonly Guid GUID_61883_CLASS                         = new Guid("7EBEFBC0-3200-11d2-B4C2-00A0C9697D07");
    public static readonly Guid GUID_DEVICE_APPLICATIONLAUNCH_BUTTON     = new Guid("629758EE-986E-4D9E-8E47-DE27F8AB054D");
    public static readonly Guid GUID_DEVICE_BATTERY                      = new Guid("72631E54-78A4-11D0-BCF7-00AA00B7B32A");
    public static readonly Guid GUID_DEVICE_LID                          = new Guid("4AFA3D52-74A7-11d0-be5e-00A0C9062857");
    public static readonly Guid GUID_DEVICE_MEMORY                       = new Guid("3FD0F03D-92E0-45FB-B75C-5ED8FFB01021");
    public static readonly Guid GUID_DEVICE_MESSAGE_INDICATOR            = new Guid("CD48A365-FA94-4CE2-A232-A1B764E5D8B4");
    public static readonly Guid GUID_DEVICE_PROCESSOR                    = new Guid("97FADB10-4E33-40AE-359C-8BEF029DBDD0");
    public static readonly Guid GUID_DEVICE_SYS_BUTTON                   = new Guid("4AFA3D53-74A7-11d0-be5e-00A0C9062857");
    public static readonly Guid GUID_DEVICE_THERMAL_ZONE                 = new Guid("4AFA3D51-74A7-11d0-be5e-00A0C9062857");
    public static readonly Guid GUID_BTHPORT_DEVICE_INTERFACE            = new Guid("0850302A-B344-4fda-9BE9-90576B8D46F0");
    public static readonly Guid GUID_DEVINTERFACE_BRIGHTNESS             = new Guid("FDE5BBA4-B3F9-46FB-BDAA-0728CE3100B4");
    public static readonly Guid GUID_DEVINTERFACE_DISPLAY_ADAPTER        = new Guid("5B45201D-F2F2-4F3B-85BB-30FF1F953599");
    public static readonly Guid GUID_DEVINTERFACE_I2C                    = new Guid("2564AA4F-DDDB-4495-B497-6AD4A84163D7");
    public static readonly Guid GUID_DEVINTERFACE_IMAGE                  = new Guid("6BDD1FC6-810F-11D0-BEC7-08002BE2092F");
    public static readonly Guid GUID_DEVINTERFACE_MONITOR                = new Guid("E6F07B5F-EE97-4a90-B076-33F57BF4EAA7");
    public static readonly Guid GUID_DEVINTERFACE_OPM                    = new Guid("BF4672DE-6B4E-4BE4-A325-68A91EA49C09");
    public static readonly Guid GUID_DEVINTERFACE_VIDEO_OUTPUT_ARRIVAL   = new Guid("1AD9E4F0-F88D-4360-BAB9-4C2D55E564CD");
    public static readonly Guid GUID_DISPLAY_DEVICE_ARRIVAL              = new Guid("1CA05180-A699-450A-9A0C-DE4FBE3DDD89");
    public static readonly Guid GUID_DEVINTERFACE_HID                    = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
    public static readonly Guid GUID_DEVINTERFACE_KEYBOARD               = new Guid("884b96c3-56ef-11d1-bc8c-00a0c91405dd");
    public static readonly Guid GUID_DEVINTERFACE_MOUSE                  = new Guid("378DE44C-56EF-11D1-BC8C-00A0C91405DD");
    public static readonly Guid GUID_DEVINTERFACE_MODEM                  = new Guid("2C7089AA-2E0E-11D1-B114-00C04FC2AAE4");
    public static readonly Guid GUID_DEVINTERFACE_NET                    = new Guid("CAC88484-7515-4C03-82E6-71A87ABAC361");
    public static readonly Guid GUID_DEVINTERFACE_SENSOR                 = new Guid(0XBA1BB692, 0X9B7A, 0X4833, 0X9A, 0X1E, 0X52, 0X5E, 0XD1, 0X34, 0XE7, 0XE2);
    public static readonly Guid GUID_DEVINTERFACE_COMPORT                = new Guid("86E0D1E0-8089-11D0-9CE4-08003E301F73");
    public static readonly Guid GUID_DEVINTERFACE_PARALLEL               = new Guid("97F76EF0-F883-11D0-AF1F-0000F800845C");
    public static readonly Guid GUID_DEVINTERFACE_PARCLASS               = new Guid("811FC6A5-F728-11D0-A537-0000F8753ED1");
    public static readonly Guid GUID_DEVINTERFACE_SERENUM_BUS_ENUMERATOR = new Guid("4D36E978-E325-11CE-BFC1-08002BE10318");
    public static readonly Guid GUID_DEVINTERFACE_CDCHANGER              = new Guid("53F56312-B6BF-11D0-94F2-00A0C91EFB8B");
    public static readonly Guid GUID_DEVINTERFACE_CDROM                  = new Guid("53F56308-B6BF-11D0-94F2-00A0C91EFB8B");
    public static readonly Guid GUID_DEVINTERFACE_DISK                   = new Guid("53F56307-B6BF-11D0-94F2-00A0C91EFB8B");
    public static readonly Guid GUID_DEVINTERFACE_FLOPPY                 = new Guid("53F56311-B6BF-11D0-94F2-00A0C91EFB8B");
    public static readonly Guid GUID_DEVINTERFACE_MEDIUMCHANGER          = new Guid("53F56310-B6BF-11D0-94F2-00A0C91EFB8B");
    public static readonly Guid GUID_DEVINTERFACE_PARTITION              = new Guid("53F5630A-B6BF-11D0-94F2-00A0C91EFB8B");
    public static readonly Guid GUID_DEVINTERFACE_STORAGEPORT            = new Guid("2ACCFE60-C130-11D2-B082-00A0C91EFB8B");
    public static readonly Guid GUID_DEVINTERFACE_TAPE                   = new Guid("53F5630B-B6BF-11D0-94F2-00A0C91EFB8B");
    public static readonly Guid GUID_DEVINTERFACE_VOLUME                 = new Guid("53F5630D-B6BF-11D0-94F2-00A0C91EFB8B");
    public static readonly Guid GUID_DEVINTERFACE_WRITEONCEDISK          = new Guid("53F5630C-B6BF-11D0-94F2-00A0C91EFB8B");
    public static readonly Guid GUID_IO_VOLUME_DEVICE_INTERFACE          = new Guid("53F5630D-B6BF-11D0-94F2-00A0C91EFB8B");
    public static readonly Guid MOUNTDEV_MOUNTED_DEVICE_GUID             = new Guid("53F5630D-B6BF-11D0-94F2-00A0C91EFB8B");
    public static readonly Guid GUID_AVC_CLASS                           = new Guid("095780C3-48A1-4570-BD95-46707F78C2DC");
    public static readonly Guid GUID_VIRTUAL_AVC_CLASS                   = new Guid("616EF4D0-23CE-446D-A568-C31EB01913D0");
    public static readonly Guid KSCATEGORY_ACOUSTIC_ECHO_CANCEL          = new Guid("BF963D80-C559-11D0-8A2B-00A0C9255AC1");
    public static readonly Guid KSCATEGORY_AUDIO                         = new Guid("6994AD04-93EF-11D0-A3CC-00A0C9223196");
    public static readonly Guid KSCATEGORY_AUDIO_DEVICE                  = new Guid("FBF6F530-07B9-11D2-A71E-0000F8004788");
    public static readonly Guid KSCATEGORY_AUDIO_GFX                     = new Guid("9BAF9572-340C-11D3-ABDC-00A0C90AB16F");
    public static readonly Guid KSCATEGORY_AUDIO_SPLITTER                = new Guid("9EA331FA-B91B-45F8-9285-BD2BC77AFCDE");
    public static readonly Guid KSCATEGORY_BDA_IP_SINK                   = new Guid("71985F4A-1CA1-11d3-9CC8-00C04F7971E0");
    public static readonly Guid KSCATEGORY_BDA_NETWORK_EPG               = new Guid("71985F49-1CA1-11d3-9CC8-00C04F7971E0");
    public static readonly Guid KSCATEGORY_BDA_NETWORK_PROVIDER          = new Guid("71985F4B-1CA1-11d3-9CC8-00C04F7971E0");
    public static readonly Guid KSCATEGORY_BDA_NETWORK_TUNER             = new Guid("71985F48-1CA1-11d3-9CC8-00C04F7971E0");
    public static readonly Guid KSCATEGORY_BDA_RECEIVER_COMPONENT        = new Guid("FD0A5AF4-B41D-11d2-9C95-00C04F7971E0");
    public static readonly Guid KSCATEGORY_BDA_TRANSPORT_INFORMATION     = new Guid("A2E3074F-6C3D-11d3-B653-00C04F79498E");
    public static readonly Guid KSCATEGORY_BRIDGE                        = new Guid("085AFF00-62CE-11CF-A5D6-28DB04C10000");
    public static readonly Guid KSCATEGORY_CAPTURE                       = new Guid("65E8773D-8F56-11D0-A3B9-00A0C9223196");
    public static readonly Guid KSCATEGORY_CLOCK                         = new Guid("53172480-4791-11D0-A5D6-28DB04C10000");
    public static readonly Guid KSCATEGORY_COMMUNICATIONSTRANSFORM       = new Guid("CF1DDA2C-9743-11D0-A3EE-00A0C9223196");
    public static readonly Guid KSCATEGORY_CROSSBAR                      = new Guid("A799A801-A46D-11D0-A18C-00A02401DCD4");
    public static readonly Guid KSCATEGORY_DATACOMPRESSOR                = new Guid("1E84C900-7E70-11D0-A5D6-28DB04C10000");
    public static readonly Guid KSCATEGORY_DATADECOMPRESSOR              = new Guid("2721AE20-7E70-11D0-A5D6-28DB04C10000");
    public static readonly Guid KSCATEGORY_DATATRANSFORM                 = new Guid("2EB07EA0-7E70-11D0-A5D6-28DB04C10000");
    public static readonly Guid KSCATEGORY_DRM_DESCRAMBLE                = new Guid("FFBB6E3F-CCFE-4D84-90D9-421418B03A8E");
    public static readonly Guid KSCATEGORY_ENCODER                       = new Guid("19689BF6-C384-48fd-AD51-90E58C79F70B");
    public static readonly Guid KSCATEGORY_ESCALANTE_PLATFORM_DRIVER     = new Guid("74F3AEA8-9768-11D1-8E07-00A0C95EC22E");
    public static readonly Guid KSCATEGORY_FILESYSTEM                    = new Guid("760FED5E-9357-11D0-A3CC-00A0C9223196");
    public static readonly Guid KSCATEGORY_INTERFACETRANSFORM            = new Guid("CF1DDA2D-9743-11D0-A3EE-00A0C9223196");
    public static readonly Guid KSCATEGORY_MEDIUMTRANSFORM               = new Guid("CF1DDA2E-9743-11D0-A3EE-00A0C9223196");
    public static readonly Guid KSCATEGORY_MICROPHONE_ARRAY_PROCESSOR    = new Guid("830A44F2-A32D-476B-BE97-42845673B35A");
    public static readonly Guid KSCATEGORY_MIXER                         = new Guid("AD809C00-7B88-11D0-A5D6-28DB04C10000");
    public static readonly Guid KSCATEGORY_MULTIPLEXER                   = new Guid("7A5DE1D3-01A1-452c-B481-4FA2B96271E8");
    public static readonly Guid KSCATEGORY_NETWORK                       = new Guid("67C9CC3C-69C4-11D2-8759-00A0C9223196");
    public static readonly Guid KSCATEGORY_PREFERRED_MIDIOUT_DEVICE      = new Guid("D6C50674-72C1-11D2-9755-0000F8004788");
    public static readonly Guid KSCATEGORY_PREFERRED_WAVEIN_DEVICE       = new Guid("D6C50671-72C1-11D2-9755-0000F8004788");
    public static readonly Guid KSCATEGORY_PREFERRED_WAVEOUT_DEVICE      = new Guid("D6C5066E-72C1-11D2-9755-0000F8004788");
    public static readonly Guid KSCATEGORY_PROXY                         = new Guid("97EBAACA-95BD-11D0-A3EA-00A0C9223196");
    public static readonly Guid KSCATEGORY_QUALITY                       = new Guid("97EBAACB-95BD-11D0-A3EA-00A0C9223196");
    public static readonly Guid KSCATEGORY_REALTIME                      = new Guid("EB115FFC-10C8-4964-831D-6DCB02E6F23F");
    public static readonly Guid KSCATEGORY_RENDER                        = new Guid("65E8773E-8F56-11D0-A3B9-00A0C9223196");
    public static readonly Guid KSCATEGORY_SPLITTER                      = new Guid("0A4252A0-7E70-11D0-A5D6-28DB04C10000");
    public static readonly Guid KSCATEGORY_SYNTHESIZER                   = new Guid("DFF220F3-F70F-11D0-B917-00A0C9223196");
    public static readonly Guid KSCATEGORY_SYSAUDIO                      = new Guid("A7C7A5B1-5AF3-11D1-9CED-00A024BF0407");
    public static readonly Guid KSCATEGORY_TEXT                          = new Guid("6994AD06-93EF-11D0-A3CC-00A0C9223196");
    public static readonly Guid KSCATEGORY_TOPOLOGY                      = new Guid("DDA54A40-1E4C-11D1-A050-405705C10000");
    public static readonly Guid KSCATEGORY_TVAUDIO                       = new Guid("A799A802-A46D-11D0-A18C-00A02401DCD4");
    public static readonly Guid KSCATEGORY_TVTUNER                       = new Guid("A799A800-A46D-11D0-A18C-00A02401DCD4");
    public static readonly Guid KSCATEGORY_VBICODEC                      = new Guid("07DAD660-22F1-11D1-A9F4-00C04FBBDE8F");
    public static readonly Guid KSCATEGORY_VIDEO                         = new Guid("6994AD05-93EF-11D0-A3CC-00A0C9223196");
    public static readonly Guid KSCATEGORY_VIRTUAL                       = new Guid("3503EAC4-1F26-11D1-8AB0-00A0C9223196");
    public static readonly Guid KSCATEGORY_VPMUX                         = new Guid("A799A803-A46D-11D0-A18C-00A02401DCD4");
    public static readonly Guid KSCATEGORY_WDMAUD                        = new Guid("3E227E76-690D-11D2-8161-0000F8775BF1");
    public static readonly Guid KSMFT_CATEGORY_AUDIO_DECODER             = new Guid("9ea73fb4-ef7a-4559-8d5d-719d8f0426c7");
    public static readonly Guid KSMFT_CATEGORY_AUDIO_EFFECT              = new Guid("11064c48-3648-4ed0-932e-05ce8ac811b7");
    public static readonly Guid KSMFT_CATEGORY_AUDIO_ENCODER             = new Guid("91c64bd0-f91e-4d8c-9276-db248279d975");
    public static readonly Guid KSMFT_CATEGORY_DEMULTIPLEXER             = new Guid("a8700a7a-939b-44c5-99d7-76226b23b3f1");
    public static readonly Guid KSMFT_CATEGORY_MULTIPLEXER               = new Guid("059c561e-05ae-4b61-b69d-55b61ee54a7b");
    public static readonly Guid KSMFT_CATEGORY_OTHER                     = new Guid("90175d57-b7ea-4901-aeb3-933a8747756f");
    public static readonly Guid KSMFT_CATEGORY_VIDEO_DECODER             = new Guid("d6c02d4b-6833-45b4-971a-05a4b04bab91");
    public static readonly Guid KSMFT_CATEGORY_VIDEO_EFFECT              = new Guid("12e17c21-532c-4a6e-8a1c-40825a736397");
    public static readonly Guid KSMFT_CATEGORY_VIDEO_ENCODER             = new Guid("f79eac7d-e545-4387-bdee-d647d7bde42a");
    public static readonly Guid KSMFT_CATEGORY_VIDEO_PROCESSOR           = new Guid("302ea3fc-aa5f-47f9-9f7a-c2188bb16302");
    public static readonly Guid GUID_DEVINTERFACE_USB_DEVICE             = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");
    public static readonly Guid GUID_DEVINTERFACE_USB_HOST_CONTROLLER    = new Guid("3ABF6F2D-71C4-462A-8A92-1E6861E6AF27");
    public static readonly Guid GUID_DEVINTERFACE_USB_HUB                = new Guid("F18A0E88-C30C-11D0-8815-00A0C906BED8");
    public static readonly Guid GUID_DEVINTERFACE_WPD                    = new Guid("6AC27878-A6FA-4155-BA85-F98F491D4F33");
    public static readonly Guid GUID_DEVINTERFACE_WPD_PRIVATE            = new Guid("BA0C718F-4DED-49B7-BDD3-FABE28661211");
    public static readonly Guid GUID_DEVINTERFACE_SIDESHOW               = new Guid("152E5811-FEB9-4B00-90F4-D32947AE1681");
}