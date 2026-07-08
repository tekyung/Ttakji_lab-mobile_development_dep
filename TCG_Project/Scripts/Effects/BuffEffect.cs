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
        public int Amount { get; private set; } = 0;
        //private int _amount = 0;
        private EffectDuration _duration = EffectDuration.ThisTurn;

        public bool RequirePreviousSuccess { get; set; } = false; // 기본값은 false (독립 실행)
        public bool IsStackAction { get; set; } = false; // 기본값은 false (카드의 IsStack을 따라가되, JSON에서 오버라이드 가능)
        private Dictionary<string, object> _cachedParams;
        private string _rewardOnSuccess = ""; // 반격 성공 시 실행할 보상
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
                Amount = Convert.ToInt32(parameters["amount"]);

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

            if (parameters.ContainsKey("rewardOnSuccess"))
                _rewardOnSuccess = parameters["rewardOnSuccess"].ToString();
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
                        owner.StackArmors.Add(Amount); // 리스트에 개별 방어구로 장전
                        EventManager.OnLogMessage?.Invoke(
                            $"  [스택 아머] {owner.Name} 스택 아머({Amount}) 장전 (현재 대기: {owner.StackArmors.Count}개)");
                    }
                    else if (_duration == EffectDuration.NextTurn)
                    {
                        owner.NextTurnArmorBonus += Amount;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [다음 턴 예약] {owner.Name}: 다음 턴 아머 +{Amount} 예약됨");
                    }
                    else // ThisTurn
                    {
                        owner.ArmorBonus += Amount;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [아머] {owner.Name} 아머 +{Amount} (이번 라운드 합계: {owner.ArmorBonus})");
                    }
                    break;

                case BuffType.SuperArmor:
                    if (_duration == EffectDuration.Stack) // 스택형 슈퍼아머(1회성) 지원
                    {
                        owner.StackSuperArmors.Add(Amount); // 리스트에 개별 방어구로 장전
                        EventManager.OnLogMessage?.Invoke(
                            $"  [스택 슈퍼아머] {owner.Name} 스택 슈퍼아머({Amount}) 장전 (현재 대기: {owner.StackSuperArmors.Count}개)");
                    }
                    else if (_duration == EffectDuration.NextTurn)
                    {
                        owner.NextTurnSuperArmorBonus += Amount;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [다음 턴 예약] {owner.Name}: 다음 턴 슈퍼아머 +{Amount} 예약됨");
                    }
                    else // ThisTurn
                    {
                        owner.SuperArmorBonus += Amount;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [슈퍼아머] {owner.Name} 슈퍼아머 +{Amount} (이번 라운드 합계: {owner.SuperArmorBonus})");
                    }
                    break;
                /*
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
                    break;*/

                case BuffType.Firepower:
                    if (_duration == EffectDuration.Stack) // 스택형 화력 지원 (1회성)
                    {
                        owner.StackFirepowers.Add(Amount); // 리스트에 개별 화력으로 장전
                        EventManager.OnLogMessage?.Invoke(
                            $"  [스택 화력] {owner.Name} 스택 화력({Amount}) 장전 (현재 대기: {owner.StackFirepowers.Count}개)");
                    }
                    else if (_duration == EffectDuration.NextTurn)
                    {
                        owner.NextTurnFirepowerBonus += Amount;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [다음 턴 예약] {owner.Name}: 다음 라운드 화력 +{Amount} 예약됨");
                    }
                    else // ThisTurn
                    {
                        owner.FirepowerBonus += Amount;
                        EventManager.OnLogMessage?.Invoke(
                            $"  [화력] {owner.Name} 화력 +{Amount} (이번 라운드 합계: {owner.FirepowerBonus})");
                    }
                    break;

                case BuffType.Invincible:
                    if (_duration == EffectDuration.Stack)
                    {
                        // ★ 개선: 스택형 무적은 카드의 '발동 원천(Source)'을 기록하여 저장합니다.
                        // 나중에 DamageResolver에서 이 리스트(Queue)의 카드를 소모하며 데미지를 무효화합니다.
                        Card sourceCard = context.ActivePlayer.PlayingCard;
                        owner.StackInvincibilities.Add(sourceCard ?? new Card { Name = "알 수 없는 스택 무적" });

                        EventManager.OnLogMessage?.Invoke(
                            $"  [스택 무적] {owner.Name} 1회성 스택 무적 장전 (현재 대기: {owner.StackInvincibilities.Count}개)");
                    }
                    else if (_duration == EffectDuration.NextTurn)
                    {
                        owner.NextTurnIsInvincible = true;
                        EventManager.OnLogMessage?.Invoke($"  [다음 턴 예약] {owner.Name}: 다음 라운드 무적 예약됨");
                    }
                    else // ThisTurn
                    {
                        owner.IsInvincible = true;
                        EventManager.OnLogMessage?.Invoke($"  [무적] {owner.Name} 이번 라운드 무적 상태");
                    }
                    break;

                case BuffType.CounterAttack:
                    if (_duration == EffectDuration.Stack)
                    {
                        // 스택으로 발동하는 반격은 '상태(HasCounterAttack)'를 영구적으로 켜는 것이 아니라,
                        // 지금 들어오는 1회의 공격에 대해 즉발성으로 반격을 '예약'하는 것입니다.
                        Card sourceCard = context.ActivePlayer.PlayingCard;
                        owner.StackCounterAttacks.Add(sourceCard);

                        EventManager.OnLogMessage?.Invoke(
                            $"  [스택 반격] {owner.Name} 즉발 스택 반격 장전 (현재 대기: {owner.StackCounterAttacks.Count}개)");

                        // 보상 처리 (마하 10 등에 보상이 있다면)
                        if (_rewardOnSuccess == "SelfToResource" && sourceCard != null)
                        {
                            owner.PendingCounterRewards.Enqueue(() =>
                            {
                                if (owner.Graveyard.Contains(sourceCard))
                                {
                                    owner.ExtractCard(ZoneType.Graveyard, sourceCard);
                                    owner.InsertCard(ZoneType.ResourceZone, sourceCard);
                                    EventManager.OnCardMove?.Invoke(sourceCard, owner, ZoneType.Graveyard, owner, ZoneType.ResourceZone);
                                    EventManager.OnLogMessage?.Invoke($"  ✨ [반격 성공 보상] '{sourceCard.Name}'이(가) 자원존으로 이동했습니다!");
                                }
                            });
                        }
                    }
                    else if (_duration == EffectDuration.NextTurn)
                    {
                        owner.NextTurnCounterAttack = true;
                        EventManager.OnLogMessage?.Invoke($"  [다음 턴 예약] {owner.Name}: 다음 라운드 반격 예약됨");
                    }
                    else // ThisTurn
                    {
                        owner.HasCounterAttack = true;
                        EventManager.OnLogMessage?.Invoke($"  [반격] {owner.Name} 이번 라운드 반격 상태!");

                        Card sourceCard = context.ActivePlayer.PlayingCard;
                        if (_rewardOnSuccess == "SelfToResource" && sourceCard != null)
                        {
                            owner.PendingCounterRewards.Enqueue(() =>
                            {
                                if (owner.Graveyard.Contains(sourceCard))
                                {
                                    owner.ExtractCard(ZoneType.Graveyard, sourceCard);
                                    owner.InsertCard(ZoneType.ResourceZone, sourceCard);
                                    EventManager.OnCardMove?.Invoke(sourceCard, owner, ZoneType.Graveyard, owner, ZoneType.ResourceZone);
                                    EventManager.OnLogMessage?.Invoke($"  ✨ [반격 성공 보상] '{sourceCard.Name}'이(가) 자원존으로 이동했습니다!");
                                }
                            });
                        }
                    }
                    break;
                /*
                case BuffType.CounterAttack:
                    owner.HasCounterAttack = true;
                    EventManager.OnLogMessage?.Invoke(
                        $"  [반격] {owner.Name} 이번 라운드 반격 상태!");

                    // 반격을 부여한 카드 정보 저장
                    Card sourceCard = context.ActivePlayer.PlayingCard;

                    // JSON에 보상이 명시되어 있다면, 큐에 행동(Action)을 장전합니다.
                    if (_rewardOnSuccess == "SelfToResource" && sourceCard != null)
                    {
                        owner.PendingCounterRewards.Enqueue(() =>
                        {
                            // 이 람다식은 미래에 반격이 성공했을 때 실행됩니다.
                            // 이미 카드가 폐기존에 가 있으므로, 폐기존에서 찾아옵니다.
                            if (owner.Graveyard.Contains(sourceCard))
                            {
                                owner.ExtractCard(ZoneType.Graveyard, sourceCard);
                                owner.InsertCard(ZoneType.ResourceZone, sourceCard);
                                EventManager.OnCardMove?.Invoke(sourceCard, owner, ZoneType.Graveyard, owner, ZoneType.ResourceZone);
                                EventManager.OnLogMessage?.Invoke($"  ✨ [반격 성공 보상] '{sourceCard.Name}'이(가) 자원존으로 이동했습니다!");
                            }
                        });
                    }
                    break;*/
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
