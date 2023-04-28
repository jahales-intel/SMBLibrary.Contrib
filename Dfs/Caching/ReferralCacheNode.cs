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
using System.Collections.Generic;
using System.Linq;

namespace SMBLibrary.Contrib.Dfs.Caching
{
    internal class ReferralCacheNode
    {
        private readonly string _pathComponent;
        private readonly ConcurrentDictionary<string, ReferralCacheNode> _childNodes = new ConcurrentDictionary<string, ReferralCacheNode>(StringComparer.OrdinalIgnoreCase);
        private volatile ReferralCacheEntry _entry;

        internal ReferralCacheNode(string pathComponent)
        {
            _pathComponent = pathComponent;
        }

        internal void AddReferralEntry(IEnumerable<string> pathComponents, ReferralCacheEntry entry)
        {
            lock (this)
            {
                AddReferralEntry(pathComponents.GetEnumerator(), entry);
            }
        }

        private void AddReferralEntry(IEnumerator<string> pathComponents, ReferralCacheEntry entry)
        {
            if (pathComponents.MoveNext())
            {
                var referralCacheNode = _childNodes.GetOrAdd(pathComponents.Current.ToLower(), key => new ReferralCacheNode(key));
                referralCacheNode.AddReferralEntry(pathComponents, entry);
            }
            else
            {
                _entry = entry;
            }
        }

        internal ReferralCacheEntry GetReferralEntry(IEnumerable<string> pathComponents)
        {
            lock (this)
            {
                return GetReferralEntry(pathComponents.GetEnumerator());
            }
        }

        private ReferralCacheEntry GetReferralEntry(IEnumerator<string> pathComponents)
        {
            if (pathComponents.MoveNext())
            {
                var component = pathComponents.Current.ToLower();

                if (_childNodes.TryGetValue(component, out var referralCacheNode))
                {
                    return referralCacheNode.GetReferralEntry(pathComponents);
                }
            }

            return _entry;
        }

        internal void DeleteExpiredReferralEntry(List<string> pathComponents)
        {
            lock (this)
            {
                if (_entry != null && _entry.IsExpired && !_entry.IsRoot)
                {
                    Clear();
                    return;
                }

                if (pathComponents != null && pathComponents.Any())
                {
                    var component = pathComponents[0].ToLower();

                    if (_childNodes.TryGetValue(component, out var referralCacheNode))
                    {
                        referralCacheNode.DeleteExpiredReferralEntry(pathComponents.Skip(1).ToList());
                    }
                }
            }
        }

        internal void Clear()
        {
            lock (this)
            {
                _childNodes.Clear();
                _entry = null;
            }
        }
    }
}