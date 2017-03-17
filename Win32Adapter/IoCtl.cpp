#include "IoCtl.h"

EXTERN_DLL_EXPORT BOOL WINAPI IoctlDiskGetDriveGeometryEx(
	HANDLE device,
	IoctlDiskGetDriveGeometryEx_Result *result)
{
	BOOL success = FALSE;

	ZeroMemory(result, sizeof(result));

	size_t diskGeometrySize = offsetof(DISK_GEOMETRY_EX, Data) + sizeof(DISK_PARTITION_INFO) + sizeof(DISK_DETECTION_INFO);
	PDISK_GEOMETRY_EX diskGeometry = (PDISK_GEOMETRY_EX)malloc(diskGeometrySize);
	if (!diskGeometry)
		goto cleanup;
	ZeroMemory(diskGeometry, diskGeometrySize);

	DWORD bytesReturned;
	if (!DeviceIoControl(
		device,								// handle to volume
		IOCTL_DISK_GET_DRIVE_GEOMETRY_EX,	// dwIoControlCode
		nullptr,                            // lpInBuffer
		0,									// nInBufferSize
		diskGeometry,						// output buffer
		diskGeometrySize,					// size of output buffer
		&bytesReturned,						// number of bytes returned
		nullptr))							// OVERLAPPED structure
	{
		goto cleanup;
	}

	PDISK_PARTITION_INFO partitionInfo = DiskGeometryGetPartition(diskGeometry);
	PDISK_DETECTION_INFO detectionInfo = DiskGeometryGetDetect(diskGeometry);

	result->DiskSize = diskGeometry->DiskSize;
	result->Cylinders = diskGeometry->Geometry.Cylinders;
	result->TracksPerCylinder = diskGeometry->Geometry.TracksPerCylinder;
	result->SectorsPerTrack = diskGeometry->Geometry.SectorsPerTrack;
	result->BytesPerSector = diskGeometry->Geometry.BytesPerSector;

	switch (partitionInfo->PartitionStyle)
	{
	case PARTITION_STYLE_GPT:
		result->DiskId = partitionInfo->Gpt.DiskId;
		break;
	case PARTITION_STYLE_MBR:
		result->DiskSignature = partitionInfo->Mbr.Signature;
		break;
	}

	success = TRUE;

cleanup:
	free(diskGeometry);
	return success;
}

struct _IoctlVolumeGetVolumeDiskExtents_Enumerator
{
	int i;
	DISK_EXTENT current;
	PVOLUME_DISK_EXTENTS extents;
};

EXTERN_DLL_EXPORT BOOL WINAPI IoctlVolumeGetVolumeDiskExtents_Enumerate(
	HANDLE device,
	IoctlVolumeGetVolumeDiskExtents_Enumerator *enumerator)
{
	_IoctlVolumeGetVolumeDiskExtents_Enumerator *e = (_IoctlVolumeGetVolumeDiskExtents_Enumerator*)enumerator;

	e->i = -1;
	ZeroMemory(&e->current, sizeof(DISK_EXTENT));
	e->extents = nullptr;

	DWORD pExtentsSize = sizeof(VOLUME_DISK_EXTENTS);
	e->extents = (PVOLUME_DISK_EXTENTS)malloc(pExtentsSize);
	if (e->extents == nullptr)
		return FALSE;
	ZeroMemory(e->extents, pExtentsSize);

	DWORD bytesReturned;

	if (!DeviceIoControl(
		device,
		IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
		nullptr,
		0,
		e->extents,
		pExtentsSize,
		&bytesReturned,
		nullptr))
	{
		free(e->extents);

		if (GetLastError() != ERROR_MORE_DATA)
			return FALSE;

		pExtentsSize = offsetof(VOLUME_DISK_EXTENTS, Extents) + e->extents->NumberOfDiskExtents * sizeof(DISK_EXTENT);
		e->extents = (PVOLUME_DISK_EXTENTS)malloc(pExtentsSize);
		if (e->extents == nullptr)
			return FALSE;
		ZeroMemory(e->extents, pExtentsSize);

		if (!DeviceIoControl(
			device,
			IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
			nullptr,
			0,
			e->extents,
			pExtentsSize,
			&bytesReturned,
			nullptr))
		{
			free(e->extents);
			return FALSE;
		}
	}

	return TRUE;
}

EXTERN_DLL_EXPORT BOOL WINAPI IoctlVolumeGetVolumeDiskExtents_Next(
	IoctlVolumeGetVolumeDiskExtents_Enumerator *enumerator)
{
	_IoctlVolumeGetVolumeDiskExtents_Enumerator *e = (_IoctlVolumeGetVolumeDiskExtents_Enumerator*)enumerator;

	e->i++;
	int i = e->i;

	long numberOfExtents = (long)e->extents->NumberOfDiskExtents;
	if (numberOfExtents <= i)
		return FALSE;

	e->current = e->extents->Extents[i];

	return TRUE;
}

EXTERN_DLL_EXPORT void WINAPI IoctlVolumeGetVolumeDiskExtents_Done(
	IoctlVolumeGetVolumeDiskExtents_Enumerator *enumerator)
{
	_IoctlVolumeGetVolumeDiskExtents_Enumerator *e = (_IoctlVolumeGetVolumeDiskExtents_Enumerator*)enumerator;

	free(e->extents);
}