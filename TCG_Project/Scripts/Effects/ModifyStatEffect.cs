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
        private string outVarParam; // [신규]
        private string durationParam; // [신규] "TurnEnd", "NextTurnStart" 등

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
            int totalChanged = 0; // 총 변화량 합계

            foreach (Target t in targets)
            {
                int actualChange = 0;

                if (t.Type == TargetType.Player)
                {
                    Player p = t.PlayerVal;
                    if (statName == "Health")
                    {
                        int prev = p.Health;
                        p.Health += amount;
                        // (최대 체력 로직이 있다면 여기서 Clamp 처리)
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
                else if (t.Type == TargetType.Card)
                {
                    Card c = t.CardVal;
                    if (statName == "Cost")
                    {
                        int prev = c.Cost;
                        c.Cost += amount;
                        if (c.Cost < 0) c.Cost = 0;
                        actualChange = c.Cost - prev; // 감소했으면 음수
                        System.Console.WriteLine($"✨ [스탯 변경] 카드 '{c.Name}'의 Cost {amount} 변동 -> {c.Cost}");
                    }
                    else if (statName == "ignorePlayCondition")
                    {
                        c.PlayCondition = null;
                        System.Console.WriteLine($"✨ [조건 해제] 카드 '{c.Name}'의 발동 조건 '{c.PlayCondition}' 해제");
                        actualChange += 1;
                    }
                }
                //ApplyStatChange(t, amount);
                totalChanged += actualChange;
            }
            // [기록] 실제 변화량 저장
            if (!string.IsNullOrEmpty(outVarParam))
            {
                context.SetVariable(outVarParam, totalChanged);
            }

            // 2. [신규] 만료 예약 (역연산: 코스트 +2)
            if (!string.IsNullOrEmpty(durationParam))
            {
                // 역연산 효과 생성 (반대 부호 amount)
                var revertEffect = new ModifyStatEffect();

                // 파라미터 재구성 (현재 타겟들을 고정)
                // 주의: 타겟을 다시 검색(Select)하면 안 되고, 지금 변경된 그 객체들을 지정해야 함.
                // 하지만 구조상 Effect는 다시 Select를 하므로, 여기서는 편의상 
                // "방금 변경된 그놈들"을 다시 타겟팅하는 로직을 단순화하거나 
                // Effect 구조를 객체 주입 가능하게 변경해야 함.

                // [간편 구현] 같은 파라미터를 쓰되 amount만 뒤집음 (-amount)
                var revertParams = new Dictionary<string, object>
            {
                { "target", targetParam }, // 주의: 조건부 타겟이면 나중에 대상이 달라질 수 있음 (리스크)
                { "stat", statName },
                { "amount", -amount } // 부호 반대로!
            };
                revertEffect.Initialize(revertParams);

                // 예약 등록
                GamePhase phase = (GamePhase)System.Enum.Parse(typeof(GamePhase), durationParam);
                context.RegisterPendingEffect(new PendingEffect
                {
                    TriggerPhase = phase,
                    OwnerPlayer = context.ActivePlayer, // 필요 시 설정
                    Effect = revertEffect,
                    Context = context // 현재 컨텍스트 유지
                });
            }
        }
    }
}