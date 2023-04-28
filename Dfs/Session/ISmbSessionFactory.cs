using System.Net;

namespace SMBLibrary.Contrib.Dfs.Session
{
    public interface ISmbSessionFactory
    {
        SmbSession Create(string path);
    }

    public class SmbSessionFactory : ISmbSessionFactory
    {
        private readonly NetworkCredential _credentials;

        public SmbSessionFactory(NetworkCredential credentials)
        {
            _credentials = credentials;
        }

        public SmbSession Create(string path)
        {
            return new SmbSession(_credentials);
        }
    }
}