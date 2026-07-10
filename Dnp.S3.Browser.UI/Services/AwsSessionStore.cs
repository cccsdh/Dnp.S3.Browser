using Amazon.Runtime;

namespace Dnp.S3.Browser.UI.Services
{
    public class AwsSessionStore
    {
        private AWSCredentials? _creds;
        private readonly object _lock = new();
        public bool HasSession
        {
            get
            {
                lock (_lock) { return _creds != null; }
            }
        }

        public AWSCredentials? GetCredentials()
        {
            lock (_lock) { return _creds; }
        }

        public void SetCredentials(AWSCredentials creds)
        {
            lock (_lock) { _creds = creds; }
        }
    }
}
