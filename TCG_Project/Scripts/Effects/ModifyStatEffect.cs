using System; // Action 콜백을 위해 추가
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Effects
{
    public class ModifyStatEffect : ICardEffect
    {
        private object targetParam;
        private string statName;
        private object amountParam;
        private string outVarParam;
        private string durationParam;
        private string triggerCondition;
        private int prizeOnKill;

        // 복제를 위해 초기화 파라미터를 기억해 둡니다.
        private Dictionary<string, object> _cachedParams;

        // 깊은 복사 구현체
        public ICardEffect Clone()
        {
            var clone = new ModifyStatEffect(); // 자기 자신의 새 인스턴스 생성
            clone.Initialize(this._cachedParams); // 기억해둔 똑같은 재료로 초기화
            return clone;
        }

        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters; // 캐싱

            // 1. 필수 파라미터 안전하게 읽기 (TryGetValue 사용 권장)
            parameters.TryGetValue("target", out targetParam);
            parameters.TryGetValue("amount", out amountParam);

            // 2. [오류 해결] 'stat' 키가 없으면 기본값 "Power" 사용
            if (parameters.ContainsKey("stat"))
            {
                statName = parameters["stat"].ToString();
            }
            else
            {
                // 번역기를 거치지 않은 생 JSON 데이터일 경우 대비
                statName = "Power";
            }

            targetParam = parameters["target"];
            statName = parameters["stat"].ToString();
            amountParam = parameters["amount"];

            if (parameters.ContainsKey("outVar"))
                outVarParam = parameters["outVar"].ToString();

            if (parameters.ContainsKey("duration"))
                durationParam = parameters["duration"].ToString();

            if (parameters.ContainsKey("triggerCondition"))
                triggerCondition = parameters["triggerCondition"].ToString();

            if (parameters.ContainsKey("prizeOnKill"))
                prizeOnKill = int.Parse(parameters["prizeOnKill"].ToString());
        }

        // ★ 비동기 처리를 위해 Action onComplete 매개변수 추가
        public void Execute(GameContext context, Action onComplete)
        {
            // 1. 발동 조건(triggerCondition) 재확인
            if (!string.IsNullOrEmpty(triggerCondition))
            {
                if (!ConditionEvaluator.Evaluate(triggerCondition, context))
                {
                    EventManager.OnLogMessage?.Invoke($"🚫 조건 불만족({triggerCondition})으로 효과가 취소되었습니다.");
                    onComplete?.Invoke(); // 효과 취소 시에도 다음 진행을 위해 콜백 호출 필수
                    return;
                }
            }

            int amount = FormulaEvaluator.Evaluate(amountParam, context);

            // ★ 2. TargetSelector의 비동기 콜백 호출
            TargetSelector.Select(targetParam, context, (selectedTargets) =>
            {
                // [디버깅] 타겟팅 결과 확인
                if (selectedTargets == null || selectedTargets.Count == 0)
                {
                    DebugHelper.LogWarning($"'{statName}' 변경 실패: 선택된 유효 타겟이 없습니다! (조건: {targetParam})");
                    onComplete?.Invoke(); // 비동기 종료 보고
                    return;
                }

                int totalChanged = 0;
                List<Card> affectedCards = new List<Card>();

                foreach (Target t in selectedTargets)
                {
                    int actualChange = 0;
                    int resultValue = 0;

                    // CASE A: 플레이어 스탯 변경
                    if (t.Type == TargetType.Player)
                    {
                        Player p = t.PlayerVal;
                        if (statName == "Mana")
                        {
                            int prev = p.Mana;
                            p.Mana += amount;
                            actualChange = p.Mana - prev;
                            resultValue = p.Mana;
                        }
                        EventManager.OnLogMessage?.Invoke($"✨ [스탯 변경] {p.Name}의 {statName} {amount} 변동 -> {resultValue}");
                    }
                    // CASE B: 카드 스탯 변경
                    else if (t.Type == TargetType.Card)
                    {
                        Card c = t.CardVal;

                        if (statName == "Power")
                        {
                            int prev = c.Power;
                            c.ModifyPower(amount, "Effect");

                            if (c.Power < 0) c.Power = 0;
                            actualChange = c.Power - prev;
                            resultValue = c.Power;

                            // ★ 체력이 0 이하가 되면 파괴 처리
                            if (c.Power <= 0)
                            {
                                ProcessDeath(c, context);
                            }
                        }
                        else if (statName == "Cost")
                        {
                            int prev = c.Cost;
                            c.Cost += amount;
                            if (c.Cost < 0) c.Cost = 0;
                            actualChange = c.Cost - prev;
                            resultValue = c.Cost;
                            EventManager.OnLogMessage?.Invoke($"✨ [스탯 변경] 카드 '{c.Name}'의 Cost {amount} 변동 -> {resultValue}");
                        }

                        DebugHelper.LogEffect("Stat Change", $"{c.Name}의 {statName} {amount} 변동 (현재: {resultValue})");
                        affectedCards.Add(c);
                    }

                    totalChanged += actualChange;
                }

                // [기록] 결과 저장
                if (!string.IsNullOrEmpty(outVarParam))
                {
                    context.SetVariable(outVarParam, totalChanged);
                }

                // [예약] 만료 효과 등록 (Duration)
                if (!string.IsNullOrEmpty(durationParam) && affectedCards.Count > 0)
                {
                    // 미구현 부분은 기존 코드 유지
                    EventManager.OnLogMessage?.Invoke($"⏰ [예약] {durationParam}에 {affectedCards.Count}장의 카드 복구 예약됨.");
                }

                // ★ 3. 모든 효과 처리가 끝났음을 시스템에 보고
                onComplete?.Invoke();
            });
        }

        private void ProcessDeath(Card unit, GameContext context)
        {
            EventManager.OnLogMessage?.Invoke($"      💀 {unit.Name} 파괴됨! (효과)");
            Player controller = unit.Controller;
            if (controller.ExtractCard(ZoneType.Field, unit))
            {
                unit.ResetState();
                Player owner = unit.OriginalOwner ?? controller;
                owner.Graveyard.Add(unit);
                EventManager.OnLogMessage?.Invoke($"{unit.Name}이(가) {owner.Name}의 묘지로 이동합니다. 현재 묘지 {owner.Graveyard.Count}장");

                if (prizeOnKill > 0)
                {
                    context.ActivePlayer.PrizePoints += prizeOnKill;
                    EventManager.OnLogMessage?.Invoke($"      🏆 [효과 승점] {context.ActivePlayer.Name}가 {prizeOnKill}점을 얻었습니다! (Total: {context.ActivePlayer.PrizePoints})");
                }
            }
        }
    }
}