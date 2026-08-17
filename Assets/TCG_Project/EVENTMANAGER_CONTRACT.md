# EventManager 계약 명세 (UI팀 온보딩 가이드)

최종 수정일: 2026-08-15

---

## 개요

`Scripts/Managers/EventManager.cs`는 로직 레이어와 UI 레이어가 소통하는 **유일한 공식 채널**이다.

- **로직팀** → `EventManager` 이벤트를 **발행(Invoke)**
- **UI팀** → 이벤트를 **구독(+=)** 하여 화면에 반영
- 로직팀이 UI를 직접 호출하거나, UI팀이 로직 메서드를 직접 호출하는 것은 **금지**

> **계약 유지 원칙**  
> 이벤트 시그니처(파라미터 타입·순서)는 로직팀이 함부로 변경하지 않는다.  
> 변경 시 UI팀 구독 코드가 일제히 깨진다. 변경이 불가피하면 반드시 UI팀에 사전 공지한다.  
> Human 입력 이벤트의 **마지막 파라미터 콜백은 반드시 호출**해야 한다. 미호출 시 게임 루프가 대기 상태에 남는다. 타임아웃 시에도 기본값으로 콜백을 호출한다.

---

## A. 로직 → UI : 로직팀이 발행, UI팀이 구독하는 이벤트

### 1. 로그 / 디버그

| 이벤트 | 시그니처 | 발행 시점 |
|--------|----------|-----------|
| `OnLogMessage` | `Action<string>` | 게임 진행 중 텍스트 메시지 출력 시 |

### 2. 턴 진행

| 이벤트 | 시그니처 | 발행 시점 |
|--------|----------|-----------|
| `OnTurnStart` | `Action<int turn, string playerName>` | 매 라운드 시작 직전 |
| `OnTurnEnd` | `Action<string playerName>` | 라운드 종료 직후 |

### 3. 룰북 6페이즈

| 이벤트 | 시그니처 | 발행 시점 |
|--------|----------|-----------|
| `OnResourcePhase` | `Action<string playerName, int turn>` | 자원 페이즈 시작 |
| `OnDrawPhase` | `Action<string playerName, int turn>` | 드로우 페이즈 시작 |
| `OnSetPhase` | `Action<string playerName, int turn>` | 세트 페이즈 시작 |
| `OnOpenPhase` | `Action<string playerName, int turn>` | 오픈 페이즈 시작 |
| `OnMainPhase` | `Action<string playerName, int turn>` | 메인 페이즈 시작 |
| `OnEndPhase` | `Action<string playerName, int turn>` | 엔드 페이즈 시작 |

### 4. 게임 / 매치 단위

| 이벤트 | 시그니처 | 발행 시점 |
|--------|----------|-----------|
| `OnGameStart` | `Action<Player p1, Player p2>` | 단일 게임 시작 |
| `OnGameSet` | `Action<Player winner>` | 단일 게임 승자 결정 |
| `OnGameDraw` | `Action<Player p1, Player p2, int tiebreaker>` | 단일 게임 무승부 |
| `OnMatchSet` | `Action<Player winner>` | 3판 2선승 매치 승자 결정 |
| `OnTiebreaker` | `Action<Player p1, Player p2>` | 타이브레이커 실행 시 |
| `OnMatchDraw` | `Action<Player p1, Player p2>` | 매치 무승부 |

### 5. 플레이어 상태

| 이벤트 | 시그니처 | 발행 시점 |
|--------|----------|-----------|
| `OnLifeChange` | `Action<Player player, int newValue>` | 라이프 토큰 변경 후 (`InitializeLifeTokens` / `GainLife` / `LoseLife`). 씬 TMP 기본값은 `"0"`이므로 UI는 `OnGameStart`에서도 `player.LifeTokens`로 한 번 더 동기화한다 |
| `OnResourceChange` | `Action<Player player, int newValue>` | 자원존 매수 변경 후 |
| `OnCharacterFieldSync` | `Action<IReadOnlyList<CharacterSlotSnapshot>>` | 게임 시작/리셋 시 용병 슬롯 전체 동기화 |
| `OnCharacterSlotUpdated` | `Action<CharacterSlotSnapshot>` | 용병 능력 사용 후 단일 슬롯 갱신 |
| `OnCharacterAbilityUsed` | `Action<Player player, string characterCardId>` | 용병 고유 능력 사용 상태 변경 |

### 6. 카드 행동 / 이동

| 이벤트 | 시그니처 | 발행 시점 |
|--------|----------|-----------|
| `OnPlayCard` | `Action<Player owner, Card card>` | 카드 효과 해결 시작 (연출용). 존 이동이 아님 |
| `OnCardStateChanged` | `Action<Card card>` | 앞면/뒷면 등 상태 변경 |
| `OnPlayFailed` | `Action<Card card>` | 카드 효과 발동 실패 |
| `OnCardMove` | `Action<Card card, Player fromOwner, ZoneType fromZone, Player toOwner, ZoneType toZone>` | 존 간 이동. UI는 이 이벤트로만 reparent. `PayCost`는 ResourceZone[0] FIFO로 ResourceZone→Graveyard, 전장 교체/파괴 BattlefieldZone→Graveyard, 베로니카 능력 Deck→Hand도 발행 |
| `OnCardDraw` | `Action<Card card, Player owner, ZoneType sourceZone>` | 드로우 |
| `OnCardDiscard` | `Action<Card card, Player owner, ZoneType sourceZone>` | 버려짐 |
| `OnCardSet` | `Action<Card card, Player owner>` | 세트존 배치 직후 (`OnCardMove(Hand→SetZone)`와 함께 발행) |
| `OnCardStacked` | `Action<Card card, Player owner>` | 스택존 추가 |
| `OnCardUnstacked` | `Action<Card card, Player owner>` | 스택존 제거 |
| `OnCardBattlefield` | `Action<Card card, Player owner>` | 전장 배치 |
| `OnCardUnbattlefield` | `Action<Card card, Player owner>` | 전장 제거 |
| `OnCardResourceAdded` | `Action<Card card, Player owner>` | 자원존 추가 |

### 7. Unity 연출 제어

| 이벤트 | 시그니처 | 발행 시점 |
|--------|----------|-----------|
| `OnRequestVisualDelay` | `Action<float seconds>` | 효과 연출 사이 딜레이 요청 |

---

## B. UI → 로직 : 콜백으로 응답하는 이벤트

콜백을 호출하지 않으면 게임이 무한 대기한다. 타임아웃 시 기본값으로 콜백을 호출한다.

### B-1. 세트 페이즈

`OnRequireSetPhaseAction : Action<Player me, GameContext ctx, Action<Card> callback>`

### B-2. 오픈 페이즈

`OnRequireOpenPhaseAction : Action<Player me, Card setCard, int effectiveCost, GameContext ctx, Action<OpenPhaseChoice> callback>`

`OpenPhaseChoice`: `Open = 0`, `Abandon = 1`

### B-3. 스택 발동

`OnRequireStackResponse : Action<Player me, Card stackCard, Card opponentCard, Action<bool> callback>`

방어 스택이 상대 공격/지원에 강제 대응하는 경우 이 이벤트는 **발행되지 않는다**.

### B-4. 목록에서 선택

`OnRequireCardPick : Action<Player me, List<Card> candidates, int count, Action<List<Card>> callback>`

### B-5. 존에서 필터 선택

`OnRequireCardChoice : Action<Player me, ZoneType zone, int count, string filter, Action<List<Card>> callback>`

### B-6. 선택적 행동

`OnRequireOptionalAction : Action<Player me, string message, GameContext ctx, Action<bool> callback>`

`true` = 수행, `false` = 취소 (`LastEffectSucceeded = false`로 "그 후" 효과 스킵).

---

## C. 참고 샘플

`Scripts/UI/` 샘플은 참고용이다. 실제 인게임 보드는 `Assets/Scripts/InGameCard/`가 담당한다.

| 파일 | 담당 |
|------|------|
| `GameStatusUI.cs` | `OnLifeChange`, `OnTurnStart`, `OnLogMessage`, `OnGameStart` |
| `PhaseInputPanel.cs` | `OnRequireSetPhaseAction`, `OnRequireOpenPhaseAction` |
| `StackResponsePanel.cs` | `OnRequireStackResponse` |
| `CardSelectionPanel.cs` | `OnRequireCardPick`, `OnRequireCardChoice` |
