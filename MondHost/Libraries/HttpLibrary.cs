using System;
using System.Collections.Generic;
using System.Linq;
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

        [MondFunction("post")]
        public static string Post(string address, MondValue formData)
        {
            if (formData == null || formData.Type != MondValueType.Object)
                throw new MondRuntimeException("Http.post: formData must be an object");

            var uri = GetUri(address);
            var client = GetHttpClient();

            var data = formData.Object
                .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value));

            var response = client.PostAsync(uri, new FormUrlEncodedContent(data)).Result;
            return response.Content.ReadAsStringAsync().Result;
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
            yield return new HttpLibrary(state);
        }
    }

    class HttpLibrary : IMondLibrary
    {
        private readonly MondState _state;

        public HttpLibrary(MondState state) => _state = state;

        public IEnumerable<KeyValuePair<string, MondValue>> GetDefinitions()
        {
            var httpModule = MondModuleBinder.Bind(typeof(HttpModule), _state);
            yield return new KeyValuePair<string, MondValue>("Http", httpModule);
        }
    }
}
