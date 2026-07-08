using UnityEngine;
using TCG_Project.Scripts.Interfaces;

namespace TCG_Project.Scripts.Systems
{
    public class UnityResourceLoader : IJsonLoader
    {
        private string _basePath; // 예: "GameData" (Resources 폴더 하위 경로)

        public UnityResourceLoader(string basePath)
        {
            _basePath = basePath;
        }

        public string LoadJson(string fileName)
        {
            // fileName에 확장자가 있다면 제거 (예: "RulebookCards.json" -> "RulebookCards")
            string nameWithoutExtension = fileName.Replace(".json", "");
            string fullPath = string.IsNullOrEmpty(_basePath) ? nameWithoutExtension : $"{_basePath}/{nameWithoutExtension}";

            TextAsset asset = Resources.Load<TextAsset>(fullPath);
            if (asset == null)
            {
                Debug.LogError($"[UnityResourceLoader] 리소스를 찾을 수 없습니다: {fullPath}");
                return string.Empty;
            }
            return asset.text;
        }
    }
}