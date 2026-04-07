# BattleManager / ServerGameManager 함수 목록 (현재 코드 기준)

대상 파일:

- `Assets/TCG_Project/Scripts/Managers/BattleManager.cs`
- `Assets/Scripts/Server Scripts/ServerGameManager.cs`

정리 기준:

- 아래 목록은 **각 파일 안에 “정의되어 있는 메서드” 전체**를 시그니처 기준으로 나열합니다.
- `BattleManager.cs`에는 과거 구현이 `/* ... */` 주석 블록으로 남아 있어 **동일 이름의 비활성 메서드**가 존재합니다. 이 문서에서는 **활성/비활성(주석 블록)** 을 구분해 표기합니다.

---

## BattleManager (`BattleManager`)

### Unity 라이프사이클 / 이벤트 핸들러

- `Awake()`
- `OnEnable()`
- `OnDisable()`
- `Start()`
- `HandleGameSet(Player winner)`
- `HandleGameDraw(Player p1, Player p2, int turn)`
- `HandleLogMessage(string msg)`
- `HandleQA_CardPick(Player player, List<Card> validCards, int count, Action<List<Card>> callback)`
- `HandleQA_OptionalAction(Player player, string message, GameContext ctx, Action<bool> callback)`

### 진입 / 초기화 / 매치

- `StartMatch(PlayerSetupData p1Data, PlayerSetupData p2Data)`
- `InitializeSystem()`
- `MatchLoop() : IEnumerator`
- `RunSingleGame() : IEnumerator`
- `InitializeSingleGame()`

### SBA(게임 오버) / 타이브레이커

- `CheckAndHandleGameOver() : bool`
- `ResolveSimultaneousDeckout()`

### 페이즈 코루틴 (활성)

- `ExecuteResourcePhaseRoutine() : IEnumerator`
- `ExecuteDrawPhaseRoutine() : IEnumerator` *(병렬 드로우 버전 — 활성)*
- `ExecuteDrawForPlayerParallel(Player player, Action onDone) : IEnumerator`
- `ExecuteSetPhaseRoutine() : IEnumerator`
- `CheckPreSetAbilities(Player player, Action onDone) : IEnumerator`
- `GetSetCardChoice(Player player, Action<Card> onChosen) : IEnumerator`
- `ExecuteOpenPhaseRoutine() : IEnumerator` *(병렬 오픈 선택 수집 버전 — 활성)*
- `GetOpenChoiceParallel(Player player, Action<OpenPhaseChoice> onChosen) : IEnumerator`
- `ApplyOpenChoice(Player player, OpenPhaseChoice choice) : Card`
- `ExecuteMainPhaseRoutine() : IEnumerator`
- `HandleStackActivation(Player stackOwner, Player cardPlayer, Card playedCard) : IEnumerator`
- `ExecuteEndPhaseRoutine() : IEnumerator`

### QA / 덱 생성 유틸

- `InjectTestCard(Player player, string cardId, ZoneType targetZone)`
- `CreateDeckFromIds(List<string> ids) : List<Card>`
- `CreateDualCharacterDeck(string charId1, string charId2, int count1, int count2) : List<Card>`
- `CreateResourceDeck() : List<Card>`

### 페이즈 코루틴 (비활성 — 주석 블록 `/* ... */` 내부)

- `ExecuteDrawPhaseRoutine() : IEnumerator` *(순차 드로우 구버전 — 비활성)*
- `ExecuteDrawForPlayer(Player player) : IEnumerator` *(구버전 — 비활성)*
- `ExecuteOpenPhaseRoutine() : IEnumerator` *(순차 오픈 구버전 — 비활성)*
- `PerformOpenOrAbandon(Player player, Action<Card> onResult) : IEnumerator` *(구버전 — 비활성)*

---

## ServerGameManager (`ServerGameManager`)

### Unity 라이프사이클 / 이벤트 핸들러

- `Awake()`
- `OnEnable()`
- `OnDisable()`
- `HandleLogMessage(string msg)`
- `HandleGameSet(Player winner)`
- `HandleGameDraw(Player p1, Player p2, int turn)`

### 진입 / 라운드 / 매치

- `StartMultiplayerGame(Player host, Player guest)`
- `StartSingleGameRound()`
- `HandleGameOverFlow(Player gameWinner, bool isDraw) : System.Collections.IEnumerator`

### SBA(게임 오버)

- `CheckAndHandleGameOver(Player p1, Player p2) : bool`

### 페이즈 상태 머신 / 동기화

- `StartPhase(GamePhase phase)`
- `SyncBoardStateThenStartPhase(GamePhase nextPhase) : System.Collections.IEnumerator`
- `SyncBoardStateAndWait() : System.Collections.IEnumerator`

### 페이즈 로직

- `ExecuteResourcePhaseRoutine()`
- `ExecuteDrawPhaseRoutine() : System.Collections.IEnumerator`
- `ExecuteDrawForPlayerParallel(Player player, Action onDone) : System.Collections.IEnumerator`
- `ExecuteSetPhaseRoutine() : System.Collections.IEnumerator`
- `CheckPreSetAbilities(Player player, Action onDone) : System.Collections.IEnumerator`
- `GetSetCardChoice(Player player, Action<Card> onChosen) : System.Collections.IEnumerator`
- `ExecuteOpenPhaseRoutine() : System.Collections.IEnumerator`
- `GetOpenChoiceParallel(Player player, int effectiveCost, Action<OpenPhaseChoice, int> onChosen) : System.Collections.IEnumerator`
- `ApplyOpenChoice(Player player, OpenPhaseChoice choice, int effectiveCost) : Card`
- `ExecuteMainPhaseRoutine() : System.Collections.IEnumerator`
- `ResolveMainPhaseCard(Player player, Player enemy, Card card, List<(Player player, Player enemy, Card card)> pendingQueue) : System.Collections.IEnumerator`
- `HandleStackActivation(Player stackOwner, Player cardPlayer, Card playedCard) : System.Collections.IEnumerator`
- `ExecuteEndPhase()`
- `ExecuteEndPhaseRoutine() : System.Collections.IEnumerator`

### 네트워크 입력 / 응답 처리

- `OnPlayerSetActionReceived(string playerName, string cardInstanceId)`
- `OnPlayerOpenActionReceived(string playerName, string choice, int effectiveCost)`
- `OnPlayerStackResponseReceived(string playerName, string stackCardInstanceId, string opponentCardInstanceId, bool isUsingStack)`
- `ClearPendingStackResponse()`

### 타이브레이커 / 유틸

- `ResolveSimultaneousDeckout(Player p1, Player p2)`
- `GetPlayerByName(string name) : Player`
- `CloneDeck(List<Card> source) : List<Card>` *(static)*

