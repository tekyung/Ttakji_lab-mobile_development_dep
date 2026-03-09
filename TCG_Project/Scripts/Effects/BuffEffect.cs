using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Effects
{
    public enum BuffType
    {
        Armor,        // 일반 데미지 경감
        SuperArmor,   // 관통 포함 모든 데미지 경감
        Invincible,   // 무적 (데미지/관통/반격 차단)
        Firepower,    // 화력 (데미지 수치 증가)
        CounterAttack // 반격 (받은 원본 데미지를 상대에게 그대로 반환)
    }

    public enum EffectDuration
    {
        ThisTurn,
        NextTurn,
        Stack      // 스택존에서 대기하다가 조건 만족 시 소모되는 1회성 지속시간
    }

    /// <summary>
    /// 버프/디버프 통합 클래스.
    /// 기존 ArmorEffect / SuperArmorEffect / InvincibilityEffect / FirepowerEffect /
    /// CounterAttackEffect / NextTurnBuffEffect 를 통합한다.
    ///
    /// GameDataManager가 Initialize()에 아래 파라미터를 주입한다:
    ///   buffType : "Armor" | "SuperArmor" | "Invincible" | "Firepower" | "CounterAttack"
    ///   amount   : int  (Invincible, CounterAttack은 0)
    ///   duration : "ThisTurn" | "NextTurn" (기본값 ThisTurn)
    /// </summary>
    public class BuffEffect : ICardEffect
    {
        public BuffType TypeOfBuff { get; private set; }
        private int _amount = 0;
        private EffectDuration _duration = EffectDuration.ThisTurn;

        public bool RequirePreviousSuccess { get; set; } = false; // 기본값은 false (독립 실행)
        public bool IsStackAction { get; set; } = false; // 기본값은 false (카드의 IsStack을 따라가되, JSON에서 오버라이드 가능)
        private Dictionary<string, object> _cachedParams;

        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters ?? new Dictionary<string, object>();

            if (parameters.ContainsKey("buffType"))
            {
                string raw = parameters["buffType"].ToString();
                if (Enum.TryParse<BuffType>(raw, true, out var bt))
                    TypeOfBuff = bt;
            }

            if (parameters.ContainsKey("amount"))
                _amount = Convert.ToInt32(parameters["amount"]);

            if (parameters.ContainsKey("duration"))
            {
                string durStr = parameters["duration"].ToString();
                if (durStr == "NextTurn") _duration = EffectDuration.NextTurn;
                else if (durStr == "Stack") _duration = EffectDuration.Stack; // 스택 기간 추가
            }

            if (parameters.ContainsKey("requirePreviousSuccess")) // '그 후,' 시맨틱 지원
                RequirePreviousSuccess = Convert.ToBoolean(parameters["requirePreviousSuccess"]);

            // 'isStackAction' 파라미터 지원 (스택형 효과 여부 지정)
            if (parameters.TryGetValue("isStackAction", out var isStackObj))
            {
                IsStackAction = Convert.ToBoolean(isStackObj.ToString());
                // ★ 임시 확인용 로그 추가
                //Console.WriteLine($"[DEBUG] {TypeOfBuff} 이펙트 초기화! isStackAction 파싱 성공: {IsStackAction}");
            }
            else
            {
                //Console.WriteLine($"[DEBUG] {TypeOfBuff} 이펙트 초기화! isStackAction 파라미터가 JSON에 없음. 기본값 false 적용.");
            }
        }

        // 스택형 버프를 지원하는 경우, 효과 실행 시점이 아니라 발동 조건이 충족되어 실제로 효과가 적용되는 시점에 버프를 부여.
        public void Execute(GameContext context, Action onComplete)
        {
            Player owner = context.ActivePlayer;
            if (owner == null) { onComplete?.Invoke(); return; }

            switch (TypeOfBuff)
            {
                case BuffType.Armor:
                    if (_duration == EffectDuration.Stack)
                    {
                        owner.StackArmors.Add(_amount); // 리스트에 개별 방어구로 장전
                        EventManager.OnLogMessage?.Invoke(
                            $"  [스택 아머] {owner.Name} 스택 아머({_amount}) 장전 (현재 대기: {owner.StackArmors.Count}개)");
                    }
                    else if (_duration == EffectDuration.NextTurn)
                    {
                        owner.NextTurnArmorBonus += _amount;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [다음 턴 예약] {owner.Name}: 다음 턴 아머 +{_amount} 예약됨");
                    }
                    else // ThisTurn
                    {
                        owner.ArmorBonus += _amount;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [아머] {owner.Name} 아머 +{_amount} (이번 라운드 합계: {owner.ArmorBonus})");
                    }
                    break;

                case BuffType.SuperArmor:
                    if (_duration == EffectDuration.Stack) // 스택형 슈퍼아머(1회성) 지원
                    {
                        owner.StackSuperArmors.Add(_amount); // 리스트에 개별 방어구로 장전
                        EventManager.OnLogMessage?.Invoke(
                            $"  [스택 슈퍼아머] {owner.Name} 스택 슈퍼아머({_amount}) 장전 (현재 대기: {owner.StackSuperArmors.Count}개)");
                    }
                    else if (_duration == EffectDuration.NextTurn)
                    {
                        owner.NextTurnSuperArmorBonus += _amount;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [다음 턴 예약] {owner.Name}: 다음 턴 슈퍼아머 +{_amount} 예약됨");
                    }
                    else // ThisTurn
                    {
                        owner.SuperArmorBonus += _amount;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [슈퍼아머] {owner.Name} 슈퍼아머 +{_amount} (이번 라운드 합계: {owner.SuperArmorBonus})");
                    }
                    break;

                case BuffType.Invincible:
                    if (_duration == EffectDuration.Stack) // 스택형 무적(1회성) 지원
                    {
                        owner.StackInvincibilities.Add(new Card { Name = "스택 무적 효과 카드" }); // 실제 카드 객체로 관리하여 추후 제거 시 참조 가능
                        EventManager.OnLogMessage?.Invoke(
                            $"  [스택 무적] {owner.Name} 스택 무적 효과 장전 (현재 대기: {owner.StackInvincibilities.Count}개)");
                    }
                    else if (_duration == EffectDuration.NextTurn)
                    {
                        owner.NextTurnIsInvincible = true;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [다음 턴 예약] {owner.Name}: 다음 라운드 무적 예약됨");
                    }
                    else // ThisTurn
                    {
                        owner.IsInvincible = true;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [무적] {owner.Name} 이번 라운드 무적 상태");
                    }
                    break;

                case BuffType.Firepower:
                    if (_duration == EffectDuration.Stack) // 스택형 화력 지원 (1회성)
                    {
                        owner.StackFirepowers.Add(_amount); // 리스트에 개별 화력으로 장전
                        EventManager.OnLogMessage?.Invoke(
                            $"  [스택 화력] {owner.Name} 스택 화력({_amount}) 장전 (현재 대기: {owner.StackFirepowers.Count}개)");
                    }
                    else if (_duration == EffectDuration.NextTurn)
                    {
                        owner.NextTurnFirepowerBonus += _amount;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [다음 턴 예약] {owner.Name}: 다음 라운드 화력 +{_amount} 예약됨");
                    }
                    else // ThisTurn
                    {
                        owner.FirepowerBonus += _amount;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [화력] {owner.Name} 화력 +{_amount} (이번 라운드 합계: {owner.FirepowerBonus})");
                    }
                    break;

                case BuffType.CounterAttack:
                    owner.HasCounterAttack = true;
                    EventManager.OnLogMessage?.Invoke(
                        $"  [반격] {owner.Name} 이번 라운드 반격 상태");
                    break;
            }

            onComplete?.Invoke();
        }

        public ICardEffect Clone()
        {
            var clone = new BuffEffect();
            clone.Initialize(_cachedParams);
            return clone;
        }
    }
}
