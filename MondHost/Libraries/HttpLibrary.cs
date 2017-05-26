using System;
using System.Collections.Generic;
using System.Net;
using Mond;
using Mond.Binding;
using Mond.Libraries;

namespace MondHost.Libraries
{
    [MondModule("Http")]
    static class HttpModule
    {
        [MondFunction("get")]
        public static MondValue Get(string address)
        {
            var uri = new Uri(address);

            if (uri.Scheme != "http" && uri.Scheme != "https")
                throw new MondRuntimeException("Http.get: {0} protocol not supported", uri.Scheme);

            var client = new WebClient();
            client.Headers.Add("User-Agent", "Mondbox");

            return client.DownloadString(address);
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
