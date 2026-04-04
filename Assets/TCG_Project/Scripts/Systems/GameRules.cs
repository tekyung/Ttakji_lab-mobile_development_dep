using System;
using System.Collections.Generic;
// using System.IO; // ★ 유니티 빌드 시의 파일 입출력 문제로 제거
using Newtonsoft.Json;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Interfaces; // ★ 심부름꾼 인터페이스 참조

namespace TCG_Project.Scripts.Systems
{
    public static class GameRules
    {
        // 파싱된 데이터를 담을 딕셔너리
        private static Dictionary<string, int> _configDict = new Dictionary<string, int>(); // ★ 숫자형 값들을 저장하는 딕셔너리
        private static Dictionary<string, string> _stringConfigDict = new Dictionary<string, string>(); // ★ 문자열 값들을 저장하는 딕셔너리 (필요시 사용)

        // ★ 기존에 경로(string)를 받던 것을, 심부름꾼(IJsonLoader)을 받도록 개조
        public static void LoadRules(IJsonLoader loader, string fileName = "CommonConfig")
        {
            // 인터페이스를 통해 환경(Unity/Console)에 맞는 JSON 문자열을 가져옴
            string json = loader.LoadJson(fileName);

            if (string.IsNullOrEmpty(json))
            {
                EventManager.OnLogMessage?.Invoke($"[Error] 룰 파일을 찾을 수 없거나 내용이 비어있습니다: {fileName}");
                return;
            }

            var wrapper = JsonConvert.DeserializeObject<CommonConfigWrapper>(json);

            _configDict.Clear();
            _stringConfigDict.Clear(); // 문자열 딕셔너리도 초기화

            if (wrapper != null && wrapper.CommonConfig != null)
            {
                foreach (var item in wrapper.CommonConfig)
                {
                    // 1. 숫자로 변환 가능하면 int 딕셔너리에 저장
                    if (int.TryParse(item.Value, out int val))
                    {
                        _configDict[item.Name] = val;
                    }
                    // 2. 문자열 자체도 무조건 저장 (경로 등을 위해)
                    _stringConfigDict[item.Name] = item.Value;
                }
            }
            
            EventManager.OnLogMessage?.Invoke($"[System] 게임 룰 데이터 {_configDict.Count + _stringConfigDict.Count}개 로드 완료.");
        }

        // 안전하게 값을 꺼내오는 내부 헬퍼 (값이 없으면 기본값 반환)
        private static int GetValue(string key, int defaultValue = 0)
        {
            return _configDict.TryGetValue(key, out int val) ? val : defaultValue;
        }
        private static string GetStringValue(string key, string defaultValue = "")
        {
            return _stringConfigDict.TryGetValue(key, out string val) ? val : defaultValue;
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
        public static int BotSingleGame => GetValue("Bot_single_game", 1);
        public static int WaitTime => GetValue("choose_wait_time", 10000);
        public static string DefaultCardBackPath => GetStringValue("default_card_back", "GameDesign/card_image/Back_Common.png");
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