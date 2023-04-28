using System;
using System.IO;
using System.Linq;

namespace SMBLibrary.Contrib.Dfs
{
    public static class SmbHelpers
    {
        public static void ValidateStatus(NTStatus status, string failureMessage, params NTStatus[] allowedStatuses)
        {
            ValidateStatus(status, failureMessage, null, allowedStatuses);
        }

        public static void ValidateStatus(NTStatus status, string failureMessage, string fileName, params NTStatus[] allowedStatuses)
        {
            if (allowedStatuses.Contains(status))
            {
                return;
            }

            switch (status)
            {
                case NTStatus.STATUS_ACCESS_DENIED:
                    throw new Exception($"Insufficient permissions to access the file or folder '{fileName}' with the specified access mode.");
                case NTStatus.STATUS_FILE_IS_A_DIRECTORY:
                    throw new Exception($"The specified object '{fileName}' should not be a directory.");
                case NTStatus.STATUS_OBJECT_PATH_NOT_FOUND:
                    throw new Exception($"The path to the file or folder '{fileName}' cannot be found.");
                case NTStatus.STATUS_OBJECT_NAME_NOT_FOUND:
                    throw new Exception($"The file or folder '{fileName}' cannot be found.");
                case NTStatus.STATUS_OBJECT_NAME_COLLISION:
                    throw new Exception($"The file or folder '{fileName}' already exists.");
            }

            throw new Exception($"Validation failure: {failureMessage}: {status} (allowed: {string.Join(", ", allowedStatuses)})");
        }

        public static bool NotFound(NTStatus status, FileStatus? fileStatus)
        {
            return status == NTStatus.STATUS_NOT_FOUND ||
                   status == NTStatus.STATUS_OBJECT_NAME_NOT_FOUND || 
                   status == NTStatus.STATUS_OBJECT_PATH_NOT_FOUND ||
                   fileStatus == FileStatus.FILE_DOES_NOT_EXIST;
        }

        public static FileInformationClass GetFileInformationClass<T>() where T : FileInformation
        {
            var name = typeof(T).Name;

            if (!Enum.TryParse(name, true, out FileInformationClass type))
            {
                throw new Exception($"Failed to parse file information type: {name}");
            }

            return type;
        }

        public static CreateDisposition FileModeToCreateDisposition(FileMode fileMode, bool isDirectory)
        {
            switch (fileMode)
            {
                case FileMode.Open:
                    return CreateDisposition.FILE_OPEN;
                case FileMode.Append:
                    return CreateDisposition.FILE_OPEN_IF;
                case FileMode.Create:
                    return isDirectory ? CreateDisposition.FILE_CREATE : CreateDisposition.FILE_OVERWRITE_IF;
                case FileMode.CreateNew:
                    return CreateDisposition.FILE_CREATE;
                case FileMode.OpenOrCreate:
                    return CreateDisposition.FILE_OPEN_IF;
                case FileMode.Truncate:
                    return CreateDisposition.FILE_OVERWRITE;
                default:
                    return CreateDisposition.FILE_OPEN;
            }
        }

        public static SmbFileAccess AccessMaskToFileAccess(AccessMask accessMask)
        {
            var fileAccess = SmbFileAccess.None;
            if (accessMask.HasFlag(AccessMask.GENERIC_READ)) fileAccess |= SmbFileAccess.Read;
            if (accessMask.HasFlag(AccessMask.GENERIC_WRITE)) fileAccess |= SmbFileAccess.Write;
            if (accessMask.HasFlag(AccessMask.DELETE)) fileAccess |= SmbFileAccess.Delete;
            return fileAccess;
        }

        public static AccessMask FileAccessToAccessMask(FileAccess fileAccess)
        {
            switch (fileAccess)
            {
                case FileAccess.Write:
                    return AccessMask.GENERIC_WRITE | AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE;
                case FileAccess.Read:
                    return AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE;
                case FileAccess.ReadWrite:
                default:
                    return AccessMask.GENERIC_ALL;
            }
        }

        public static AccessMask FileAccessToAccessMask(SmbFileAccess fileAccess)
        {
            switch (fileAccess)
            {
                case SmbFileAccess.Write:
                    return AccessMask.GENERIC_WRITE | AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE;
                case SmbFileAccess.Read:
                    return AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE;
                case SmbFileAccess.Delete:
                    return AccessMask.DELETE;
                case SmbFileAccess.ReadWrite:
                    return AccessMask.GENERIC_WRITE | AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE;
                default:
                    return AccessMask.GENERIC_ALL;
            }
        }

        public static ShareAccess FileShareToShareAccess(FileShare fileShare)
        {
            var shareAccess = ShareAccess.None;
            if (fileShare.HasFlag(FileShare.Delete)) shareAccess |= ShareAccess.Delete;
            if (fileShare.HasFlag(FileShare.Read)) shareAccess |= ShareAccess.Read;
            if (fileShare.HasFlag(FileShare.Write)) shareAccess |= ShareAccess.Write;
            return shareAccess;
        }

        public static CreateOptions FileOptionsToCreateOptions(FileOptions fileOptions, bool isDirectory)
        {
            var createOptions = isDirectory ? CreateOptions.FILE_DIRECTORY_FILE : CreateOptions.FILE_NON_DIRECTORY_FILE;
            if (fileOptions.HasFlag(FileOptions.DeleteOnClose)) createOptions |= CreateOptions.FILE_DELETE_ON_CLOSE;
            if (fileOptions.HasFlag(FileOptions.RandomAccess)) createOptions |= CreateOptions.FILE_RANDOM_ACCESS;
            if (fileOptions.HasFlag(FileOptions.SequentialScan)) createOptions |= CreateOptions.FILE_SEQUENTIAL_ONLY;
            if (fileOptions.HasFlag(FileOptions.WriteThrough)) createOptions |= CreateOptions.FILE_WRITE_THROUGH;
            return createOptions;
        }

        public static SmbFileAccess SmbToIoFileAccess(FileAccess access)
        {
            return (SmbFileAccess)access;
        }

        public static SMBLibrary.FileAttributes ConvertAttributes(System.IO.FileAttributes attributes)
        {
            return (SMBLibrary.FileAttributes)attributes;
        }

        public static System.IO.FileAttributes ConvertAttributes(SMBLibrary.FileAttributes attributes)
        {
            return (System.IO.FileAttributes)attributes;
        }
    }
}