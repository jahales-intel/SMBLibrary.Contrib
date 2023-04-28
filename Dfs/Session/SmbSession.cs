using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using SMBLibrary.Client;

namespace SMBLibrary.Contrib.Dfs.Session
{
    public class SmbSession : IDisposable
    {
        internal NetworkCredential Credentials { get; }
        internal AuthenticationMethod AuthenticationMethod { get; }
        internal SMBTransportType TransportType { get; }
        internal DfsPathResolver PathResolver { get; }

        private readonly ConcurrentDictionary<string, SmbSessionClient> _clients = new ConcurrentDictionary<string, SmbSessionClient>(StringComparer.OrdinalIgnoreCase);

        public SmbSession(NetworkCredential credentials,
            SMBTransportType transportType = SMBTransportType.DirectTCPTransport,
            AuthenticationMethod authenticationMethod = AuthenticationMethod.NTLMv2,
            DfsPathResolver pathResolver = null)
        {
            Credentials = credentials;
            TransportType = transportType;
            AuthenticationMethod = authenticationMethod;
            PathResolver = pathResolver ?? new DfsPathResolver();
        }

        internal ISMBFileStore GetFileStore(string path)
        {
            var result = RunAction(path, GetFileStoreProc);
            return result.Data;
        }

        public SmbSessionResult<SmbFileSystemObject> OpenFile(string path,
            FileMode fileMode = FileMode.Open,
            SmbFileAccess fileAccess = SmbFileAccess.ReadWrite,
            FileShare fileShare = FileShare.ReadWrite,
            FileOptions fileOptions = FileOptions.None,
            bool validate = true)
        {
            return OpenFileSystemObject(path, false, fileMode, fileAccess, fileShare, fileOptions, validate);
        }

        public SmbSessionResult<SmbFileSystemObject> OpenDirectory(string path,
            FileMode fileMode = FileMode.Open,
            SmbFileAccess fileAccess = SmbFileAccess.ReadWrite,
            FileShare fileShare = FileShare.ReadWrite,
            FileOptions fileOptions = FileOptions.None,
            bool validate = true)
        {
            return OpenFileSystemObject(path, true, fileMode, fileAccess, fileShare, fileOptions, validate);
        }

        public SmbSessionResult<SmbFileSystemObject> OpenFileSystemObject(string path,
            bool isDirectory = false,
            FileMode fileMode = FileMode.Open,
            SmbFileAccess fileAccess = SmbFileAccess.ReadWrite,
            FileShare fileShare = FileShare.ReadWrite,
            FileOptions fileOptions = FileOptions.None,
            bool validate = true)
        {
            var accessMask = SmbHelpers.FileAccessToAccessMask(fileAccess);
            var shareAccess = SmbHelpers.FileShareToShareAccess(fileShare);
            var createDisposition = SmbHelpers.FileModeToCreateDisposition(fileMode, isDirectory);
            var createOptions = SmbHelpers.FileOptionsToCreateOptions(fileOptions, isDirectory);
            var fileAttributes = isDirectory ? FileAttributes.Directory : FileAttributes.Normal;
            return OpenFileSystemObject(path, accessMask, fileAttributes, shareAccess, createDisposition, createOptions, null, validate);
        }

        public SmbSessionResult<SmbFileSystemObject> OpenFileSystemObject(string path, 
            AccessMask desiredAccess,
            FileAttributes fileAttributes, 
            ShareAccess shareAccess, 
            CreateDisposition createDisposition,
            CreateOptions createOptions, 
            SecurityContext securityContext,
            bool validate)
        {
            if (path.Length > 255)
            {
                throw new Exception($"The specified file system name exceeded the maximum length (255): {path}");
            }

            var result = RunAction(path, resolvedPath => OpenFileSystemObjectProc(resolvedPath, desiredAccess, fileAttributes, shareAccess, createDisposition, createOptions, securityContext));

            if (validate)
            {
                SmbHelpers.ValidateStatus(result.Status, "Failed to open file system object.", path, NTStatus.STATUS_SUCCESS);
            }
            
            return result;
        }

        private SmbSessionResult<SmbFileSystemObject> OpenFileSystemObjectProc(string path, AccessMask desiredAccess, FileAttributes fileAttributes, ShareAccess shareAccess, CreateDisposition createDisposition, CreateOptions createOptions, SecurityContext securityContext)
        {
            if (path.Length > 255)
            {
                throw new Exception($"The specified file system name exceeded the maximum length (255): {path}");
            }

            var smbPath = SmbPath.Parse(path);
            var relativePath = smbPath.Path ?? string.Empty;
            var fileStore = GetFileStore(path);
            var status = (NTStatus)fileStore.CreateFile(out var handle, out var fileStatus, relativePath, desiredAccess, fileAttributes, shareAccess, createDisposition, createOptions, securityContext);

            if (status == NTStatus.STATUS_SUCCESS)
            {
                var fileHandle = new SmbFileSystemObject(handle, fileStore, path, desiredAccess);
                return new SmbSessionResult<SmbFileSystemObject>(fileHandle, status, fileStatus);
            }

            return new SmbSessionResult<SmbFileSystemObject>(status, fileStatus);
        }

        private SmbSessionClient GetClient(string server)
        {
            return _clients.GetOrAdd(server, key => SmbSessionClient.Create(key, Credentials, TransportType, AuthenticationMethod));
        }

        private SmbSessionResult<ISMBFileStore> GetFileStoreProc(string path)
        {
            var smbPath = SmbPath.Parse(path);
            var client = GetClient(smbPath.HostName);
            var fileStore = client.TreeConnect(smbPath.ShareName, out var status);
            return new SmbSessionResult<ISMBFileStore>(fileStore, status);
        }

        private SmbSessionResult<T> RunAction<T>(string path, Func<string, SmbSessionResult<T>> action)
        {
            var result = action(path);

            if (result.Status == NTStatus.STATUS_PATH_NOT_COVERED)
            {
                return PathResolver.Resolve(this, path, action);
            }

            return result;
        }

        public void Dispose()
        {
            try
            {
                foreach (var client in _clients.Values)
                {
                    client.Dispose();
                }
            }
            catch 
            {
                // Do nothing
            }
        }
    }
}
