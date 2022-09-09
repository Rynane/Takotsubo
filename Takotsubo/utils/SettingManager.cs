using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Xml.Serialization;

namespace Takotsubo.utils
{
    public class UserData
    {
        public string UserName, SessionToken, IksmSession, Principal_ID, Version;
    }

    public class SettingManager
    {
        private static readonly string filePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/Takotsubo/";

        public static void SaveConfig(UserData data)
        {
            var xmlSerializer = new XmlSerializer(typeof(UserData));

            try
            {
                if (!Directory.Exists(filePath))
                {
                    Directory.CreateDirectory(filePath);
                }
            }
            catch (Exception)
            {
                MessageBox.Show("dataフォルダを作成することが出来ませんでした。");
            }

            var sw = new StreamWriter($"{filePath}config.xml", false, new UTF8Encoding(false));
            xmlSerializer.Serialize(sw, data);
            sw.Close();
        }

        public static UserData LoadConfig()
        {
            if (!File.Exists($"{filePath}config.xml")) return new UserData();

            var serializer = new XmlSerializer(typeof(UserData));
            var sr = new StreamReader($"{filePath}config.xml", new UTF8Encoding(false));
            UserData data = new UserData();
            try
            {
                data = (UserData)serializer.Deserialize(sr);
            }
            catch (System.Windows.Markup.XamlParseException)
            {
                MessageBox.Show("config.xmlの形式が正しくありません。", "無効なconfig.xml", MessageBoxButton.OK);
            }
            finally
            {
                sr.Close();
            }

            return data;
        }
    }
}
