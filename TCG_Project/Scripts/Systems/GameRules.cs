using System;
using System.IO;
using Newtonsoft.Json;

namespace TCG_Project.Scripts.Systems
{
    public class GameRulesData
    {
        public int StartingHealth { get; set; }
        public int StartingMana { get; set; }
        public int MaxMana { get; set; }
        public int ManaGainPerTurn { get; set; }
        public int StartingDrawCount { get; set; }
        public int DrawPerTurn { get; set; }
        public int MaxHandSize { get; set; }
    }

    public static class GameRules
    {
        private static GameRulesData currentRules;

        // 게임 시작 시 한 번만 호출해서 로드
        public static void LoadRules(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"Rules.json을 찾을 수 없습니다: {jsonPath}");
            }

            string json = File.ReadAllText(jsonPath);
            currentRules = JsonConvert.DeserializeObject<GameRulesData>(json);
            Console.WriteLine("[System] 게임 룰 데이터 로드 완료.");
        }

        // 어디서든 접근 가능한 프로퍼티들
        public static int StartingHealth => currentRules.StartingHealth;
        public static int StartingMana => currentRules.StartingMana;
        public static int MaxMana => currentRules.MaxMana;
        public static int ManaGainPerTurn => currentRules.ManaGainPerTurn;
        public static int StartingDrawCount => currentRules.StartingDrawCount;
        public static int DrawPerTurn => currentRules.DrawPerTurn;
        public static int MaxHandSize => currentRules.MaxHandSize;
    }
}