using System.Collections.Generic;
using Utilities;

namespace SMBLibrary.Contrib.Dfs.Messages
{
    public class DfsReferral
    {
        internal const int BaseOffset = 8;
        internal int Size;

        public int VersionNumber { get; set; }
        public int Ttl { get; set; }
        public ReferralServerTypeFlags ServerType { get; set; }
        public ReferralEntryFlags ReferralEntryFlags { get; set; }
        public string Path { get; protected set; }
        public string DfsPath { get; set; }
        public string DfsAlternatePath { get; set; }
        public string SpecialName { get; set; }
        public List<string> ExpandedNames { get; set; }
        
        public void Read(byte[] buffer, int offset)
        {
            VersionNumber = LittleEndianConverter.ToUInt16(buffer, offset + 0);
            Size = LittleEndianConverter.ToUInt16(buffer, offset + 2);
            ServerType = (ReferralServerTypeFlags)LittleEndianConverter.ToUInt16(buffer, offset + 4);
            ReferralEntryFlags = (ReferralEntryFlags)LittleEndianConverter.ToUInt16(buffer, offset + 6);
            ReadProc(buffer, offset);
        }

        protected virtual void ReadProc(byte[] buffer, int offset)
        {

        }

        public override string ToString()
        {
            return $"DFSReferral[path={Path},dfsPath={DfsPath},dfsAlternatePath={DfsAlternatePath},specialName={SpecialName},ttl={Ttl}]";
        }
    }
}