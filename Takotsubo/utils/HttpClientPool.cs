using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Takotsubo.utils
{
    public class HttpClientPool
    {
        private static readonly CookieContainer CookieContainer = new CookieContainer();
        // private static readonly WebProxy Proxy = WebProxy.GetDefaultProxy();
        // private static readonly HttpClientHandler Handler = new HttpClientHandler { CookieContainer = CookieContainer, Proxy = Proxy };
        private static readonly HttpClientHandler Handler = new HttpClientHandler() { UseCookies = false };
        private static readonly HttpClient Client = new HttpClient(Handler);

        public static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) => await Client.SendAsync(request);
        public static async Task<string> GetStringAsync(string uri) => await Client.GetStringAsync(uri);

        public static async Task<T> GetDeserializedJsonAsync<T>(string uri)
        {
            var response = await Client.GetStringAsync(uri);
            return Utf8Json.JsonSerializer.Deserialize<T>(response);
        }

        // gzipで圧縮されているかを判定しながらデータ形式に落とし込む
        public static async Task<T> GetAutoDeserializedJsonAsync<T>(HttpRequestMessage request)
        {
            var response = await Client.SendAsync(request);

            if (response.Content.Headers.ContentEncoding.FirstOrDefault() == "gzip")
            {
                response.EnsureSuccessStatusCode();
                using (var inStream = await response.Content.ReadAsStreamAsync())
                using (var decompStream = new GZipStream(inStream, CompressionMode.Decompress))
                {
                    using (var reader = new StreamReader(decompStream, Encoding.UTF8, true))
                    {
                        await Logger.WriteLogAsync(String.Format("request: {0}", request));
                        await Logger.WriteLogAsync(String.Format("response: {0}", response));
                        string str = await reader.ReadToEndAsync();
                        var result = Utf8Json.JsonSerializer.Deserialize<T>(str);
                        return result;
                    }
                }
            }
            else
            {
                string str = await response.Content.ReadAsStringAsync();
                var result = Utf8Json.JsonSerializer.Deserialize<T>(str);
                return result;
            }
        }

        public static async Task<T> GetDeserializedJsonAsync<T>(HttpRequestMessage request)
        {
            var response = await Client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            return Utf8Json.JsonSerializer.Deserialize<T>(json);
        }

        public static async Task<string> GetDecompressedAsync(HttpRequestMessage request)
        {
            using (var response = await Client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                var inStream = await response.Content.ReadAsStreamAsync();

                var decompStream = new GZipStream(inStream, CompressionMode.Decompress);
                using (inStream)
                using (decompStream)
                {
                    using (var reader = new StreamReader(decompStream, Encoding.UTF8, true) as TextReader)
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
            }
        }

        public static async Task<T> GetDecompressedDeserializedJsonAsync<T>(HttpRequestMessage request)
        {
            var json = await GetDecompressedAsync(request);
            return Utf8Json.JsonSerializer.Deserialize<T>(json);
        }

        public static async Task<CookieContainer> GetCookieContainer(HttpRequestMessage request)
        {
            await Client.SendAsync(request);
            return Handler.CookieContainer;
        }

        public static async Task<string> GetStringAsyncWithCookieContainer(string uri, Cookie cookie)
        {
            Handler.CookieContainer.Add(new Uri(""), cookie);
            return await Client.GetStringAsync(uri);
        }

        public static async Task<T> GetDeserializedJsonAsyncWithCookieContainer<T>(string uri, Cookie cookie)
        {
            Handler.CookieContainer.Add(new Uri("https://app.splatoon2.nintendo.net/"), cookie);
            var json = await Client.GetStringAsync(uri);

            return Utf8Json.JsonSerializer.Deserialize<T>(json);
        }

        public static async Task<string> DownloadAsync(string uri, string folderPath)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using (var content = response.Content)
                {
                    using (var stream = await content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(folderPath + "/" + request.RequestUri.Segments.Last(), FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        stream.CopyTo(fileStream);
                        return request.RequestUri.Segments.Last();
                    }
                }
            }
        }
    }
}
