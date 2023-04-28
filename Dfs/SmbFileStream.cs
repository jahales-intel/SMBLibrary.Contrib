using System;
using System.IO;
using SMBLibrary.Contrib.Dfs.Session;

namespace SMBLibrary.Contrib.Dfs
{
    public class SmbFileStream : Stream
    {
        private SmbSession _smbSession;
        private SmbFileSystemObject _smbFileSystemObject;
        private long _length;
        private long _position;

        public override bool CanRead => _smbFileSystemObject.Access.HasFlag(AccessMask.GENERIC_READ);
        public override bool CanSeek => true;
        public override bool CanWrite => _smbFileSystemObject.Access.HasFlag(AccessMask.GENERIC_WRITE);
        public override long Length => _length;
        public int WriteBufferSize { get; }
        public int ReadBufferSize { get; }

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > Length)
                {
                    throw new Exception($"File offset position references an invalid location (requested {value} for file of size {Length}.");
                }

                _position = value;
            }
        }

        public SmbFileStream(ISmbSessionFactory smbSessionFactory, 
            string path,
            FileMode fileMode = FileMode.Open,
            SmbFileAccess fileAccess = SmbFileAccess.ReadWrite,
            FileShare fileShare = FileShare.ReadWrite,
            FileOptions fileOptions = FileOptions.None,
            bool useAsync = false)
        {
            _smbSession = smbSessionFactory.Create(path);
            _smbFileSystemObject = _smbSession.OpenFile(path, fileMode, fileAccess, fileShare, fileOptions).Data;

            RefreshFileInformation();

            if (fileMode == FileMode.Append)
            {
                _position = _length;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            using (var stream = new MemoryStream(buffer, offset, count))
            {
                var localOffset = 0;

                while (localOffset < count && _position < _length)
                {
                    var bytesToRead = (int)Math.Min(count - localOffset, _smbFileSystemObject.MaxReadSize);
                    var status = _smbFileSystemObject.ReadFile(out var data, _position, bytesToRead);
                    data = data ?? Array.Empty<byte>();

                    SmbHelpers.ValidateStatus(status, "Failed to read content from file", NTStatus.STATUS_SUCCESS, NTStatus.STATUS_END_OF_FILE);

                    if (data.Length > 0)
                    {
                        _position += data.Length;
                        localOffset += data.Length;
                        stream.Write(data, 0, data.Length);
                    }

                    if (status == NTStatus.STATUS_END_OF_FILE || data.Length == 0)
                    {
                        break;
                    }
                }

                return localOffset;
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            using (var stream = new MemoryStream(buffer, offset, count))
            {
                for (var i = 0; i < count;)
                {
                    var writeBufferSize = Math.Min(_smbFileSystemObject.MaxWriteSize, stream.Length - stream.Position);
                    var writeBuffer = new byte[writeBufferSize];
                    var bytesRead = stream.Read(writeBuffer, 0, writeBuffer.Length);
                    i += bytesRead;

                    if (bytesRead != writeBufferSize)
                    {
                        throw new Exception("Failed to read the expected number of bytes from the write buffer");
                    }

                    var status = _smbFileSystemObject.WriteFile(out var bytesWritten, Position, writeBuffer);

                    SmbHelpers.ValidateStatus(status, "Failed to write content to file", NTStatus.STATUS_SUCCESS);
                    _position += bytesWritten;
                    _length = Math.Max(_length, _position);

                    if (bytesWritten != writeBufferSize)
                    {
                        throw new Exception("Failed to write the expected number of bytes to the client");
                    }
                }
            }
        }

        public override void Flush()
        {
            _smbFileSystemObject.FlushFileBuffers();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var position = Position;

            switch (origin)
            {
                case SeekOrigin.Begin: position = offset; break;
                case SeekOrigin.Current: position += offset; break;
                case SeekOrigin.End: position = Length + offset; break;
            }

            Position = position;

            return position;
        }

        public override void SetLength(long value)
        {
            _length = value;
        }

        private void RefreshFileInformation()
        {
            var information = _smbFileSystemObject.GetFileInformation<FileStandardInformation>(validate: true).Data;
            _length = information.EndOfFile;
        }

        protected override void Dispose(bool disposing)
        {
            _smbFileSystemObject?.Dispose();
            _smbFileSystemObject = null;
            _smbSession?.Dispose();
            _smbSession = null;
            base.Dispose(disposing);
        }
    }
}
