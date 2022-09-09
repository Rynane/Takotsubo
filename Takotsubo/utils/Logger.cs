using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Takotsubo.utils
{
    public static class Logger
    {
        private static readonly string filePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/Takotsubo/";
        private static FileStream sourceStream;

        public static async Task WriteLogAsync(string text)
        {
            if (!Directory.Exists(filePath))
            {
                Directory.CreateDirectory(filePath);
            }

            sourceStream = sourceStream ?? new FileStream($"{filePath}log.log", FileMode.Append, FileAccess.Write, FileShare.Write, 4096, true);

            var encodedText = Encoding.UTF8.GetBytes($"[{DateTime.Now}] {text} \n");
            await sourceStream.WriteAsync(encodedText, 0, encodedText.Length);
        }
    }
}
