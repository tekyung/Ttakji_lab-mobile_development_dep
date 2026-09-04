# EventManager 계약 명세 (UI팀 온보딩 가이드)

최종 확인: 2026-08-18 (온라인 대전 반영)

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

> ### ⚠️ 온라인(사람 vs 사람)에서는 규칙이 다르다 (2026-08-18)
>
> `ServerGameManager`는 이 이벤트들을 쏠 때 **콜백 자리에 `null`을 넣는다**(L520·L661).
> 응답을 로컬 콜백이 아니라 **파이어베이스 요청**으로 받도록 설계돼 있기 때문이다.
> 그래서 온라인에서는 다음을 지켜야 한다:
>
> - **콜백이 `null`인지 반드시 확인**하고, null이면 `session_game_manage`의 전송 API로 보낸다
>   (`SendSetPhaseChoice` / `SendOpenPhaseChoice` — `OnlineMatchStarter`가 감싸 두었다)
> - 그냥 `callback.Invoke(...)` 하면 `NullReferenceException`이 나고, 그 예외가 서버 코루틴을 죽여
>   **게임이 그 페이즈에서 통째로 멈춘다.** 실제로 겪은 사고다
> - 요청 대상이 **내 플레이어인지** 확인한다. 온라인은 양쪽 다 `UserType.Human`이라
>   사람 여부만 보면 호스트가 게스트 몫까지 답해 버린다 → `LocalPlayerContext.IsMine` 사용

### B-1. 세트 페이즈

`OnRequireSetPhaseAction : Action<Player me, GameContext ctx, Action<Card> callback>`

### B-2. 오픈 페이즈

`OnRequireOpenPhaseAction : Action<Player me, Card setCard, int effectiveCost, GameContext ctx, Action<OpenPhaseChoice> callback>`

`OpenPhaseChoice`: `Open = 0`, `Abandon = 1`

### B-3. 스택 발동

`OnRequireStackResponse : Action<Player me, Card stackCard, Card opponentCard, Action<bool> callback>`

> 🚫 **현재 발행처가 0곳인 죽은 계약이다 (2026-08-18 확인).**
> 스택 발동 여부는 `OnRequireCardPick`(B-4)으로 묻는다 (`ServerGameManager` L958, `BattleManager` 동일).
> 되살릴지 폐기할지 아직 결정되지 않았다. 새로 구독하지 말 것.

### B-3-1. 간접 발동에 대한 스택 응답

`OnRequireIndirectStackResponse : Action<Player stackOwner, Player cardPlayer, Card playedCard, Action<bool> callback>`

[기뢰]처럼 **다른 카드가 대신 내주는** 경로로 카드가 나갈 때, 상대에게 스택으로 막을지 묻는다.

> 왜 따로 있는가 — 효과(`PlayFromBufferEffect`)는 진행 코드를 직접 부를 수 없다.
> 그래서 이 훅으로 위임하고, 구독자가 자기 방식(코루틴/동기)으로 응답을 받은 뒤 콜백을 부른다.
>
> ⚠️ 구독자는 **`context == null` 가드를 반드시 넣을 것.** 온라인에서 컨텍스트 없이 불려
> 매 프레임 `NullReferenceException`이 난 적이 있다(`BattleManager.HandleIndirectStackResponse`).
> 무한 대기도 막아야 한다 — 제한 시간이 0 이하이면 `GameRules.ChooseWaitTime`으로 되돌린다.

### B-4. 목록에서 선택

`OnRequireCardPick : Action<Player me, List<Card> candidates, int count, Action<List<Card>> callback>`

### B-5. 존에서 필터 선택

`OnRequireCardChoice : Action<Player me, ZoneType zone, int count, string filter, Action<List<Card>> callback>`

> 🚫 **현재 발행처가 0곳인 죽은 계약이다 (2026-08-18 확인).** 새로 구독하지 말 것.

### B-6. 선택적 행동

`OnRequireOptionalAction : Action<Player me, string message, GameContext ctx, Action<bool> callback>`

`true` = 수행, `false` = 취소 (`LastEffectSucceeded = false`로 "그 후" 효과 스킵).

---

## B-7. 발행되지 않는 이벤트 (선언만 있음)

아래는 `EventManager`에 선언돼 있으나 **현재 발행하는 코드가 없다.** 구독해도 아무 일도 일어나지 않는다.

| 이벤트 | 비고 |
| ------ | ---- |
| `OnCardDiscard` | 폐기는 `OnCardMove(→Graveyard)`로 처리된다 |
| `OnCardUnstacked` | 스택 소진도 `OnCardMove`로 처리된다 |
| `OnCardUnbattlefield` | 전장 파괴도 `OnCardMove`로 처리된다 |
| `OnRequireStackResponse` · `OnRequireCardChoice` | 위 참조 |

---

## C. 참고 샘플

`Scripts/UI/` 샘플은 참고용이다. 실제 인게임 보드는 `Assets/Scripts/InGameCard/`가 담당한다.

| 파일 | 담당 |
|------|------|
| `GameStatusUI.cs` | `OnLifeChange`, `OnTurnStart`, `OnLogMessage`, `OnGameStart` |
| `PhaseInputPanel.cs` | `OnRequireSetPhaseAction`, `OnRequireOpenPhaseAction` |
| `StackResponsePanel.cs` | `OnRequireStackResponse` |
| `CardSelectionPanel.cs` | `OnRequireCardPick`, `OnRequireCardChoice` |
