#include "SetupApi.h"

struct _DeviceEnumeratorState {
	GUID deviceClass;
	HDEVINFO devInfo;
	PSP_DEVINFO_DATA devInfoData;
};

struct _SetupApiDeviceEnumerator {
	int i;
	BOOL WasError;
	WCHAR Current[SetupApiDeviceEnumeratorPathSize];
	_DeviceEnumeratorState *_state;
};

static void ResetEnumerator(_SetupApiDeviceEnumerator *e, int i)
{
	e->Current[0] = L'\0';
	e->i = i;
	e->WasError = FALSE;
}

static void ResetEnumerator(_SetupApiDeviceEnumerator *e)
{
	ResetEnumerator(e, -1);
}

EXTERN_DLL_EXPORT BOOL WINAPI SetupApi_Enumerate(
	GUID classGuid,
	DWORD flags,
	SetupApiDeviceEnumerator *enumerator)
{
	_SetupApiDeviceEnumerator *e = (_SetupApiDeviceEnumerator*)enumerator;
	ResetEnumerator(e);

	HDEVINFO devInfo = SetupDiGetClassDevs(
		&classGuid,
		nullptr,
		nullptr,
		flags | DIGCF_DEVICEINTERFACE);
	if (devInfo == INVALID_HANDLE_VALUE)
		return FALSE;

	e->_state = new _DeviceEnumeratorState;
	if (e->_state == nullptr)
		return FALSE;
	ZeroMemory(e->_state, sizeof(_DeviceEnumeratorState));

	e->_state->deviceClass = classGuid;
	e->_state->devInfo = devInfo;

	e->_state->devInfoData = new SP_DEVINFO_DATA;
	e->_state->devInfoData->cbSize = sizeof(SP_DEVINFO_DATA);

	return TRUE;
}

EXTERN_DLL_EXPORT BOOL WINAPI SetupApi_Next(SetupApiDeviceEnumerator *enumerator)
{
	_SetupApiDeviceEnumerator *e = (_SetupApiDeviceEnumerator*)enumerator;
	ResetEnumerator(e, e->i + 1);

	PSP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData = nullptr;

	e->WasError = TRUE;
	BOOL result = FALSE;

	SP_DEVICE_INTERFACE_DATA deviceInterfaceData = { sizeof(SP_DEVICE_INTERFACE_DATA) };

	if (!SetupDiEnumDeviceInterfaces(
		e->_state->devInfo,
		nullptr,
		&e->_state->deviceClass,
		e->i,
		&deviceInterfaceData))
	{
		if (GetLastError() == ERROR_NO_MORE_ITEMS)
			e->WasError = FALSE;
		goto cleanup;
	}

	DWORD deviceInterfaceDetailDataSize = offsetof(SP_DEVICE_INTERFACE_DETAIL_DATA, DevicePath) + sizeof(WCHAR)*SetupApiDeviceEnumeratorPathSize;
	deviceInterfaceDetailData = (SP_DEVICE_INTERFACE_DETAIL_DATA*)malloc(deviceInterfaceDetailDataSize);
	if (deviceInterfaceDetailData == nullptr)
		goto cleanup;
	ZeroMemory(deviceInterfaceDetailData, deviceInterfaceDetailDataSize);
	deviceInterfaceDetailData->cbSize = sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA);

	DWORD requiredSize;
	if (!SetupDiGetDeviceInterfaceDetailW(
		e->_state->devInfo,
		&deviceInterfaceData,
		deviceInterfaceDetailData,
		deviceInterfaceDetailDataSize,
		&requiredSize,
		e->_state->devInfoData))
	{
		goto cleanup;
	}

	wcsncpy_s(e->Current, deviceInterfaceDetailData->DevicePath, SetupApiDeviceEnumeratorPathSize - 1);

	e->WasError = FALSE;
	result = TRUE;

cleanup:
	free(deviceInterfaceDetailData);
	return result;
}

EXTERN_DLL_EXPORT BOOL WINAPI SetupApi_ReadProperty(
	_In_      SetupApiDeviceEnumerator	*enumerator,
	_In_	  DWORD						property,
	_Out_opt_ PDWORD					propertyRegDataType,
	_Out_opt_ PBYTE						propertyBuffer,
	_In_      DWORD						propertyBufferSize,
	_Out_opt_ PDWORD					requiredSize)
{
	_SetupApiDeviceEnumerator *e = (_SetupApiDeviceEnumerator*)enumerator;

	return SetupDiGetDeviceRegistryProperty(e->_state->devInfo, e->_state->devInfoData, property, propertyRegDataType, propertyBuffer, propertyBufferSize, requiredSize);
}

EXTERN_DLL_EXPORT void WINAPI SetupApi_Done(SetupApiDeviceEnumerator *enumerator)
{
	_SetupApiDeviceEnumerator *e = (_SetupApiDeviceEnumerator*)enumerator;
	ResetEnumerator(e);

	SetupDiDestroyDeviceInfoList(e->_state->devInfo);
	delete e->_state;
}
