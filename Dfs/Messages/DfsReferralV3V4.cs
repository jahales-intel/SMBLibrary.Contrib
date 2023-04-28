using Utilities;

namespace SMBLibrary.Contrib.Dfs.Messages
{
    public class DfsReferralV3V4 : DfsReferral
    {
        public uint TimeToLive;
        
        protected override void ReadProc(byte[] buffer, int offset)
        {
            TimeToLive = LittleEndianConverter.ToUInt32(buffer, offset + BaseOffset);

            if ((ReferralEntryFlags & ReferralEntryFlags.NameListReferral) == 0)
            {
                var dfsPathOffset = LittleEndianConverter.ToUInt16(buffer, offset + BaseOffset + 4);
                var dfsAlternatePathOffset = LittleEndianConverter.ToUInt16(buffer, offset + BaseOffset + 6);
                var networkAddressOffset = LittleEndianConverter.ToUInt16(buffer, offset + BaseOffset + 8);
                DfsPath = ByteReader.ReadNullTerminatedUTF16String(buffer, offset + (int)dfsPathOffset);
                DfsAlternatePath = ByteReader.ReadNullTerminatedUTF16String(buffer, offset + (int)dfsAlternatePathOffset);
                Path = ByteReader.ReadNullTerminatedUTF16String(buffer, offset + (int)networkAddressOffset);
            }
            else
            {
                var specialNameOffset = LittleEndianConverter.ToUInt16(buffer, offset + BaseOffset + 4);
                var numberOfExpandedNames = LittleEndianConverter.ToUInt16(buffer, offset + BaseOffset + 6);
                var expandedNameOffset = (int)LittleEndianConverter.ToUInt16(buffer, offset + BaseOffset + 8);
                SpecialName = ByteReader.ReadNullTerminatedUTF16String(buffer, specialNameOffset);

                for (var i = 0; i < numberOfExpandedNames; i++)
                {
                    var expandedName = ByteReader.ReadNullTerminatedUTF16String(buffer, ref expandedNameOffset);
                    ExpandedNames.Add(expandedName);
                }
            }
        }
    }
}