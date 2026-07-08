# EventManager 계약 명세 (UI팀 온보딩 가이드)

최종 수정일: 2026-03-02 (Phase 12 완료)

---

## 개요

`Scripts/Manager/EventManager.cs`는 로직 레이어와 UI 레이어가 소통하는 **유일한 공식 채널**이다.

- **로직팀** → `EventManager` 이벤트를 **발행(Invoke)**
- **UI팀** → 이벤트를 **구독(+=)** 하여 화면에 반영
- 로직팀이 UI를 직접 호출하거나, UI팀이 로직 메서드를 직접 호출하는 것은 **금지**

> **⚠ 계약 유지 원칙**  
> 이벤트 시그니처(파라미터 타입·순서)는 로직팀이 함부로 변경하지 않는다.  
> 변경 시 UI팀 구독 코드가 일제히 깨진다. 변경이 불가피하면 반드시 UI팀에 사전 공지한다.

---

## A. 로직 → UI : 로직팀이 발행, UI팀이 구독하는 이벤트

### 1. 로그 / 디버그

| 이벤트 | 시그니처 | 발행 시점 | 사용 예 |
|--------|----------|-----------|---------|
| `OnLogMessage` | `Action<string>` | 게임 진행 중 텍스트 메시지 출력 시 | 디버그 콘솔, 배틀로그 패널 |

> `<color=cyan>...</color>` 같은 Unity Rich Text 태그 포함 가능.

---

### 2. 턴 진행

| 이벤트 | 시그니처 | 발행 시점 |
|--------|----------|-----------|
| `OnTurnStart` | `Action<int turn, string playerName>` | 매 라운드 시작 직전 |
| `OnTurnEnd` | `Action<string playerName>` | 라운드 종료 직후 |

---

### 3. 룰북 6페이즈

| 이벤트 | 시그니처 | 발행 시점 |
|--------|----------|-----------|
| `OnResourcePhase` | `Action<string playerName, int turn>` | 자원 페이즈 시작 (자원덱→자원존 이동 직전) |
| `OnDrawPhase` | `Action<string playerName, int turn>` | 드로우 페이즈 시작 (드로우 직전) |
| `OnSetPhase` | `Action<string playerName, int turn>` | 세트 페이즈 시작 (세트 직전) |
| `OnOpenPhase` | `Action<string playerName, int turn>` | 오픈 페이즈 시작 (공개/폐기 선택 직전) |
| `OnMainPhase` | `Action<string playerName, int turn>` | 메인 페이즈 시작 (SpeedResolver 처리 직전) |
| `OnEndPhase` | `Action<string playerName, int turn>` | 엔드 페이즈 시작 (승리 확인·버프 만료 직전) |

---

### 4. 게임 / 매치 단위

| 이벤트 | 시그니처 | 발행 시점 |
|--------|----------|-----------|
| `OnGameStart` | `Action<Player p1, Player p2>` | 단일 게임 시작 |
| `OnGameSet` | `Action<Player winner>` | 단일 게임 승자 결정 |
| `OnGameDraw` | `Action<Player p1, Player p2, int tiebreaker>` | 단일 게임 무승부 |
| `OnMatchSet` | `Action<Player winner>` | 3판 2선승 매치 승자 결정 |
| `OnMatchDraw` | `Action<Player p1, Player p2>` | 3판 2선승 매치 무승부 |

---

### 5. 플레이어 상태

| 이벤트 | 시그니처 | 발행 시점 |
|--------|----------|-----------|
| `OnLifeChange` | `Action<Player player, int newValue>` | 라이프 토큰 변경 시 (`newValue` = 변경 후 값) |
| `OnPrizeChange` | `Action<Player player, int newValue>` | 승점 변경 시 |

---

### 6. 카드 행동 / 이동

| 이벤트 | 시그니처 | 발행 시점 |
|--------|----------|-----------|
| `OnPlayCard` | `Action<Player owner, Card card>` | 카드 사용(Play) 직후 |
| `OnCardMove` | `Action<Card card, Player fromOwner, ZoneType fromZone, Player toOwner, ZoneType toZone>` | 카드가 존 간 이동할 때 |
| `OnCardDraw` | `Action<Card card, Player owner, ZoneType sourceZone>` | 카드가 드로우될 때 |

---

### 7. Unity 연출 제어

| 이벤트 | 시그니처 | 발행 시점 |
|--------|----------|-----------|
| `OnRequestVisualDelay` | `Action<float seconds>` | 효과 연출 사이 딜레이 요청 시 |

> BattleManager에서 `WaitForSeconds(seconds)` 코루틴으로 대기한다.  
> 콘솔 환경에서는 구독자 없이 자동 무시된다.

---

## B. UI → 로직 : UI팀이 콜백을 통해 응답하는 이벤트

이 이벤트들은 로직 레이어가 **UI의 응답을 기다리며 일시 정지**할 때 발행한다.  
UI팀은 이벤트를 구독하고, 플레이어의 조작이 완료되면 **마지막 파라미터인 콜백 Action을 호출**하여 로직을 재개시킨다.

> **콜백을 반드시 호출해야 한다.** 호출하지 않으면 게임이 무한 대기 상태에 빠진다.

---

### B-1. 세트 페이즈 — 카드 선택

```
OnRequireSetPhaseAction
  : Action<Player me, GameContext ctx, Action<Card> callback>
```

| 파라미터 | 설명 |
|----------|------|
| `me` | 선택 중인 플레이어 (`me.Hand`에 패가 있음) |
| `ctx` | 현재 게임 컨텍스트 |
| `callback(Card card)` | 선택한 카드를 넘기면 로직 재개 |

**UI 처리 순서:**  
1. `me.Hand` 카드 목록을 버튼으로 표시  
2. 플레이어가 카드 선택  
3. `callback(selectedCard)` 호출

---

### B-2. 오픈 페이즈 — 공개 또는 폐기 결정

```
OnRequireOpenPhaseAction
  : Action<Player me, Card setCard, int effectiveCost, GameContext ctx, Action<OpenPhaseChoice> callback>
```

| 파라미터 | 설명 |
|----------|------|
| `me` | 결정 중인 플레이어 |
| `setCard` | 세트존에 있는 카드 (뒷면 공개 대상) |
| `effectiveCost` | 전장 코스트 감소가 반영된 **실효 코스트** (UI에 표시 권장) |
| `ctx` | 현재 게임 컨텍스트 |
| `callback(OpenPhaseChoice choice)` | `Open` 또는 `Abandon` 선택 후 호출 |

`OpenPhaseChoice` enum: `Open = 0`, `Abandon = 1`

---

### B-3. 스택 발동 여부

```
OnRequireStackResponse
  : Action<Player me, Card stackCard, Card opponentCard, Action<bool> callback>
```

| 파라미터 | 설명 |
|----------|------|
| `me` | 스택 카드를 보유한 플레이어 |
| `stackCard` | 발동 가능한 스택 카드 |
| `opponentCard` | 상대가 이번에 사용한 카드 (팝업 설명 용도) |
| `callback(bool activate)` | `true` = 발동, `false` = 패스 |

> 시간 제한 카운트다운 UI를 넣을 경우: 타임아웃 시 `callback(false)` 호출.

> **Phase 18 (룰북 준수)**: 방어 스택이 상대 공격/지원 카드에 대응 가능한 경우, 로직 레이어가 **강제 발동**하므로 `OnRequireStackResponse`가 호출되지 않는다. 이때 UI 팝업은 표시되지 않는다.

---

### B-4. 제시된 카드 목록에서 선택

```
OnRequireCardPick
  : Action<Player me, List<Card> candidates, int count, Action<List<Card>> callback>
```

| 파라미터 | 설명 |
|----------|------|
| `me` | 선택 중인 플레이어 |
| `candidates` | 선택 가능한 카드 목록 (예: 베로니카 덱 탑 3장) |
| `count` | 선택해야 하는 장 수 |
| `callback(List<Card> picked)` | 선택한 카드 목록을 넘기면 로직 재개 |

> `count`장 선택이 완료될 때까지 확정 버튼 비활성화 처리 권장.

---

### B-5. 존에서 카드 선택 (필터 조건)

```
OnRequireCardChoice
  : Action<Player me, ZoneType zone, int count, string filter, Action<List<Card>> callback>
```

| 파라미터 | 설명 |
|----------|------|
| `me` | 선택 중인 플레이어 |
| `zone` | 검색 대상 존 (`ZoneType` enum) |
| `count` | 선택해야 하는 최대 장 수 |
| `filter` | 카드 필터 (예: `"character:ELLI,type:Attack"` — 쉼표 구분 AND 조건) |
| `callback(List<Card> chosen)` | 선택 완료 후 호출 |

**filter 형식 규칙:**

| 키 | 설명 | 예시 |
|----|------|------|
| `character:ID` | 특정 캐릭터 덱 카드만 (`CharacterId` 비교) | `character:ELLI` |
| `type:TypeName` | 특정 카드 타입만 | `type:Attack`, `type:Defense`, `type:Support`, `type:Resource` |
| `""` (빈 문자열) | 필터 없음 — 존 전체 카드 | |

---

## C. 참고 샘플 코드

`Scripts/UI/` 폴더에 이벤트 구독 구조 샘플 MonoBehaviour가 있다.  
UI팀은 이 파일들을 참고하되, 직접 덮어써도 무방하다.

| 파일 | 담당 이벤트 |
|------|------------|
| `GameStatusUI.cs` | `OnLifeChange`, `OnTurnStart`, `OnLogMessage`, `OnGameStart` |
| `PhaseInputPanel.cs` | `OnRequireSetPhaseAction`, `OnRequireOpenPhaseAction` |
| `StackResponsePanel.cs` | `OnRequireStackResponse` |
| `CardSelectionPanel.cs` | `OnRequireCardPick`, `OnRequireCardChoice` |

---

## D. 구독 코드 예시 (C#)

```csharp
void OnEnable()
{
    EventManager.OnLifeChange += HandleLifeChange;
    EventManager.OnRequireSetPhaseAction += HandleSetPhase;
}

void OnDisable()
{
    EventManager.OnLifeChange -= HandleLifeChange;
    EventManager.OnRequireSetPhaseAction -= HandleSetPhase;
}

void HandleLifeChange(Player player, int newValue)
{
    if (player == localPlayer)
        lifeText.text = $"라이프: {newValue}";
}

void HandleSetPhase(Player me, GameContext ctx, Action<Card> callback)
{
    // 패 카드 목록 표시 후 플레이어 선택을 기다린다.
    ShowHandPanel(me.Hand, selectedCard =>
    {
        HideHandPanel();
        callback(selectedCard); // 반드시 호출!
    });
}
```
