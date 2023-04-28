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
using System.Text;

namespace SMBLibrary.Contrib.Dfs
{
    public class DfsPath
    {
        public List<string> PathComponents { get; }

        public DfsPath(string uncPath)
        {
            PathComponents = SplitPath(uncPath);
        }

        public DfsPath(List<string> pathComponents)
        {
            PathComponents = new List<string>(pathComponents);
        }

        public DfsPath ReplacePrefix(string prefixToReplace, string target)
        {
            var componentsToReplace = SplitPath(prefixToReplace);
            var replacedComponents = new List<string>(SplitPath(target));

            for (var i = componentsToReplace.Count; i < PathComponents.Count; i++)
            {
                replacedComponents.Add(PathComponents[i]);
            }

            return new DfsPath(replacedComponents);
        }

        public bool HasOnlyOnePathComponent => PathComponents.Count == 1;

        public bool IsSysVolOrNetLogon
        {
            get
            {
                if (PathComponents.Count > 1)
                {
                    var second = PathComponents[1];
                    return "SYSVOL".Equals(second) || "NETLOGON".Equals(second);
                }

                return false;
            }
        }

        public bool IsIpc
        {
            get
            {
                if (PathComponents.Count > 1)
                {
                    var second = PathComponents[1];
                    return "IPC$".Equals(second);
                }

                return false;
            }
        }

        public static List<string> SplitPath(string pathPart)
        {
            return new List<string>(pathPart.Split(new [] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public string ToPath()
        {
            var sb = new StringBuilder();

            foreach (var pathComponent in PathComponents)
            {
                sb.Append("\\").Append(pathComponent);
            }

            return sb.ToString();
        }

        public override string ToString()
        {
            return $"DFSPath[{string.Join(", ", PathComponents.ToArray())}]";
        }
    }
}