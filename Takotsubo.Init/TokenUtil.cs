using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Takotsubo.utils;

namespace Takotsubo.Init
{
    internal class TokenUtil
    {
        // https://github.com/frozenpandaman/splatnet2statink/blob/master/iksm.py
        private static long GetUnixTime() => (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        private const string appVersion = "2.2.0";

        /// <summary>
        /// Generate login URL
        /// </summary>
        /// <returns>login url</returns>
        public static (string authCodeVerifier, string url) GenerateLoginURL()
        {
            var rnd = new Random();
            var rndBytes = new byte[36];
            rnd.NextBytes(rndBytes);
            var authState = Convert.ToBase64String(rndBytes).Replace('+', '-').Replace('/', '_');

            rndBytes = new byte[32];
            rnd.NextBytes(rndBytes);
            var authCodeVerifier = Convert.ToBase64String(rndBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            var sha256 = SHA256.Create();
            var authCvHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(authCodeVerifier)));

            var authCodeChallenge = authCvHash.TrimEnd('=').Replace('+', '-').Replace('/', '_');


            var url = "https://accounts.nintendo.com/connect/1.0.0/authorize?";

            var body = new Dictionary<string, string>
            {
                {"redirect_uri", "npf71b963c1b7b6d119://auth"},
                {"client_id", "71b963c1b7b6d119"},
                {"state", authState},
                {"scope", "openid user user.birthday user.mii user.screenName"},
                {"response_type", "session_token_code"},
                {"session_token_code_challenge", authCodeChallenge},
                {"session_token_code_challenge_method", "S256"},
                {"theme", "login_form"}
            };
            url += string.Join("&", body.Select(v => v.Key + "=" + v.Value));
            return (authCodeVerifier, url);
        }

        /// <summary>
        /// Get session token
        /// </summary>
        /// <returns>session_token</returns>
        public static async Task<string> GetSessionToken(string sessionTokenCode, string authCodeVerifier)
        {
            const string url = "https://accounts.nintendo.com/connect/1.0.0/api/session_token";
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {

                string version = SettingManager.LoadConfig().Version;
                if (version == null || version == "") version = appVersion;

                request.Headers.Add("Accept-Language", "en-US");
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("Connection", "Keep-Alive");
                request.Headers.Add("Connection-Type", "application/x-www-form-urlencoded");
                request.Headers.Add("Accept-Encoding", "gzip");
                request.Headers.Add("User-Agent", "OnlineLounge/" + version + " NASDKAPI Android");
                request.Headers.Add("Host", "accounts.nintendo.com");

                var body = new Dictionary<string, string>
            {
                {"client_id", "71b963c1b7b6d119"},
                {"session_token_code", sessionTokenCode},
                {"session_token_code_verifier", authCodeVerifier}
            };

                if (IsBodyEmpty(body))
                {
                    await Logger.WriteLogAsync("Body is null");
                    return "";
                }

                request.Content = new FormUrlEncodedContent(body);

                try
                {
                    var res = await HttpClientPool.GetAutoDeserializedJsonAsync<SplaNetData.SessionToken>(request);
                    return res.session_token;
                }
                catch (HttpRequestException e)
                {
                    await Logger.WriteLogAsync($"Failed to get \"session token\". {e}");
                    return "";
                }
                catch (InvalidDataException e)
                {
                    await Logger.WriteLogAsync($"Failed to decompress \"session token\". {e}");
                    return "";
                }
            }
        }

        /// <summary>
        /// Login process for SplatNet2
        /// </summary>
        /// <returns>iksm session</returns>
        public static async Task<string> GetCookie(string sessionToken)
        {
            var timeStamp = GetUnixTime();
            var guid = Guid.NewGuid().ToString();

            string version = SettingManager.LoadConfig().Version;
            if (version == null || version == "") version = appVersion;

            #region access token 取得
            const string url = "https://accounts.nintendo.com/connect/1.0.0/api/token";

            var request = new HttpRequestMessage(HttpMethod.Post, url);

            request.Headers.Add("Accept-Language", "ja-JP");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Connection", "Keep-Alive");
            request.Headers.Add("Accept-Encoding", "gzip");
            request.Headers.Add("User-Agent", "OnlineLounge/" + version + " NASDKAPI Android");
            request.Headers.Add("Host", "accounts.nintendo.com");

            var body = new Dictionary<string, string>
            {
                {"client_id", "71b963c1b7b6d119"},  //splatoon2 service
                {"session_token", sessionToken},
                {"grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer-session-token"}
            };

            if (IsBodyEmpty(body))
            {
                await Logger.WriteLogAsync("Body is null");
                return "";
            }

            var json = Utf8Json.JsonSerializer.ToJsonString(body);

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            string accessToken;
            try
            {
                var res = await HttpClientPool.GetAutoDeserializedJsonAsync<SplaNetData.AccessToken>(request);
                accessToken = res.access_token;
            }
            catch (HttpRequestException e)
            {
                await Logger.WriteLogAsync(String.Format("Failed to get \"access token\". {0}", e));
                return "";
            }
            catch (InvalidDataException e)
            {
                await Logger.WriteLogAsync(String.Format("Failed to decompress \"access token\". {0}", e));
                return "";
            }
            finally
            {
                request.Dispose();
            }

            #endregion access token 取得

            #region user info 取得
            const string url2 = "https://api.accounts.nintendo.com/2.0.0/users/me";
            request = new HttpRequestMessage(HttpMethod.Get, url2);

            request.Headers.Add("Accept-Language", "ja-JP");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Authorization", "Bearer " + accessToken);
            request.Headers.Add("Connection", "Keep-Alive");
            request.Headers.Add("Accept-Encoding", "gzip");
            request.Headers.Add("User-Agent", "OnlineLounge/" + version + " NASDKAPI Android");
            request.Headers.Add("Host", "api.accounts.nintendo.com");
            request.Headers.Add("Accept-Encoding", "gzip");

            if (IsBodyEmpty(accessToken))
            {
                await Logger.WriteLogAsync("Body is null");
                return "";
            }

            SplaNetData.UserInfo userInfo;
            try
            {
                userInfo = await HttpClientPool.GetAutoDeserializedJsonAsync<SplaNetData.UserInfo>(request);
            }
            catch (HttpRequestException e)
            {
                await Logger.WriteLogAsync(String.Format("Failed to get \"user data\". {0}", e));
                return "";
            }
            catch (InvalidDataException e)
            {
                await Logger.WriteLogAsync(String.Format("Failed to decompress \"user data\". {0}", e));
                return "";
            }
            finally
            {
                request.Dispose();
            }

            #endregion user data 取得

            #region access token 取得
            request = new HttpRequestMessage(HttpMethod.Post, "https://api-lp1.znc.srv.nintendo.net/v3/Account/Login");

            request.Headers.Add("Accept-Language", "ja-JP");
            request.Headers.Add("Accept", "application/json; charset=utf-8");
            request.Headers.Add("Connection", "Keep-Alive");
            request.Headers.Add("Accept-Encoding", "gzip");
            request.Headers.Add("Authorization", "Bearer");
            request.Headers.Add("X-Platform", "Android");
            request.Headers.Add("User-Agent", "com.nintendo.znca/" + version + " (Android/7.1.2)");
            request.Headers.Add("Host", "api-lp1.znc.srv.nintendo.net");
            request.Headers.Add("X-ProductVersion", version);

            var flapgNSO = await CallFlapgAPI(accessToken, Steps.Access_Token);

            var body3 = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "parameter",
                    new Dictionary<string, string>
                    {
                        {"f", flapgNSO.f},
                        {"naIdToken", accessToken},
                        {"timestamp", flapgNSO.timestamp.ToString()},
                        {"requestId", flapgNSO.request_id},
                        {"naCountry", userInfo.country},
                        {"naBirthday", userInfo.birthday},
                        {"language", userInfo.language}
                    }
                },
            };

            if (IsBodyEmpty(body3))
            {
                await Logger.WriteLogAsync("Body is null");
                return "";
            }

            var json3 = Utf8Json.JsonSerializer.ToJsonString(body3);

            request.Content = new StringContent(json3, Encoding.UTF8, "application/json");

            string idToken;
            try
            {
                var res = await HttpClientPool.GetAutoDeserializedJsonAsync<SplaNetData.SplatoonToken>(request);
                idToken = res.result.webApiServerCredential.accessToken;
            }
            catch (HttpRequestException e)
            {
                await Logger.WriteLogAsync(String.Format("Failed to get \"id token\". {0}", e));
                return "";
            }
            catch (InvalidDataException e)
            {
                await Logger.WriteLogAsync(String.Format("Failed to decompress \"id token\". {0}", e));
                return "";
            }
            finally
            {
                request.Dispose();
            }

            #endregion access token 取得

            #region splatoon access token 取得
            var flapgApp = await CallFlapgAPI(idToken, Steps.Splatoon_Token);

            request = new HttpRequestMessage(HttpMethod.Post, "https://api-lp1.znc.srv.nintendo.net/v2/Game/GetWebServiceToken");

            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Connection", "Keep-Alive");
            request.Headers.Add("Accept-Encoding", "gzip");
            request.Headers.Add("Authorization", "Bearer " + idToken);
            request.Headers.Add("X-Platform", "Android");
            request.Headers.Add("User-Agent", "com.nintendo.znca/" + version + " (Android/7.1.2)");
            request.Headers.Add("Host", "api-lp1.znc.srv.nintendo.net");
            request.Headers.Add("X-ProductVersion", "2.1.1");

            if (IsBodyEmpty(idToken))
            {
                await Logger.WriteLogAsync("Body is null");
                return "";
            }

            var body4 = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "parameter",
                    new Dictionary<string, string>
                    {
                        {"id", "5741031244955648"},
                        {"f", flapgApp.f},
                        {"registrationToken", idToken},
                        {"timestamp", flapgApp.timestamp.ToString()},
                        {"requestId", flapgApp.request_id}
                    }
                },
            };

            if (IsBodyEmpty(body4))
            {
                await Logger.WriteLogAsync("Body is null");
                return "";
            }

            var json4 = Utf8Json.JsonSerializer.ToJsonString(body4);

            request.Content = new StringContent(json4, Encoding.UTF8, "application/json");

            string splatoonAccessToken;
            try
            {
                var res = await HttpClientPool.GetAutoDeserializedJsonAsync<SplaNetData.WebServiceToken>(request);
                splatoonAccessToken = res.result.accessToken;
            }
            catch (HttpRequestException e)
            {
                await Logger.WriteLogAsync(String.Format("Failed to get \"splatoon access token\". {0}", e));
                return "";
            }
            catch (InvalidDataException e)
            {
                await Logger.WriteLogAsync(String.Format("Failed to decompress \"session token\". {0}", e));
                return "";
            }
            finally
            {
                request.Dispose();
            }

            #endregion splatoon access token 取得

            #region iksm_session 取得
            request = new HttpRequestMessage(HttpMethod.Get, "https://app.splatoon2.nintendo.net/?lang=en-US");

            request.Headers.Add("X-IsAppAnalyticsOptedIn", "false");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.Add("Accept-Encoding", "gzip,deflate");
            request.Headers.Add("X-GameWebToken", splatoonAccessToken);
            request.Headers.Add("Accept-Language", "ja-JP");
            request.Headers.Add("X-IsAnalyticsOptedIn", "false");
            request.Headers.Add("Connection", "keep-alive");
            request.Headers.Add("DNT", "0");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Linux; Android 11; Pixel 5) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/94.0.4606.61 Mobile Safari/537.36");
            request.Headers.Add("Host", "app.splatoon2.nintendo.net");
            request.Headers.Add("X-Requested-With", "com.nintendo.znca");

            if (IsBodyEmpty(splatoonAccessToken))
            {
                await Logger.WriteLogAsync("Body is null");
                return "";
            }

            try
            {
                var cookies = await HttpClientPool.GetCookieContainer(request);
                var responseCookies = cookies.GetCookies(new Uri("https://app.splatoon2.nintendo.net/")).Cast<Cookie>();
                return responseCookies.First().Value;
            }
            catch (HttpRequestException e)
            {
                await Logger.WriteLogAsync(String.Format("Failed to get \"iksm session\". {0}", e));
                return "";
            }
            catch (IndexOutOfRangeException e)
            {
                await Logger.WriteLogAsync(String.Format("Failed to get \"cookie\". {0}", e));
                return "";
            }
            finally
            {
                request.Dispose();
            }

            #endregion iksm_session 取得
        }

        public enum Steps : int
        {
            Access_Token = 1,
            Splatoon_Token = 2
        }

        public static async Task<SplaNetData.FlapgResult> CallFlapgAPI(string idToken, Steps step)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, "https://api.imink.app/f"))
                {
                    request.Headers.Add("User-Agent", "splatnet2statink/1.8.2");  //1.8.2

                    var body = new Dictionary<string, string>()
                    {
                        { "token", idToken },
                        { "hashMethod", ((int)step).ToString() }
                    };

                    request.Content = new StringContent(Utf8Json.JsonSerializer.ToJsonString(body), Encoding.UTF8, "application/json");

                    return await HttpClientPool.GetAutoDeserializedJsonAsync<SplaNetData.FlapgResult>(request);
                }
            }
            catch (HttpRequestException e)
            {
                await Logger.WriteLogAsync(String.Format("Failed to get \"f\". {0}", e));
                return new SplaNetData.FlapgResult();
            }
        }

        private static bool IsBodyEmpty(Dictionary<string, string> dic)
        {
            return dic.Any(data => string.IsNullOrEmpty(data.Value));
        }

        private static bool IsBodyEmpty(Dictionary<string, Dictionary<string, string>> dic)
        {
            return dic.Any(data => Enumerable.Any<KeyValuePair<string, string>>(data.Value, data2 => string.IsNullOrEmpty(data2.Value)));
        }

        private static bool IsBodyEmpty(params string[] val)
        {
            return val.Any(data => string.IsNullOrEmpty(data));
        }
    }
}
