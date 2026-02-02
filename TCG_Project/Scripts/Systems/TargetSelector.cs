using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Systems
{
    public static class TargetSelector
    {
        public static List<Target> Select(object targetParam, GameContext context)
        {
            var results = new List<Target>();

            // 1. 문자열 (단축형) 처리
            if (targetParam is string strParam)
            {
                var players = TargetEvaluator.Evaluate(strParam, context);
                foreach (var p in players) results.Add(new Target(p));
                return results;
            }

            // 2. 객체 (상세 설정) 처리
            if (targetParam is JObject obj)
            {
                // A. 수집 (Collection)
                var candidates = CollectCandidates(obj, context);

                // B. 필터링 (Filter)
                if (obj["filter"] != null)
                {
                    string filter = obj["filter"].ToString();
                    candidates = candidates.Where(t => ConditionEvaluator.EvaluateTarget(t, filter, context)).ToList();
                }

                // C. 선택 (Selection) - 모드별 분기
                int count = 1;
                if (obj["count"] != null) count = FormulaEvaluator.Evaluate(obj["count"], context);

                string mode = obj["mode"]?.ToString() ?? "Random";

                results = SelectFinalTargets(candidates, count, mode, context);
            }

            return results;
        }

        private static List<Target> CollectCandidates(JObject info, GameContext context)
        {
            var list = new List<Target>();

            string ctrlStr = info["controller"]?.ToString() ?? "Self";
            var controllers = TargetEvaluator.Evaluate(ctrlStr, context);

            var zones = info["zones"]?.ToObject<List<string>>() ?? new List<string>();

            foreach (Player p in controllers)
            {
                if (zones.Contains("Player")) list.Add(new Target(p));
                if (zones.Contains("Hand")) foreach (var c in p.Hand) list.Add(new Target(c));
                if (zones.Contains("Field")) foreach (var c in p.Field) if (c != null) list.Add(new Target(c));
                if (zones.Contains("Deck")) foreach (var c in p.Deck) list.Add(new Target(c));
            }
            return list;
        }

        private static List<Target> SelectFinalTargets(List<Target> candidates, int count, string mode, GameContext context)
        {
            if (candidates.Count == 0) return new List<Target>();

            switch (mode)
            {
                case "All":
                    // 개수 제한 없이 전부 반환
                    return candidates;

                case "Random":
                    // 셔플 후 N개
                    var rnd = new Random();
                    return candidates.OrderBy(x => rnd.Next()).Take(count).ToList();

                case "Manual":
                    // [직접 선택]
                    // 봇인 경우(현재 로직상 구분 어려우면 임시로 Random 처리)
                    // 여기서는 ActivePlayer가 대상 선택권을 가진다고 가정
                    if (context.ActivePlayer.Name.Contains("Bot") || context.ActivePlayer.Name == "Player 2")
                    {
                        // AI는 그냥 랜덤/앞에서부터 선택 (AI 로직 추후 고도화 필요)
                        return candidates.Take(count).ToList();
                    }
                    else
                    {
                        // 사람은 콘솔 입력으로 선택
                        return ManualSelectConsole(candidates, count);
                    }

                default:
                    return candidates.Take(count).ToList();
            }
        }

        // 콘솔 UI: 유저가 번호를 입력해 선택
        private static List<Target> ManualSelectConsole(List<Target> candidates, int count)
        {
            var selected = new List<Target>();
            Console.WriteLine($"\n[Target Selection] 대상을 {count}개 선택하세요:");

            while (selected.Count < count && candidates.Count > 0)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    Console.WriteLine($"   {i + 1}. {candidates[i].Name} (Type: {candidates[i].Type})");
                }

                Console.Write($">> 선택 ({selected.Count + 1}/{count}): ");
                string input = Console.ReadLine();

                if (int.TryParse(input, out int index) && index >= 1 && index <= candidates.Count)
                {
                    var choice = candidates[index - 1];
                    selected.Add(choice);
                    Console.WriteLine($"   -> '{choice.Name}' 선택됨.");

                    // 중복 선택 방지 (선택된 건 후보에서 제거)
                    candidates.RemoveAt(index - 1);
                }
                else
                {
                    Console.WriteLine("   [!] 잘못된 입력입니다.");
                }
            }
            return selected;
        }
    }
}