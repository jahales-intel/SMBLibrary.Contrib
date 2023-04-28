/*
 * Copyright (C)2016 - SMBJ Contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
/*
 * Ported from SMBJ project (Java) to C# 2023
 */

using System;
using System.Collections.Generic;
using SMBLibrary.Contrib.Dfs.Messages;

namespace SMBLibrary.Contrib.Dfs.Caching
{
    public class ReferralCacheEntry
    {
        public string DfsPathPrefix { get; }
        public ReferralServerTypeFlags RootOrLink { get; }
        public bool Interlink { get; }
        public int Ttl { get; }
        public DateTime Expires { get; }
        public bool TargetFailback { get; }
        public int TargetHint { get; private set; }
        public  List<TargetSetEntry> TargetList { get; }

        public bool IsRoot => RootOrLink == ReferralServerTypeFlags.Root;
        public bool IsLink => RootOrLink == ReferralServerTypeFlags.Link;
        public bool IsExpired => DateTime.Now > Expires;
        public bool IsInterlink => IsLink && Interlink;
        public TargetSetEntry TargetHintEntry => TargetHint < TargetList.Count ? TargetList[TargetHint] : null;

        public ReferralCacheEntry(Messages.ResponseGetDfsReferral response, DomainCache domainCache)
        {
            foreach (var referralEntry in response.ReferralEntries)
            {
                if (referralEntry.Path == null)
                {
                    throw new Exception("Path cannot be null for a ReferralCacheEntry.");
                }
            }

            if (response.ReferralEntries.Count < 1)
            {
                throw new Exception("DFS referral response did not contain any valid referral entries.");
            }

            var firstReferral = response.ReferralEntries[0];
            DfsPathPrefix = firstReferral.DfsPath;
            RootOrLink = firstReferral.ServerType;

            Interlink = 
                (response.ReferralHeaderFlags & ReferralHeaderFlags.ReferralServers) != 0 && 
                (response.ReferralHeaderFlags & ReferralHeaderFlags.StorageServers) == 0;

            if (!Interlink && response.ReferralEntries.Count == 1)
            {
                var pathEntries = new DfsPath(firstReferral.Path).PathComponents;
                Interlink = domainCache.Lookup(pathEntries[0]) != null;
            }

            Ttl = firstReferral.Ttl;
            Expires = DateTime.Now.AddMilliseconds(Ttl);
            TargetFailback = (response.ReferralHeaderFlags & ReferralHeaderFlags.TargetFailback) != 0;

            TargetList = new List<TargetSetEntry>(response.ReferralEntries.Count);

            foreach (var referralEntry in response.ReferralEntries)
            {
                TargetList.Add(new TargetSetEntry(referralEntry.Path, false));
            }
        }

        public TargetSetEntry NextTargetHint()
        {
            TargetHint++;
            return TargetHintEntry;
        }
    }
}