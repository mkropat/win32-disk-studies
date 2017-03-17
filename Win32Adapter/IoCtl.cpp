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