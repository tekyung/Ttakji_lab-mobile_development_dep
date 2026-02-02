using System;
using System.Collections.Generic;
using System.Linq; // FirstOrDefault 사용
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Systems;

namespace TCG_Project.Scripts.Effects
{
    public class MoveCardEffect : ICardEffect
    {
        private ZoneType srcZone;
        private ZoneType destZone;
        private object srcTargetParam;
        private object destTargetParam;
        private object countParam;

        public void Initialize(Dictionary<string, object> parameters)
        {
            // 1. Zone 파싱 (안전장치)
            srcZone = parameters.ContainsKey("src") ? Enum.Parse<ZoneType>(parameters["src"].ToString()) : ZoneType.Deck;
            destZone = parameters.ContainsKey("dest") ? Enum.Parse<ZoneType>(parameters["dest"].ToString()) : ZoneType.Hand;

            // 2. 타겟 파싱 (Shorthand 지원)
            object commonTarget = parameters.ContainsKey("target") ? parameters["target"] : null;

            srcTargetParam = parameters.ContainsKey("srcTarget") ? parameters["srcTarget"] : (commonTarget ?? "Self");
            destTargetParam = parameters.ContainsKey("destTarget") ? parameters["destTarget"] : (commonTarget ?? "Self");

            // 3. 개수
            if (parameters.ContainsKey("count")) countParam = parameters["count"];
            else if (parameters.ContainsKey("amount")) countParam = parameters["amount"];
            else countParam = 1;
        }

        public void Execute(GameContext context)
        {
            int count = FormulaEvaluator.Evaluate(countParam, context);

            // [핵심 변경 1] TargetEvaluator -> TargetSelector로 업그레이드!
            // 이제 JSON 객체로 된 타겟 정보도 해석할 수 있습니다.
            List<Target> srcTargets = TargetSelector.Select(srcTargetParam, context);
            List<Target> destTargets = TargetSelector.Select(destTargetParam, context);

            if (srcTargets.Count == 0 || destTargets.Count == 0) return;

            // 목적지(Destination)는 보통 플레이어입니다. (누구 묘지로 보낼지 등)
            // 만약 destTarget이 카드로 잡히면, 그 카드의 주인(Controller)을 찾습니다.
            Player destPlayer = GetPlayerFromTarget(destTargets[0], context);
            if (destPlayer == null) return;

            // [핵심 변경 2] 소스 타겟의 타입(Player vs Card)에 따른 분기 처리

            // CASE A: 타겟이 '카드'인 경우 (예: 저격 - 이미 특정 카드가 선택됨)
            if (srcTargets[0].Type == TargetType.Card)
            {
                foreach (Target t in srcTargets)
                {
                    if (t.Type == TargetType.Card)
                    {
                        Card cardToMove = t.CardVal;
                        Player owner = FindOwnerOfCard(cardToMove, context); // 카드의 주인을 찾음

                        if (owner != null)
                        {
                            MoveSpecificCard(owner, destPlayer, cardToMove);
                        }
                    }
                }
            }
            // CASE B: 타겟이 '플레이어'인 경우 (예: 드로우 - 덱에서 아무거나 꺼냄)
            else
            {
                // 드로우처럼 "N개"를 수행해야 하는 경우
                Player srcPlayer = srcTargets[0].PlayerVal;

                for (int i = 0; i < count; i++)
                {
                    // 기존 로직: 존에서 Top/Random 전략으로 추출
                    Card cardToMove = srcPlayer.ExtractCard(srcZone, "Top");
                    if (cardToMove == null) break;

                    InsertCardToDest(destPlayer, cardToMove);
                }
            }
        }

        // --- 헬퍼 메서드 ---
        // 특정 카드를 강제로 이동 (저격 등)
        private void MoveSpecificCard(Player srcPlayer, Player destPlayer, Card card)
        {
            // 1. 공간 체크
            if (!destPlayer.HasSpaceInZone(destZone))
            {
                Console.WriteLine($"🚫 [이동 중단] {destPlayer.Name}의 {destZone} 공간 부족.");
                return;
            }

            // 2. 지정된 카드 추출
            // [수정] 실패 시 로그 추가
            if (srcPlayer.ExtractCard(srcZone, card))
            {
                // isSilent: true를 전달하여 중복 로그 방지!
                InsertCardToDest(destPlayer, card, isSilent: true);
                Console.WriteLine($"🚚 [지정 이동] {card.Name}: {srcPlayer.Name}({srcZone}) -> {destPlayer.Name}({destZone})");
            }
            else
            {
                // [신규] 카드는 찾았는데 꺼내지 못했을 때 경고
                Console.WriteLine($"[Error] 이동 실패! '{card.Name}'을(를) {srcPlayer.Name}의 '{srcZone}'에서 찾을 수 없습니다.");
                Console.WriteLine($"   -> 힌트: JSON에 \"src\": \"Hand\"가 빠져있는지 확인하세요.");
            }
        }

        // 카드를 목적지에 삽입
        // [변경] isSilent 파라미터 추가 (기본값 false)
        private void InsertCardToDest(Player destPlayer, Card card, bool isSilent = false)
        {
            bool success = destPlayer.InsertCard(destZone, card);

            // 성공했고, 조용히 하라는 명령이 없을 때만 로그 출력
            if (success && !isSilent)
            {
                Console.WriteLine($"🚚 [이동] {card.Name} -> {destPlayer.Name}({destZone})");
            }
        }

        // 타겟에서 플레이어 정보 추출
        private Player GetPlayerFromTarget(Target t, GameContext context)
        {
            if (t.Type == TargetType.Player) return t.PlayerVal;
            if (t.Type == TargetType.Card) return FindOwnerOfCard(t.CardVal, context);
            return null;
        }

        // 카드의 주인 찾기 (Context 뒤지기)
        private Player FindOwnerOfCard(Card card, GameContext context)
        {
            foreach (var p in context.Players)
            {
                if (p.Hand.Contains(card) || p.Field.Contains(card) || p.Deck.Contains(card) || p.Graveyard.Contains(card))
                    return p;
            }
            return null;
        }
    }
}