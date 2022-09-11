using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Windows;
using Takotsubo.utils;

namespace Takotsubo.Init
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        string authCode;

        public MainWindow() => InitializeComponent();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(Callback);
            var (authCodeVerifier,  url) = TokenUtil.GenerateLoginURL();
            authCode = authCodeVerifier;
            IksmSessionTextBox.Text = url;
        }

        private async void AuthorizeButton_Click(object sender, RoutedEventArgs e)
        {
            var text = IksmSessionTextBox.Text.Trim();
            if (Regex.IsMatch(text, "session_token_code=(.*)&"))
            {
                var sessionTokenCode = Regex.Match(text, "session_token_code=(.*)&").Groups[1].Value;
                var sessionToken = await TokenUtil.GetSessionToken(sessionTokenCode, authCode);
                var cookie = await TokenUtil.GetCookie(sessionToken);
                IksmSessionTextBox.Text = cookie;
                var userData = new UserData { IksmSession = cookie, SessionToken = sessionToken };
                SettingManager.SaveConfig(userData);
            }
        }

        public bool Callback(object sender, X509Certificate certificate, X509Chain chain, System.Net.Security.SslPolicyErrors errors) => true;


    }
}
