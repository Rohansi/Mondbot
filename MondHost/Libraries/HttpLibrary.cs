using System;
using System.Collections.Generic;
using System.Net;
using Mond;
using Mond.Binding;
using Mond.Libraries;
using System.Net.Http;

namespace MondHost.Libraries
{
    [MondModule("Http")]
    static class HttpModule
    {
        [MondFunction("htmlEncode")]
        public static string HtmlEncode(string value) => WebUtility.HtmlEncode(value);

        [MondFunction("htmlDecode")]
        public static string HtmlDecode(string value) => WebUtility.HtmlDecode(value);

        [MondFunction("urlEncode")]
        public static string UrlEncode(string value) => WebUtility.UrlEncode(value);

        [MondFunction("urlDecode")]
        public static string UrlDecode(string value) => WebUtility.UrlDecode(value);

        [MondFunction("get")]
        public static string Get(string address)
        {
            var uri = GetUri(address);
            var client = GetHttpClient();

            return client.GetStringAsync(uri).Result;
        }

        private static Uri GetUri(string uriString)
        {
            var uri = new Uri(uriString);

            if (uri.Scheme != "http" && uri.Scheme != "https")
                throw new MondRuntimeException("Http: {0} protocol not supported", uri.Scheme);

            return uri;
        }

        private static HttpClient GetHttpClient()
        {
            var client = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ClientCertificateOptions = ClientCertificateOption.Automatic,
                MaxAutomaticRedirections = 3
            });

            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MondBot");

            return client;
        }
    }

    class HttpLibraries : IMondLibraryCollection
    {
        public IEnumerable<IMondLibrary> Create(MondState state)
        {
            yield return new HttpLibrary();
        }
    }

    class HttpLibrary : IMondLibrary
    {
        public IEnumerable<KeyValuePair<string, MondValue>> GetDefinitions()
        {
            var httpModule = MondModuleBinder.Bind(typeof(HttpModule));
            yield return new KeyValuePair<string, MondValue>("Http", httpModule);
        }
    }
}
