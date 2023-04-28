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
using System.Linq;
using SMBLibrary.Contrib.Dfs.Caching;
using SMBLibrary.Contrib.Dfs.Messages;
using SMBLibrary.Contrib.Dfs.Session;
using SMBLibrary.SMB2;

namespace SMBLibrary.Contrib.Dfs
{
    public class DfsPathResolver
    {
        public static readonly FileID RootId = new FileID { Persistent = 0xFFFFFFFFFFFFFFFF, Volatile = 0xFFFFFFFFFFFFFFFF };

        private readonly ReferralCache _referralCache = new ReferralCache();
        private readonly DomainCache _domainCache = new DomainCache();

        public SmbSessionResult<T> Resolve<T>(SmbSession smbSession, string path, Func<string, SmbSessionResult<T>> action)
        {
            var result = Start(smbSession, SmbPath.Parse(path), action);
            return result;
        }

        private SmbSessionResult<T> Start<T>(SmbSession smbSession, SmbPath smbPath, Func<string, SmbSessionResult<T>> action)
        {
            var dfsPath = new DfsPath(smbPath.ToUncPath());

            var state = new ResolveState<T>
            {
                Path = dfsPath,
                Action = action
            };

            return Step1(smbSession, state);
        }

        /**
         * Step 1: If the path has only one path component (for example, \abc), go to step 12; otherwise, go to step 2.
        */
        private SmbSessionResult<T> Step1<T>(SmbSession session, ResolveState<T> state)
        {
            if (state.Path.HasOnlyOnePathComponent || state.Path.IsIpc) // Also shortcircuit IPC$ connects.
            {
                return Step12(session, state);
            }

            return Step2(session, state);
        }

        /**
         * Step 2: Look up the path in ReferralCache if a cache is being maintained.
         * If no cache is being maintained, go to step 5.
         * 1. If no matching entry is found in ReferralCache, go to step 5.
         * 2. If an entry's TTL has expired:
         * - If RootOrLink indicates DFS root targets, goto step 5.
         * - If RootOrLink indicates DFS link targets, goto step 9.
         * 3. If an entry contains DFS link targets as indicated by RootOrLink, go to step 4; otherwise, go to
         * step 3.
         */
        private SmbSessionResult<T> Step2<T>(SmbSession session, ResolveState<T> state)
        {
            var lookup = _referralCache.Lookup(state.Path);

            if (lookup == null || (lookup.IsExpired && lookup.IsRoot))
            {
                return Step5(session, state); // Resolve Root Referral
            }

            if (lookup.IsExpired) // Expired LINK target
            {
                return Step9(session, state, lookup);
            }

            if (lookup.IsLink)
            {
                return Step4(session, state, lookup);
            }

            return Step3(session, state, lookup);
        }

        /**
         * Step 3: [ReferralCache hit, unexpired TTL] Replace the portion of the path that matches DFSPathPrefix of the
         * ReferralCache entry with the DFS target path of TargetHint of the ReferralCache entry. For example,
         * if the path is \MyDomain\MyDfs\MyDir and the ReferralCache entry contains \MyDomain\MyDfs with a
         * DFS target path of \someserver\someshare\somepath, the effective path becomes
         * \someserver\someshare\somepath\MyDir. Go to step 8.
         */
        private SmbSessionResult<T> Step3<T>(SmbSession session, ResolveState<T> state, ReferralCacheEntry lookup)
        {
            SmbSessionResult<T> result = null;
            var initialPath = state.Path;

            for (var target = lookup.TargetHintEntry; target != null; target = lookup.NextTargetHint())
            {
                state.Path = initialPath.ReplacePrefix(lookup.DfsPathPrefix, target.TargetPath);
                state.IsDfsPath = true;
                result = Step8(session, state, lookup);

                if (result.Status == NTStatus.STATUS_SUCCESS)
                {
                    return result;
                }

                if (result.Status == NTStatus.STATUS_PATH_NOT_COVERED)
                {
                    break;
                }
            }

            return new SmbSessionResult<T>(result?.Status ?? NTStatus.STATUS_DATA_ERROR);
        }

        /**
         * Step 4: [ReferralCache hit, unexpired TTL, RootOrLink=link]
         * 1. If the second component of the path is "SYSVOL" or "NETLOGON" go to step 3.
         * 2. Check the Interlink element of the ReferralCache entry.
         * - If Interlink is set in the ReferralCache entry,then the TargetHint is in another DFS namespace. Go to step 11.
         * - If Interlink is not set in the ReferralCache entry then the TargetHint is not in another DFS namespace. Go to step 3.
         */
        private SmbSessionResult<T> Step4<T>(SmbSession session, ResolveState<T> state, ReferralCacheEntry lookup)
        {
            if (state.Path.IsSysVolOrNetLogon)
            {
                return Step3(session, state, lookup);
            }

            if (lookup.IsInterlink)
            {
                return Step11(session, state, lookup);
            }

            return Step3(session, state, lookup);
        }

        /**
         * Step 5: [ReferralCache miss] [ReferralCache hit, expired TTL, RootOrLink=root]
         * Look up the first path component in DomainCache.
         * 1. If no matching DomainCache entry is found, use the first path component as the host name for DFS root referral
         * request purposes. Go to step 6.
         * 2. If a matching DomainCache entry is found:
         * 1. If DCHint is not valid, send DC referral request, as specified in section 3.1.4.2,
         * providing "DC", BootstrapDC, UserCredentials, MaxOutputSizeff, and Path as parameters.
         * The processing of the referral response is specified in section 3.1.5.4.2. If the referral request fails, go to step 13.
         * 2. If the second path component is "SYSVOL" or "NETLOGON", go to step 10.
         * 3. Use DCHint as host name for DFS root referral request purposes. Go to step 6.
         */
        private SmbSessionResult<T> Step5<T>(SmbSession session, ResolveState<T> state)
        {
            var potentialDomain = state.Path.PathComponents[0];
            var domainCacheEntry = _domainCache.Lookup(potentialDomain);

            if (domainCacheEntry == null)
            {
                state.HostName = potentialDomain;
                state.ResolvedDomainEntry = false;
                return Step6(session, state);
            }

            if (string.IsNullOrEmpty(domainCacheEntry.DcHint))
            {
                var bootstrapDc = session.Credentials.Domain;
                var referralResult = SendDfsReferralRequest(DfsRequestType.DC, bootstrapDc, session, state.Path);

                if (referralResult.Status != NTStatus.STATUS_SUCCESS)
                {
                    return Step13(session, state, referralResult);
                }
                else
                {
                    domainCacheEntry = referralResult.DomainCacheEntry;
                }
            }

            if (state.Path.IsSysVolOrNetLogon)
            {
                return Step10(session, state, domainCacheEntry);
            }


            state.HostName = domainCacheEntry.DcHint;
            state.ResolvedDomainEntry = true;
            return Step6(session, state);
        }

        /**
         * [DFS Root referral request] Issue a DFS root referral request, as specified in section 3.1.4.2,
         * providing "ROOT", the first path component, UserCredentials, MaxOutputSize, and Path as parameters.
         * The processing of the referral response and/or error is as specified in section 3.1.5.4.3, which will update the ReferralCache
         * on success. On DFS root referral request success, go to step 7.
         * On DFS root referral request failure:
         * 1. If the immediately preceding processing step was step 5.2.3, this is a domain name or path. Go to step 13.
         * 2. If processing of this I/O request encountered a ReferralCache hit, or one of its DFS referral requests succeeded
         * (as would have occurred in the case of a previous Interlink - see step 11 - or a domain root referral,
         * when entering from step 5), the path is in a DFS namespace. Go to step 14.
         * 3. The path is not a DFS path and no further processing is required. Go to step 12.
         */
        private SmbSessionResult<T> Step6<T>(SmbSession session, ResolveState<T> state)
        {
            var result = SendDfsReferralRequest(DfsRequestType.ROOT, state.Path.PathComponents[0], session, state.Path);

            if (result.Status == NTStatus.STATUS_SUCCESS)
            {
                return Step7(session, state, result.ReferralCacheEntry);
            }

            if (state.ResolvedDomainEntry)
            {
                return Step13(session, state, result);
            }

            if (state.IsDfsPath)
            {
                return Step14(session, state, result);
            }

            return Step12(session, state);
        }

        /**
         * [DFS root referral success] If the current ReferralCache entry's RootOrLink indicates
         * root targets, go to step 3; otherwise, go to step 4.
         */
        private SmbSessionResult<T> Step7<T>(SmbSession session, ResolveState<T> state, ReferralCacheEntry lookup)
        {
            if (lookup.IsRoot)
            {
                return Step3(session, state, lookup);
            }

            return Step4(session, state, lookup);
        }

        /**
         * Step 8: [I/O request, path fully resolved] Issue I/O operation to TargetHint of ReferralCache entry.
         * 1. If the I/O operation fails with STATUS_PATH_NOT_COVERED.
         * - If the RootOrLink of ReferralCache entry indicates link targets, set the failure status to the last error that occurred and go to step 14.
         * - If the RootOrLink of ReferralCache entry indicates root targets, the process is as specified in section 3.1.5.1.
         * If this processing does not successfully determine a ReferralCache entry to traverse the link, set the failure status
         * to the last error that occurred and go to step 14.
         * - ReferralCache entry for the link determined successfully. Go to step 4.
         * 2. If the I/O operation fails with an error other than STATUS_PATH_NOT_COVERED, then the process is as specified in section 3.1.5.2.
         * If the processing of that section specifies a new TargetHint, repeat step 8. Otherwise, set the failure status to the last error that
         * occurred and go to step 14.
         * 3. If the I/O operation is successful, the process is as specified in section 3.1.5.3. Complete the I/O operation and
         * user/application-initiated I/O request with success.
         */
        private SmbSessionResult<T> Step8<T>(SmbSession session, ResolveState<T> state, ReferralCacheEntry lookup)
        {
            return state.Action(state.Path.ToPath());
        }

        /**
         * Step 9: [ReferralCache hit, expired TTL, RootOrLink=link] The link referral request is issued to a DFS root target of the namespace.
         * Find the root ReferralCache entry corresponding to the first two path components, noting that this will already be in the cache due
         * to processing that resulted in acquiring the expired link ReferralCache entry. Issue a DFS link referral request,
         * as specified in section 3.1.4.2, providing "LINK", TargetHint of the root ReferralCache entry, UserCredentials, MaxOutputSize, and Path
         * as parameters, and process the DFS referral response and/or error as specified in section 3.1.5.4.3, which will update the ReferralCache
         * on success.
         * <p>
         * If the DFS Link referral request fails, set the failure status to the last error that occurred and go to step 14. Otherwise:
         * 1. If the RootOrLink of the refreshed ReferralCache entry indicates DFS root targets, go to step 3.
         * 2. If the RootOrLink of the refreshed ReferralCache entry indicates DFS link targets, go to step 4.
         */
        private SmbSessionResult<T> Step9<T>(SmbSession session, ResolveState<T> state, ReferralCacheEntry lookup)
        {
            var rootPath = new DfsPath(state.Path.PathComponents.Take(2).ToList());
            var rootReferralCacheEntry = _referralCache.Lookup(rootPath);

            if (rootReferralCacheEntry == null)
            {
                _referralCache.Clear(state.Path);
                return Step1(session, state);
            }

            var result = SendDfsReferralRequest(DfsRequestType.LINK, rootReferralCacheEntry.TargetHintEntry.TargetPath, session, state.Path);

            if (result.Status != NTStatus.STATUS_SUCCESS)
            {
                return Step14(session, state, result);
            }

            if (result.ReferralCacheEntry.IsRoot)
            {
                return Step3(session, state, result.ReferralCacheEntry);
            }

            return Step4(session, state, lookup);
        }

        /**
         * Step 10: [sysvol referral request] Issue a sysvol referral request, as specified in
         * section 3.1.4.2, providing 'SYSVOL', the DCHint DC of the DomainCache entry that
         * corresponds to the domain name in the first path component, UserCredentials, MaxOutputSize,
         * and Path as parameters. The processing of the referral response and/or error is as
         * specified in section 3.1.5.4.4, which will update the ReferralCache on success.
         * If the referral request is successful, go to step 3; otherwise, go to step 13.
         */
        private SmbSessionResult<T> Step10<T>(SmbSession session, ResolveState<T> state, DomainCacheEntry domainCacheEntry)
        {
            var result = SendDfsReferralRequest(DfsRequestType.SYSVOL, domainCacheEntry.DcHint, session, state.Path);

            if (result.Status == NTStatus.STATUS_SUCCESS)
            {
                return Step3(session, state, result.ReferralCacheEntry);
            }

            return Step13(session, state, result);
        }

        /**
         * Step 11: [interlink] Replace the portion of the path that matches the DFSPathPrefix of
         * the ReferralCache entry with TargetHint. For example, if the path is \MyDomain\MyDfs\MyLink\MyDir
         * and the referral entry contains \MyDomain\MyDfs\MyLink with a DFS target path of
         * \someserver\someshare\somepath, the effective path becomes
         * \someserver\someshare\somepath\MyDir. Go to step 2.
         */
        private SmbSessionResult<T> Step11<T>(SmbSession session, ResolveState<T> state, ReferralCacheEntry lookup)
        {
            state.Path = state.Path.ReplacePrefix(lookup.DfsPathPrefix, lookup.TargetHintEntry.TargetPath);
            state.IsDfsPath = true;
            return Step2(session, state);
        }

        /**
         * Step 12: [not DFS] The path does not correspond to a DFS namespace or a SYSVOL/NETLOGON share.
         * Do not change the path, and return an implementation-defined error.
         * The user/application initiated I/O request is handled by the local operating system.
         */
        private SmbSessionResult<T> Step12<T>(SmbSession session, ResolveState<T> state)
        {
            return state.Action(state.Path.ToPath());
        }

        /**
         * Step 13: [Cannot get DC for domain] The first path component is a domain name.
         * Fail the I/O operation and user/application-initiated I/O request with the last
         * error code that occurred before the jump to this step.
         */
        private SmbSessionResult<T> Step13<T>(SmbSession session, ResolveState<T> state, ReferralResult result)
        {
            throw new Exception($"Cannot get DC for domain {state.Path.PathComponents[0]} (status = {result.Status})");
        }

        /**
         * Step 14: [DFS path] The path is known to be in a DFS namespace, but the DFS root referral
         * request or DFS Link referral request has failed. Complete the user/application-initiated
         * I/O request with the error code that occurred before the jump to this step.
         */
        private SmbSessionResult<T> Step14<T>(SmbSession session, ResolveState<T> state, ReferralResult result)
        {
            throw new Exception($"DFS request failed for path {state.Path.ToPath()} (status = {result.Status})");
        }

        private ReferralResult SendDfsReferralRequest(DfsRequestType type, string hostName, SmbSession session, DfsPath path)
        {
            var fileStore = session.GetFileStore($@"{hostName}\IPC$");

            var request = new Messages.RequestGetDfsReferral
            {
                RequestFileName = path.ToPath(),
                MaxReferralLevel = 4
            };

            var status = (NTStatus)fileStore.DeviceIOControl(RootId, (uint)IoControlCode.FSCTL_DFS_GET_REFERRALS, request.GetBytes(), out var output, 4096);

            return HandleReferralResponse(path, output, type, status);
        }

        private ReferralResult HandleReferralResponse(DfsPath path, byte[] output, DfsRequestType type, NTStatus status)
        {
            var result = new ReferralResult { Status = status };

            if (status != NTStatus.STATUS_SUCCESS)
            {
                return result;
            }

            var response = new Messages.ResponseGetDfsReferral(path.ToPath());
            response.Read(output);

            switch (type)
            {
                case DfsRequestType.DC:
                    HandleDcReferralResponse(result, response);
                    break;
                case DfsRequestType.DOMAIN:
                    throw new Exception();
                case DfsRequestType.SYSVOL:
                case DfsRequestType.ROOT:
                case DfsRequestType.LINK:
                    HandleRootOrLinkReferralResponse(result, response);
                    break;
                default:
                    throw new Exception();
            }

            return result;
        }

        private void HandleRootOrLinkReferralResponse(ReferralResult result, Messages.ResponseGetDfsReferral response)
        {
            if (response.ReferralEntries.Count < 1)
            {
                result.Status = NTStatus.STATUS_OBJECT_PATH_NOT_FOUND;
                return;
            }

            var referralCacheEntry = new ReferralCacheEntry(response, _domainCache);
            _referralCache.Add(referralCacheEntry);
            result.ReferralCacheEntry = referralCacheEntry;
        }

        private void HandleDcReferralResponse(ReferralResult result, Messages.ResponseGetDfsReferral response)
        {
            if (response.VersionNumber < 3)
            {
                return;
            }

            var domainCacheEntry = new DomainCacheEntry(response);
            _domainCache.Add(domainCacheEntry);
            result.DomainCacheEntry = domainCacheEntry;
        }
    }
}