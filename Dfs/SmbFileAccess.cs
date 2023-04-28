using System;

namespace SMBLibrary.Contrib.Dfs
{
    [Flags]
    public enum SmbFileAccess
    {
        None = 0,
        Read = 1,
        Write = 2,
        Delete = 4,
        ReadWrite = Write | Read, // 0x00000003
    }
}