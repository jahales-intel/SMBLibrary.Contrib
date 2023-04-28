using System;
using System.Collections.Generic;
using Utilities;

namespace SMBLibrary.Contrib.Dfs.Messages
{
    /// <summary>
    /// [MS-DFSC] RESP_GET_DFS_REFERRAL
    /// </summary>
    public class ResponseGetDfsReferral
    {
        public string OriginalPath { get; }

        public int VersionNumber => ReferralEntries.Count < 1 ? 0 : ReferralEntries[0].VersionNumber;
        public ushort PathConsumed;
        public ushort NumberOfReferrals;
        public ReferralHeaderFlags ReferralHeaderFlags;
        public List<DfsReferral> ReferralEntries = new List<DfsReferral>();
        public List<string> StringBuffer;
        // Padding
        
        public ResponseGetDfsReferral(string originalPath)
        {
            OriginalPath = originalPath;
        }
        
        public void Read(byte[] buffer)
        {
            PathConsumed = LittleEndianConverter.ToUInt16(buffer, 0);
            NumberOfReferrals = LittleEndianConverter.ToUInt16(buffer, 2);
            ReferralHeaderFlags = (ReferralHeaderFlags)LittleEndianConverter.ToUInt16(buffer, 4);

            for (int i = 0, ibuffer = 8; i < NumberOfReferrals; i++)
            {
                var referral = ReadReferral(buffer, ibuffer);
                ibuffer += referral.Size;
                ReferralEntries.Add(referral);

                if (referral.DfsPath == null)
                {
                    referral.DfsPath = OriginalPath;
                }
            }
        }

        public byte[] GetBytes()
        {
            throw new NotImplementedException();
        }

        public DfsReferral ReadReferral(byte[] buffer, int offset)
        {
            var version = LittleEndianConverter.ToUInt16(buffer, offset);
            DfsReferral referral;

            switch (version)
            {
                case 1:
                    referral = new DfsReferralV1();
                    break;
                case 2:
                    referral = new DfsReferralV2();
                    break;
                case 3:
                    referral = new DfsReferralV3V4();
                    break;
                default: throw new Exception($"Failed to parse DFS referral response--DFS referral response version = '{version}' but expected 1 - 4.");
            }

            referral.Read(buffer, offset);

            return referral;
        }
    }
}
