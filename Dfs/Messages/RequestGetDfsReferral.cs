using Utilities;

namespace SMBLibrary.Contrib.Dfs.Messages
{
    /// <summary>
    /// [MS-DFSC] REQ_GET_DFS_REFERRAL
    /// </summary>
    public class RequestGetDfsReferral
    {
        public ushort MaxReferralLevel;
        public string RequestFileName; // Unicode

        public RequestGetDfsReferral()
        {
        }

        public RequestGetDfsReferral(byte[] buffer)
        {
            MaxReferralLevel = LittleEndianConverter.ToUInt16(buffer, 0);
            RequestFileName = ByteReader.ReadNullTerminatedUTF16String(buffer, 2);
        }

        public byte[] GetBytes()
        {
            int length = 2 + RequestFileName.Length * 2 + 2;
            byte[] buffer = new byte[length];
            LittleEndianWriter.WriteUInt16(buffer, 0, MaxReferralLevel);
            ByteWriter.WriteUTF16String(buffer, 2, RequestFileName);
            return buffer;
        }
    }
}
