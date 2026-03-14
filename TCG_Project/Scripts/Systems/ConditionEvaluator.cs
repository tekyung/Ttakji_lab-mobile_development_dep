using System;
using System.Data;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Effects;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Systems
{
    /// <summary>
    /// JSON으로 정의된 문자열 기반 조건식(Condition Formula)을 파싱하고 평가하는 정적 헬퍼 클래스.
    /// 예: "activePlayer.Hand.Count >= 5" -> DataTable.Compute로 true/false 반환
    /// </summary>
    public static class ConditionEvaluator
    {
        /// <summary>
        /// 게임 전체/플레이어 상태를 기반으로 조건을 평가합니다.
        /// </summary>
        public static bool Evaluate(string conditionFormula, GameContext context)
        {
            // 조건이 비어있거나 "true"로 명시된 경우 무조건 통과
            if (string.IsNullOrWhiteSpace(conditionFormula) || conditionFormula.Trim().ToLower() == "true")
                return true;

            // [규칙] 복잡한 수식이 아닌 예약어(Keyword) 단축 문자열 처리
            switch (conditionFormula)
            {
                case "DeckNotEmpty":
                    return context.ActivePlayer.Deck.Count > 0;

                case "HandFull":
                    return context.ActivePlayer.Hand.Count >= GameRules.MaxHandSize;
            }

            // 1. 컨텍스트 변수 치환 (게임 상태를 수식에 반영)
            string parsedFormula = ReplaceVariables(conditionFormula, context);

            // 2. C# 논리 연산자를 DataTable 호환 SQL 문법으로 변경
            parsedFormula = parsedFormula.Replace("&&", " AND ");
            parsedFormula = parsedFormula.Replace("||", " OR ");
            parsedFormula = parsedFormula.Replace("==", "=");  // DataTable은 일치 비교에 '='를 사용
            parsedFormula = parsedFormula.Replace("!=", "<>"); // 불일치는 '<>' 사용

            try
            {
                // 3. DataTable의 Compute 엔진을 빌려 수학/논리식 연산 수행
                DataTable table = new DataTable();
                var result = table.Compute(parsedFormula, "");

                if (result is bool boolResult) return boolResult;
                return false;
            }
            catch (Exception e)
            {
                // 파싱 실패 시 크래시를 막고 false 반환 (디버그 로그 출력)
                EventManager.OnLogMessage?.Invoke($"<color=red>[조건 파싱 오류] 수식: \"{conditionFormula}\" -> 변환: \"{parsedFormula}\"\n사유: {e.Message}</color>");
                return false;
            }
        }

        /// <summary>
        /// 예약어들을 현재 게임 상태(GameContext)의 실제 숫자로 치환합니다.
        /// </summary>
        private static string ReplaceVariables(string formula, GameContext context)
        {
            Player p = context.ActivePlayer;
            Player opp = context.GetOpponent(p);

            // [주의] Inclusive 속성: 현재 발동 중인 카드(PlayingCard)를 패의 개수에 포함시킬지 여부.
            // 카드를 낼 때 이미 패에서 빠져나갔지만, 조건 검사에서는 패에 있던 것으로 취급해야 할 때 사용합니다.
            int myHandInc = p.Hand.Count + (p.PlayingCard != null ? 1 : 0);
            formula = formula.Replace("activePlayer.Hand.CountInclusive", myHandInc.ToString());

            if (opp != null)
            {
                int oppHandInc = opp.Hand.Count + (opp.PlayingCard != null ? 1 : 0);
                formula = formula.Replace("opponent.Hand.CountInclusive", oppHandInc.ToString());
            }

            // [Turn] ★ 리팩토링: ConsoleRunner 하드코딩 제거, GameContext의 CurrentTurn 사용
            formula = formula.Replace("turnCount", context.CurrentTurn.ToString());

            // [Active Player] 시전자 상태
            formula = formula.Replace("activePlayer.Hand.Count", p.Hand.Count.ToString());
            formula = formula.Replace("activePlayer.Graveyard.Count", p.Graveyard.Count.ToString());
            formula = formula.Replace("activePlayer.Deck.Count", p.Deck.Count.ToString());
            formula = formula.Replace("activePlayer.ResourceZone.Count", p.ResourceZone.Count.ToString());

            // [Opponent] 상대방 상태
            if (opp != null)
            {
                formula = formula.Replace("opponent.Hand.Count", opp.Hand.Count.ToString());
                formula = formula.Replace("opponent.Graveyard.Count", opp.Graveyard.Count.ToString());
                formula = formula.Replace("opponent.Deck.Count", opp.Deck.Count.ToString());
                formula = formula.Replace("opponent.ResourceZone.Count", opp.ResourceZone.Count.ToString());
            }

            // [Rules] 게임 기본 룰
            formula = formula.Replace("maxHandSize", GameRules.MaxHandSize.ToString());
            formula = formula.Replace("drawPerTurn", GameRules.DrawPerTurn.ToString());

            return formula;
        }

        /// <summary>
        /// ★ 리팩토링: 레거시 Target 객체 대신 최신 Card 객체를 직접 받아 개별 카드 조건을 평가합니다.
        /// 예: "target.Cost >= 3 && target.HasEffect('Damage')"
        /// </summary>
        public static bool EvaluateCardCondition(Card targetCard, string condition, GameContext context)
        {
            if (string.IsNullOrWhiteSpace(condition) || condition.Trim().ToLower() == "true")
                return true;

            string parsed = condition;

            // 1. 카드 전용 속성 치환

            // ★ 핵심 수정: 전장 효과가 반영된 '실제 결제 코스트(Effective Cost)'를 가져옵니다!
            // 타겟 카드의 주인을 알아야 전장 할인을 적용하므로 context.ActivePlayer를 넘겨줍니다.
            int effectiveCost = GameLogicHelpers.GetEffectiveCost(targetCard, context.ActivePlayer);

            parsed = parsed.Replace("target.Cost", effectiveCost.ToString());           // 할인된 최종 코스트
            parsed = parsed.Replace("target.BaseCost", targetCard.Cost.ToString());     // 카드에 적힌 원래 코스트 (기획 확장용)

            parsed = parsed.Replace("target.Speed", ((int)targetCard.Speed).ToString());

            // [특수 검사] 타겟 카드가 특정 이펙트를 가지고 있는지 검사
            if (parsed.Contains("target.HasEffect"))
            {
                bool hasDamage = targetCard.Effects.Any(e => e is DamageEffect);
                // bool hasHeal = targetCard.Effects.Any(e => e is HealEffect);

                parsed = parsed.Replace("target.HasEffect('Damage')", hasDamage.ToString().ToLower());
                // parsed = parsed.Replace("target.HasEffect('Heal')", hasHeal.ToString().ToLower());
            }

            // 2. 일반 전역 변수 치환
            parsed = ReplaceVariables(parsed, context);

            // 3. 연산자 문법 전환
            parsed = parsed.Replace("&&", " AND ").Replace("||", " OR ").Replace("==", "=").Replace("!=", "<>");

            // 4. 평가 수행
            try
            {
                System.Data.DataTable table = new System.Data.DataTable();
                var result = table.Compute(parsed, "");
                if (result is bool b) return b;
                return false;
            }
            catch { return false; }
        }
        /*
        public static bool EvaluateCardCondition(Card targetCard, string condition, GameContext context)
        {
            if (string.IsNullOrWhiteSpace(condition) || condition.Trim().ToLower() == "true")
                return true;

            string parsed = condition;

            // 1. 카드 전용 속성 치환
            parsed = parsed.Replace("target.Cost", targetCard.Cost.ToString());
            parsed = parsed.Replace("target.Speed", ((int)targetCard.Speed).ToString()); // 스피드를 숫자로 치환

            // [특수 검사] 타겟 카드가 특정 이펙트를 가지고 있는지 검사
            // 예: "target.HasEffect('Damage')" -> 해당 문자열을 찾아 true/false 문자열로 치환
            if (parsed.Contains("target.HasEffect"))
            {
                // LINQ를 사용하여 최신 ICardEffect 리스트를 스캔
                bool hasDamage = targetCard.Effects.Any(e => e is DamageEffect);
                // bool hasHeal = targetCard.Effects.Any(e => e is HealEffect); // 필요시 추가

                parsed = parsed.Replace("target.HasEffect('Damage')", hasDamage.ToString().ToLower());
                // parsed = parsed.Replace("target.HasEffect('Heal')", hasHeal.ToString().ToLower());
            }

            // 2. 일반 전역 변수(턴수, 손패 등) 치환
            parsed = ReplaceVariables(parsed, context);

            // 3. 연산자 문법 전환
            parsed = parsed.Replace("&&", " AND ").Replace("||", " OR ").Replace("==", "=").Replace("!=", "<>");

            // 4. 평가 수행
            try
            {
                System.Data.DataTable table = new System.Data.DataTable();
                var result = table.Compute(parsed, "");
                if (result is bool b) return b;
                return false;
            }
            catch { return false; }
        }*/
    }
}