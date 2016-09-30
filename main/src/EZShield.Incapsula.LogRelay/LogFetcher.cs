using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;

namespace EZShield.Incapsula.LogRelay
{
    public class LogFetcher : IDisposable
    {
        public static readonly Version HTTP_VERSION_1_0 = Version.Parse("1.0");
        public static readonly Version HTTP_VERSION_1_1 = Version.Parse("1.1");

        // How to use HttpClient efficiently:
        //   https://channel9.msdn.com/Series/aspnetmonsters/ASPNET-Monsters-62-You-are-probably-using-HttpClient-wrong
        // HttpClient and related usage:
        //    https://blogs.msdn.microsoft.com/henrikn/2012/08/07/httpclient-httpclienthandler-and-webrequesthandler-explained/

        private HttpClientHandler _httpHandler;
        private HttpClient _httpClient;

        private WebProxy _proxy;

        public Uri Proxy
        {
            get
            {
                return _proxy?.ProxyUri;
            }
            set
            {
                if (value == null)
                    _httpHandler.Proxy = _proxy = null;
                else
                    _httpHandler.Proxy = _proxy = new WebProxy
                    {
                        ProxyUri = value
                    };
            }
        }

        public LogFetcher(Uri baseUrl, string username, string password)
        {
            _httpHandler = new HttpClientHandler();
            _httpHandler.AllowAutoRedirect = true;

            // We throw in this cookie in there
            // to uniquely identify this client
            var ti = typeof(LogFetcher).GetTypeInfo();
            _httpHandler.CookieContainer.Add(baseUrl,
                    new Cookie(ti.Namespace,
                    ti.Assembly.GetName().Version.ToString()));

            _httpClient = new HttpClient(_httpHandler);
            _httpClient.BaseAddress = baseUrl;

            // We want to send the creds along, even with the very first
            // request, and not just react to a 401 challenge response
            string credsEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                    $"{username}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", credsEncoded);

            // NOTE:  This does not work because .PreAuthenticate does not do
            // what it sounds like -- it actually caches the credentials only
            // after the first 401 response; see this for more info:
            //    https://weblog.west-wind.com/posts/2010/feb/18/net-webrequestpreauthenticate-not-quite-what-it-sounds-like
            /*
            _httpHandler.Credentials = new NetworkCredential(username, password);      
            _httpHandler.PreAuthenticate = true;
            */
        }

        public string FetchString(string urlPath)
        {
            var result = _httpClient.GetAsync(urlPath).Result;
            result.EnsureSuccessStatusCode();
            return result.Content.ReadAsStringAsync().Result;
        }

        public byte[] FetchBytes(string urlPath)
        {
            var result = _httpClient.GetAsync(urlPath).Result;
            result.EnsureSuccessStatusCode();
            return result.Content.ReadAsByteArrayAsync().Result;
        }

#region -- IDisposable Support --

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    _httpClient.Dispose();
                    _httpClient = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~LogFetcher() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

 #endregion -- IDisposable Support --

        /// <summary>
        /// Internal implementation of proxy interface, that only specifies
        /// the proxy Uri (hostname + port) and does not bypass any URL path. 
        /// </summary>
        public class WebProxy : IWebProxy
        {
            public ICredentials Credentials
            { get; set; }

            public Uri ProxyUri
            { get; set; }

            public Uri GetProxy(Uri destination)
            {
                return ProxyUri;
            }

            public bool IsBypassed(Uri host)
            {
                return false;
            }
        }

    }
}
