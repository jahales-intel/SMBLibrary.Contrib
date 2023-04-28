using System;
using SMBLibrary.Contrib.Dfs.Session;

namespace SMBLibrary.Contrib.Dfs
{
    public class ResolveState<T>
    {
        public DfsPath Path { get; set; }
        public bool ResolvedDomainEntry { get; set; }
        public bool IsDfsPath { get; set; }
        public string HostName { get; set; }
        public Func<string, SmbSessionResult<T>> Action { get; set; }
    }
}