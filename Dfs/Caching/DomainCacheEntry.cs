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
    public class DomainCacheEntry
    {
        public string DomainName { get; }
        public string DcHint { get; }
        public List<string> DcList { get; }

        public DomainCacheEntry(Messages.ResponseGetDfsReferral response)
        {
            if (response.ReferralEntries.Count != 1)
            {
                throw new Exception("Expecting exactly 1 referral for a domain referral, found: " + response.ReferralEntries.Count);
            }

            var dfsReferral = response.ReferralEntries[0];

            if ((dfsReferral.ReferralEntryFlags & ReferralEntryFlags.NameListReferral) == 0)
            {
                throw new Exception("Referral Entry for '" + dfsReferral.SpecialName + "' does not have NameListReferral bit set.");
            }

            DomainName = dfsReferral.SpecialName;
            DcHint = dfsReferral.ExpandedNames[0];
            DcList = new List<string>(dfsReferral.ExpandedNames);
        }

        public override string ToString()
        {
            return $"{DomainName}->{DcHint}, {string.Join("; ", DcList.ToArray())}";
        }
    }
}