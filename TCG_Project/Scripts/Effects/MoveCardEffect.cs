using System;
using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Interfaces;
using TCG_Project.Scripts.Managers;
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

        private string triggerCondition; // [신규] 발동 조건
        private int prizeOnKill;         // [신규] 처치 시 승점

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

            // 효과 발동 조건, 승점 파라미터 로드
            if (parameters.ContainsKey("triggerCondition"))
                triggerCondition = parameters["triggerCondition"].ToString();

            if (parameters.ContainsKey("prizeOnKill"))
                prizeOnKill = int.Parse(parameters["prizeOnKill"].ToString());
        }

        public void Execute(GameContext context, Action onComplete)
        {
            // 발동 조건(triggerCondition) 재확인
            if (!string.IsNullOrEmpty(triggerCondition))
            {
                // ConditionEvaluator를 사용하여 조건 체크 (예: EnemyUnitExist)
                if (!ConditionEvaluator.Evaluate(triggerCondition, context))
                {
                    Console.WriteLine($"🚫 조건 불만족({triggerCondition})으로 효과가 취소되었습니다.");
                    return;
                }
            }

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
                // 되돌리기 효과: "원래 주인에게 이동" -> 아직 구현 중
                var revertEffect = new MoveCardEffect();

                // 여기서 중요한 건 "방금 옮긴 그 카드"를 정확히 찍어야 합니다.
                // 따라서 TargetSelector가 "SpecificCards"를 지원하거나, 
                // 임시로 해당 카드들의 ID를 이용한 필터링 쿼리를 만들어야 합니다.

                // [심화 구현] 실제로는 카드의 Instance ID(GUID)를 쓰는 게 제일 정확합니다.
                // 여기서는 개념적으로 "방금 이동한 카드들"을 되돌린다고 가정합니다.

                // 되돌릴 타겟 리스트 생성 (JSON 아님, 직접 주입 방식이 필요)
                // 이 부분은 ICardEffect 인터페이스의 한계로 인해, 
                // "특정 카드를 타겟으로 하는 MoveCardEffect"를 동적으로 생성하는 팩토리 메서드가 필요합니다.

                // (약식 구현: 소유권 복구 로직 활용)
                // 묘지로 보내거나 덱으로 보내는 게 아니라 "원래 주인 손패"로 보낸다면:
                var revertParams = new Dictionary<string, object>
                {
                    { "src", destZone },        // 현재 위치 (내 필드/패)
                    { "dest", srcZone },        // 원래 위치 (상대 필드/패)
                    { "srcTarget", "Self" },    // 지금 나한테 있으니까
                    { "destTarget", "Opponent" }, // 돌려줄 놈
                    { "count", 1 },
                    // ★ 중요: 아무거나 돌려주면 안 되고 "뺏어온 그 놈"이어야 함.
                    // 이를 위해선 'movedCards' 리스트를 별도로 관리하거나
                    // PendingEffect가 'List<Card>'를 직접 들고 있어야 함.
                };

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

                        // Controller 속성보다 '실제 위치'를 우선합니다.
                        // 훔친 카드를 버릴 때, Controller가 갱신 안 됐어도 실제 위치(FindOwnerOfCard)는 정확합니다.
                        Player currentHolder = FindOwnerOfCard(cardToMove, context);

                        if (currentHolder != null)
                        {
                            // 이동하기 전에 값을 먼저 캡처합니다!
                            // 묘지로 가면 Reset되어서 코스트 정보를 잃어버리기 때문.
                            int capturedValue = GetStatValue(cardToMove);

                            bool isMoved = ProcessMove(currentHolder, destPlayer, cardToMove);
                            // 3. 카드를 다시 읽지 말고, 아까 캡처해둔 값을 더함!
                            if (isMoved)
                            {
                                totalRecordedValue += capturedValue;
                            }
                        }
                    }
                }
            }
            // CASE B: 타겟이 '플레이어'인 경우 (일반 이동/드로우)
            else
            {
                // N:N 매칭 로직 (All -> All 드로우 지원)
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
                        
                        InsertCardToDest(dPlayer, cardToMove);

                        RecordStat(cardToMove, ref totalRecordedValue);
                    }
                }
            }
        }

        // 기록할 값을 추출하는 메서드
        private int GetStatValue(Card card)
        {
            if (string.IsNullOrEmpty(recordStatParam)) return 1; // 기본: 개수(1)
            else if (recordStatParam == "Cost") return card.Cost; // 코스트
            // 추후 Power, HP 등 추가 가능
            return 0;
        }

        // 카드 이동 단계, 성공 여부(bool) 반환
        public bool ProcessMove(Player srcPlayer, Player destPlayer, Card card)
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
                // [디버깅] 이동 로그
                DebugHelper.LogEffect("Move Card", $"{card.Name}: {srcZone} -> {destZone} ({destPlayer.Name})");
                
                Console.WriteLine($"🚚 [지정 이동] {card.Name}: {srcPlayer.Name}({srcZone}) -> {finalDestPlayer.Name}({destZone}) {(needReset ? "[Reset]" : "")}");
                EventManager.OnCardMove?.Invoke(card, srcPlayer, srcZone, finalDestPlayer, destZone);
                return true;
            }
            else
            {
                Console.WriteLine($"[Error] 이동 실패! '{card.Name}'을(를) {srcPlayer.Name}의 {srcZone}에서 찾을 수 없습니다.");
                return false;
            }
        }

        public void InsertCardToDest(Player destPlayer, Card card, bool isSilent = false)
        {
            bool success = destPlayer.InsertCard(destZone, card);
            if (success && !isSilent)
            {
                Console.WriteLine($"🚚 [이동] {card.Name} -> {destPlayer.Name}({destZone})");
                EventManager.OnCardDraw?.Invoke(card , destPlayer, destZone);
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

        // 내부 시스템용 효과 (JSON으로 안 만듦)
        public class RevertControlEffect : ICardEffect
        {
            private List<Card> cardsToRevert;
            private Player originalOwner;
            private ZoneType returnZone;

            public RevertControlEffect(List<Card> cards, Player owner, ZoneType zone)
            {
                cardsToRevert = cards;
                originalOwner = owner;
                returnZone = zone;
            }

            public void Initialize(Dictionary<string, object> parameters) { } // 미사용

            public void Execute(GameContext context, Action onComplete)
            {
                foreach (var card in cardsToRevert)
                {
                    // 현재 컨트롤러(나)에게서 -> 원래 주인(owner)에게로
                    Player currentController = card.Controller;

                    if (currentController != null && currentController.ExtractCard(ZoneType.Field, card)) // 필드라고 가정
                    {
                        card.ResetState(); // 상태 초기화 (주인 복귀)
                        originalOwner.InsertCard(returnZone, card);
                        Console.WriteLine($"↩️ [만료] {card.Name}의 컨트롤이 {originalOwner.Name}에게 돌아갑니다.");
                    }
                }
                onComplete?.Invoke(); // 효과 종료 알림
            }
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