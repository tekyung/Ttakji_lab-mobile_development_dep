using System;
using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Systems
{
    public static class TargetSelector
    {
        public static List<Target> Select(object targetParam, GameContext context)
        {
            List<Target> results = new List<Target>();

            // 1. 문자열 (단축형) 처리
            if (targetParam is string strParam)
            {
                if (strParam == "ActivePlayer" || strParam == "Self") results.Add(new Target(context.ActivePlayer));
                else if (strParam == "Opponent") results.Add(new Target(context.TargetPlayer));
                return results;
            }

            // 2. 딕셔너리 (상세 설정) 처리 - [수정] JObject가 아니라 Dictionary로 받아야 함
            if (targetParam is Dictionary<string, object> options)
            {
                // A. 컨트롤러(주체) 확인
                Player targetPlayer = context.ActivePlayer;
                if (options.ContainsKey("controller") && options["controller"].ToString() == "Opponent")
                    targetPlayer = context.TargetPlayer;

                // B. 존(Zone) 탐색 및 후보 수집
                if (options.ContainsKey("zones"))
                {
                    var zoneObj = options["zones"];
                    string[] zones = zoneObj is string[] arr ? arr : ((IEnumerable<object>)zoneObj).Select(o => o.ToString()).ToArray();

                    foreach (string zoneName in zones)
                    {
                        if (Enum.TryParse(zoneName, out ZoneType zone))
                        {
                            var cardsInZone = targetPlayer.GetZone(zone);
                            foreach (var card in cardsInZone)
                            {
                                if (card == null) continue;

                                // [핵심] 조건 필터링 (파이어볼: 공격력 300 이하 등)
                                if (CheckCondition(card, options))
                                {
                                    results.Add(new Target(card));
                                }
                            }
                        }
                    }
                }
                else
                {
                    // 존 지정이 없으면 플레이어 자체가 타겟 (예: 마나 물약)
                    results.Add(new Target(targetPlayer));
                }

                // C. 모드별 최종 선택 (Random, Manual, Top)
                string mode = options.ContainsKey("mode") ? options["mode"].ToString() : "Random";
                int count = options.ContainsKey("count") ? Convert.ToInt32(options["count"]) : 1;

                return SelectFinalTargets(results, count, mode, context);
            }

            return results;
        }

        // [신규] 조건 체크 로직 (파이어볼, 학습 등이 작동하려면 필수)
        private static bool CheckCondition(Card card, Dictionary<string, object> options)
        {
            // 조건이 없으면 통과
            if (!options.ContainsKey("condition")) return true;

            string condType = options["condition"].ToString();
            int condVal = options.ContainsKey("conditionValue") ? Convert.ToInt32(options["conditionValue"]) : 0;

            switch (condType)
            {
                case "PowerUnderOrEqual": // 파이어볼용
                    return card.Power <= condVal;

                case "DeckHighOrEqual": // 학습용 (덱 장수 체크)
                    // 카드가 속한 덱의 장수를 체크
                    return card.Controller.Deck.Count >= condVal;

                default: return true;
            }
        }

        private static List<Target> SelectFinalTargets(List<Target> candidates, int count, string mode, GameContext context)
        {
            if (candidates.Count == 0) return new List<Target>();

            // 봇 시뮬레이션을 위해 Random 모드 우선 처리
            if (mode == "Random")
            {
                var rnd = new Random();
                return candidates.OrderBy(x => rnd.Next()).Take(count).ToList();
            }
            else if (mode == "Top")
            {
                return candidates.Take(count).ToList();
            }
            else // Manual
            {
                // 봇이거나 시뮬레이터 환경이면 Random처럼 동작하게 처리
                // (실제 유니티 등 UI 환경에서는 여기서 입력 대기 로직 필요)
                var rnd = new Random();
                return candidates.OrderBy(x => rnd.Next()).Take(count).ToList();
            }
        }
    }
}