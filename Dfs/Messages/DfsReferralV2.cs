using Utilities;

namespace SMBLibrary.Contrib.Dfs.Messages
{
    public class DfsReferralV2 : DfsReferral
    {
        protected override void ReadProc(byte[] buffer, int offset)
        {
            var proximity = LittleEndianConverter.ToUInt32(buffer, offset + BaseOffset);
            Ttl = (int)LittleEndianConverter.ToUInt32(buffer, offset + BaseOffset + 4);
            var dfsPathOffset = LittleEndianConverter.ToUInt16(buffer, offset + BaseOffset + 8);
            var dfsAlternatePathOffset = LittleEndianConverter.ToUInt16(buffer, offset + BaseOffset + 10);
            var networkAddressOffset = LittleEndianConverter.ToUInt16(buffer, offset + BaseOffset + 12);

            DfsPath = ByteReader.ReadNullTerminatedUTF16String(buffer, offset + (int)dfsPathOffset);
            DfsAlternatePath = ByteReader.ReadNullTerminatedUTF16String(buffer, offset + (int)dfsAlternatePathOffset);
            Path = ByteReader.ReadNullTerminatedUTF16String(buffer, offset + (int)networkAddressOffset);
        }
    }
}