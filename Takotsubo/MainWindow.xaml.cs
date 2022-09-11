using System.Windows;
using Takotsubo.utils;

namespace Takotsubo
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow() => InitializeComponent();

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var userData = SettingManager.LoadConfig();
            if (string.IsNullOrEmpty(userData.IksmSession))
            {
                return;
            }

            AppStaticStatus.WebServiceToken = userData.IksmSession;
            AppStaticStatus.BulletToken = await BulletTokenUtil.GetBulletToken(userData.IksmSession);
            StatusLabel.Content = "Done";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            TokenInfo tokenInfo = new TokenInfo();
            tokenInfo.Show();
        }

    }
}
