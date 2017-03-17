#pragma once

#include "stdafx.h"

#include <winioctl.h>

typedef struct {
	LARGE_INTEGER DiskSize;
	LARGE_INTEGER Cylinders;
	DWORD TracksPerCylinder;
	DWORD SectorsPerTrack;
	DWORD BytesPerSector;
	DWORD DiskSignature;
	GUID DiskId;
} IoctlDiskGetDriveGeometryEx_Result;

EXTERN_DLL_EXPORT BOOL WINAPI IoctlDiskGetDriveGeometryEx(
	HANDLE device,
	IoctlDiskGetDriveGeometryEx_Result *result);

struct IoctlVolumeGetVolumeDiskExtents_Enumerator
{
	int i;
	DISK_EXTENT current;
	HANDLE handle;
};

EXTERN_DLL_EXPORT BOOL WINAPI IoctlVolumeGetVolumeDiskExtents_Enumerate(
	HANDLE device,
	IoctlVolumeGetVolumeDiskExtents_Enumerator *enumerator);

EXTERN_DLL_EXPORT BOOL WINAPI IoctlVolumeGetVolumeDiskExtents_Next(
	IoctlVolumeGetVolumeDiskExtents_Enumerator *enumerator);

EXTERN_DLL_EXPORT void WINAPI IoctlVolumeGetVolumeDiskExtents_Done(
	IoctlVolumeGetVolumeDiskExtents_Enumerator *enumerator);