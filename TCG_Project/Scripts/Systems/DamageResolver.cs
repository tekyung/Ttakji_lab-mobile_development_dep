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

            int originalDamage = rawDamage;

            // 1. 화력 적용
            int firepower = attacker.FirepowerBonus;
            int damage = rawDamage + firepower; // 화력 적용 후 데미지 (방어 적용 전)
            int effectiveDamage = damage; // 방어 적용 후 최종 데미지 (초기값은 화력 적용된 데미지)

            if (firepower > 0)
                EventManager.OnLogMessage?.Invoke(
                    $"  [화력] {rawDamage} + {firepower} = {effectiveDamage}");

            // 2. 무적 적용 (키워드 효과 전체 차단)
            if (defender.IsInvincible)
            {
                EventManager.OnLogMessage?.Invoke(
                    $"  [무적] 데미지 {effectiveDamage} → 0으로 차단");
                effectiveDamage = 0;
            }

            // 3. 관통 공격은 슈퍼아머 적용
            if (isPiercing && effectiveDamage > 0)
            {
                // 1. 관통 공격: [슈퍼 아머]만 적용 (유지형)
                effectiveDamage = Math.Max(0, effectiveDamage - defender.SuperArmorBonus);
                if (effectiveDamage < damage) // 방어가 데미지에 영향을 미쳤을 때만 로그 출력
                    EventManager.OnLogMessage?.Invoke($"    🛡️ [방어] 슈퍼아머({defender.SuperArmorBonus}) 경감");

                // 2. 스택 슈퍼 아머 적용 (1회 타격당 맨 앞의 1개만 꺼내서 강제 소진)
                if (defender.StackSuperArmors.Count > 0)
                {
                    int usedStack = defender.StackSuperArmors[0];
                    defender.StackSuperArmors.RemoveAt(0); // 잔여 데미지 상관없이 무조건 1개 소멸
                    effectiveDamage = Math.Max(0, effectiveDamage - usedStack);
                    EventManager.OnLogMessage?.Invoke($"    🛡️ [방어] 스택 슈퍼아머({usedStack}) 소진");
                }
            }
            // 4. 일반 공격은 아머 - 슈퍼아머 순으로 적용
            else if (!isPiercing && effectiveDamage > 0)
            {
                // 1. 일반 공격: [아머] + [슈퍼 아머] 적용 (유지형)
                effectiveDamage = Math.Max(0, effectiveDamage - defender.ArmorBonus - defender.SuperArmorBonus);
                if (effectiveDamage < damage) // 방어가 데미지에 영향을 미쳤을 때만 로그 출력
                    EventManager.OnLogMessage?.Invoke($"    🛡️ [방어] 아머({defender.ArmorBonus}) + 슈퍼아머({defender.SuperArmorBonus}) 경감");

                // 2. 스택 아머 우선 적용 (1회 타격당 맨 앞의 1개만 꺼내서 강제 소진)
                if (defender.StackArmors.Count > 0)
                {
                    int usedStack = defender.StackArmors[0];
                    defender.StackArmors.RemoveAt(0); // 잔여 데미지 상관없이 무조건 1개 소멸
                    effectiveDamage = Math.Max(0, effectiveDamage - usedStack);
                    EventManager.OnLogMessage?.Invoke($"    🛡️ [방어] 스택 아머({usedStack}) 소진");
                }
                else if (defender.StackSuperArmors.Count > 0) // 일반 공격이지만 스택 슈퍼 아머만 남아있다면 활용
                {
                    int usedStack = defender.StackSuperArmors[0];
                    defender.StackSuperArmors.RemoveAt(0);
                    effectiveDamage = Math.Max(0, effectiveDamage - usedStack);
                    EventManager.OnLogMessage?.Invoke($"    🛡️ [방어] 스택 슈퍼아머({usedStack}) 소진");
                }
            }
            
            // 4. 최종 데미지가 남아있을 때만 라이프 감소
            if (effectiveDamage > 0)
            {                EventManager.OnLogMessage?.Invoke(
                    $"  [최종 데미지] {effectiveDamage} → [{defender.Name}] 라이프 감소");
                defender.LoseLife(effectiveDamage, context);
            }
            else
            {
                EventManager.OnLogMessage?.Invoke(
                    $"  [방어 완료] 최종 데미지 0");
            }

            // 5. 반격 (최종 데미지를 attacker에게 반환, 방어 우회). Phase 18: 무적은 반격 피해도 막음.
            if (defender.HasCounterAttack && !attacker.IsInvincible)
            {
                EventManager.OnLogMessage?.Invoke(
                    $"  [반격] 최종 데미지 {damage} → [{attacker.Name}]");
                attacker.LoseLife(damage, context);
            }
            else if (defender.HasCounterAttack && attacker.IsInvincible)
            {
                EventManager.OnLogMessage?.Invoke(
                    $"  [반격] [{attacker.Name}] 무적 → 반격 피해 차단");
            }
        }
    }
}
