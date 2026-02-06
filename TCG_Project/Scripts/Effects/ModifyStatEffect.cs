using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
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

        public void Initialize(Dictionary<string, object> parameters)
        {
            targetParam = parameters["target"];
            statName = parameters["stat"].ToString();
            amountParam = parameters["amount"];

            if (parameters.ContainsKey("outVar"))
                outVarParam = parameters["outVar"].ToString();

            if (parameters.ContainsKey("duration"))
                durationParam = parameters["duration"].ToString();
        }

        public void Execute(GameContext context)
        {
            int amount = FormulaEvaluator.Evaluate(amountParam, context);
            List<Target> targets = TargetSelector.Select(targetParam, context);

            // [디버깅] 타겟팅 결과 확인
            if (targets.Count == 0)
            {
                DebugHelper.LogWarning($"'{statName}' 변경 실패: 유효한 타겟이 없습니다! (조건: {targetParam})");
                return;
            }

            int totalChanged = 0; // 총 변화량 합계
            List<Card> affectedCards = new List<Card>(); // 되돌리기용 명단

            foreach (Target t in targets)
            {
                int actualChange = 0;

                // CASE A: 플레이어 스탯 변경
                if (t.Type == TargetType.Player)
                {
                    Player p = t.PlayerVal;
                    if (statName == "Health")
                    {
                        int prev = p.Health;
                        p.Health += amount;
                        // (Clamp 로직 필요 시 추가)
                        actualChange = p.Health - prev;
                    }
                    else if (statName == "Mana")
                    {
                        int prev = p.Mana;
                        p.Mana += amount;
                        actualChange = p.Mana - prev;
                    }
                    System.Console.WriteLine($"✨ [스탯 변경] {p.Name}의 {statName} {amount} 변동 -> {(statName == "Health" ? p.Health : p.Mana)}");
                }
                // CASE B: 카드 스탯 변경
                else if (t.Type == TargetType.Card)
                {
                    Card c = t.CardVal;

                    // [수정] 중복 호출 제거하고 여기서 한 번만 처리
                    if (statName == "Cost")
                    {
                        int prev = c.Cost;
                        c.Cost += amount;
                        if (c.Cost < 0) c.Cost = 0;
                        actualChange = c.Cost - prev;

                        System.Console.WriteLine($"✨ [스탯 변경] 카드 '{c.Name}'의 Cost {amount} 변동 -> {c.Cost}");
                    }
                    else if (statName == "ignorePlayCondition")
                    {
                        // 조건 해제 로직 등
                        c.PlayCondition = null;
                        System.Console.WriteLine($"✨ [조건 해제] 카드 '{c.Name}'의 발동 조건 해제");
                        actualChange = 1;
                    }

                    // [디버깅] 최종 결과 출력
                    DebugHelper.LogEffect("Stat Change", $"{c.Name}의 {statName} {amount} 변동 (현재: {(statName == "Cost" ? c.Cost : 0)})");
                    // [중요] 변경된 카드를 명단에 추가 (되돌리기 예약용)
                    affectedCards.Add(c);
                }

                // [삭제] ApplyStatChange(t, amount); <--- 이거 지우세요! (중복 적용 원인)

                totalChanged += actualChange;
            }

            // [기록] 결과 저장
            if (!string.IsNullOrEmpty(outVarParam))
            {
                context.SetVariable(outVarParam, totalChanged);
            }

            // [예약] 만료 효과 등록
            if (!string.IsNullOrEmpty(durationParam) && affectedCards.Count > 0)
            {
                // 반대 부호로 되돌리기 (-amount)
                var revertEffect = new RevertStatEffect(affectedCards, statName, -amount);

                GamePhase phase = System.Enum.Parse<GamePhase>(durationParam);

                context.RegisterPendingEffect(new PendingEffect
                {
                    TriggerPhase = phase,
                    OwnerPlayer = context.ActivePlayer,
                    Effect = revertEffect,
                    Context = context
                });

                System.Console.WriteLine($"⏰ [예약] {phase}에 {affectedCards.Count}장의 카드 복구 예약됨.");
            }
        }

    }
}