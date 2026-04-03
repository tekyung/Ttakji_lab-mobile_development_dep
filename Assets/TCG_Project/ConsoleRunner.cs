// 룰북 6페이즈 구조 (자원→드로우→세트→오픈→메인→엔드) + 3판 2선승 매치
using System;
using System.Collections.Generic;
using System.Linq;
using TCG_Project.Scripts.Abilities;
using TCG_Project.Scripts.Core;
using TCG_Project.Scripts.Effects;   // BattlefieldEffect, MoveEffect 등
using TCG_Project.Scripts.Manager;
using TCG_Project.Scripts.Managers;
using TCG_Project.Scripts.Systems;
using TCG_Project.Scripts.Utils;

namespace TCG_Project
{
    public class ConsoleRunner
    {
        private static GameDataManager _dataManager;
        private static GameContext context;

        // 오픈 페이즈에서 공개한 카드를 메인 페이즈까지 전달
        private static Card _p1RevealedCard = null;
        private static Card _p2RevealedCard = null;

        // 현재 게임 승자 (MatchManager에서 참조)
        private static Player _currentGameWinner = null;

        // 듀얼 캐릭터: Bot_Red(ELLI + DAINA), Bot_Blue(VERONICA + SONIA)
        private const string P1_CHAR1 = "ELLI-01";
        private const string P2_CHAR1 = "VERO-01";
        private const string P1_CHAR2 = "DAIN-01";
        private const string P2_CHAR2 = "SONI-01";

        // ─────────────────────────────────────────────────────────
        //  진입점: 3판 2선승 매치 전체를 실행한다
        // ─────────────────────────────────────────────────────────
        public static void Run()
        {
            // 1. 이벤트 구독 (매치 전체에서 1회)
            // 1. 이벤트 구독 (기명 함수로 구독하여 누수 방지)
            EventManager.OnLogMessage += CustomColoredConsoleLogger;
            EventManager.OnTurnStart += OnTurnStartLog;
            EventManager.OnGameSet += OnGameSetLog;
            EventManager.OnGameDraw += OnGameDrawLog;
            EventManager.OnMatchSet += OnMatchSetLog;
            EventManager.OnMatchDraw += OnMatchDrawLog;


            Console.WriteLine("=== 콘솔 시뮬레이터 (단판제) 시작 ===");

            // 2. 데이터 로드 (매치 전체에서 1회)
            //GameRules.LoadRules("../Resources/GameData/CommonConfig.json");
            GameRules.LoadRules("./Data/CommonConfig.json"); // 예비 경로
            _dataManager = new GameDataManager();
            //_dataManager.LoadRulebookCards("../Resources/GameData");
            _dataManager.LoadRulebookCards("./Data"); // 예비 경로
            //_dataManager.LoadCharacterCards("../Resources/GameData");
            _dataManager.LoadCharacterCards("./Data"); // 예비 경로
            //_dataManager.LoadResourceCards("../Resources/GameData");
            _dataManager.LoadResourceCards("./Data"); // 예비 경로

            // 3. 플레이어 객체 생성 (이름만 결정, 게임 간 재사용)
            Player p1 = new Player { Name = "Bot_Red" };
            Player p2 = new Player { Name = "Bot_Blue" };

            // 4. 매치 루프
            int MaxGame = 3;
            int PlayToWin = 2;

            if (GameRules.BotSingleGame == 1)
            {
                MaxGame = 1;
                PlayToWin = 1;
            }

            var match = new MatchManager(gamesToWin: PlayToWin, maxGames: MaxGame);
            // var match = new MatchManager(gamesToWin: 2, maxGames: 3);

            while (!match.IsMatchOver())
            {
                int gameNum = match.GamesPlayed + 1;
                Console.WriteLine($"\n{'=',0}{'=',0}{'=',0}{'=',0}{'=',0}" +
                    $"  게임 {gameNum}  " +
                    $"{'=',0}{'=',0}{'=',0}{'=',0}{'=',0}");

                // 게임 상태 초기화
                _currentGameWinner = null;
                InitializeSingleGame(p1, p2);

                // 단일 게임 실행
                RunGameLoop(p1, p2);

                // 결과 기록
                match.RecordResult(_currentGameWinner, p1, p2);
                Console.WriteLine($"\n  현재 전적: {match.GetStatusString(p1, p2)}");
            }

            // 5. 세트 최종 결과
            Player matchWinner = match.GetMatchWinner(p1, p2);
            if (matchWinner != null)
                EventManager.OnMatchSet?.Invoke(matchWinner);
            else
                EventManager.OnMatchDraw?.Invoke(p1, p2);

            // 이벤트 구독 해제 (클린업)
            EventManager.OnLogMessage -= CustomColoredConsoleLogger;
            EventManager.OnTurnStart -= OnTurnStartLog;
            EventManager.OnGameSet -= OnGameSetLog;
            EventManager.OnGameDraw -= OnGameDrawLog;
            EventManager.OnMatchSet -= OnMatchSetLog;
            EventManager.OnMatchDraw -= OnMatchDrawLog;
        }
        private static void OnTurnStartLog(int turn, string _) => Console.WriteLine($"\n========== [ ROUND {turn} ] ==========");
        private static void OnGameSetLog(Player winner) { _currentGameWinner = winner; Console.WriteLine($"\n[GAME SET] {winner.Name} 승리!"); }
        private static void OnGameDrawLog(Player p1, Player p2, int turn) => Console.WriteLine($"\n[GAME DRAW] {p1.Name} vs {p2.Name} 무승부! (라운드 {turn})");
        private static void OnMatchSetLog(Player winner) => Console.WriteLine($"\n★ [MATCH SET] {winner.Name} 세트 승리! ★");
        private static void OnMatchDrawLog(Player p1, Player p2) => Console.WriteLine($"\n★ [MATCH DRAW] {p1.Name} vs {p2.Name} 세트 무승부! ★");

        // ─────────────────────────────────────────────────────────
        //  단일 게임 초기화
        // ─────────────────────────────────────────────────────────
        private static void InitializeSingleGame(Player p1, Player p2)
        {
            _p1RevealedCard = null;
            _p2RevealedCard = null;

            // 캐릭터 설정 (듀얼: 주캐릭터=능력, 부캐릭터=덱 풀)
            p1.CharacterCardId = P1_CHAR1;
            p1.SecondaryCharacterId = P1_CHAR2;
            p2.CharacterCardId = P2_CHAR1;
            p2.SecondaryCharacterId = P2_CHAR2;

            // ==============================================================
            // ★ [임시 테스트용] 원하는 타겟 카드 ID 하드코딩
            // 배열에 적어둔 ID 1개당 자동으로 2장씩 덱에 들어갑니다.
            // 10개를 적으면 정상적인 20장 덱이 되고, 적게 적으면 미니 덱이 됩니다.
            // ==============================================================
            string[] p1TestIds = // BotRed : 엘리 + 다이나
            {
                "ELLI-02", // 퀵 드로우
                // "ELLI-03", // 수류탄 투척
                // "ELLI-04", // 미니건 난사
                "ELLI-05", // 준비된 방어선
                "ELLI-06", // 격추 시스템
                "ELLI-07", // 카모플라쥬

                // "DAIN-02", // 함포 준비, 발사
                "DAIN-03", // 미사일 발사
                "DAIN-07", // 강도 테스트
                "DAIN-10", // 기뢰
                "DAIN-11", // 조선소
                        // 필요시 여기에 ID를 더 추가하세요.
            };

            string[] p2TestIds = // BorBlue : 베로니카 + 소니아
            {
                "VERO-02", // 숙청
                // "VERO-03", // 계획대로
                "VERO-05", // 요새화
                // "VERO-07", // 시위 해산
                "VERO-11", // 체크메이트

                "SONI-02", // 빵야!
                // "SONI-03", // 내 선물이야 ♬
                "SONI-05", // 곡예 비행
                "SONI-09", // 공격적인 전술
                "SONI-10", // 신재생에너지
                "SONI-11", // 노을지는 활주로
                        // 필요시 여기에 ID를 더 추가하세요.
            };

            // 하드코딩된 배열을 기반으로 덱을 생성합니다.
            var deck1 = CreateDeckFromIds(p1TestIds);
            var deck2 = CreateDeckFromIds(p2TestIds);

            // 덱 셔플 및 플레이어 초기화
            p1.ResetForNewGame(deck1, CreateResourceDeck());
            p2.ResetForNewGame(deck2, CreateResourceDeck());
            // ==============================================================

            // 덱 재구성 (듀얼 5:5 비율) 후 플레이어 상태 완전 초기화 - 원본 코드
            // p1.ResetForNewGame(CreateDualCharacterDeck(P1_CHAR1, P1_CHAR2, 5, 5), CreateResourceDeck());
            // p2.ResetForNewGame(CreateDualCharacterDeck(P2_CHAR1, P2_CHAR2, 5, 5), CreateResourceDeck());

            // GameContext 새로 생성
            context = new GameContext();
            context.CurrentTurn = 1;

            // 플레이어 추가
            context.Players.Add(p1);
            context.Players.Add(p2);

            // 시작 패 드로우
            GameLogicHelpers.DrawCards(p1, GameRules.StartingHands, context);
            GameLogicHelpers.DrawCards(p2, GameRules.StartingHands, context);

            /* ==============================================================
            // ★ QA 인젝션 테스트: 게임 시작하자마자 Bot_Blue 스택존에 방어 카드 3장 수동 장전
            // ==============================================================
            InjectTestCard(p2, "SONI-10", ZoneType.Graveyard); // 신재생에너지
            InjectTestCard(p2, "SONI-09", ZoneType.Graveyard); // 공격적인 전술
            // InjectTestCard(p2, "VERO-05", ZoneType.StackZone); // 요새화
            
            // Bot_Red가 바로 함포 준비를 쏠 수 있게 패에 강제 주입
            InjectTestCard(p2, "SONI-10", ZoneType.Hand);      // 신재생에너지
            // ============================================================== */

            EventManager.OnLogMessage?.Invoke($"\n[초기 상태]");
            EventManager.OnLogMessage?.Invoke(
                $"{p1.Name} - 라이프: {p1.LifeTokens} / 덱: {p1.Deck.Count}장 / 자원덱: {p1.ResourceDeck.Count}장 / 패: {p1.Hand.Count}장");
            EventManager.OnLogMessage?.Invoke(
                $"{p2.Name} - 라이프: {p2.LifeTokens} / 덱: {p2.Deck.Count}장 / 자원덱: {p2.ResourceDeck.Count}장 / 패: {p2.Hand.Count}장");

            // ★ 게임 시작 이벤트 방송! (UI가 체력바와 패를 렌더링할 타이밍)
            EventManager.OnGameStart?.Invoke(p1, p2);
        }

        // ─────────────────────────────────────────────────────────
        //  단일 게임 루프
        // ─────────────────────────────────────────────────────────
        private static void RunGameLoop(Player p1, Player p2)
        {
            while (!context.IsGameOver && context.CurrentTurn <= 20)
            {
                EventManager.OnTurnStart?.Invoke(context.CurrentTurn, "양측");
                _p1RevealedCard = null;
                _p2RevealedCard = null;

                context.CurrentPhase = GamePhase.ResourcePhase;
                ExecuteResourcePhase(p1, p2, context.CurrentTurn);
                if (CheckAndHandleGameOver(p1, p2)) break; // ★ 체크포인트

                context.CurrentPhase = GamePhase.DrawPhase;
                ExecuteDrawPhase(p1, p2, context.CurrentTurn);
                if (CheckAndHandleGameOver(p1, p2)) break;

                context.CurrentPhase = GamePhase.SetPhase;
                ExecuteSetPhase(p1, p2, context.CurrentTurn);
                if (CheckAndHandleGameOver(p1, p2)) break;

                context.CurrentPhase = GamePhase.OpenPhase;
                ExecuteOpenPhase(p1, p2, context.CurrentTurn);
                if (CheckAndHandleGameOver(p1, p2)) break;

                context.CurrentPhase = GamePhase.MainPhase;
                ExecuteMainPhase(p1, p2, context.CurrentTurn);
                if (CheckAndHandleGameOver(p1, p2)) break;

                context.CurrentPhase = GamePhase.EndPhase;
                ExecuteEndPhase(p1, p2, context.CurrentTurn);
                if (CheckAndHandleGameOver(p1, p2)) break;

                // ★ 턴 종료 이벤트 방송! (UI에서 턴 정리 연출에 사용)
                EventManager.OnTurnEnd?.Invoke("양측");

                context.CurrentTurn++;
            }

            // 라운드 한도 초과 → 무승부
            if (!context.IsGameOver && context.CurrentTurn > 20)
            {
                context.IsGameOver = true;
                EventManager.OnGameDraw?.Invoke(p1, p2, 20);
            }
        }

        #region Phase Logic (룰북 6페이즈)

        // 페이즈 1: 자원 페이즈 — 양측 각 자원덱에서 자원존으로 1장 이동. Phase 18: 다음턴 버프는 자원 페이즈부터 적용.
        private static void ExecuteResourcePhase(Player p1, Player p2, int turn)
        {
            EventManager.OnResourcePhase?.Invoke("양측", turn);
            EventManager.OnLogMessage?.Invoke("[ 자원 페이즈 ]");
            if (p1.BattlefieldCard != null || p2.BattlefieldCard != null)
            {
                EventManager.OnLogMessage?.Invoke("[ 적용 중인 전장 카드 ]");
                EventManager.OnLogMessage?.Invoke($"  {p1.Name} 전장: {(p1.BattlefieldCard != null ? p1.BattlefieldCard.Name : "없음")}");
                EventManager.OnLogMessage?.Invoke($"  {p2.Name} 전장: {(p2.BattlefieldCard != null ? p2.BattlefieldCard.Name : "없음")}");
            }

            // 이전 턴에 발동한 다음턴 버프 적용
            p1.ApplyNextTurnBuffs();
            p2.ApplyNextTurnBuffs();

            context.ActivePlayer = p1; context.TargetPlayer = p2;
            p1.TakeResourceCard();
            // 전장 주기 효과 적용 — 자원 페이즈 (ELLI-11 무작위 노획)
            if (GameLogicHelpers.ApplyBattlefieldResourcePhaseEffects(p1, context)) return; // 헬퍼가 true를 주면 즉시 페이즈 컷

            context.ActivePlayer = p2; context.TargetPlayer = p1;
            p2.TakeResourceCard();
            if (GameLogicHelpers.ApplyBattlefieldResourcePhaseEffects(p2, context)) return;
        }

        // 페이즈 2: 드로우 페이즈 — 양측 각 메인덱에서 1장 드로우 + 유닛 상태 초기화
        private static void ExecuteDrawPhase(Player p1, Player p2, int turn)
        {
            EventManager.OnDrawPhase?.Invoke("양측", turn);
            EventManager.OnLogMessage?.Invoke("[ 드로우 페이즈 ]");

            // 전장 주기 효과 적용 — 매 드로우 페이즈 (VERO-11 체크메이트, DAIN-11 조선소, SONI-11 노을지는 활주로)
            context.ActivePlayer = p1; context.TargetPlayer = p2;
            if (GameLogicHelpers.ApplyBattlefieldTurnEffects(p1, context)) return;

            context.ActivePlayer = p2; context.TargetPlayer = p1;
            if (GameLogicHelpers.ApplyBattlefieldTurnEffects(p2, context)) return;

            // 캐릭터 능력: VERONICA(VERO-01) — 드로우 대신 덱 탑 3장 보기 → 1장 패로
            // p1 드로우 처리
            bool p1DrawHandled = false;
            foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(p1))
            {
                if (ability.CanUse(p1, context))
                {
                    ability.OnDrawPhase(p1, context, used => p1DrawHandled = used);
                    if (p1DrawHandled) break;
                }
            }
            if (!p1DrawHandled) GameLogicHelpers.DrawCards(p1, GameRules.DrawPerTurn, context); // 기본 드로우 처리

            // p2 드로우 처리
            bool p2DrawHandled = false;
            foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(p2))
            {
                if (ability.CanUse(p2, context))
                {
                    ability.OnDrawPhase(p2, context, used => p2DrawHandled = used);
                    if (p2DrawHandled) break;
                }
            }
            if (!p2DrawHandled) GameLogicHelpers.DrawCards(p2, GameRules.DrawPerTurn, context);

            EventManager.OnLogMessage?.Invoke($"{p1.Name} 패: {p1.Hand.Count}장 | {string.Join(", ", p1.Hand.Select(c => c.Name))}");
            EventManager.OnLogMessage?.Invoke($"{p2.Name} 패: {p2.Hand.Count}장 | {string.Join(", ", p2.Hand.Select(c => c.Name))}");
        }

        // ─── 페이즈 3: 세트 페이즈 (동시 처리 로직 검증용) ──────────────────────────
        private static void ExecuteSetPhase(Player p1, Player p2, int turn)
        {
            EventManager.OnSetPhase?.Invoke("양측", turn);
            EventManager.OnLogMessage?.Invoke("[ 세트 페이즈 ]");

            // 1. 세트 전 능력 동시 처리 (소니아 등)
            foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(p1))
            {
                if (ability.CanUse(p1, context)) ability.OnSetPhase(p1, context, _ => { });
            }
            foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(p2))
            {
                if (ability.CanUse(p2, context)) ability.OnSetPhase(p2, context, _ => { });
            }

            // 2. 수집(Gather): 무엇을 낼지 '결정'만 하고 아직 필드에 깔지 않음!
            Card p1Decision = GetBotSetCardDecision(p1);
            Card p2Decision = GetBotSetCardDecision(p2);

            // 3. 일괄 실행(Execute): 결정된 카드를 동시에 세트존에 배치
            if (p1Decision != null) p1.SetCard(p1Decision);
            if (p2Decision != null) p2.SetCard(p2Decision);

            EventManager.OnLogMessage?.Invoke($"{p1.Name} 세트존: {(p1.SetZoneCard != null ? "세트됨" : "없음")}");
            EventManager.OnLogMessage?.Invoke($"{p2.Name} 세트존: {(p2.SetZoneCard != null ? "세트됨" : "없음")}");
        }

        // 헬퍼: 봇이 세트할 카드를 고르기만 하고 반환함
        private static Card GetBotSetCardDecision(Player p)
        {
            if (p.Hand.Count == 0) return null;

            var affordableCards = p.Hand.Where(c => GameLogicHelpers.GetEffectiveCost(c, p) <= p.ResourceZone.Count).ToList();
            var candidates = affordableCards.Count > 0 ? affordableCards : p.Hand;
            return candidates.OrderBy(c => System.Guid.NewGuid()).FirstOrDefault();
        }

        /* 페이즈 3: 세트 페이즈 — 양측 동시에 패에서 카드 1장을 뒷면으로 세트존에 놓음
        private static void ExecuteSetPhase(Player p1, Player p2, int turn)
        {
            EventManager.OnSetPhase?.Invoke("양측", turn);
            EventManager.OnLogMessage?.Invoke("[ 세트 페이즈 ]");

            // 캐릭터 능력: SONIA(SONI-01) — 세트 전 폐기존에서 소니아 카드 1장 → 패로
            // 세트 전 능력(소니아 등) 발동
            foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(p1))
            {
                if (ability.CanUse(p1, context)) ability.OnSetPhase(p1, context, _ => { });
            }
            foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(p2))
            {
                if (ability.CanUse(p2, context)) ability.OnSetPhase(p2, context, _ => { });
            }

            BotChooseSetCard(p1);
            BotChooseSetCard(p2);

            EventManager.OnLogMessage?.Invoke($"{p1.Name} 세트존: {(p1.SetZoneCard != null ? "세트됨" : "없음")}");
            EventManager.OnLogMessage?.Invoke($"{p2.Name} 세트존: {(p2.SetZoneCard != null ? "세트됨" : "없음")}");
        }

        // 봇 세트 전략 (QA 테스트용): 지불 가능한 카드 중 완전히 무작위로 선택하여 다양한 효과 충돌 유도
        private static void BotChooseSetCard(Player p)
        {
            if (p.Hand.Count == 0) return;

            // 1. 코스트 지불이 가능하여 '실제로 발동될 수 있는' 카드들만 먼저 추립니다.
            // (효과가 취소되고 버려지는 상황을 최소화하여 실제 이펙트 테스트 횟수를 극대화)
            var affordableCards = p.Hand
                .Where(c => GameLogicHelpers.GetEffectiveCost(c, p) <= p.ResourceZone.Count)
                .ToList();

            // 2. 만약 낼 수 있는 카드가 하나도 없다면, 어차피 버려질 테니 패 전체를 후보로 둡니다.
            var candidates = affordableCards.Count > 0 ? affordableCards : p.Hand;

            // 3. 기존의 획일화된 기준(Speed, Type)을 버리고, Guid를 이용한 무작위 정렬(Shuffle)을 수행합니다.
            // 시간 복잡도: O(N log N). 패의 최대 장수가 적으므로(약 5~10장) 성능 오버헤드는 0에 수렴합니다.
            var candidate = candidates
                .OrderBy(c => System.Guid.NewGuid())
                .FirstOrDefault();

            if (candidate != null)
                p.SetCard(candidate);
        }

        /* 기존 코드: 봇 세트 전략: Speed가 가장 낮은(빠른) 카드 우선 → 같으면 Defense > Attack > Support
        private static void BotChooseSetCard(Player p)
        {
            if (p.Hand.Count == 0) return;

            if (p.ResourceZone.Count >= 2 && p.BattlefieldCard == null) // 전장 카드가 없고 자원존이 2장 이상이면 전장 우선 고려
            {
                // 전장 카드가 있으면 우선 세트
                var battlefieldCandidate = p.Hand
                    .Where(c => c.IsBattlefield)
                    .OrderBy(c => c.Speed == CardSpeed.None ? 999 : (int)c.Speed)
                    .FirstOrDefault();
                if (battlefieldCandidate != null)
                {
                    p.SetCard(battlefieldCandidate);
                    return;
                }
            }

            // 룰북 타입(Attack/Defense/Support) 카드를 우선 탐색
            var candidate = p.Hand
                .Where(c => c.Type == CardType.Attack || c.Type == CardType.Defense || c.Type == CardType.Support)
                .OrderBy(c => c.Speed == CardSpeed.None ? 999 : (int)c.Speed)
                .ThenBy(c => c.Type == CardType.Defense ? 0 : c.Type == CardType.Attack ? 1 : c.Type == CardType.Support ? 2 : 3) // Defense > Attack > Support
                .FirstOrDefault();

            if (candidate != null)
                p.SetCard(candidate);
        }*/

        // ─── 페이즈 4: 오픈 페이즈 (동시 처리 로직 검증용) ──────────────────────────
        private static void ExecuteOpenPhase(Player p1, Player p2, int turn)
        {
            EventManager.OnOpenPhase?.Invoke("양측", turn);
            EventManager.OnLogMessage?.Invoke("[ 오픈 페이즈 ]");

            context.ClearOpenPhaseStates();

            // 1. 수집(Gather): 오픈할지 폐기할지 '결정'만 함
            OpenPhaseChoice p1Choice = GetBotOpenChoiceDecision(p1);
            OpenPhaseChoice p2Choice = GetBotOpenChoiceDecision(p2);

            // 2. 일괄 실행(Execute): 결정된 행동을 동시에 적용
            _p1RevealedCard = ApplyOpenChoice(p1, p1Choice);
            context.OpenPhaseStates[p1.Name] = new PlayerOpenPhaseState { HasOpened = (_p1RevealedCard != null), RevealedCard = _p1RevealedCard };

            _p2RevealedCard = ApplyOpenChoice(p2, p2Choice);
            context.OpenPhaseStates[p2.Name] = new PlayerOpenPhaseState { HasOpened = (_p2RevealedCard != null), RevealedCard = _p2RevealedCard };
        }

        // 헬퍼: 봇이 오픈/폐기 여부를 결정만 함
        private static OpenPhaseChoice GetBotOpenChoiceDecision(Player p)
        {
            if (p.SetZoneCard == null) return OpenPhaseChoice.Abandon;

            // 룰: 오픈 선언은 코스트와 무관(메인에서 지불 실패 시 폐기).
            return OpenPhaseChoice.Open;
        }

        // 헬퍼: 수집된 결정을 실제로 집행함
        private static Card ApplyOpenChoice(Player player, OpenPhaseChoice choice)
        {
            if (player.SetZoneCard == null) return null;

            if (choice == OpenPhaseChoice.Abandon)
            {
                int effectiveCost = GameLogicHelpers.GetEffectiveCost(player.SetZoneCard, player);
                EventManager.OnLogMessage?.Invoke($"{player.Name}: 카드 [{player.SetZoneCard.Name}] → 폐기 선택 / 코스트 필요: {effectiveCost} / 자원존: {player.GetResourceCount()}");

                player.AbandonSetCard();
                GameLogicHelpers.DrawCards(player, 1, context);

                foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(player))
                {
                    if (ability.CanUse(player, context)) ability.OnOpenPhaseAbandon(player, context, _ => { });
                }
                return null;
            }
            else
            {
                return player.RevealSetCard();
            }
        }

        /* 페이즈 4: 오픈 페이즈 — 양측 동시에 세트 카드를 공개 or 폐기 선택
        private static void ExecuteOpenPhase(Player p1, Player p2, int turn)
        {
            EventManager.OnOpenPhase?.Invoke("양측", turn);
            EventManager.OnLogMessage?.Invoke("[ 오픈 페이즈 ]");

            // 이번 턴의 오픈 상태 초기화
            context.ClearOpenPhaseStates();

            _p1RevealedCard = BotChooseOpenOrAbandon(p1);
            context.OpenPhaseStates[p1.Name] = new PlayerOpenPhaseState { HasOpened = (_p1RevealedCard != null), RevealedCard = _p1RevealedCard };

            _p2RevealedCard = BotChooseOpenOrAbandon(p2);
            context.OpenPhaseStates[p2.Name] = new PlayerOpenPhaseState { HasOpened = (_p2RevealedCard != null), RevealedCard = _p2RevealedCard };
        }

        // 봇 오픈 전략: 코스트 지불 가능 → 공개, 불가능 → 폐기 후 드로우 1장
        // 반환값: 공개한 카드 (폐기 선택 시 null)
        private static Card BotChooseOpenOrAbandon(Player p)
        {
            if (p.SetZoneCard == null) return null;

            Card card = p.SetZoneCard;
            int effectiveCost = GameLogicHelpers.GetEffectiveCost(card, p);
            bool canAfford = effectiveCost == 0 || p.CanAfford(effectiveCost);

            if (!canAfford)
            {
                EventManager.OnLogMessage?.Invoke($"{p.Name}: 코스트 부족 (필요: {effectiveCost} / 자원존: {p.GetResourceCount()}) → 폐기 선택");
                p.AbandonSetCard();
                GameLogicHelpers.DrawCards(p, 1, context);

                // 폐기 시 능력(DAIN 다이나 등) 발동
                foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(p))
                {
                    if (ability.CanUse(p, context))
                    {
                        ability.OnOpenPhaseAbandon(p, context, _ => { });
                    }
                }

                return null;
            }

            // 공개 선택
            return p.RevealSetCard();
        }*/

        // 페이즈 5: 메인 페이즈 — 스피드 순서로 공개된 카드 효과 해결 (SpeedResolver 사용)
        private static void ExecuteMainPhase(Player p1, Player p2, int turn)
        {
            EventManager.OnMainPhase?.Invoke("양측", turn);
            EventManager.OnLogMessage?.Invoke("[ 메인 페이즈 ]");

            // 1. 동적 대기열(Queue) 생성 (엘리 능력이 여기에 새치기(Inject)될 예정)
            var pendingQueue = new List<(Player player, Player enemy, Card card)>();
            if (_p1RevealedCard != null) pendingQueue.Add((p1, p2, _p1RevealedCard));
            if (_p2RevealedCard != null) pendingQueue.Add((p2, p1, _p2RevealedCard));

            if (pendingQueue.Count == 0)
            {
                EventManager.OnLogMessage?.Invoke("공개된 카드가 없습니다. 메인 페이즈 생략.");
                return;
            }

            // 2. 대기열에 카드가 남아있는 동안 계속 반복
            while (pendingQueue.Count > 0)
            {
                // 현재 남은 카드들로 스피드 재정렬 (가장 빠른 그룹 1개만 추출)
                var groups = SpeedResolver.GroupByResolutionOrder(pendingQueue);
                var currentGroup = groups[0];

                // 추출된 카드는 대기열에서 제거
                foreach (var item in currentGroup)
                    pendingQueue.Remove(item);

                // 3. 동시 처리 그룹 실행 (★ 내부에서 IsGameOver로 break하지 않음 -> 무승부 성립 보장)
                foreach (var (player, enemy, card) in currentGroup)
                {
                    context.ActivePlayer = player;
                    context.TargetPlayer = enemy;

                    if (card.Cost > 0)
                    {
                        int effectiveCost = GameLogicHelpers.GetEffectiveCost(card, player);
                        if (effectiveCost > 0 && !player.PayCost(effectiveCost))
                        {
                            EventManager.OnLogMessage?.Invoke($"{player.Name}: [{card.Name}] 코스트 지불 실패 → 효과 취소");
                            player.ExtractCard(ZoneType.SetZone, card);
                            player.InsertCard(ZoneType.Graveyard, card);
                            EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.Graveyard);
                            continue;
                        }
                        else if (effectiveCost == 0 && card.Cost > 0)
                        {
                            // 원본 코스트가 있었으나 전장 효과로 0이 된 경우의 로그
                            EventManager.OnLogMessage?.Invoke($"  ✨ [{card.Name}] 전장 효과로 코스트 무료 발동!");
                        }
                    }

                    // 1. 스택 반응 (이 안에서 주체가 바뀌므로 복구 로직이 필수적입니다)
                    HandleStackActivation(enemy, player, card);

                    // 2. 스택 여부 상관없이 일단 카드 발동(isStackTrigger=false로 호출하여 스택 행동이 아닌 즉발 효과만)
                    player.PlayingCard = card;
                    // ★ 카드 사용(Play) 방송 추가! (UI 카드 튀어나오는 연출용)
                    EventManager.OnPlayCard?.Invoke(player, card);

                    context.LastEffectSucceeded = true;
                    card.Play(context, () => { }, isStackTrigger: false);
                    player.PlayingCard = null;

                    // 3. 카드 이동 분기 처리 (여기가 핵심입니다!)
                    if (card.IsStack)
                    {
                        // Play를 마친(즉발 효과만 터뜨린) 스택 카드를 스택존에 배치
                        player.AddToStackZone(card);
                        player.ExtractCard(ZoneType.SetZone, card);
                        // 이동 방송 (UI 스택존 연출용)
                        EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.StackZone);
                        EventManager.OnLogMessage?.Invoke($"  [{card.Name}] 스택존에 대기 상태로 전환.");
                    }
                    else if (card.IsBattlefield)
                    {
                        EventManager.OnLogMessage?.Invoke($"  [{card.Name}] 전장 카드 발동!");
                        player.ExtractCard(ZoneType.SetZone, card);
                        // ★ 이동 방송
                        player.PlaceBattlefield(card); // 전장 카드는 폐기하지 않음
                        EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.BattlefieldZone);

                        GameLogicHelpers.ApplyBattlefieldTurnEffects(player, context); // 전장 카드가 즉시 효과를 발동하는 경우가 있으므로
                    }
                    else if (player.ResourceZone.Contains(card))
                    {
                        player.InsertCard(ZoneType.ResourceZone, card);
                        player.ExtractCard(ZoneType.SetZone, card);
                        // ★ 이동 방송
                        EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.ResourceZone);
                        // 자원존 카드는 폐기하지 않음
                        EventManager.OnLogMessage?.Invoke($"  [{card.Name}] 자원존에 배치됨.");
                    }
                    else
                    {
                        // 일반 카드는 Play를 마쳤으므로 폐기존으로 이동
                        player.ExtractCard(ZoneType.SetZone, card);
                        player.InsertCard(ZoneType.Graveyard, card);
                        EventManager.OnCardMove?.Invoke(card, player, ZoneType.SetZone, player, ZoneType.Graveyard);

                        // 카드가 성공적으로 발동한 직후, 엘리 능력 체크
                        foreach (var ability in CharacterAbilityRegistry.GetPlayerAbilities(player))
                        {
                            bool abilityDone = false;
                            ability.OnMainPhaseAfterAttack(player, card, enemy, context, followUpCard =>
                            {
                                if (followUpCard != null)
                                {
                                    pendingQueue.Add((player, enemy, followUpCard));
                                }
                                abilityDone = true;
                            });

                            /* 캐릭터 능력이 큐에 넣고 싶은 후속 카드를 반환하면
                            Card followUpCard = ability.OnMainPhaseAfterAttack(player, card, enemy, context);

                            if (followUpCard != null)
                            {
                                // 큐에 밀어넣음 -> 다음 while 루프에서 스피드 비교 후 알아서 발동됨
                                pendingQueue.Add((player, enemy, followUpCard));
                            }*/
                        }
                    }
                } // -- 동시 그룹 1회 처리 완료 --

                // 동시 처리 그룹이 모두 끝난 '직후'에 게임오버 체크 (동시 킬 = 무승부 성립 조건)
                // 여기서 승패 판정(방송)을 일괄 수행 (동시타격, 자해 완벽 처리)
                if (CheckAndHandleGameOver(p1, p2))
                {
                    EventManager.OnLogMessage?.Invoke("최후의 일격으로 교전이 중단되었습니다!");
                    break;
                }
            }
        }

        // 페이즈 6: 엔드 페이즈 — 상태 출력, 덱아웃 체크, Duration 만료
        private static void ExecuteEndPhase(Player p1, Player p2, int turn)
        {
            EventManager.OnEndPhase?.Invoke("양측", turn);
            EventManager.OnLogMessage?.Invoke("[ 엔드 페이즈 ]");

            EventManager.OnLogMessage?.Invoke($"{p1.Name} - 라이프: {p1.LifeTokens} / 자원존: {p1.ResourceZone.Count} / 패: {p1.Hand.Count} / 덱: {p1.Deck.Count}");
            EventManager.OnLogMessage?.Invoke($"{p2.Name} - 라이프: {p2.LifeTokens} / 자원존: {p2.ResourceZone.Count} / 패: {p2.Hand.Count} / 덱: {p2.Deck.Count}\n");

            if (context.IsGameOver) return;

            // 덱아웃 체크 (엔드 페이즈에서만 확인)
            bool p1DeckOut = p1.Deck.Count == 0;
            bool p2DeckOut = p2.Deck.Count == 0;

            if (p1DeckOut && p2DeckOut)
            {
                // 동시 덱아웃 → 6단계 타이브레이커
                EventManager.OnLogMessage?.Invoke("양측 덱 동시 고갈 → 타이브레이커 판정!");
                context.IsGameOver = true;
                ResolveSimultaneousDeckout(p1, p2, turn);
            }
            else if (p1DeckOut)
            {
                EventManager.OnLogMessage?.Invoke($"{p1.Name}: 덱 고갈 → 패배");
                context.IsGameOver = true;
                EventManager.OnGameSet?.Invoke(p2);
            }
            else if (p2DeckOut)
            {
                EventManager.OnLogMessage?.Invoke($"{p2.Name}: 덱 고갈 → 패배");
                context.IsGameOver = true;
                EventManager.OnGameSet?.Invoke(p1);
            }

            // ThisTurn 전투 버프 만료 처리
            p1.ClearCombatBuffs();
            p2.ClearCombatBuffs();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 글로벌 심판: 현재 상태를 검사하여 누군가의 HP가 0이라면 게임 오버를 선언합니다.
        /// 어느 시점에서든 호출할 수 있는 전천후 상태 기반 행동(SBA) 체크포인트입니다.
        /// </summary>
        private static bool CheckAndHandleGameOver(Player p1, Player p2)
        {
            if (context.IsGameOver) return true;

            bool p1Dead = p1.LifeTokens <= 0;
            bool p2Dead = p2.LifeTokens <= 0;

            if (p1Dead && p2Dead)
            {
                context.IsGameOver = true;
                EventManager.OnLogMessage?.Invoke("\n⚔️ 양측 플레이어의 라이프가 동시에 0이 되었습니다! (무승부)");
                EventManager.OnGameDraw?.Invoke(p1, p2, context.CurrentTurn);
                return true;
            }
            else if (p1Dead)
            {
                context.IsGameOver = true;
                EventManager.OnGameSet?.Invoke(p2); // p2 승리
                return true;
            }
            else if (p2Dead)
            {
                context.IsGameOver = true;
                EventManager.OnGameSet?.Invoke(p1); // p1 승리
                return true;
            }

            return false; // 아직 아무도 죽지 않음
        }

        // 동시 덱아웃 타이브레이커 처리
        private static void ResolveSimultaneousDeckout(Player p1, Player p2, int turn)
        {
            int result = DeckValidator.ResolveTiebreaker(p1, p2);

            if (result > 0)
            {
                EventManager.OnLogMessage?.Invoke($"타이브레이커: {p1.Name} 승리");
                _currentGameWinner = p1;
                EventManager.OnGameSet?.Invoke(p1);
            }
            else if (result < 0)
            {
                EventManager.OnLogMessage?.Invoke($"타이브레이커: {p2.Name} 승리");
                _currentGameWinner = p2;
                EventManager.OnGameSet?.Invoke(p2);
            }
            else
            {
                // 6단계 모두 동일 → 코인토스
                bool p1WinsToss = new Random().Next(0, 2) == 0;
                Player tossWinner = p1WinsToss ? p1 : p2;
                EventManager.OnLogMessage?.Invoke(
                    $"타이브레이커 6단계 모두 동일 → 코인토스! {tossWinner.Name} 승리");
                _currentGameWinner = tossWinner;
                EventManager.OnGameSet?.Invoke(tossWinner);
            }
        }

        // ID 배열로부터 덱 생성 (각 카드 2장씩)
        private static List<Card> CreateDeckFromIds(string[] ids)
        {
            var deck = new List<Card>();
            foreach (string id in ids)
            {
                if (_dataManager.AllCards.TryGetValue(id, out Card c))
                    for (int i = 0; i < 2; i++) deck.Add(c.Clone());
            }
            return deck;
        }

        /// <summary>
        /// 듀얼 캐릭터 덱 생성: char1에서 count1종×2장 + char2에서 count2종×2장 = 20장. 당장은 수동 덱 세팅으로 사용하지 않음
        /// </summary>
        private static List<Card> CreateDualCharacterDeck(string charId1, string charId2, int count1, int count2)
        {
            // Support 카드를 1순위로 긁어오도록 설정
            var ids1 = GetPrioritizedCardIds(charId1, count1, CardType.Support);
            var ids2 = GetPrioritizedCardIds(charId2, count2, CardType.Support);

            var ids = ids1.Concat(ids2).ToArray();
            return CreateDeckFromIds(ids);
        }

        /// <summary>
        /// 지정한 캐릭터의 카드 풀에서 선호하는 타입(preferredType)의 카드를 우선적으로 추출합니다.
        /// </summary>
        private static IEnumerable<string> GetPrioritizedCardIds(string charId, int count, CardType preferredType)
        {
            // "ELLI-01" -> "ELLI" 추출
            string prefix = charId.Contains("-") ? charId.Split('-')[0] : charId;

            return _dataManager.AllCards.Values
                // 해당 캐릭터의 카드만 필터링 (캐릭터 카드 본체는 제외)
                .Where(c => c.Id.StartsWith(prefix + "-") && c.Id != charId)
                // 1순위 정렬: 선호 타입(Support)이면 0, 아니면 1 부여 -> Support 카드가 리스트 맨 위로 올라옴
                .OrderBy(c => c.Type == preferredType ? 0 : 1)
                // 2순위 정렬: ID 오름차순 (동일 타입 내에서)
                .ThenBy(c => c.Id)
                .Select(c => c.Id)
                .Take(count); // 필요한 종류(count)만큼만 잘라냄
        }

        // 자원 카드 15장 생성 (Data/ResourceCards.json RES-01 기반)
        private static List<Card> CreateResourceDeck()
        {
            var deck = new List<Card>();
            if (!_dataManager.AllCards.TryGetValue("RES-01", out Card template))
            {
                // JSON 미로드 시 폴백: 프로그래밍 생성
                for (int i = 0; i < GameRules.ResourceDeckCount; i++)
                    deck.Add(new Card { Id = $"res_{i:000}", DataId = "RES-01", Name = "자원 카드", Type = CardType.Resource, Cost = 0, Speed = CardSpeed.None });
                return deck;
            }
            for (int i = 0; i < GameRules.ResourceDeckCount; i++)
                deck.Add(template.Clone());
            return deck;
        }

        /// <summary>
        /// 스택 발동: stackOwner가 스택 카드를 보유 중이고 상대 카드에 반응할 수 있으면 봇이 자동 발동.
        /// (수집 -> 강제 횟수 계산 -> 순차 발동 파이프라인 적용)
        /// </summary>
        private static void HandleStackActivation(Player stackOwner, Player cardPlayer, Card playedCard)
        {
            // 스택 발동 조건: 스택존에 카드가 있고, 무적 상태가 아니어야 함
            if (stackOwner.StackZone.Count == 0 || stackOwner.IsInvincible) return;

            int incomingHits = 0;
            bool isPiercingAttack = false;

            // 1. 공격 카드의 타격 횟수 스캔
            foreach (var effect in playedCard.Effects)
            {
                if (effect is DamageEffect dmgEffect)
                {
                    // 자해(Self) 데미지는 방어할 필요가 없으므로 타격 횟수에서 제외
                    if (!dmgEffect.TargetSelf)
                    {
                        incomingHits += dmgEffect.Times;
                        if (dmgEffect.isPiercing) isPiercingAttack = true;
                    }
                }
            }

            // 데미지가 없는 카드라면 스택을 아낍니다.
            if (incomingHits == 0) return;

            // 2. 발동 "가능한" 방어 카드 모두 추리기 (수집)
            List<Card> validStackCards = new List<Card>();
            foreach (var stackCard in stackOwner.StackZone)
            {
                bool canBlock = false;
                if (stackCard.Type == CardType.Defense || stackCard.Type == CardType.Support)
                {
                    foreach (var effect in stackCard.Effects)
                    {
                        if (effect is BuffEffect buffEffect)
                        {
                            if (isPiercingAttack)
                            {
                                if (buffEffect.TypeOfBuff == BuffType.SuperArmor || buffEffect.TypeOfBuff == BuffType.Invincible)
                                    canBlock = true;
                            }
                            else
                            {
                                if (buffEffect.TypeOfBuff == BuffType.Armor || buffEffect.TypeOfBuff == BuffType.SuperArmor || buffEffect.TypeOfBuff == BuffType.Invincible)
                                    canBlock = true;
                            }
                        }
                    }
                }
                if (canBlock) validStackCards.Add(stackCard);
            }

            // 막을 수 있는 카드가 하나도 없다면 종료
            if (validStackCards.Count == 0) return;

            // 3. 강제 발동해야 할 횟수 계산 
            // (유니티 전용 Mathf.Min 대신 순수 C#의 System.Math.Min 사용)
            int requiredCount = System.Math.Min(incomingHits, validStackCards.Count);

            // 4. 대상 선택 (ConsoleRunner는 전원 봇이므로 무조건 앞에서부터 강제 선택)
            List<Card> selectedCards = validStackCards.Take(requiredCount).ToList();

            // 5. 선택된 카드들을 순서대로 발동 (실행)
            Player originalActive = context.ActivePlayer;
            Player originalTarget = context.TargetPlayer;

            foreach (var stackCard in selectedCards)
            {
                context.ActivePlayer = stackOwner;
                context.TargetPlayer = cardPlayer;

                stackOwner.PlayingCard = stackCard;
                EventManager.OnLogMessage?.Invoke($"{stackOwner.Name}: [{stackCard.Name}] 스택 발동! (← 상대: [{playedCard.Name}])");

                bool done = false;
                context.LastEffectSucceeded = true;

                // 콘솔 환경은 연출 대기가 없으므로 콜백이 즉시 실행됩니다.
                stackCard.Play(context, () => done = true, isStackTrigger: true);

                stackOwner.PlayingCard = null;
                stackOwner.UseAndDiscardStack(stackCard);
            }

            // ★ 상태 복구 (State Restore)
            context.ActivePlayer = originalActive;
            context.TargetPlayer = originalTarget;
        }

        /* <summary>
        /// 스택 발동: stackOwner가 스택 카드를 보유 중이고 상대 카드에 반응할 수 있으면 봇이 자동 발동.
        /// </summary>
        private static void HandleStackActivation(Player stackOwner, Player cardPlayer, Card playedCard)
        {
            // 스택 발동 조건: 스택존에 카드가 있고, 무적 상태가 아니어야 함 (무적은 모든 효과 무효 + 스택 발동 불가)
            if (stackOwner.StackZone.Count == 0 || stackOwner.IsInvincible) return;

            // 1. 상대방의 공격 카드 스캔 (총 타격 횟수 및 관통 여부 계산)
            int incomingHits = 0;
            bool isPiercingAttack = false;

            // 공격 카드의 효과를 분석하여 타격 횟수와 관통 여부를 계산합니다.
            foreach (var effect in playedCard.Effects)
            {

                if (effect is DamageEffect dmgEffect)
                {
                    // ★ 피아식별 로직 추가!
                    // 자해(Self) 데미지는 방어할 필요가 없으므로 타격 횟수에서 제외합니다.
                    // 나(방어자)를 향한 공격일 때만 카운트
                    if (!dmgEffect.TargetSelf)
                    {
                        incomingHits += dmgEffect.Times;
                        if (dmgEffect.isPiercing) isPiercingAttack = true;
                    }
                }
            }


            // [AI 포인트 1] 데미지가 없는 카드라면 스택을 아낍니다.
            if (incomingHits == 0) return;

            // ★ 상태 저장 (State Save)
            Player originalActive = context.ActivePlayer;
            Player originalTarget = context.TargetPlayer;

            int activatedCount = 0; // 현재까지 발동한 스택 카드 개수

            // 스택존 카드들을 복사해서 순회
            foreach (var stackCard in new List<Card>(stackOwner.StackZone))
            {
                // [AI 포인트 2] 타격 횟수만큼 방어 카드를 썼다면 추가 발동 중단
                if (activatedCount >= incomingHits) break;

                bool canBlock = false;

                // 내 스택 카드가 방어 카드일 경우, 이 공격을 막을 수 있는지 검증
                if (stackCard.Type == CardType.Defense || stackCard.Type == CardType.Support)
                {
                    foreach (var effect in stackCard.Effects)
                    {
                        if (effect is BuffEffect buffEffect)
                        {
                            if (isPiercingAttack)
                            {
                                // 관통 공격: 슈퍼아머나 무적만 유효
                                if (buffEffect.TypeOfBuff == BuffType.SuperArmor || buffEffect.TypeOfBuff == BuffType.Invincible)
                                    canBlock = true;
                            }
                            else
                            {
                                // 일반 공격: 아머, 슈퍼아머, 무적 모두 유효
                                if (buffEffect.TypeOfBuff == BuffType.Armor || buffEffect.TypeOfBuff == BuffType.SuperArmor || buffEffect.TypeOfBuff == BuffType.Invincible)
                                    canBlock = true;
                            }
                        }
                    }
                }

                if (canBlock)
                {
                    EventManager.OnLogMessage?.Invoke(
                        $"{stackOwner.Name}: [{stackCard.Name}] 스택 발동! (← 상대: [{playedCard.Name}])");

                    // 컨텍스트 스위칭
                    context.ActivePlayer = stackOwner;
                    context.TargetPlayer = cardPlayer;

                    // ★ 추가: 스택 카드를 발동하기 전에 시스템에 "현재 사용 중인 카드"를 명시적으로 알려줍니다!
                    stackOwner.PlayingCard = stackCard;
                    bool done = false;
                    context.LastEffectSucceeded = true;
                    // stackCard.Play(context, () => { }, isStackTrigger: true);
                    stackCard.Play(context, () => done = true, isStackTrigger: true);

                    // ★ 복구: 발동이 끝났으니 다시 null로 비워줍니다.
                    stackOwner.PlayingCard = null;
                    stackOwner.UseAndDiscardStack(stackCard);
                    activatedCount++; // 유효하게 발동했으므로 카운트 증가
                }
            }

            // ★ 상태 복구 (State Restore)
            context.ActivePlayer = originalActive;
            context.TargetPlayer = originalTarget;
        }*/

        /// <summary>
        /// [QA 전용] 특정 플레이어의 원하는 위치(Zone)에 특정 카드를 강제로 생성하여 주입합니다.
        /// 복잡한 엣지 케이스(스택 3개 중첩 등)를 1턴 만에 재현하기 위한 디버깅용 툴입니다.
        /// </summary>
        private static void InjectTestCard(Player player, string cardId, ZoneType targetZone)
        {
            // 1. 데이터 매니저에서 카드 템플릿 검색
            if (!_dataManager.AllCards.TryGetValue(cardId, out Card template))
            {
                EventManager.OnLogMessage?.Invoke($"<color=red>[QA Error] 주입 실패: ID '{cardId}'를 찾을 수 없습니다.</color>");
                return;
            }

            // 2. 실제 게임에 사용될 독립된 객체로 복제 (Deep Copy)
            Card injectedCard = template.Clone();

            // 3. 타겟 존의 성격에 맞춰 안전하게 밀어넣기
            switch (targetZone)
            {
                case ZoneType.Hand:
                    player.InsertCard(ZoneType.Hand, injectedCard);
                    break;
                case ZoneType.Deck:
                    // 덱 조작: 다음 턴에 바로 뽑히도록 덱의 맨 위(0번 인덱스)에 강제 삽입
                    player.Deck.Insert(0, injectedCard);
                    break;
                case ZoneType.Graveyard:
                    player.InsertCard(ZoneType.Graveyard, injectedCard);
                    break;
                case ZoneType.ResourceZone:
                    player.InsertCard(ZoneType.ResourceZone, injectedCard);
                    break;
                case ZoneType.StackZone:
                    // 스택존 전용 공식 파이프라인 탑재
                    player.AddToStackZone(injectedCard);
                    break;
                case ZoneType.BattlefieldZone:
                    // 전장 전용 공식 파이프라인 탑재 (기존 전장이 있다면 덮어씌움)
                    player.PlaceBattlefield(injectedCard);
                    break;
                default:
                    EventManager.OnLogMessage?.Invoke($"<color=red>[QA Error] '{targetZone}'은(는) 주입이 지원되지 않는 존입니다.</color>");
                    return;
            }

            EventManager.OnLogMessage?.Invoke($"<color=yellow>[QA Inject] {player.Name}의 {targetZone}에 '{injectedCard.Name}' 강제 장전 완료.</color>");
        }

        private static void CustomColoredConsoleLogger(string message)
        {
            if (string.IsNullOrEmpty(message)) { Console.WriteLine(); return; }

            if (message.StartsWith("<color=cyan>"))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(message.Replace("<color=cyan>", "").Replace("</color>", ""));
                Console.ResetColor();
            }
            else if (message.StartsWith("<color=yellow>"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(message.Replace("<color=yellow>", "").Replace("</color>", ""));
                Console.ResetColor();
            }
            else if (message.StartsWith("<color=red>"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(message.Replace("<color=red>", "").Replace("</color>", ""));
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        #endregion
    }
}
