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
using System.Collections.Concurrent;

namespace SMBLibrary.Contrib.Dfs.Caching
{
    public class DomainCache
    {
        private readonly ConcurrentDictionary<string, DomainCacheEntry> _cache = new ConcurrentDictionary<string, DomainCacheEntry>(StringComparer.OrdinalIgnoreCase);

        public DomainCacheEntry Lookup(string domainName)
        {
            lock (this)
            {
                return _cache.TryGetValue(domainName, out var domainCacheEntry) ? domainCacheEntry : null;
            }
        }

        public void Add(DomainCacheEntry domainCacheEntry)
        {
            lock (this)
            {
                _cache[domainCacheEntry.DomainName] = domainCacheEntry;
            }
        }
    }
}
