using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Takotsubo.utils
{
    internal class BulletTokenUtil
    {

        public static async Task<string> GetBulletToken(string webServiceToken)
        {
            if(string.IsNullOrEmpty(webServiceToken))
            {
                return string.Empty;
            }

            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://api.lp1.av5ja.srv.nintendo.net/api/bullet_tokens"))
            {
                request.Headers.Add("Host", "api.lp1.av5ja.srv.nintendo.net");
                request.Headers.Add("x-nacountry", "JP");
                request.Headers.Add("Accept", "*/*");
                request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                request.Headers.Add("Accept-Language", "ja-JP");
                request.Headers.Add("x-web-view-ver", "1.0.0-5e2bcdfb");
                request.Headers.Add("Origin", "https://api.lp1.av5ja.srv.nintendo.net");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 15_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148");
                request.Headers.Add("Referer", "https://api.lp1.av5ja.srv.nintendo.net/?lang=ja-JP&na_country=JP&na_lang=ja-JP");
                request.Headers.Add("Connection", "keep-alive");
                request.Headers.Add("Cookie", $"_dnt=0; _gtoken={webServiceToken}; ");

                try
                {
                    var res = await HttpClientPool.GetAutoDeserializedJsonAsync<SplaNetData.BulletTokens>(request);
                    return res.bulletToken;
                }
                catch (Exception e)
                {
                    await Logger.WriteLogAsync($"Failed to get bullet token. {e}");
                    return "";
                }
            }

        }


    }
}
