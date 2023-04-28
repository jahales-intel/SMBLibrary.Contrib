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
namespace SMBLibrary.Contrib.Dfs.Caching
{
    public class ReferralCache
    {
        private readonly ReferralCacheNode _cacheRoot = new ReferralCacheNode("<root>");

        public ReferralCacheEntry Lookup(DfsPath dfsPath)
        {
            return _cacheRoot.GetReferralEntry(dfsPath.PathComponents);
        }

        public void Clear(DfsPath dfsPath)
        {
            _cacheRoot.DeleteExpiredReferralEntry(dfsPath.PathComponents);
        }

        public void Add(ReferralCacheEntry referralCacheEntry)
        {
            var path = new DfsPath(referralCacheEntry.DfsPathPrefix);
            _cacheRoot.AddReferralEntry(path.PathComponents, referralCacheEntry);
        }

        public void Clear()
        {
            _cacheRoot.Clear();
        }
    }
}