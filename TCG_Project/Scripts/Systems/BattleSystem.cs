using System;
using TCG_Project.Scripts.Core;

namespace TCG_Project.Scripts.Systems
{
    public class BattleSystem
    {
        // 공격 실행
        public void Attack(Card attacker, object target, GameContext context)
        {
            // 1. 공격 유효성 검사
            if (!ValidateAttack(attacker, target)) return;

            // 2. 공격 비용(Arts Cost) 지불
            attacker.Controller.Mana -= attacker.AttackCost;
            attacker.IsExhausted = true; // 행동력 소진

            Console.WriteLine($"\n⚔️ [공격] {attacker.Name}(이)가 공격합니다! (소모 마나: {attacker.AttackCost})");

            // 3. 데미지 처리
            if (target is Player targetPlayer)
            {
                // 직접 공격은 무조건 1 데미지 (추후 증폭 가능)
                int directDamage = 1;

                // (나중에 '직접 공격 데미지 증가' 버프가 있다면 여기서 directDamage += buff);
                // [유닛 -> 플레이어] 명치 치기
                Console.WriteLine($"   💥 {targetPlayer.Name}에게 다이렉트 어택!");
                attacker.Controller.PrizePoints += directDamage;
                Console.WriteLine($"      🏆 [전투 승점] {attacker.Controller.Name}가 {directDamage}점을 얻었습니다! (Total: {attacker.Controller.PrizePoints})");
                // targetPlayer.TakeDamage(directDamage);
            }
            else if (target is Card targetUnit)
            {
                // [유닛 -> 유닛] 교전
                Console.WriteLine($"   ⚔️ 교전: {attacker.Name}({attacker.Power}) vs {targetUnit.Name}({targetUnit.Power})");

                // 공격 대상만 데미지를 입게 변경
                int attackerDmg = attacker.Power;
                // int defenderDmg = targetUnit.Power;

                ApplyDamage(targetUnit, attackerDmg);
                // ApplyDamage(attacker, defenderDmg);

                if (targetUnit.Health <= 0)
                {
                    ProcessDeath(targetUnit, attacker); // 죽음 처리

                    // [승점] 공격자(attacker.Controller)가 승점 획득
                    int prize = targetUnit.Prize;
                    if (prize > 0)
                    {
                        attacker.Controller.PrizePoints += prize;
                        Console.WriteLine($"      🏆 [전투 승점] {attacker.Controller.Name}가 {prize}점을 얻었습니다! (Total: {attacker.Controller.PrizePoints})");
                    }
                }
            }
        }

        // 대상의 이름 가져오기 (디버그용)
        private string GetName(object obj)
        {
            if (obj is Player p) return p.Name;
            if (obj is Card c) return c.Name;
            return "Unknown";
        }

        // 공격 가능 여부 확인
        public bool ValidateAttack(Card attacker, object target)
        {
            if (attacker == null) return false;
            if (attacker.Type != CardType.Unit) return false;

            // 행동력 체크
            if (attacker.IsExhausted)
            {
                Console.WriteLine("   🚫 공격 불가: 이미 행동했습니다.");
                return false;
            }

            // 공격 비용 체크
            if (attacker.Controller.Mana < attacker.AttackCost)
            {
                Console.WriteLine($"   🚫 공격 불가: 마나가 부족합니다. (필요 마나: {attacker.AttackCost})");
                return false;
            }

            Console.WriteLine("   ⚔️ 공격 가능: 공격할 수 있습니다.");
            return true;
        }

        // 데미지 적용 및 사망 처리
        private void ApplyDamage(Card unit, int damage)
        {
            unit.Health -= damage;
            Console.WriteLine($"      🩸 {unit.Name} HP: {unit.Health + damage} -> {unit.Health}");
        }

        // 유닛 사망 처리
        private void ProcessDeath(Card unit, Card attacker)
        {
            Console.WriteLine($"      💀 {unit.Name} 파괴됨!(전투)");

            Player controller = unit.Controller;

            // 필드에서 제거하고 묘지로
            if (controller.ExtractCard(ZoneType.Field, unit))
            {
                unit.ResetState();

                // 원래 주인의 묘지로
                Player owner = unit.OriginalOwner ?? controller;
                owner.Graveyard.Add(unit); 
                Console.WriteLine($"{unit.Name} : {owner.Name}의 묘지로 이동합니다.(현재 묘지 {owner.Graveyard.Count}장)");
            }
            // 3. 전투 파괴 승점 처리
            // TODO: 여기서 '처치 보상(Prize)' 로직 추가 가능
            // if (enemyKilled) GainPrize(unit.Prize);
        }
    }
}