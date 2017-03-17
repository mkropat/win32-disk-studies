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