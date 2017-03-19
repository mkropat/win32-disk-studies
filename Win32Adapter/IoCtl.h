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

struct IoctlDiskGetDriveLayout_PartitionInfo
{
	LARGE_INTEGER StartingOffset;
	LARGE_INTEGER PartitionLength;
	DWORD PartitionNumber;
	BYTE MbrParititonType;
	BOOL MbrIsRecognizedType;
	BOOL MbrIsBootable;
	DWORD MbrHiddenSectorCount;
	GUID PartitionType;
	GUID PartitionId;
	DWORD64 Attributes;
	WCHAR Name[36];
};

struct IoctlDiskGetDriveLayout_Enumerator
{
	DWORD ParitionStyle;
	long i;
	long Total;
	IoctlDiskGetDriveLayout_PartitionInfo Current;
	HANDLE Handle;
};

EXTERN_DLL_EXPORT BOOL WINAPI IoctlDiskGetDriveLayout_Enumerate(
	HANDLE device,
	IoctlDiskGetDriveLayout_Enumerator *enumerator);

EXTERN_DLL_EXPORT BOOL WINAPI IoctlDiskGetDriveLayout_Next(
	IoctlDiskGetDriveLayout_Enumerator *enumerator);

EXTERN_DLL_EXPORT void WINAPI IoctlDiskGetDriveLayout_Done(
	IoctlDiskGetDriveLayout_Enumerator *enumerator);

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