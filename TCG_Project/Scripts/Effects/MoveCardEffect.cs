using System;
using System.Collections.Generic;
using System.Linq;
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

        private string outVarParam;
        private string recordStatParam;
        private string operationParam;
        private object destCountParam; // [신규] 상대방(목적지)에서 가져올 수량 (수식 가능)

        public void Initialize(Dictionary<string, object> parameters)
        {
            srcZone = parameters.ContainsKey("src") ? Enum.Parse<ZoneType>(parameters["src"].ToString()) : ZoneType.Deck;
            destZone = parameters.ContainsKey("dest") ? Enum.Parse<ZoneType>(parameters["dest"].ToString()) : ZoneType.Hand;

            object commonTarget = parameters.ContainsKey("target") ? parameters["target"] : null;
            srcTargetParam = parameters.ContainsKey("srcTarget") ? parameters["srcTarget"] : (commonTarget ?? "Self");
            destTargetParam = parameters.ContainsKey("destTarget") ? parameters["destTarget"] : (commonTarget ?? "Self");

            if (parameters.ContainsKey("count")) countParam = parameters["count"];
            else if (parameters.ContainsKey("amount")) countParam = parameters["amount"];
            else countParam = 1;

            if (parameters.ContainsKey("outVar")) outVarParam = parameters["outVar"].ToString();
            if (parameters.ContainsKey("recordStat")) recordStatParam = parameters["recordStat"].ToString();
            operationParam = parameters.ContainsKey("operation") ? parameters["operation"].ToString() : "Move";

            // [신규] destCount가 없으면 count(1:1 교환)를 따라감
            if (parameters.ContainsKey("destCount")) destCountParam = parameters["destCount"];
            else if (parameters.ContainsKey("destAmount")) destCountParam = parameters["destAmount"];
            else destCountParam = countParam;
        }

        public void Execute(GameContext context)
        {
            int totalRecordedValue = 0;
            int count = FormulaEvaluator.Evaluate(countParam, context);

            List<Target> srcTargets = TargetSelector.Select(srcTargetParam, context);
            List<Target> destTargets = TargetSelector.Select(destTargetParam, context);

            if (srcTargets.Count == 0 || destTargets.Count == 0) return;

            if (operationParam == "Swap")
            {
                ExecuteSwap(srcTargets, destTargets, context);
            }
            else
            {
                ExecuteMove(srcTargets, destTargets, count, ref totalRecordedValue, context);
            }

            if (!string.IsNullOrEmpty(outVarParam))
            {
                context.SetVariable(outVarParam, totalRecordedValue);
            }
        }

        private void ExecuteMove(List<Target> srcTargets, List<Target> destTargets, int count, ref int totalRecordedValue, GameContext context)
        {
            // CASE A: 타겟이 '카드'인 경우 (지정 이동)
            if (srcTargets[0].Type == TargetType.Card)
            {
                // 목적지 플레이어 결정 (보통 1명)
                Player destPlayer = GetPlayerFromTarget(destTargets[0], context);
                if (destPlayer == null) return;

                foreach (Target t in srcTargets)
                {
                    if (t.Type == TargetType.Card)
                    {
                        Card cardToMove = t.CardVal;

                        // [핵심 수정] Controller 속성보다 '실제 위치'를 우선합니다.
                        // 훔친 카드를 버릴 때, Controller가 갱신 안 됐어도 실제 위치(FindOwnerOfCard)는 정확합니다.
                        Player currentHolder = FindOwnerOfCard(cardToMove, context);

                        if (currentHolder != null)
                        {
                            bool isMoved = ProcessMove(currentHolder, destPlayer, cardToMove);
                            if (isMoved) RecordStat(cardToMove, ref totalRecordedValue);
                        }
                    }
                }
            }
            // CASE B: 타겟이 '플레이어'인 경우 (일반 이동/드로우)
            else
            {
                // [핵심 수정] N:N 매칭 로직 (All -> All 드로우 지원)
                // 소스와 목적지 리스트의 최대 길이만큼 반복하며 짝을 맞춥니다.
                int iterations = Math.Max(srcTargets.Count, destTargets.Count);

                for (int i = 0; i < iterations; i++)
                {
                    // 인덱스가 넘치면 0번부터 다시 순회 (Wrap-around)
                    // 예: src(1명) -> dest(2명)이면 src[0]이 두 번 쓰임 (분배)
                    // 예: src(2명) -> dest(2명)이면 src[0]->dest[0], src[1]->dest[1] (각자 드로우)
                    Player sPlayer = srcTargets[i % srcTargets.Count].PlayerVal;
                    Player dPlayer = destTargets[i % destTargets.Count].PlayerVal;

                    for (int k = 0; k < count; k++)
                    {
                        Card cardToMove = sPlayer.ExtractCard(srcZone, "Top");
                        if (cardToMove == null) break;

                        // 일반 이동은 ProcessMove 대신 InsertCardToDest 사용 (ProcessMove 써도 되지만 로그 제어 위해)
                        // 단, ProcessMove와 달리 여기선 초기화 로직을 수동으로 호출하거나
                        // 단순 드로우/생성이라 가정하고 넘어갑니다. (드로우는 초기화 불필요)
                        InsertCardToDest(dPlayer, cardToMove);

                        RecordStat(cardToMove, ref totalRecordedValue);
                    }
                }
            }
        }

        // [수정] 성공 여부(bool) 반환
        private bool ProcessMove(Player srcPlayer, Player destPlayer, Card card)
        {
            // 1. 룰 보정 (묘지행일 경우 원래 주인 묘지로)
            Player finalDestPlayer = destPlayer;
            if (destZone == ZoneType.Graveyard)
            {
                finalDestPlayer = card.OriginalOwner ?? destPlayer;
            }

            // 2. 공간 체크
            if (!finalDestPlayer.HasSpaceInZone(destZone))
            {
                Console.WriteLine($"🚫 [이동 중단] {finalDestPlayer.Name}의 {destZone} 공간 부족.");
                return false;
            }

            // 3. 추출 및 이동
            if (srcPlayer.ExtractCard(srcZone, card))
            {
                bool needReset = CheckIfResetNeeded(srcZone, destZone);

                if (needReset) card.ResetState();
                else card.Controller = finalDestPlayer; // 컨트롤러 갱신

                InsertCardToDest(finalDestPlayer, card, isSilent: true);

                Console.WriteLine($"🚚 [지정 이동] {card.Name}: {srcPlayer.Name}({srcZone}) -> {finalDestPlayer.Name}({destZone}) {(needReset ? "[Reset]" : "")}");
                return true;
            }
            else
            {
                Console.WriteLine($"[Error] 이동 실패! '{card.Name}'을(를) {srcPlayer.Name}의 {srcZone}에서 찾을 수 없습니다.");
                return false;
            }
        }

        private void InsertCardToDest(Player destPlayer, Card card, bool isSilent = false)
        {
            bool success = destPlayer.InsertCard(destZone, card);
            if (success && !isSilent)
            {
                Console.WriteLine($"🚚 [이동] {card.Name} -> {destPlayer.Name}({destZone})");
            }
        }
        
        private void RecordStat(Card card, ref int totalValue)
        {
            if (string.IsNullOrEmpty(recordStatParam)) totalValue++;
            else if (recordStatParam == "Cost") totalValue += card.Cost;
        }

        // [수정] N:M 교환 로직
        private void ExecuteSwap(List<Target> srcTargets, List<Target> destTargets, GameContext context)
        {
            if (srcTargets.Count == 0 || destTargets.Count == 0) return;

            Player p1 = srcTargets[0].PlayerVal;
            Player p2 = destTargets[0].PlayerVal;
            if (p1 == null || p2 == null) return;

            // 1. 수량 계산 (N : M)
            int count1 = FormulaEvaluator.Evaluate(countParam, context);     // 내가 줄 개수
            int count2 = FormulaEvaluator.Evaluate(destCountParam, context); // 걔가 줄 개수

            // 2. 카드 추출 (아직 넣지 않고 들고 있음)
            List<Card> cardsFromP1 = ExtractCardsSafe(p1, srcZone, count1);
            List<Card> cardsFromP2 = ExtractCardsSafe(p2, destZone, count2);

            // 3. 교차 삽입
            // P1 -> P2
            foreach (Card c in cardsFromP1)
            {
                c.Controller = p2;
                p2.InsertCard(destZone, c);
            }
            // P2 -> P1
            foreach (Card c in cardsFromP2)
            {
                c.Controller = p1;
                p1.InsertCard(srcZone, c);
            }

            Console.WriteLine($"🔄 [교환] {p1.Name}({cardsFromP1.Count}장) <-> {p2.Name}({cardsFromP2.Count}장)");
        }

        // [헬퍼] 안전하게 N장 꺼내는 메서드
        private List<Card> ExtractCardsSafe(Player p, ZoneType zone, int n)
        {
            List<Card> extracted = new List<Card>();
            for (int i = 0; i < n; i++)
            {
                Card c = p.ExtractCard(zone, "Top"); // 혹은 Random, Manual 등 전략 적용 가능
                if (c != null) extracted.Add(c);
                else break;
            }
            return extracted;
        }

        private List<Card> GetCardsInZone(Player p, ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Hand: return p.Hand;
                case ZoneType.Field: return p.Field.Where(c => c != null).ToList();
                case ZoneType.Deck: return p.Deck;
                default: return new List<Card>();
            }
        }

        private void ClearZone(Player p, ZoneType zone)
        {
            switch (zone)
            {
                case ZoneType.Hand: p.Hand.Clear(); break;
                case ZoneType.Field: Array.Clear(p.Field, 0, p.Field.Length); break;
            }
        }

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
    }
}