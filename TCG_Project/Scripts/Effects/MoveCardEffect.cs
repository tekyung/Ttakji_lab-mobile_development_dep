using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Effects
{
    public class MoveCardEffect : ICardEffect
    {
        private Dictionary<string, object> _cachedParams; // 깊은 복사용 캐시

        private ZoneType srcZone;
        private ZoneType destZone;
        private object srcTargetParam;
        private object destTargetParam;
        private object countParam;

        private string triggerCondition;
        private int prizeOnKill;

        public void Initialize(Dictionary<string, object> parameters)
        {
            _cachedParams = parameters; // Clone()을 위한 원본 파라미터 저장

            srcZone = parameters.ContainsKey("src") ? Enum.Parse<ZoneType>(parameters["src"].ToString()) : ZoneType.Deck;
            destZone = parameters.ContainsKey("dest") ? Enum.Parse<ZoneType>(parameters["dest"].ToString()) : ZoneType.Hand;

            object commonTarget = parameters.ContainsKey("target") ? parameters["target"] : null;
            srcTargetParam = parameters.ContainsKey("srcTarget") ? parameters["srcTarget"] : (commonTarget ?? "Self");
            destTargetParam = parameters.ContainsKey("destTarget") ? parameters["destTarget"] : (commonTarget ?? "Self");

            countParam = parameters.ContainsKey("count") ? parameters["count"]
                       : (parameters.ContainsKey("amount") ? parameters["amount"] : 1);

            triggerCondition = parameters.ContainsKey("triggerCondition") ? parameters["triggerCondition"].ToString() : null;
            prizeOnKill = parameters.ContainsKey("prizeOnKill") ? Convert.ToInt32(parameters["prizeOnKill"]) : 0;
        }

        public void Execute(GameContext context, Action onComplete)
        {
            // 1. 발동 조건(triggerCondition) 재확인
            if (!string.IsNullOrEmpty(triggerCondition))
            {
                if (!ConditionEvaluator.Evaluate(triggerCondition, context))
                {
                    EventManager.OnLogMessage?.Invoke($"🚫 조건 불만족({triggerCondition})으로 효과가 취소되었습니다.");
                    onComplete?.Invoke();
                    return;
                }
            }

            int count = FormulaEvaluator.Evaluate(countParam, context);

            // 2. [비동기] Source(출발지) 타겟팅 대기
            TargetSelector.Select(srcTargetParam, context, (srcTargets) =>
            {
                if (srcTargets == null || srcTargets.Count == 0)
                {
                    onComplete?.Invoke();
                    return;
                }

                // 3. [비동기] Destination(도착지) 타겟팅 대기
                TargetSelector.Select(destTargetParam, context, (destTargets) =>
                {
                    if (destTargets == null || destTargets.Count == 0)
                    {
                        onComplete?.Invoke();
                        return;
                    }

                    // 타겟팅 완료 후 실제 이동 처리
                    ExecuteMove(srcTargets, destTargets, count, context);

                    // ★ 모든 물리적 이동이 끝나면 완료 콜백 호출
                    onComplete?.Invoke();
                });
            });
        }

        private void ExecuteMove(List<Target> srcTargets, List<Target> destTargets, int count, GameContext context)
        {
            // CASE A: 특정 카드 지정 파괴(Kill) 또는 탈취
            if (srcTargets[0].Type == TargetType.Card)
            {
                Player destPlayer = GetPlayerFromTarget(destTargets[0], context);
                if (destPlayer == null) return;

                foreach (Target t in srcTargets)
                {
                    if (t.Type == TargetType.Card)
                    {
                        Card cardToMove = t.CardVal;
                        Player currentHolder = FindOwnerOfCard(cardToMove, context);

                        if (currentHolder != null)
                        {
                            ProcessMove(currentHolder, destPlayer, cardToMove, context);
                        }
                    }
                }
            }
            // CASE B: 플레이어 지정 다중 드로우 / 버리기
            else
            {
                int iterations = Math.Max(srcTargets.Count, destTargets.Count);

                for (int i = 0; i < iterations; i++)
                {
                    Player sPlayer = srcTargets[i % srcTargets.Count].PlayerVal;
                    Player dPlayer = destTargets[i % destTargets.Count].PlayerVal;

                    for (int k = 0; k < count; k++)
                    {
                        Card cardToMove = sPlayer.ExtractCard(srcZone, "Top");
                        if (cardToMove == null) break; // 덱 고갈 등 뽑을 카드가 없으면 중단

                        InsertCardToDest(dPlayer, cardToMove);
                    }
                }
            }
        }

        private void ProcessMove(Player srcPlayer, Player destPlayer, Card card, GameContext context)
        {
            // 1. 묘지행 룰 보정 (파괴되면 무조건 원래 주인의 묘지로 간다)
            Player finalDestPlayer = destPlayer;
            if (destZone == ZoneType.Graveyard)
            {
                finalDestPlayer = card.OriginalOwner ?? destPlayer;
            }

            // 2. 도착지 공간 체크
            if (!finalDestPlayer.HasSpaceInZone(destZone))
            {
                EventManager.OnLogMessage?.Invoke($"🚫 [이동 중단] {finalDestPlayer.Name}의 {destZone} 공간 부족.");
                return;
            }

            // 3. 이동 및 승점 계산
            if (srcPlayer.ExtractCard(srcZone, card))
            {
                bool needReset = CheckIfResetNeeded(srcZone, destZone);

                if (needReset) card.ResetState();
                else card.Controller = finalDestPlayer;

                InsertCardToDest(finalDestPlayer, card, isSilent: true);

                // 파괴(묘지행) 시 승점(Prize) 계산
                if (destZone == ZoneType.Graveyard && prizeOnKill > 0)
                {
                    context.ActivePlayer.PrizePoints += prizeOnKill;
                    EventManager.OnLogMessage?.Invoke($"      🏆 [파괴 승점] {context.ActivePlayer.Name}가 {prizeOnKill}점을 얻었습니다! (Total: {context.ActivePlayer.PrizePoints})");
                }

                EventManager.OnLogMessage?.Invoke($"🚚 [지정 이동] {card.Name}: {srcPlayer.Name}({srcZone}) -> {finalDestPlayer.Name}({destZone}) {(needReset ? "[Reset]" : "")}");
                EventManager.OnCardMove?.Invoke(card, srcPlayer, srcZone, finalDestPlayer, destZone);
            }
        }
        // 드로우 등의 단순 이동
        private void InsertCardToDest(Player destPlayer, Card card, bool isSilent = false)
        {
            bool success = destPlayer.InsertCard(destZone, card);
            if (success && !isSilent)
            {
                EventManager.OnLogMessage?.Invoke($"🚚 [이동] {card.Name} -> {destPlayer.Name}({destZone})");
                EventManager.OnCardDraw?.Invoke(card, destPlayer, destZone); // UI 단순 이동 연출용
            }
        }

        // 유틸리티 메서드들
        private Player GetPlayerFromTarget(Target t, GameContext context)
        {
            if (t.Type == TargetType.Player) return t.PlayerVal;
            if (t.Type == TargetType.Card) return FindOwnerOfCard(t.CardVal, context);
            return null;
        }

        private Player FindOwnerOfCard(Card card, GameContext context)
        {
            foreach (var p in context.Players)
            {
                if (p.Hand.Contains(card) || p.Field.Contains(card) || p.Deck.Contains(card) || p.Graveyard.Contains(card))
                    return p;
            }
            return null;
        }

        private bool CheckIfResetNeeded(ZoneType src, ZoneType dest)
        {
            if (dest == ZoneType.Deck || dest == ZoneType.Graveyard) return true;
            if (src == ZoneType.Field && dest == ZoneType.Hand) return true;
            return false;
        }

        // ★ 깊은 복사 구현
        public ICardEffect Clone()
        {
            var clone = new MoveCardEffect();
            clone.Initialize(this._cachedParams);
            return clone;
        }
    }
}