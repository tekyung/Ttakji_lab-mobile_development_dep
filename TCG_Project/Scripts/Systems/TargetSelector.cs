using System;
using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Systems
{
    public static class TargetSelector
    {
        public static void Select(object targetParam, GameContext context, Action<List<Target>> onTargetSelected)
        {
            List<Target> results = new List<Target>();

            // 1. 단순 문자열 처리 (ActivePlayer, Opponent 등)
            if (targetParam is string strParam)
            {
                if (strParam == "ActivePlayer" || strParam == "Self") results.Add(new Target(context.ActivePlayer));
                else if (strParam == "Opponent") results.Add(new Target(context.TargetPlayer));
                onTargetSelected?.Invoke(results);
                return;
            }

            // 2. 딕셔너리 기반 상세 타겟팅
            if (targetParam is Dictionary<string, object> options)
            {
                Player targetPlayer = (options.ContainsKey("controller") && options["controller"].ToString() == "Opponent")
                                      ? context.TargetPlayer : context.ActivePlayer;

                List<Target> candidates = new List<Target>();

                // 구역(Zones) 탐색
                if (options.ContainsKey("zones"))
                {
                    var zones = options["zones"] as IEnumerable<object>; // 다양한 타입 대응
                    foreach (var zName in zones)
                    {
                        if (Enum.TryParse(zName.ToString(), out ZoneType zone))
                        {
                            var cards = targetPlayer.GetZone(zone);
                            candidates.AddRange(cards.Where(c => c != null && CheckCondition(c, options)).Select(c => new Target(c)));
                        }
                    }
                }
                else { candidates.Add(new Target(targetPlayer)); }

                string mode = options.ContainsKey("mode") ? options["mode"].ToString() : "Random";
                int count = options.ContainsKey("count") ? Convert.ToInt32(options["count"]) : 1;

                // ★ HumanChoice 분기 처리
                if (mode == "HumanChoice")
                {
                    if (candidates.Count == 0) onTargetSelected?.Invoke(new List<Target>());
                    else context.ActivePlayer.Brain.SelectTarget(candidates, count, onTargetSelected);
                }
                else
                {
                    var finalTargets = SelectFinalTargets(candidates, count, mode, context);
                    onTargetSelected?.Invoke(finalTargets);
                }
            }
            else { onTargetSelected?.Invoke(results); }
        }

        /* ★ 반환형이 List<Target>에서 void로 바뀌고, 콜백(onTargetSelected)이 추가되었습니다.
        public static void Select(object targetParam, GameContext context, Action<List<Target>> onTargetSelected)
        {
            List<Target> results = new List<Target>();

            // 1. 문자열 (자동 타겟팅 - 예: ActivePlayer, Self)
            if (targetParam is string strParam)
            {
                if (strParam == "ActivePlayer" || strParam == "Self") results.Add(new Target(context.ActivePlayer));
                else if (strParam == "Opponent") results.Add(new Target(context.TargetPlayer));

                onTargetSelected?.Invoke(results); // 즉시 콜백 반환
                return;
            }

            // 2. 딕셔너리 (상세 조건 검색)
            if (targetParam is Dictionary<string, object> options)
            {
                Player targetPlayer = context.ActivePlayer;
                if (options.ContainsKey("controller") && options["controller"].ToString() == "Opponent")
                    targetPlayer = context.TargetPlayer;

                List<Target> candidates = new List<Target>(); // 후보군 수집

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
                                if (CheckCondition(card, options))
                                {
                                    candidates.Add(new Target(card)); // 조건에 맞는 녀석들을 후보군에 넣음
                                }
                            }
                        }
                    }
                }
                else
                {
                    candidates.Add(new Target(targetPlayer));
                }

                int count = options.ContainsKey("count") ? Convert.ToInt32(options["count"]) : 1;
                string mode = options.ContainsKey("mode") ? options["mode"].ToString() : "Random";

                // ★ [핵심] 모드가 HumanChoice 라면? -> 플레이어의 '두뇌(Brain)'에게 위임한다!
                if (mode == "HumanChoice")
                {
                    if (candidates.Count == 0)
                    {
                        // 칠 후보가 아예 없으면 그냥 빈 채로 넘김
                        onTargetSelected?.Invoke(new List<Target>());
                    }
                    else
                    {
                        // ActivePlayer(스펠을 쓴 사람)의 뇌에게 "이 후보들 중 골라줘!" 라고 넘긴다.
                        // (만약 사람이면 UI가 열릴 것이고, 봇이면 즉시 연산해서 돌려줄 것임)
                        context.ActivePlayer.Brain.SelectTarget(candidates, count, onTargetSelected);
                    }
                }
                else
                {
                    // 봇의 자동 연산 모드 (HighestPower, Random 등)
                    var finalTargets = SelectFinalTargets(candidates, count, mode, context);
                    onTargetSelected?.Invoke(finalTargets); // 연산 후 즉시 반환
                }
            }
            else
            {
                onTargetSelected?.Invoke(results);
            }
        }
        */

        private static bool CheckCondition(Card card, Dictionary<string, object> options)
        {
            if (!options.ContainsKey("condition")) return true;

            string condType = options["condition"].ToString();
            int condVal = options.ContainsKey("conditionValue") ? Convert.ToInt32(options["conditionValue"]) : 0;

            switch (condType)
            {
                case "PowerUnderOrEqual": return card.Power <= condVal;
                case "DeckMoreOrEqual": return card.Controller.Deck.Count >= condVal;
                default: return true;
            }
        }

        private static List<Target> SelectFinalTargets(List<Target> candidates, int count, string mode, GameContext context)
        {
            if (candidates.Count == 0) return new List<Target>();

            // Power가 높은 순서대로 선택
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