#include "IoCtl.h"

EXTERN_DLL_EXPORT BOOL WINAPI IoctlDiskGetDriveGeometryEx(
	HANDLE device,
	IoctlDiskGetDriveGeometryEx_Result *result)
{
	BOOL success = FALSE;

	ZeroMemory(result, sizeof(result));

	DWORD diskGeometrySize = offsetof(DISK_GEOMETRY_EX, Data) + sizeof(DISK_PARTITION_INFO) + sizeof(DISK_DETECTION_INFO);
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

struct _IoctlDiskGetDriveLayout_Enumerator
{
	DWORD ParitionStyle;
	long i;
	long Total;
	IoctlDiskGetDriveLayout_PartitionInfo Current;
	PDRIVE_LAYOUT_INFORMATION_EX layoutInfo;
};

static const int MAX_GPT_PARTITION_COUNT = 128;

EXTERN_DLL_EXPORT BOOL WINAPI IoctlDiskGetDriveLayout_Enumerate(
	HANDLE device,
	IoctlDiskGetDriveLayout_Enumerator *enumerator)
{
	_IoctlDiskGetDriveLayout_Enumerator *e = (_IoctlDiskGetDriveLayout_Enumerator*)enumerator;
	ZeroMemory(e, sizeof(_IoctlDiskGetDriveLayout_Enumerator));
	e->i = -1;

	DWORD layoutInfoSize = offsetof(DRIVE_LAYOUT_INFORMATION_EX, PartitionEntry) + sizeof(PARTITION_INFORMATION_EX) * MAX_GPT_PARTITION_COUNT;
	e->layoutInfo = (PDRIVE_LAYOUT_INFORMATION_EX)malloc(layoutInfoSize);
	if (!e->layoutInfo)
		return FALSE;
	ZeroMemory(e->layoutInfo, layoutInfoSize);

	DWORD bytesReturned;

	if (!DeviceIoControl(
		device,							// handle to device
		IOCTL_DISK_GET_DRIVE_LAYOUT_EX,	// dwIoControlCode
		nullptr,                        // lpInBuffer
		0,								// nInBufferSize
		e->layoutInfo,					// output buffer
		layoutInfoSize,					// size of output buffer
		&bytesReturned,					// number of bytes returned
		nullptr))						// OVERLAPPED structure
	{
		free(e->layoutInfo);
		return FALSE;
	}

	e->ParitionStyle = e->layoutInfo->PartitionStyle;
	e->Total = e->layoutInfo->PartitionCount;

	return TRUE;
}

EXTERN_DLL_EXPORT BOOL WINAPI IoctlDiskGetDriveLayout_Next(
	IoctlDiskGetDriveLayout_Enumerator *enumerator)
{
	_IoctlDiskGetDriveLayout_Enumerator *e = (_IoctlDiskGetDriveLayout_Enumerator*)enumerator;

	e->i++;
	int i = e->i;

	ZeroMemory(&e->Current, sizeof(IoctlDiskGetDriveLayout_PartitionInfo));
	
	long partitionCount = e->layoutInfo->PartitionCount;
	if (partitionCount <= i)
		return FALSE;

	e->Current.StartingOffset = e->layoutInfo->PartitionEntry[i].StartingOffset;
	e->Current.PartitionLength = e->layoutInfo->PartitionEntry[i].PartitionLength;
	e->Current.PartitionNumber = e->layoutInfo->PartitionEntry[i].PartitionNumber;

	switch (e->layoutInfo->PartitionStyle)
	{
	case PARTITION_STYLE_MBR:
		e->Current.MbrParititonType = e->layoutInfo->PartitionEntry[i].Mbr.PartitionType;
		e->Current.MbrIsBootable = e->layoutInfo->PartitionEntry[i].Mbr.BootIndicator;
		e->Current.MbrIsRecognizedType = e->layoutInfo->PartitionEntry[i].Mbr.RecognizedPartition;
		e->Current.MbrHiddenSectorCount = e->layoutInfo->PartitionEntry[i].Mbr.HiddenSectors;
		break;

	case PARTITION_STYLE_GPT:
		e->Current.PartitionType = e->layoutInfo->PartitionEntry[i].Gpt.PartitionType;
		e->Current.PartitionId = e->layoutInfo->PartitionEntry[i].Gpt.PartitionId;
		e->Current.Attributes = e->layoutInfo->PartitionEntry[i].Gpt.Attributes;
		wcscpy_s(e->Current.Name, e->layoutInfo->PartitionEntry[i].Gpt.Name);

		break;
	}

	return TRUE;
}

EXTERN_DLL_EXPORT void WINAPI IoctlDiskGetDriveLayout_Done(
	IoctlDiskGetDriveLayout_Enumerator *enumerator)
{
	_IoctlDiskGetDriveLayout_Enumerator *e = (_IoctlDiskGetDriveLayout_Enumerator*)enumerator;
	free(e->layoutInfo);
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