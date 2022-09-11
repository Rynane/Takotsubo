using System.Windows;

namespace Takotsubo.utils
{
    /// <summary>
    /// TokenInfo.xaml の相互作用ロジック
    /// </summary>
    public partial class TokenInfo : Window
    {
        //本来Bindingで実装するべきかも

        public TokenInfo()
        {
            InitializeComponent();
            ServiceTokenLabel.Text = AppStaticStatus.WebServiceToken;
            BulletTokenLabel.Text=AppStaticStatus.BulletToken;
        }
    }
}
