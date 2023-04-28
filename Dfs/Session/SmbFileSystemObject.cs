using System;
using System.Collections.Generic;
using SMBLibrary.Client;

namespace SMBLibrary.Contrib.Dfs.Session
{
    public class SmbFileSystemObject : IDisposable
    {
        protected ISMBFileStore FileStore { get; set; }

        public object Handle { get; private set; }
        public NTStatus CloseStatus { get; private set; } = NTStatus.STATUS_SUCCESS;
        public uint MaxReadSize => 65536; 
        public uint MaxWriteSize => 65536;
        public string Path { get; private set; }
        public AccessMask Access { get; private set; }

        public SmbFileSystemObject()
        {
        }

        public SmbFileSystemObject(object handle, ISMBFileStore fileStore, string path, AccessMask access)
        {
            Handle = handle;
            FileStore = fileStore;
            Path = path;
            Access = access;
        }

        public void Dispose()
        {
            if (Handle == null)
            {
                return;
            }

            try
            {
                CloseStatus = (NTStatus)FileStore.CloseFile(Handle);
            }
            catch
            {
                CloseStatus = NTStatus.STATUS_DATA_ERROR;
            }
            finally
            {
                Handle = null;
            }
        }

        public NTStatus ReadFile(out byte[] data, long offset, int maxCount)
        {
            return (NTStatus)FileStore.ReadFile(out data, Handle, offset, maxCount);
        }

        public NTStatus WriteFile(out int numberOfBytesWritten, long offset, byte[] data)
        {
            return (NTStatus)FileStore.WriteFile(out numberOfBytesWritten, Handle, offset, data);
        }

        public NTStatus FlushFileBuffers()
        {
            return (NTStatus)FileStore.FlushFileBuffers(Handle);
        }

        public NTStatus LockFile(long byteOffset, long length, bool exclusiveLock)
        {
            return (NTStatus)FileStore.LockFile(Handle, byteOffset, length, exclusiveLock);
        }

        public NTStatus UnlockFile(long byteOffset, long length)
        {
            return (NTStatus)FileStore.UnlockFile(Handle, byteOffset, length);
        }

        public List<QueryDirectoryFileInformation> QueryDirectory(string filePattern, bool validate = true)
        {
            var status = (NTStatus)FileStore.QueryDirectory(out var result, Handle, filePattern, FileInformationClass.FileDirectoryInformation);

            if (validate)
            {
                SmbHelpers.ValidateStatus(status, "Failed to query directory.", Path, NTStatus.STATUS_SUCCESS, NTStatus.STATUS_NO_MORE_FILES);
            }

            return result;
        }

        public SmbSessionResult<T> GetFileInformation<T>(bool validate = true) where T : FileInformation
        {
            var fileInformationClass = SmbHelpers.GetFileInformationClass<T>();
            var status = (NTStatus)FileStore.GetFileInformation(out var result, Handle, fileInformationClass);

            if (validate)
            {
                SmbHelpers.ValidateStatus(status, "Failed to query file information.", Path, NTStatus.STATUS_SUCCESS);
            }

            return new SmbSessionResult<T>((T)result, status);
        }

        public NTStatus SetFileInformation(FileInformation information, bool validate = true)
        {
            var status = (NTStatus)FileStore.SetFileInformation(Handle, information);

            if (validate)
            {
                SmbHelpers.ValidateStatus(status, "Failed to set file information", Path, NTStatus.STATUS_SUCCESS);
            }

            return status;
        }

        public NTStatus GetSecurityInformation(out SecurityDescriptor result, object handle, SecurityInformation securityInformation)
        {
            return (NTStatus)FileStore.GetSecurityInformation(out result, Handle, securityInformation);
        }

        public NTStatus SetSecurityInformation(SecurityInformation securityInformation, SecurityDescriptor securityDescriptor)
        {
            return (NTStatus)FileStore.SetSecurityInformation(Handle, securityInformation, securityDescriptor);
        }

        public NTStatus NotifyChange(out object ioRequest, NotifyChangeFilter completionFilter, bool watchTree, int outputBufferSize, OnNotifyChangeCompleted onNotifyChangeCompleted, object context)
        {
            return (NTStatus)FileStore.NotifyChange(out ioRequest, Handle, completionFilter, watchTree, outputBufferSize, onNotifyChangeCompleted, context);
        }

        public NTStatus DeviceIOControl(uint ctlCode, byte[] input, out byte[] output, int maxOutputLength)
        {
            return (NTStatus)FileStore.DeviceIOControl(Handle, ctlCode, input, out output, maxOutputLength);
        }
    }
}