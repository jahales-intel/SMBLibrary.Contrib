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
using System.Text;

namespace SMBLibrary.Contrib.Dfs
{
    public class SmbPath
    {
        public string HostName { get; }
        public string ShareName { get; }
        public string Path { get; }

        public SmbPath Parent
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                {
                    return this;
                }

                var index = Path.LastIndexOf('\\');

                return index > 0
                    ? new SmbPath(HostName, ShareName, Path.Substring(0, index)) 
                    : new SmbPath(HostName, ShareName);
            }
        }

        public SmbPath(string hostName) : this(hostName, null, null)
        {
        }

        public SmbPath(string hostName, string shareName) : this(hostName, shareName, null)
        {
        }

        public SmbPath(string hostName, string shareName, string path)
        {
            HostName = hostName;
            ShareName = shareName;
            Path = RewritePath(path);
        }

        public SmbPath(SmbPath parent, string path)
        {
            if (string.IsNullOrEmpty(parent.ShareName))
            {
                throw new Exception("Can only make child SmbPath of fully specified SmbPath.");
            }

            HostName = parent.HostName;
            ShareName = parent.ShareName;

            if (!string.IsNullOrEmpty(parent.Path))
            {
                Path = parent.Path + "\\" + RewritePath(path);
            }
            else
            {
                Path = RewritePath(path);
            }
        }

        public string ToUncPath()
        {
            var sb = new StringBuilder("\\\\");
            sb.Append(HostName);

            if (!string.IsNullOrEmpty(ShareName))
            {
                // Clients can either pass \share or share
                if (ShareName[0] != '\\')
                {
                    sb.Append("\\");
                }

                sb.Append(ShareName);

                if (!string.IsNullOrEmpty(Path))
                {
                    sb.Append("\\").Append(Path);
                }
            }

            return sb.ToString();
        }

        public static SmbPath Parse(string path)
        {
            var rewritten = RewritePath(path);
            var split = rewritten.Split(new [] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

            if (split.Length == 1)
            {
                return new SmbPath(split[0]);
            }

            if (split.Length == 2)
            {
                return new SmbPath(split[0], split[1]);
            }

            return new SmbPath(split[0], split[1], string.Join("\\", split, 2, split.Length - 2));
        }

        private static string RewritePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            var replaced = path.Replace('/', '\\');

            if (replaced[0] == '\\')
            {
                if (replaced.Length > 1 && replaced[1] == '\\')
                {
                    return replaced.Substring(2);
                }
                else
                {
                    return replaced.Substring(1);
                }
            }

            return replaced;
        }

        public bool IsOnSameHost(SmbPath other)
        {
            return other != null && string.Equals(HostName, other.HostName);
        }

        public bool IsOnSameShare(SmbPath other)
        {
            return IsOnSameHost(other) && string.Equals(ShareName, other.ShareName);
        }

        protected bool Equals(SmbPath other)
        {
            return HostName == other.HostName && ShareName == other.ShareName && Path == other.Path;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SmbPath)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (HostName != null ? HostName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ShareName != null ? ShareName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Path != null ? Path.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}