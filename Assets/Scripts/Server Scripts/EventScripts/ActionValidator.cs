using System;
using System.Collections.Generic;
using TCG_Project.Scripts.Core; 
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using UnityEngine;

namespace ServerScripts.EventScripts
{
    public class ActionValidator : MonoBehaviour
    {

        // ==========================================================
        // ─── 2 턴 진행 관련 유효성 검사 ───
        // ==========================================================
        public static bool ValidateOnTurnStart(int turn, string subject, Player currentPlayer, bool isGameOver, out string error)
        {
            if (turn <= 0)
            {
                error = "TurnStart 실패: 턴 수가 올바르지 않습니다.";
                return false;
            }
            if (string.IsNullOrEmpty(subject))
            {
                error = "TurnStart 실패: 플레이어 이름이 비어 있습니다.";
                return false;
            }
            if (isGameOver)
            {
                error = "TurnStart 실패: 이미 종료된 게임입니다.";
                return false;
            }
            if (currentPlayer == null || currentPlayer.Name != subject)
            {
                error = "TurnStart 실패: 현재 턴 플레이어와 이름이 일치하지 않습니다.";
                return false;
            }
            error = null;
            return true;
        }
        public static bool ValidateOnTurnEnd(string playerName, Player currentPlayer, bool isTurnEnded, bool isGameOver, out string error)
        {
            if (string.IsNullOrEmpty(playerName))
            {
                error = "TurnEnd 실패: 플레이어 이름이 비어 있습니다.";
                return false;
            }
            if (isGameOver)
            {
                error = "TurnEnd 실패: 이미 종료된 게임입니다.";
                return false;
            }
            if (currentPlayer == null || currentPlayer.Name != playerName)
            {
                error = "TurnEnd 실패: 현재 턴 플레이어와 이름이 일치하지 않습니다.";
                return false;
            }
            if (isTurnEnded)
            {
                error = "TurnEnd 실패: 이미 이 턴은 종료되었습니다.";
                return false;
            }
            error = null;
            return true;
        }

        // ==========================================================
        // ─── 2-1. 룰북 6페이즈 이벤트 ───
        // ==========================================================
        private static bool PhaseCheck(string subject, GameContext context, GamePhase expectedPhase, out string error, out Player player)
        {
            player = null;
            if (context == null) { error = "게임 상태(Context)가 존재하지 않습니다."; return false; }
            if (context.IsGameOver) { error = "게임이 이미 종료되었습니다."; return false; }

            // 페이즈가 맞는지 확인
            if (context.CurrentPhase != expectedPhase)
            {
                error = $"현재 {expectedPhase} 상태가 아닙니다! (현재: {context.CurrentPhase})";
                return false;
            }

            player = context.Players.Find(p => p.Name == subject);
            if (player == null) { error = $"[{subject}] 플레이어를 찾을 수 없습니다."; return false; }

            error = null;
            return true;
        }
        public static bool ValidateOnResourcePhase(string subject, int turn, GameContext context, out string error)
        {
            if (!PhaseCheck(subject, context, GamePhase.ResourcePhase, out error, out Player p)) return false;

            // 자원덱이 비어있는지 확인 (비어있으면 더 이상 자원을 받을 수 없음)
            if (p.ResourceDeck.Count == 0)
            {
                error = "자원 덱이 고갈되어 더 이상 자원을 추가할 수 없습니다.";
                return false;
            }

            return true;
        }

        /// <summary>드로우 페이즈 시작: 메인덱 패 1장 드로우 직전 검증</summary>
        public static bool ValidateOnDrawPhase(string subject, int turn, GameContext context, out string error)
        {
            if (!PhaseCheck(subject, context, GamePhase.DrawPhase, out error, out Player p)) return false;

            if (p.Deck.Count == 0)
            {
                error = "메인 덱이 고갈되어 카드를 뽑을 수 없습니다.";
                return false;
            }

            return true;
        }

        /// <summary>세트 페이즈 시작: 양측 카드 세트 직전 검증</summary>
        public static bool ValidateOnSetPhase(string subject, int turn, string cardInstanceId, GameContext context, out string error)
        {
            if (!PhaseCheck(subject, context, GamePhase.SetPhase, out error, out Player p)) return false;

            if (p.Hand.Count == 0)
            {
                error = "패에 카드가 없어 세트할 수 없습니다.";
                return false;
            }

            if (p.SetZoneCard != null)
            {
                error = "이미 세트존에 카드가 배치되어 있습니다.";
                return false;
            }

            Card cardToSet = p.Hand.Find(c => c.InstanceId == cardInstanceId);
            if (cardToSet == null)
            {
                error = "세트하려는 카드가 내 패에 존재하지 않습니다!";
                return false;
            }

            return true;
        }

        /// <summary>오픈 페이즈 시작: 공개/폐기 선택 직전 검증</summary>
        public static bool ValidateOnOpenPhase(string subject, int turn, OpenPhaseChoice choice, GameContext context, out string error)
        {
            if (!PhaseCheck(subject, context, GamePhase.OpenPhase, out error, out Player p)) return false;

            else if (p.SetZoneCard == null)
            {
                error = "세트존에 카드가 없어서 공개/폐기를 선택할 수 없습니다.";
                return false;
            }

            else if (choice != OpenPhaseChoice.Open && choice != OpenPhaseChoice.Abandon)
            {
                error = "유효하지 않은 선택입니다. (Open 또는 Abandon만 가능)";
                return false;
            }

            return true;
        }

        /// <summary>메인 페이즈 시작: SpeedResolver 처리 직전 검증</summary>
        public static bool ValidateOnMainPhase(string subject, int turn, GameContext context, out string error)
        {
            if (!PhaseCheck(subject, context, GamePhase.MainPhase, out error, out Player p)) return false;

            if (p.SetZoneCard == null)
            {
                error = "세트된 카드가 없습니다.";
                return false;
            }
            return true;
        }

        /// <summary>엔드 페이즈 시작: 승리 조건 확인 및 버프 만료 직전 검증</summary>
        public static bool ValidateOnEndPhase(string subject, int turn, GameContext context, out string error)
        {
            if (!PhaseCheck(subject, context, GamePhase.EndPhase, out error, out Player p)) return false;

            return true;
        }

        // ==========================================================
        // ─── 3. 매치(다판제) 판정 유효성 검사 ───
        // ==========================================================

        public static bool ValidateOnGameStart(Player p1, Player p2, GameContext context, bool isGameRunning)
        {
            if (p1 == null || p2 == null)
            {
                EventManager.OnLogMessage?.Invoke("플레이어 데이터 Null");
                return false;
            }

            if (context == null || !context.Players.Contains(p1) || !context.Players.Contains(p2))
            {
                EventManager.OnLogMessage?.Invoke("세션에 없는 플레이어");
                return false;
            }

            if (isGameRunning)
            {
                EventManager.OnLogMessage?.Invoke("이미 시작됨");
                return false;
            }
            return true;
        }

        public static bool ValidateOnGameSet(Player winner, GameContext context, bool isAlreadyFinished, out string error)
        {
            // 1) null 검사
            if (winner == null)
            {
                error = "GameSet 실패: 승리 플레이어 정보가 비어 있습니다.";
                return false;
            }
            // 2) 이미 끝난 게임인지
            if (isAlreadyFinished)
            {
                error = "GameSet 실패: 이미 종료된 게임에서 다시 GameSet을 호출했습니다.";
                return false;
            }
            // 3) 참가자 여부 검사 (GameContext를 쓰는 경우)
            if (context != null && context.Players != null && !context.Players.Contains(winner))
            {
                error = "GameSet 실패: 승리자로 전달된 플레이어가 현재 게임 참가자가 아닙니다.";
                return false;
            }
            error = null;
            return true;
        }
        public static bool ValidateOnGameDraw(Player p1, Player p2, int turn, bool isAlreadyFinished,   // context.IsGameOver 같은 값
            out string error)
        {
            // 1) null 검사
            if (p1 == null || p2 == null)
            {
                error = "GameDraw 실패: 플레이어 정보가 비어 있습니다.";
                return false;
            }
            // 2) 같은 사람인지 검사
            if (ReferenceEquals(p1, p2))
            {
                error = "GameDraw 실패: 두 플레이어가 동일 객체입니다.";
                return false;
            }
            // 3) 턴 값 기본 검사
            if (turn <= 0)
            {
                error = "GameDraw 실패: 턴 수가 올바르지 않습니다.";
                return false;
            }
            // 4) 이미 종료된 게임인지
            if (isAlreadyFinished)
            {
                error = "GameDraw 실패: 이미 종료된 게임에서 다시 GameDraw를 호출했습니다.";
                return false;
            }
            // 5) 무승부 조건 검사
            int MaxTurn = 20;
            if (turn < MaxTurn)
            {
                error = $"GameDraw 실패: 무승부 조건(최대 턴 {MaxTurn})을 만족하지 않았습니다. 현재 턴: {turn}";
                return false;
            }
            error = null;
            return true;
        }

        public static bool ValidateOnMatchSet(Player winner, GameContext context, out string error)
        {
            if (context == null)
            {
                error = "게임 상태(Context)가 존재하지 않습니다.";
                return false;
            }

            if (winner == null || !context.Players.Contains(winner))
            {
                error = "유효하지 않은 승자 정보입니다. (방에 존재하지 않는 플레이어)";
                return false;
            }


            error = null;
            return true;
        }

        public static bool ValidateOnMatchDraw(Player p1, Player p2, GameContext context, out string error)
        {
            if (context == null)
            {
                error = "게임 상태(Context)가 존재하지 않습니다.";
                return false;
            }

            if (p1 == null || p2 == null || !context.Players.Contains(p1) || !context.Players.Contains(p2))
            {
                error = "무승부를 판정할 플레이어 정보가 유효하지 않습니다.";
                return false;
            }

            error = null;
            return true;
        }

        // ==========================================================
        // ─── 4. 라이프(체력) 변경 유효성 검사 ───
        // ==========================================================

        public static bool ValidateOnPrizeChange(Player p, int PrizePoints, out string error)
        {
            if (p == null)
            {
                error = "PrizeChange 실패: 플레이어 정보가 비어 있습니다.";
                return false;
            }
            if (PrizePoints < 0)
            {
                error = "PrizeChange 실패: 승점은 0 이상이어야 합니다.";
                return false;
            }
            //if (PrizePoints > GameRules.WinPrizePoints)
            //{
            //    error = $"PrizeChange 실패: 승점이 승리 조건({GameRules.WinPrizePoints})을 초과합니다.";
            //    return false;
            //}
            error = null;
            return true;
        }

        public static bool ValidateOnLifeChange(Player player, int LifeTokens, GameContext context, out string error)
        {
            if (context == null)
            {
                error = "게임 상태(Context)가 존재하지 않습니다.";
                return false;
            }

            if (player == null || !context.Players.Contains(player))
            {
                error = "라이프를 변경할 플레이어 정보가 유효하지 않습니다.";
                return false;
            }

            error = null;
            return true;
        }

        // ==========================================================
        // ─── 5. 전투 및 카드 행동 ───
        // ==========================================================
        public static bool ValidateOnPlayCard(Player p, Card c, GameContext context, out string error)
        {
            // 1) null
            if (p == null || c == null)
            {
                error = "PlayCard 실패: 플레이어 또는 카드 정보가 비어 있습니다.";
                return false;
            }

            // 2) 게임 상태
            if (context == null || context.IsGameOver)
            {
                error = "PlayCard 실패: 유효하지 않은 게임 상태입니다.";
                return false;
            }

            // 3) 게임에 참가한 플레이어인지
            if (context.Players == null || !context.Players.Contains(p))
            {
                error = "PlayCard 실패: 세션에 등록되지 않은 플레이어입니다.";
                return false;
            }

            // 4) 턴/페이즈, 권한
            if (context.ActivePlayer != p)
            {
                error = "PlayCard 실패: 현재 턴 플레이어가 아닙니다.";
                return false;
            }

            // 5) 카드 소유/위치: 실제로 이 플레이어의 패에 있는지
            if (!p.Hand.Contains(c))
            {
                error = "PlayCard 실패: 해당 카드는 플레이어의 패에 존재하지 않습니다.";
                return false;
            }

            // 6) 코스트/커스텀 조건: Card.IsPlayable 사용
            if (!c.IsPlayable(context))
            {
                error = "PlayCard 실패: 코스트 또는 카드 조건을 만족하지 않습니다.";
                return false;
            }

            error = null;
            return true;
        }

        // ==========================================================
        // ─── 6. 카드 이동 ───
        // ==========================================================
        public static bool ValidateOnCardMove(Card c, Player p1, ZoneType z1, Player p2, ZoneType z2, GameContext context, out string error)
        {
            // 1) 기본 null / 컨텍스트 검사
            if (c == null)
            {
                error = "CardMove 실패: 카드 정보가 비어 있습니다.";
                return false;
            }
            if (p1 == null || p2 == null)
            {
                error = "CardMove 실패: 원 소유자 또는 대상 소유자 정보가 비어 있습니다.";
                return false;
            }
            if (context == null || context.IsGameOver)
            {
                error = "CardMove 실패: 유효하지 않은 게임 상태입니다.";
                return false;
            }
            // 2) 게임 참가자 검사
            if (context.Players == null ||
                !context.Players.Contains(p1) ||
                !context.Players.Contains(p2))
            {
                error = "CardMove 실패: 원 소유자 또는 대상 소유자가 현재 게임 참가자가 아닙니다.";
                return false;
            }
            // 3) from 존에 실제로 카드가 있는지
            var fromList = p1.GetZone(z1);
            if (fromList == null || !fromList.Contains(c))
            {
                error = $"CardMove 실패: 지정된 from 존({z1})에 해당 카드가 존재하지 않습니다.";
                return false;
            }
            // 4)  존에 공간이 있는지 (손, 세트존 등 제한된 존)
            if (!p2.HasSpaceInZone(z2))
            {
                error = $"CardMove 실패: 대상 존({z2})에 더 이상 카드를 넣을 수 없습니다.";
                return false;
            }
            // 5) 컨트롤러 일치 (fromOwner가 현재 컨트롤러인 것이 정상)
            if (c.Controller != p1)
            {
                error = "CardMove 실패: 카드의 컨트롤러와 fromOwner가 일치하지 않습니다.";
                return false;
            }
            error = null;
            return true;
        }
        public static bool ValidateOnCardDraw(Card c, Player p, ZoneType z, GameContext context, out string error)
        {
            // 1) 기본 null / 컨텍스트 검사
            if (c == null)
            {
                error = "CardDraw 실패: 카드 정보가 비어 있습니다.";
                return false;
            }
            if (p == null)
            {
                error = "CardDraw 실패: 플레이어 정보가 비어 있습니다.";
                return false;
            }
            if (context == null || context.IsGameOver)
            {
                error = "CardDraw 실패: 유효하지 않은 게임 상태입니다.";
                return false;
            }
            // 2) 게임 참가자 검사
            if (context.Players == null || !context.Players.Contains(p))
            {
                error = "CardDraw 실패: 해당 플레이어가 현재 게임 참가자가 아닙니다.";
                return false;
            }
            // 3) 허용된 Zone 인지
            var allowedToZones = new HashSet<ZoneType>
    {
        ZoneType.Deck,           // 메인덱 (List)
        ZoneType.Hand,           // 패 (List)
        ZoneType.Graveyard,      // 폐기존 (List)
        ZoneType.Field,          // 필드 - 레거시 프로토타입용 (Array)
       
        ZoneType.SetZone,        // 세트존: 매 턴 뒷면으로 올려두는 카드 1장
        ZoneType.ResourceDeck,   // 자원덱: 매 턴 1장씩 자원존으로 공급
        ZoneType.ResourceZone,   // 자원존: 코스트 지불에 사용하는 자원 카드 보관
        ZoneType.StackZone,      // 스택존: 대기 중인 스택 카드 보관 (복수 가능)
        ZoneType.BattlefieldZone,// 전장존: 파괴 전까지 지속되는 전장 카드 (1장)
        ZoneType.PlayBuffer      // 임시 발동 대기 존: ReplayCardEffect 등 복합 효과에서 카드를 잠시 보관
    };
            if (!allowedToZones.Contains(z))
            {
                error = $"CardDraw 실패: 허용되지 않은 도착 존({z})입니다.";
                return false;
            }
            // 4) 도착 존 공간 확인
            if (!p.HasSpaceInZone(z))
            {
                error = $"CardDraw 실패: 대상 존({z})에 더 이상 카드를 넣을 수 없습니다.";
                return false;
            }

            var zoneList = p.GetZone(z);
            if (zoneList == null || !zoneList.Contains(c))
            {
                error = "CardDraw 실패: 드로우 처리 후 대상 존에 해당 카드가 존재하지 않습니다.";
                return false;
            }
            error = null;
            return true;
        }

        // ==========================================================
        // ─── 7. Human Input (상세 UI 선택) 유효성 검사 ───
        // ==========================================================
        // 1. 세트 페이즈 (수정: Action 제거, pickedCard 추가)
        public static bool ValidateOnRequireSetPhaseAction(Player player, GameContext context, Card pickedCard, out string error)
        {
            if (context == null || player == null) { error = "게임 상태나 플레이어 정보가 유효하지 않습니다."; return false; }
            if (context.CurrentPhase != GamePhase.SetPhase) { error = "현재 세트 페이즈가 아닙니다!"; return false; }

            if (pickedCard == null)
            {
                error = "선택된 카드가 없습니다.";
                return false;
            }

            // 그 카드가 진짜 내 '패(Hand)'에 있는 카드인지 확인
            if (!player.Hand.Contains(pickedCard))
            {
                error = "선택한 카드가 현재 패에 존재하지 않습니다";
                return false;
            }

            if (player.SetZoneCard != null)
            {
                error = "이미 세트존에 카드가 배치되어 있습니다.";
                return false;
            }

            error = null;
            return true;
        }

        // 2. 오픈 페이즈 
        public static bool ValidateOnRequireOpenPhaseAction(Player player, Card setCard, int effectiveCost, GameContext context, OpenPhaseChoice choice, out string error)
        {
            if (context == null || player == null) { error = "게임 상태나 플레이어 정보가 유효하지 않습니다."; return false; }
            if (context.CurrentPhase != GamePhase.OpenPhase) { error = "현재 오픈 페이즈가 아닙니다!"; return false; }

            // 유저가 다른 카드를 오픈하겠다고 조작했는지 검사
            if (player.SetZoneCard != setCard)
            {
                error = "세트존에 있는 카드와 오픈하려는 카드의 정보가 일치하지 않습니다!";
                return false;
            }

            if (choice == OpenPhaseChoice.Open)
            {
                // 공개하려면 비용(effectiveCost)을 낼 수 있는지 확인
                if (!player.CanAfford(effectiveCost))
                {
                    error = $"자원이 부족하여 카드를 공개할 수 없습니다. (필요: {effectiveCost})";
                    return false;
                }
            }
            else if (choice != OpenPhaseChoice.Abandon)
            {
                error = "유효하지 않은 선택입니다. (Open 또는 Abandon만 가능)";
                return false;
            }

            error = null;
            return true;
        }

        // 3. 스택 응답 
        public static bool ValidateOnRequireStackResponse(Player player, Card stackCard, Card opponentCard, bool isUsingStack, GameContext context, out string error)
        {
            if (context == null || player == null) { error = "게임 상태나 플레이어 정보가 유효하지 않습니다."; return false; }

            if (isUsingStack)
            {
                if (stackCard == null)
                {
                    error = "스택으로 사용할 카드가 지정되지 않았습니다.";
                    return false;
                }

                // 스택 응답은 실제 스택존에서만 허용합니다.
                bool isInStackZone = player.StackZone.Contains(stackCard);
                if (!isInStackZone)
                {
                    error = "발동하려는 스택 카드가 내 스택존에 존재하지 않습니다";
                    return false;
                }
            }

            error = null;
            return true;
        }

        // 4. 카드 픽 
        public static bool ValidateOnRequireCardPick(Player player, List<Card> pickedCards, int count, List<Card> presentedCards, GameContext context, out string error)
        {
            if (context == null || player == null) { error = "게임 상태나 플레이어 정보가 유효하지 않습니다."; return false; }
            if (pickedCards == null) { error = "선택된 카드 목록이 없습니다."; return false; }

            // 정해진 개수(count)보다 많이 골랐는지 확인 (예외: 제시된 카드가 요구량보다 적을 땐 최대치까지만)
            int maxAllowedPick = Math.Min(count, presentedCards.Count);
            if (pickedCards.Count > maxAllowedPick)
            {
                error = $"카드를 너무 많이 선택했습니다 (최대 {maxAllowedPick}장 가능, 현재 {pickedCards.Count}장 선택)";
                return false;
            }

            // 제시해 준 목록(presentedCards)에 없는 엉뚱한 카드를 훔쳐 가는지 확인
            foreach (var card in pickedCards)
            {
                if (!presentedCards.Contains(card))
                {
                    error = "제시된 카드 목록에 없는 카드가 선택되었습니다";
                    return false;
                }
            }

            error = null;
            return true;
        }

        // 5. 카드 서치
        public static bool ValidateOnRequireCardChoice(Player player, ZoneType zone, int count, string filter, List<Card> pickedCards, GameContext context, out string error)
        {
            if (context == null || player == null) { error = "게임 상태나 플레이어 정보가 유효하지 않습니다."; return false; }
            if (pickedCards == null) { error = "선택된 카드 목록이 없습니다."; return false; }

            // 허용된 개수 초과 검사
            if (pickedCards.Count > count)
            {
                error = $"카드를 너무 많이 선택했습니다! (최대 {count}장 가능)";
                return false;
            }

            error = null;
            return true;
        }


        // ==========================================================
        // ─── 8. 연출 및 딜레이 ───
        // ==========================================================
        public static bool ValidateOnRequestVisualDelay(float seconds, out string error)
        {
            error = null;
            return true;
        }
        // ==========================================================
        // ─── 9. 선택적 행동 ───
        // ==========================================================
        public static bool ValidateOnRequireOptionalAction(Player player, bool choice, string messageOrActionId, GameContext context, out string error)
        {
            if (context == null)
            {
                error = "게임 상태(Context)가 존재하지 않습니다.";
                return false;
            }

            if (player == null || !context.Players.Contains(player))
            {
                error = "응답한 플레이어 정보가 유효하지 않습니다. (방에 존재하지 않는 유저)";
                return false;
            }

            // 비어있는 식별자(Action ID) 차단
            if (string.IsNullOrEmpty(messageOrActionId) || string.IsNullOrWhiteSpace(messageOrActionId))
            {
                error = "선택적 행동의 식별자(Action ID 또는 Message)가 누락되었습니다!";
                return false;
            }

            error = null;
            return true;
        }
    }
}