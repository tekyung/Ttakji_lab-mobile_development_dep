using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TCG_Project.Scripts.Managers; // ★ EventManager를 쓰기 위해 추가

namespace TCG_Project.Scripts.Systems
{
    public static class GameRules
    {
        // 파싱된 데이터를 담을 딕셔너리
        private static Dictionary<string, int> _configDict = new Dictionary<string, int>();

        public static void LoadRules(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                EventManager.OnLogMessage?.Invoke($"[Error] 룰 파일을 찾을 수 없습니다: {jsonPath}");
                return;
            }

            string json = File.ReadAllText(jsonPath);
            var wrapper = JsonConvert.DeserializeObject<CommonConfigWrapper>(json);

            _configDict.Clear();
            if (wrapper != null && wrapper.CommonConfig != null)
            {
                foreach (var item in wrapper.CommonConfig)
                {
                    // Value가 문자열이므로 int로 변환해서 저장
                    if (int.TryParse(item.Value, out int val))
                    {
                        _configDict[item.Name] = val;
                    }
                }
            }
            EventManager.OnLogMessage?.Invoke($"[System] 게임 룰 데이터 {_configDict.Count}개 로드 완료.");
        }

        // 안전하게 값을 꺼내오는 내부 헬퍼 (값이 없으면 기본값 반환)
        private static int GetValue(string key, int defaultValue = 0)
        {
            return _configDict.TryGetValue(key, out int val) ? val : defaultValue;
        }

        // === 어디서든 접근 가능한 프로퍼티들 ===
        public static int MaxHandSize => GetValue("max_hand_size", 20);

        public static int MinDeckCardCount => GetValue("min_deck_card_count", 20);
        public static int MaxDeckCardCount => GetValue("max_deck_card_count", 20);

        public static int RoomSessionTime => GetValue("room_session_time", 120000);
        public static int ChooseWaitTime => GetValue("choose_wait_time", 20000);
        public static int StartingHands => GetValue("starting_hands", 5);
        public static int DrawPerTurn => GetValue("draw_per_turn", 1);

        // --- 룰북 신규 룰 ---
        public static int LifeTokens => GetValue("life_tokens", 5);
        public static int ResourceDeckCount => GetValue("resource_deck_count", 15);
        public static float BotDelayTime => GetValue("Bot_delay_time", 500) / 1000f;
    }
    
    // JSON 구조에 맞춘 래퍼 클래스
    [Serializable]
    public class ConfigItem
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    [Serializable]
    public class CommonConfigWrapper
    {
        public List<ConfigItem> CommonConfig { get; set; }
    }
}