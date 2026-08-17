using System;
using System.IO;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Utils
{
    public class ConsoleFileLoader : IJsonLoader
    {
        private string _baseDirectory;

        public ConsoleFileLoader(string baseDirectory)
        {
            _baseDirectory = baseDirectory;
        }

        public string LoadJson(string fileName)
        {
            // 콘솔 환경에서는 확장자가 필요하므로 없으면 붙여줌
            string nameWithExtension = fileName.EndsWith(".json") ? fileName : fileName + ".json";
            string fullPath = Path.Combine(_baseDirectory, nameWithExtension);

            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[ConsoleFileLoader] 파일을 찾을 수 없습니다: {fullPath}");
                return string.Empty;
            }
            return File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
        }
    }
}