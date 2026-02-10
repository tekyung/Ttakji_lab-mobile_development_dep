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

            // 1. 문자열 (ActivePlayer 등)
            if (targetParam is string strParam)
            {
                if (strParam == "ActivePlayer" || strParam == "Self") results.Add(new Target(context.ActivePlayer));
                else if (strParam == "Opponent") results.Add(new Target(context.TargetPlayer));
                return results;
            }

            // 2. 딕셔너리 (상세 설정) - ★ JObject가 아니라 Dictionary여야 함 ★
            if (targetParam is Dictionary<string, object> options)
            {
                Player targetPlayer = context.ActivePlayer;
                if (options.ContainsKey("controller") && options["controller"].ToString() == "Opponent")
                    targetPlayer = context.TargetPlayer;

                if (options.ContainsKey("zones"))
                {
                    var zoneObj = options["zones"];
                    string[] zones = zoneObj is string[] arr ? arr : ((IEnumerable<object>)zoneObj).Select(o => o.ToString()).ToArray();

                    foreach (string zoneName in zones)
                    {
                        if (Enum.TryParse(zoneName, out ZoneType zone))
                        {
                            // Player.GetZone 메서드가 필요함 (없으면 Player.cs에 추가)
                            var cardsInZone = targetPlayer.GetZone(zone);
                            foreach (var card in cardsInZone)
                            {
                                if (card == null) continue;

                                // ★ 조건 체크 (이게 없으면 파이어볼이 작동 안 함)
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
                    results.Add(new Target(targetPlayer));
                }

                string mode = options.ContainsKey("mode") ? options["mode"].ToString() : "Random";
                int count = options.ContainsKey("count") ? Convert.ToInt32(options["count"]) : 1;
                return SelectFinalTargets(results, count, mode, context);
            }

            return results;
        }

        private static bool CheckCondition(Card card, Dictionary<string, object> options)
        {
            if (!options.ContainsKey("condition")) return true;

            string condType = options["condition"].ToString();
            int condVal = options.ContainsKey("conditionValue") ? Convert.ToInt32(options["conditionValue"]) : 0;

            switch (condType)
            {
                case "PowerUnderOrEqual": return card.Power <= condVal;
                case "DeckHighOrEqual": return card.Controller.Deck.Count >= condVal;
                default: return true;
            }
        }

        private static List<Target> SelectFinalTargets(List<Target> candidates, int count, string mode, GameContext context)
        {
            if (candidates.Count == 0) return new List<Target>();

            // [신규] Power가 높은 순서대로 선택
            if (mode == "HighestPower")
            {
                return candidates
                    .OrderByDescending(t => t.Type == TargetType.Card ? t.CardVal.Power : 0)
                    .Take(count)
                    .ToList();
            }
            // 기존 Random 모드
            else if (mode == "Random")
            {
                var rnd = new Random();
                return candidates.OrderBy(x => rnd.Next()).Take(count).ToList();
            }
            // Top 모드
            else
            {
                return candidates.Take(count).ToList();
            }
        }
    }
}