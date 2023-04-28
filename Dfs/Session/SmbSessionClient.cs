using System;
using System.Collections.Concurrent;
using System.Net;
using SMBLibrary.Client;

namespace SMBLibrary.Contrib.Dfs.Session
{
    public class SmbSessionClient : IDisposable
    {
        public ISMBClient Client { get; private set; }
        public ConcurrentDictionary<string, ISMBFileStore> FileStores { get; } = new ConcurrentDictionary<string, ISMBFileStore>(StringComparer.OrdinalIgnoreCase);
        public string Server { get; }

        public SmbSessionClient(string server, ISMBClient client)
        {
            Server = server;
            Client = client;
        }

        public static SmbSessionClient Create(string server,
            NetworkCredential credentials,
            SMBTransportType transportType = SMBTransportType.DirectTCPTransport,
            AuthenticationMethod authenticationMethod = AuthenticationMethod.NTLMv2)
        {
            var client = new SMB2Client();
            client.Connect(server, transportType); 
            client.Login(credentials.Domain, credentials.UserName, credentials.Password, authenticationMethod);
            return new SmbSessionClient(server, client);
        }

        public ISMBFileStore TreeConnect(string share, out NTStatus status)
        {
            SMBLibrary.NTStatus legacyStatus;
            status = NTStatus.STATUS_SUCCESS;

            if (!FileStores.TryGetValue(share, out var store))
            {
                store = Client.TreeConnect(share, out legacyStatus);
                SmbHelpers.ValidateStatus((NTStatus)legacyStatus, "Failed to connect to share.", share, NTStatus.STATUS_SUCCESS);
                FileStores[share] = store;
                status = (NTStatus)legacyStatus;
            }

            return store;
        }

        public void Dispose()
        {
            if (Client == null)
            {
                return;
            }

            // Close file stores
            try
            {
                foreach (var fileStore in FileStores.Values)
                {
                    fileStore?.Disconnect();
                }

                FileStores.Clear();
            }
            catch
            {
                // Do nothing
            }

            // Close client
            try
            {
                Client?.Logoff();
            }
            catch
            {
                // Do nothing
            }

            try
            {
                Client?.Disconnect();
            }
            catch
            {
                // Do nothing
            }

            Client = null;
        }
    }
}