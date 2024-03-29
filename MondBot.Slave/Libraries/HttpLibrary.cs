﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Mond;
using Mond.Binding;
using Mond.Libraries;
using System.Net.Http;

namespace MondBot.Slave.Libraries
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

        [MondFunction("getAsync")]
        public static MondValue GetAsync(string address)
        {
            AsyncUtil.EnsureAsync();
            
            var uri = GetUri(address);
            var client = GetHttpClient();

            return AsyncUtil.ToObject(client.GetStringAsync(uri).ContinueWith(t => (MondValue)t.Result));
        }

        [MondFunction("post")]
        public static string Post(string address, MondValue formData)
        {
            if (formData.Type != MondValueType.Object)
                throw new MondRuntimeException("Http.post: formData must be an object");

            var uri = GetUri(address);
            var client = GetHttpClient();

            var data = formData.AsDictionary
                .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value));

            var response = client.PostAsync(uri, new FormUrlEncodedContent(data)).Result;
            return response.Content.ReadAsStringAsync().Result;
        }

        [MondFunction("postJson")]
        public static string PostJson(MondState state, string address, MondValue obj)
        {
            if (obj.Type != MondValueType.Object)
                throw new MondRuntimeException("Http.postJson: obj must be an object");

            var uri = GetUri(address);
            var client = GetHttpClient();

            var data = JsonModule.Serialize(state, obj);
            var response = client.PostAsync(uri, new StringContent(data)).Result;
            return response.Content.ReadAsStringAsync().Result;
        }

        private static Uri GetUri(string uriString)
        {
            if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
                throw new MondRuntimeException("Http: could not parse URI");

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
        public IEnumerable<KeyValuePair<string, MondValue>> GetDefinitions(MondState state)
        {
            var httpModule = MondModuleBinder.Bind(typeof(HttpModule), state);
            yield return new KeyValuePair<string, MondValue>("Http", httpModule);
        }
    }
}
