using Utilities;

namespace SMBLibrary.Contrib.Dfs.Messages
{
    public class DfsReferralV1 : DfsReferral
    {
        public string ShareName;

        protected override void ReadProc(byte[] buffer, int offset)
        {
            Path = ByteReader.ReadNullTerminatedUTF16String(buffer, offset + BaseOffset);
        }
    }
}