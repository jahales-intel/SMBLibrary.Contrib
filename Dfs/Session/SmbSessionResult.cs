using System;

namespace SMBLibrary.Contrib.Dfs.Session
{
    public class SmbSessionResult<T> : IDisposable
    {
        public T Data { get; }
        public NTStatus Status { get; }
        public FileStatus? FileStatus { get; }

        public SmbSessionResult(NTStatus status) : this(default, status, null)
        {
        }

        public SmbSessionResult(NTStatus status, FileStatus fileStatus) : this(default, status, fileStatus)
        {
        }

        public SmbSessionResult(T data, NTStatus status) : this(data, status, null)
        {
        }

        public SmbSessionResult(T data, NTStatus status, FileStatus? fileStatus)
        {
            Data = data;
            Status = status;
            FileStatus = fileStatus;
        }

        public void Dispose()
        {
            if (Data is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}