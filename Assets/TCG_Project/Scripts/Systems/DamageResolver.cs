using System;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Systems
{
    /// <summary>
    /// 룰북 데미지 해결 파이프라인.
    ///
    /// 처리 순서:
    ///   1. 화력(Firepower) 적용: attacker.FirepowerBonus 더하기
    ///   2. 아머 적용:
    ///        관통(piercing) → SuperArmorBonus만 적용
    ///        일반           → ArmorBonus 우선, 없으면 SuperArmorBonus 적용
    ///   3. 무적(IsInvincible) → 데미지 0으로 차단 (키워드 효과에만 반응)
    ///   4. defender.LoseLife(finalDamage) 호출
    ///   5. 반격(HasCounterAttack) → 최종 데미지를 attacker에게 반환 (상대 방어 우회)
    ///
    /// Zero Unity Dependency 원칙 준수.
    /// </summary>
    public static class DamageResolver
    {
        /// <summary>
        /// 데미지 파이프라인을 실행한다.
        /// </summary>
        /// <param name="attacker">데미지를 주는 플레이어</param>
        /// <param name="defender">데미지를 받는 플레이어</param>
        /// <param name="rawDamage">원본 데미지 수치</param>
        /// <param name="isPiercing">관통 여부 (true면 아머 무시, 슈퍼아머만 반응)</param>
        /// <param name="context">현재 게임 컨텍스트</param>
        public static void ResolveDamage(
            Player attacker, Player defender,
            int rawDamage, bool isPiercing,
            GameContext context)
        {
            if (context.IsGameOver) return;

            int originalDamage = rawDamage; // 원본 데미지
            int firedDamage = rawDamage;    // 최대 증가한 데미지 (화력 적용 후)
            int defencedDamage = rawDamage; // 방어만큼 경감된 데미지 (방어 적용 후)
            int effectiveDamage = rawDamage; // 화력, 방어 로직 계산 후 최종 데미지

            // 1. 전장 화력 적용 및 소진 (첫 타격에만)
            if (attacker.BattlefieldFirepower > 0)
            {
                firedDamage += attacker.BattlefieldFirepower; // 전장 화력 적용 후 데미지 (방어 적용 전)
                EventManager.OnLogMessage?.Invoke($"    🔥 [전장 화력] 데미지 +{attacker.BattlefieldFirepower} 증가! (전장 화력 소진)");
                attacker.BattlefieldFirepower = 0; // ★ 소모됨 (다단히트의 다음 타격엔 적용 안 됨)
            }

            // 2. 화력 버프 적용
            int firepower = attacker.FirepowerBonus;
            if (firepower > 0)
                EventManager.OnLogMessage?.Invoke($"  [화력] {firedDamage} + {firepower} = {firedDamage + firepower}");
            firedDamage += firepower; // 화력 적용 후 데미지 (방어 적용 전)

            // 2-1. 스택 화력 버프 적용
            if (attacker.StackFirepowers.Count > 0)
            {
                int usedStackPower = attacker.StackFirepowers[0];
                attacker.StackFirepowers.RemoveAt(0); // 잔여 데미지 상관없이 무조건 1개 소멸
                firedDamage += usedStackPower;
                EventManager.OnLogMessage?.Invoke($"    [화력] 스택 화력({usedStackPower}) 증가! -> {firedDamage}");
            }

            effectiveDamage = firedDamage; // 화력 적용 후 중간 결산

            // 3. 무적 적용 (키워드 효과 전체 차단)
            if (defender.IsInvincible)
            {
                EventManager.OnLogMessage?.Invoke(
                    $"  [무적] 데미지 {effectiveDamage} → 0으로 차단");
                effectiveDamage = 0;
            }

            // 3-1. 스택 무적 처리
            if (effectiveDamage > 0 && defender.StackInvincibilities.Count > 0)
            {
                Card usedStackCard = defender.StackInvincibilities[0];
                defender.StackInvincibilities.RemoveAt(0); // 무조건 1개 소멸
                effectiveDamage = 0;
                EventManager.OnLogMessage?.Invoke($"    🛡️ [무적] 스택 무적({usedStackCard.Name}) 사용");
            }

            // 방어 계산용 데미지
            defencedDamage = effectiveDamage;

            // 4. 관통 공격은 슈퍼아머 적용
            if (isPiercing && defencedDamage > 0)
            {
                // 1. 관통 공격: [슈퍼 아머]만 적용 (유지형)
                defencedDamage = Math.Max(0, defencedDamage - defender.SuperArmorBonus);
                if (defencedDamage < firedDamage) // 방어가 데미지에 영향을 미쳤을 때만 로그 출력
                    EventManager.OnLogMessage?.Invoke($"    🛡️ [방어] 슈퍼아머({defender.SuperArmorBonus}) 경감");

                // 2. 스택 슈퍼 아머 적용 (1회 타격당 맨 앞의 1개만 꺼내서 강제 소진)
                if (defender.StackSuperArmors.Count > 0)
                {
                    int usedStack = defender.StackSuperArmors[0];
                    defender.StackSuperArmors.RemoveAt(0); // 잔여 데미지 상관없이 무조건 1개 소멸
                    defencedDamage = Math.Max(0, defencedDamage - usedStack);
                    EventManager.OnLogMessage?.Invoke($"    🛡️ [방어] 스택 슈퍼아머({usedStack}) 소진");
                }

            }

            // 4. 일반 공격은 아머 - 슈퍼아머 순으로 적용
            else if (!isPiercing && defencedDamage > 0)
            {
                int BattlefieldArmorAfter = defencedDamage; // 방어 적용 후 데미지 계산용 임시 변수 (전장 아머 적용 전)
                // 0. 방어자의 전장 아머 적용 및 소진 (첫 타격에만)
                if (!isPiercing && defender.BattlefieldArmor > 0)
                {
                    int blocked = defender.BattlefieldArmor; // 방어량 계산용 임시 변수
                    defencedDamage = Math.Max(0, defencedDamage - defender.BattlefieldArmor);
                    // 사용하면 즉시 0 처리
                    defender.BattlefieldArmor = 0;
                    if (defencedDamage < firedDamage) // 방어가 데미지에 영향을 미쳤을 때만 로그 출력
                        EventManager.OnLogMessage?.Invoke($"    🛡️ [전장 방어] 데미지 {blocked} 경감! (전장 아머 소진)");
                    BattlefieldArmorAfter = defencedDamage; // 전장 아머 적용 후 데미지 계산
                }

                // 1. 일반 공격: [아머] + [슈퍼 아머] 적용 (유지형)
                defencedDamage = Math.Max(0, defencedDamage - defender.ArmorBonus - defender.SuperArmorBonus);
                if (defencedDamage < BattlefieldArmorAfter) // 방어가 데미지에 영향을 미쳤을 때만 로그 출력
                    EventManager.OnLogMessage?.Invoke($"    🛡️ [방어] 아머({defender.ArmorBonus}) + 슈퍼아머({defender.SuperArmorBonus}) 경감");

                // 2. 스택 아머 우선 적용 (1회 타격당 맨 앞의 1개만 꺼내서 강제 소진)
                if (defender.StackArmors.Count > 0)
                {
                    int usedStack = defender.StackArmors[0];
                    defender.StackArmors.RemoveAt(0); // 잔여 데미지 상관없이 무조건 1개 소멸
                    defencedDamage = Math.Max(0, defencedDamage - usedStack);
                    EventManager.OnLogMessage?.Invoke($"    🛡️ [방어] 스택 아머({usedStack}) 소진");
                }
                else if (defender.StackSuperArmors.Count > 0) // 일반 공격이지만 스택 슈퍼 아머만 남아있다면 활용
                {
                    int usedStack = defender.StackSuperArmors[0];
                    defender.StackSuperArmors.RemoveAt(0);
                    defencedDamage = Math.Max(0, defencedDamage - usedStack);
                    EventManager.OnLogMessage?.Invoke($"    🛡️ [방어] 스택 슈퍼아머({usedStack}) 소진");
                }
            }

            effectiveDamage = defencedDamage; // 최종 데미지 계산

            // 4. 최종 데미지가 남아있을 때만 라이프 감소
            if (effectiveDamage > 0)
            {
                EventManager.OnLogMessage?.Invoke(
                    $"  [최종 데미지] {effectiveDamage} → [{defender.Name}] 라이프 감소");
                defender.LoseLife(effectiveDamage, context);
            }
            else
            {
                EventManager.OnLogMessage?.Invoke(
                    $"  [방어 완료] 최종 데미지 0");
            }

            // 5. 반격 (원본 데미지를 attacker에게 반환, 방어 우회)
            // [설계 원칙 1] 상시 반격이 켜져 있으면 스택을 아낀다.
            // [설계 원칙 2] 한 번의 타격(Time)에는 1번의 반격만 나간다.

            bool willCounterAttack = false;
            string counterSourceName = "";

            // --- Step 1: 반격 트리거 검사 및 자원 소모 ---
            if (defender.HasCounterAttack)
            {
                // 1순위: 이번 턴 상시 반격 상태 (리벤지 등) -> 스택을 소모하지 않음
                willCounterAttack = true;
                counterSourceName = "상시 반격";
            }
            else if (defender.StackCounterAttacks.Count > 0)
            {
                // 2순위: 큐에 장전된 스택형 반격 -> 1개 꺼내서 소모
                Card consumedStack = defender.StackCounterAttacks[0];
                defender.StackCounterAttacks.RemoveAt(0);
                willCounterAttack = true;
                counterSourceName = $"스택 반격({consumedStack.Name})";
            }

            // --- Step 2 & 3: 반격 실행 및 보상 처리 ---
            if (willCounterAttack)
            {
                // 공격자가 무적일 경우: 반격 자체는 발동(스택 소모)했으나, 데미지는 씹힘
                if (attacker.IsInvincible)
                {
                    EventManager.OnLogMessage?.Invoke(
                        $"  [반격] {counterSourceName} 발동! 그러나 [{attacker.Name}] 무적 → 반격 피해 차단");
                }
                else
                {
                    // 정상 타격
                    EventManager.OnLogMessage?.Invoke(
                        $"  [반격] {counterSourceName} 발동! 반격 데미지 {originalDamage} → [{attacker.Name}]");

                    attacker.LoseLife(originalDamage, context);

                    // 반격 성공 시 대기열에 장전된 보상 행동(Action) 실행
                    // 큐에 대기 중인 보상이 있다면 모두 실행하고 큐에서 비웁니다.
                    while (defender.PendingCounterRewards != null && defender.PendingCounterRewards.Count > 0)
                    {
                        Action rewardAction = defender.PendingCounterRewards.Dequeue();
                        rewardAction?.Invoke();
                    }
                }
            }

            /* 5. 반격 (원본 데미지를 attacker에게 반환, 방어 우회). Phase 18: 무적은 반격 피해도 막음.
            if (defender.HasCounterAttack && !attacker.IsInvincible)
            {
                EventManager.OnLogMessage?.Invoke(
                    $"  [반격] 반격 데미지 {originalDamage} → [{attacker.Name}]");
                attacker.LoseLife(originalDamage, context);

                // 반격 성공 시 대기열에 장전된 보상 행동(Action) 실행 (1회성)
                // 큐에 대기 중인 보상이 있다면 모두 실행하고 큐에서 빼버립니다.
                while (defender.PendingCounterRewards.Count > 0)
                {
                    Action rewardAction = defender.PendingCounterRewards.Dequeue();
                    rewardAction?.Invoke();
                }
                // 1타째에 큐가 비워지므로, 2타째 반격이 터져도 이 while문은 안전하게 스킵
            }
            else if (defender.HasCounterAttack && attacker.IsInvincible)
            {
                EventManager.OnLogMessage?.Invoke(
                    $"  [반격] [{attacker.Name}] 무적 → 반격 피해 차단");
            }*/
        }
    }
}
