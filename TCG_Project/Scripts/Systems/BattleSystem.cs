using System;
using System.Diagnostics;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Effects;
using TCG_Project.Scripts.Interfaces;
using UnityEngine;

namespace TCG_Project.Scripts.Systems
{
    public class BattleSystem
    {
        // 공격 실행
        public void Attack(Card attacker, object target, GameContext context)
        {
            // ★ [상태 가드] 누군가 이겨서 게임이 끝났다면 남은 공격 취소
            if (context.IsGameOver) return;

            // 1. 공격 유효성 검사
            if (!ValidateAttack(attacker, target)) return;

            // 2. 공격 비용(Arts Cost) 지불
            attacker.Controller.Mana -= attacker.AttackCost;
            EventManager.OnManaChange?.Invoke(attacker.Controller, attacker.AttackCost);

            // Console.WriteLine($"\n⚔️ [공격] {attacker.Name}(이)가 공격합니다! (소모 마나: {attacker.AttackCost})");

            // 3. 데미지 처리
            if (target is Player targetPlayer)
            {
                // 직접 공격은 무조건 1 승점 (추후 증폭 가능)
                int directDamage = GameRules.GainPrizeByDirect;

                // [유닛 -> 플레이어] 명치 치기
                EventManager.OnLogMessage?.Invoke($"💥 {attacker.Name} : {targetPlayer.Name}를 직접 공격! (소비 마나 : {attacker.AttackCost})");
                
                // ★ 승점 획득 (이 안에서 7점이 넘으면 즉시 OnGameSet 이벤트가 터짐)
                attacker.Controller.GetPrize(directDamage, context);

                //Console.WriteLine($"      🏆 [전투 승점] {attacker.Controller.Name}가 {directDamage}점을 얻었습니다! (Total: {attacker.Controller.PrizePoints})");
                // EventManager.OnPrizeChange?.Invoke(attacker.Controller, attacker.Controller.PrizePoints);
            }
            else if (target is Card targetUnit)
            {
                // [유닛 -> 유닛] 교전
                //Console.WriteLine($"   ⚔️ 교전: {attacker.Name}({attacker.Power}) vs {targetUnit.Name}({targetUnit.Power})");
                EventManager.OnLogMessage?.Invoke($"⚔️ {attacker.Name} -> {targetUnit.Name} 공격! (소비 마나 : {attacker.AttackCost})");

                // 공격 대상만 데미지를 입음 (반격 없음)
                int attackerDmg = attacker.Power;
                // int defenderDmg = targetUnit.Power;

                ApplyDamage(targetUnit, attackerDmg);
                // ApplyDamage(attacker, defenderDmg);
                
                if (targetUnit.Power <= 0)
                {
                    int prize = targetUnit.Prize;
                    ProcessDeath(targetUnit, attacker); // 죽음 처리

                    // [승점] 공격자(attacker.Controller)가 승점 획득
                    if (prize > 0)
                    {
                        // ★ 승점 획득 (이 안에서 7점이 넘으면 즉시 OnGameSet 이벤트가 터짐)
                        attacker.Controller.GetPrize(prize, context);
                    }
                }
            }
            attacker.IsExhausted = true; // 행동력 소진
            EventManager.UnitStatusChange?.Invoke(attacker, "Exhausted"); // 지침 상태(공격권 소진)
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
               // Console.WriteLine("   🚫 공격 불가: 이미 행동했습니다.");
                return false;
            }

            // 공격 비용 체크
            if (attacker.Controller.Mana < attacker.AttackCost)
            {
                //Console.WriteLine($"   🚫 공격 불가: 마나가 부족합니다. (필요 마나: {attacker.AttackCost})");
                return false;
            }

            //Console.WriteLine("   ⚔️ 공격 가능: 공격할 수 있습니다.");
            return true;
        }

        // 데미지 적용 및 사망 처리
        private void ApplyDamage(Card unit, int damage)
        {
            unit.Power -= damage;
            EventManager.OnLogMessage?.Invoke($"      🩸 {unit.Name} Power: {unit.Power + damage} -> {unit.Power}");
            EventManager.OnUnitTakeDamage?.Invoke(unit, damage);
        }

        // 유닛 사망 처리
        private void ProcessDeath(Card unit, Card attacker)
        {
            EventManager.OnLogMessage?.Invoke($"💀 {unit.Name} 파괴됨!(전투)");
            EventManager.OnUnitDeath?.Invoke(unit);
            Player controller = unit.Controller;

            // 필드에서 제거하고 묘지로
            if (controller.ExtractCard(ZoneType.Field, unit))
            {
                unit.ResetState();
                // 원래 주인의 묘지로
                Player owner = unit.OriginalOwner ?? controller;
                owner.InsertCard(ZoneType.Graveyard, unit);
                EventManager.OnCardMove?.Invoke(unit, controller, ZoneType.Field, owner, ZoneType.Graveyard);
                EventManager.OnLogMessage?.Invoke($"💀 {unit.Name}이(가) {owner.Name}의 묘지로 이동합니다. (현재 묘지 {owner.Graveyard.Count}장)");
            }
        }
    }
}