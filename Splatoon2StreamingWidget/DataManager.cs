﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Xml.Serialization;

namespace Splatoon2StreamingWidget
{
    public class UserData
    {
        public string user_name { get; set; }
        public string iksm_session { get; set; }
        public string principal_id { get; set; }
    }

    static class DataManager
    {
        private const string filePath = "data/config.xml";

        internal static void SaveConfig(UserData data)
        {
            var xmlSerializer = new XmlSerializer(typeof(UserData));

            if (!Directory.Exists("data"))
                Directory.CreateDirectory("data");
            var sw = new StreamWriter(filePath, false, new UTF8Encoding(false));
            xmlSerializer.Serialize(sw, data);
            sw.Close();
        }

        internal static UserData LoadConfig()
        {
            if (!File.Exists(filePath)) return new UserData();

            var serializer = new XmlSerializer(typeof(UserData));
            var sr = new StreamReader(filePath, new UTF8Encoding(false));
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
