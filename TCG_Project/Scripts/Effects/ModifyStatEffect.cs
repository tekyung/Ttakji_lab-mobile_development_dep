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
        private string triggerCondition; // [신규] 발동 조건
        private int prizeOnKill;         // [신규] 처치 시 승점

        public void Initialize(Dictionary<string, object> parameters)
        {
            targetParam = parameters["target"];
            statName = parameters["stat"].ToString();
            amountParam = parameters["amount"];

            if (parameters.ContainsKey("outVar"))
                outVarParam = parameters["outVar"].ToString();

            if (parameters.ContainsKey("duration"))
                durationParam = parameters["duration"].ToString();

            // [신규] 파라미터 로드
            if (parameters.ContainsKey("triggerCondition"))
                triggerCondition = parameters["triggerCondition"].ToString();

            if (parameters.ContainsKey("prizeOnKill"))
                prizeOnKill = int.Parse(parameters["prizeOnKill"].ToString());
        }

        public void Execute(GameContext context)
        {
            // 1. [과제 1] 발동 조건(triggerCondition) 재확인
            if (!string.IsNullOrEmpty(triggerCondition))
            {
                // ConditionEvaluator를 사용하여 조건 체크 (예: EnemyUnitExist)
                if (!ConditionEvaluator.Evaluate(triggerCondition, context))
                {
                    System.Console.WriteLine($"🚫 조건 불만족({triggerCondition})으로 효과가 취소되었습니다.");
                    return;
                }
            }
            // 2. 스탯 변경 처리
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
                int resultValue = 0;

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
                        resultValue = p.Health;
                    }
                    else if (statName == "Mana")
                    {
                        int prev = p.Mana;
                        p.Mana += amount;
                        actualChange = p.Mana - prev;
                        resultValue = p.Mana;
                    }
                    System.Console.WriteLine($"✨ [스탯 변경] {p.Name}의 {statName} {amount} 변동 -> {resultValue}");
                }
                // CASE B: 카드 스탯 변경
                else if (t.Type == TargetType.Card)
                {
                    Card c = t.CardVal;
                    // 공격력 처리
                    if (statName == "Power")
                    {
                        int prev = c.Power;
                        c.Power += amount;
                        if (c.Power < 0) c.Power = 0;
                        actualChange = c.Power - prev;
                        resultValue = c.Power;
                        System.Console.WriteLine($"✨ [스탯 변경] 카드 '{c.Name}'의 Power {amount} 변동 -> {resultValue}");
                        
                        // ★ 파워는 일단 체력과 동일시, 0 이하가 되면 파괴
                        if (c.Health <= 0)
                        {
                            // BattleSystem의 사망 처리를 호출하거나, 여기서 직접 묘지로 보냄
                            // (BattleSystem 인스턴스가 없으므로 직접 처리 예시)
                            ProcessDeath(c, context);
                        }
                    }
                    // [추가] 체력 처리 (파이어볼/픽시드래곤 용)
                    else if (statName == "Health")
                    {
                        int prev = c.Health;
                        c.Health += amount;
                        // (최대 체력 초과 방지 로직 필요시 추가)
                        // if (c.Health > c.MaxHealth) c.Health = c.MaxHealth;

                        actualChange = c.Health - prev;
                        resultValue = c.Health;
                        System.Console.WriteLine($"✨ [스탯 변경] 카드 '{c.Name}'의 Health {amount} 변동 -> {resultValue}");

                        // ★ 중요: 체력이 0 이하가 되면 파괴 처리!
                        if (c.Health <= 0)
                        {
                            // BattleSystem의 사망 처리를 호출하거나, 여기서 직접 묘지로 보냄
                            // (BattleSystem 인스턴스가 없으므로 직접 처리 예시)
                            ProcessDeath(c, context);
                        }
                    }
                    else if (statName == "Cost")
                    {
                        int prev = c.Cost;
                        c.Cost += amount;
                        if (c.Cost < 0) c.Cost = 0;
                        actualChange = c.Cost - prev;
                        resultValue= c.Cost;
                        System.Console.WriteLine($"✨ [스탯 변경] 카드 '{c.Name}'의 Cost {amount} 변동 -> {resultValue}");
                    }
                    else if (statName == "ignorePlayCondition") // 아직 미구현
                    {
                        // 조건 해제 로직 등
                        c.PlayCondition = null;
                        System.Console.WriteLine($"✨ [조건 해제] 카드 '{c.Name}'의 발동 조건 해제");
                        actualChange = 1;
                    }

                    // [디버깅] 최종 결과 출력
                    DebugHelper.LogEffect("Stat Change", $"{c.Name}의 {statName} {amount} 변동 (현재: {resultValue})");
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

        private void ProcessDeath(Card unit, GameContext context) // HP, Power 0시 파괴 처리
        {
            System.Console.WriteLine($"      💀 {unit.Name} 파괴됨! (효과)");
            Player controller = unit.Controller;
            if (controller.ExtractCard(ZoneType.Field, unit))
            {
                unit.ResetState();
                Player owner = unit.OriginalOwner ?? controller;
                owner.Graveyard.Add(unit);
                System.Console.WriteLine($"{unit.Name}이(가) {owner.Name}의 묘지로 이동합니다. 현재 묘지 {owner.Graveyard.Count}장");
                
                // 효과 파괴 승점 처리
                if (prizeOnKill > 0)
                {
                    // 효과를 발동한 플레이어(ActivePlayer)가 점수를 얻음
                    context.ActivePlayer.PrizePoints += prizeOnKill;
                    System.Console.WriteLine($"      🏆 [효과 승점] {context.ActivePlayer.Name}가 {prizeOnKill}점을 얻었습니다! (Total: {context.ActivePlayer.PrizePoints})");
                }
            }
        }

    }
}