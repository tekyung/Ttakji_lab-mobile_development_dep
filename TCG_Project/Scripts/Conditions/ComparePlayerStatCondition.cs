using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Conditions
{
    // 플레이어의 특정 스탯(Mana, PrizePoints 등)을 비교하는 범용 조건기
    public class ComparePlayerStatCondition : ICardCondition
    {
        private string targetType; // "Self" or "Opponent"
        private string statName;   // "Mana", "PrizePoints" 등 비교할 대상 스탯
        private string op;         // "Greater", "Less", "Equal"...
        private object valueParam; // 비교할 값 (고정값 또는 수식 문자열)

        public void Initialize(Dictionary<string, object> parameters)
        {
            // 필수 파라미터 방어 코드 적용
            targetType = parameters.ContainsKey("target") ? parameters["target"].ToString() : "Self";
            op = parameters.ContainsKey("operator") ? parameters["operator"].ToString() : "Equal";

            // 어떤 스탯을 비교할지 결정 (기본값은 Mana로 설정하여 크래시 방지)
            statName = parameters.ContainsKey("stat") ? parameters["stat"].ToString() : "Mana";

            valueParam = parameters.ContainsKey("value") ? parameters["value"] : 0;
        }

        public bool IsMet(GameContext context)
        {
            // 1. 비교 대상(주체) 플레이어 가져오기
            List<Player> targets = TargetEvaluator.Evaluate(targetType, context);
            if (targets.Count == 0) return false;
            Player subject = targets[0];

            // 2. [핵심] 비교할 주체의 스탯 값 동적 추출
            int subjectStat = GetPlayerStat(subject, statName);

            // 3. 비교할 목표 값 계산 (고정값 OR 수식)
            int compareValue = FormulaEvaluator.Evaluate(valueParam, context);

            // 4. 비교 연산
            switch (op)
            {
                case "Greater": return subjectStat > compareValue;
                case "GreaterOrEqual": return subjectStat >= compareValue;
                case "Less": return subjectStat < compareValue;
                case "LessOrEqual": return subjectStat <= compareValue;
                case "Equal": return subjectStat == compareValue;
                case "NotEqual": return subjectStat != compareValue;
                default: return false;
            }
        }

        // 플레이어의 스탯을 안전하게 가져오는 헬퍼 메서드
        private int GetPlayerStat(Player player, string stat)
        {
            switch (stat)
            {
                case "Mana": return player.Mana;
                case "PrizePoints": return player.PrizePoints;
                case "DeckCount": return player.Deck.Count;
                case "HandCount": return player.Hand.Count;
                case "FieldCount":
                    int count = 0;
                    foreach (var card in player.Field)
                        if (card != null) count++;
                    return count;
                case "GraveyardCount": return player.Graveyard.Count;

                default:
                    EventManager.OnLogMessage?.Invoke($"[Warning] 알 수 없는 스탯 비교 요청: {stat}");
                    return 0;
            }
        }
    }
}