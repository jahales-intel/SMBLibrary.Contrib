using System;

namespace SMBLibrary.Contrib.Dfs.Messages
{
    [Flags]
    public enum ReferralEntryFlags : long
    {
        None = 0x00,
        NameListReferral = 0x02,
        TargetSetBoundary = 0x04
    }

    [Flags]
    public enum ReferralHeaderFlags : long
    {
        None = 0x00,
        ReferralServers = 0x1L,
        StorageServers = 0x2L,
        TargetFailback = 0x4L
    }

    [Flags]
    public enum ReferralServerTypeFlags : long
    {
        Link = 0x0,
        Root = 0x1
    }

    [Flags]
    enum DfsRequestType
    {
        DOMAIN,
        DC,
        SYSVOL,
        ROOT,
        LINK
    }
}