# TCG_Project 상태 진단서 및 해결법 추천

작성일: 2026-03-02  
작성 근거: Phase 12 완료 직후 코드 전수 검토 및 중간 평가 세션  
대상 독자: 이 프로젝트를 이어받는 다음 AI

> **범위 한정**: 이 문서는 **게임 로직 레이어**만 다룬다. Unity 씬 배치, UI MonoBehaviour 연결,
> 덱 빌딩 화면 등 UI팀 소유 작업은 진단 대상에서 제외한다.

---

## 1. 현재 상태 스냅샷

### 완료된 것

| 항목                                                                                                                 | 상태    |
| -------------------------------------------------------------------------------------------------------------------- | ------- |
| 룰북 6페이즈 엔진 (ConsoleRunner + BattleManager)                                                                    | ✅ 완성 |
| SpeedResolver — 9단계 스피드 해결                                                                                    | ✅ 완성 |
| DamageResolver — 데미지 파이프라인 (아머·슈퍼아머·무적·반격)                                                         | ✅ 완성 |
| 효과 시스템 — 6개 통합 클래스 (DamageEffect·BuffEffect·MoveEffect·BattlefieldEffect·SelfPlaceEffect·CompositeEffect) | ✅ 완성 |
| 카드 데이터 — 룰북 카드 40종 + 캐릭터 4종 JSON                                                                       | ✅ 완성 |
| 캐릭터 능력 4종 (ELLIE·VERONICA·DAINA·SONIA)                                                                         | ✅ 완성 |
| 스택 카드 시스템 (스택존 + 반응 타이밍)                                                                              | ✅ 완성 |
| 전장 카드 시스템 (매 턴·자원페이즈 주기 효과·코스트 감소)                                                            | ✅ 완성 |
| 3판 2선승 MatchManager                                                                                               | ✅ 완성 |
| 덱 검증 API (DeckValidator) — UI팀 계약                                                                              | ✅ 완성 |
| EventManager 레거시 정리 (Phase 12)                                                                                  | ✅ 완성 |
| PlayBuffer + TemporaryCostEffect + PlayFromBufferEffect (기뢰 카드 처리)                                             | ✅ 완성 |
| EVENTMANAGER_CONTRACT.md — UI팀 온보딩 문서                                                                          | ✅ 완성 |

### 미구현 룰 (신규 게임 룰 — Phase 16·17 예정)

| 룰                    | 현재 상태              | 목표 (Phase)                                      |
| --------------------- | ---------------------- | ------------------------------------------------- |
| **듀얼 캐릭터 덱**     | 단일 캐릭터 10종×2장   | 2종 캐릭터 선택, 10종류×2장=20장, 비율 자유 (16) |
| **캐릭터 능력 1회 제한** | 게임당 무제한 발동     | 게임당 1회만 사용, 매치 각 게임마다 리셋 (17)     |

### 대결 모드별 로직 구현 정도

| 모드           | 로직 완성도 | 비고                                                                                |
| -------------- | ----------- | ----------------------------------------------------------------------------------- |
| Bot vs Bot     | 100%        | dotnet run으로 즉시 검증 가능                                                       |
| Human vs Bot   | 95%         | BattleManager 코루틴+이벤트 분기 완성. UI 연결 없을 때 폴백 동작(HumanBrain)은 약함 |
| Human vs Human | 30%         | 인프라만. 동시 세트/오픈 페이즈 동시 행동 처리 로직 미구현                          |

---

## 2. 로직 레이어 미해결 문제 목록

### 문제 A — ConsoleRunner와 BattleManager의 코드 중복 [우선순위: 높음]

**현상:**  
게임 루프를 구동하는 로직이 `ConsoleRunner.cs`(콘솔 테스트용)와
`BattleManager.cs`(Unity MonoBehaviour)에 거의 동일하게 중복 존재한다.

**중복 메서드 목록:**

| 메서드                                            | ConsoleRunner |   BattleManager   | 내용                                          |
| ------------------------------------------------- | :-----------: | :---------------: | --------------------------------------------- |
| `CreateResourceDeck()`                            |     L531      |       L848        | 자원 카드 15장 생성                           |
| `DrawCards()`                                     |     L745      |     별도 구현     | 덱 → 패 드로우                                |
| `GetEffectiveCost()`                              |     L310      |     별도 구현     | 전장 코스트 감소 반영                         |
| `MatchesFilter()` / `CardMatchesFilter()`         |  L329, L616   |       L800        | 카드 필터 평가                                |
| `ApplyBattlefieldTurnEffects()`                   |     L581      |     별도 구현     | 전장 매 턴 효과                               |
| `ApplyBattlefieldResourcePhaseEffects()`          |     L602      |     별도 구현     | 전장 자원페이즈 효과                          |
| `HandleStackActivation()`                         |     L692      |       L565        | 스택 반응 처리                                |
| `ApplyEllieExtraAttack()`                         |     L718      |       L612        | 엘리 추가 발동                                |
| `ApplySoniaPreSetAbility()`                       |     L673      |       L353        | 소니아 세트 전 능력                           |
| `BotChooseSetCard()` / `BotChooseOpenOrAbandon()` |  L250, L281   | BotBrain에 위임됨 | (BM에서는 Brain으로 해결, CR에서는 직접 구현) |

**위험도:**  
어떤 게임 규칙이나 캐릭터 능력을 수정할 때 반드시 두 파일을 같이 수정해야 한다.
한쪽만 수정하면 ConsoleRunner와 BattleManager의 동작이 달라진다.

**해결 방향:**  
공유 로직을 `Scripts/Systems/GameLogicHelpers.cs`(정적 클래스)로 추출하고
두 파일이 이를 호출하는 구조로 리팩터링한다.

```
// 제안 구조
public static class GameLogicHelpers
{
    public static int GetEffectiveCost(Card card, Player player) { ... }
    public static void ApplyBattlefieldTurnEffects(Player p, GameContext ctx) { ... }
    public static void ApplyBattlefieldResourcePhaseEffects(Player p, GameContext ctx) { ... }
    // DrawCards는 Player 확장 메서드 또는 MoveEffect로 처리 가능
}
```

---

### 문제 B — MatchesFilter 4중 복제 [우선순위: 중간]

**현상:**  
카드가 필터 조건에 맞는지 평가하는 동일 로직이 4곳에 존재한다.

| 위치                    | 메서드명                                           |
| ----------------------- | -------------------------------------------------- |
| `ConsoleRunner.cs` L329 | `MatchesFilter(Card, string)`                      |
| `ConsoleRunner.cs` L616 | `CardMatchesFilter(Card, string)` — 위와 동일 로직 |
| `BattleManager.cs` L800 | `CardMatchesFilter(Card, string)`                  |
| `BotBrain.cs` L87       | `MatchesFilter(Card, string)`                      |

**이미 정식 구현이 존재함:**  
`Scripts/Effects/CardSelector.cs`의 `CardSelector.ApplyFilter(List<Card>, string)`가
공식 구현이며 `"type:Effect"` 통합 필터까지 지원한다(ConsoleRunner/BattleManager 버전보다 기능 우위).

**해결 방향:**  
ConsoleRunner, BattleManager, BotBrain에서 직접 구현된 `MatchesFilter()`를
`CardSelector.ApplyFilter()` 호출로 교체한다.

```csharp
// 교체 전 (ConsoleRunner, BattleManager, BotBrain 각각 구현)
private bool MatchesFilter(Card card, string filter) { ... }

// 교체 후
using TCG_Project.Scripts.Effects;
// 단일 카드 매칭:
bool matches = CardSelector.ApplyFilter(new List<Card> { card }, filter).Count > 0;
// 또는 CardSelector에 정적 단일 카드 오버로드 추가:
public static bool MatchesSingle(Card card, string filter) { ... }
```

---

### 문제 C — 캐릭터 능력의 하드코딩 [우선순위: 중간]

**현상:**  
엘리·베로니카·다이나·소니아 4종의 캐릭터 능력이 `ConsoleRunner.cs`와
`BattleManager.cs`에 ID 문자열 비교(`CharacterCardId == "ELLI-01"`) 조건문으로
하드코딩되어 있다.

**위험도:**  
5번째 캐릭터가 추가될 때 두 파일 각각에 분기를 추가해야 한다.
JSON에서 캐릭터 능력을 정의하는 데이터 주도 원칙(아키텍처 원칙 4)에 위배된다.

**현재 상황 파악:**  
`Data/Character.json`에는 캐릭터 설명과 `triggerPhase`가 있지만,
실제 능력 실행 코드는 JSON과 무관하게 C# 코드에 직접 존재한다.

```json
// 현재 Character.json — 데이터만 있고 실행 코드는 분리됨
{
  "id": "ELLI-01",
  "triggerPhase": "MainPhase",
  "description": "공격 카드 사용 후..."
}
```

**해결 방향 (2가지 옵션):**

옵션 1 — 가벼운 개선 (권장):  
`ICharacterAbility` 인터페이스 + 캐릭터별 클래스 분리.

```
Scripts/Abilities/
  ICharacterAbility.cs          ← 인터페이스
  EllieAbility.cs               ← ELLI-01 능력 구현
  VeronicaAbility.cs            ← VERO-01 능력 구현
  DainaAbility.cs               ← DAIN-01 능력 구현
  SoniaAbility.cs               ← SONI-01 능력 구현
  CharacterAbilityRegistry.cs   ← ID → 인스턴스 매핑 딕셔너리
```

게임 루프에서는 `CharacterAbilityRegistry.Get(player.CharacterCardId)?.Execute(phase, player, ctx)` 한 줄로 처리.

옵션 2 — 무거운 개선:  
`Character.json`에 effect 배열을 추가하고 `GameDataManager`가 캐릭터 능력을
효과 클래스로 조립. 그러나 베로니카의 "덱 탑 보기 후 1장 선택"처럼
단순 효과 조합으로 표현하기 어려운 능력이 있어 구현 복잡도가 높다.

---

### 문제 D — 자원 카드 프로그래밍 생성 [우선순위: 낮음]

**현상:**  
`ConsoleRunner.cs` L531, `BattleManager.cs` L848에서 자원 카드 15장을
JSON 없이 코드로 직접 생성한다.

```csharp
deck.Add(new Card { Id = $"res_{i:000}", Name = "자원 카드", Type = CardType.Resource, ... });
```

**자원 카드의 개념 (설계 확정):**  
자원 카드는 **특수 효과를 가지지 않는** 코스트 전용 카드다.  
모든 플레이어가 동일한 **깡통 카드** (단일 종류)를 15장씩 보유하며,  
구별되는 종류나 개별 효과는 존재하지 않는다.  
→ JSON화 시에도 1종(예: `RES-01`)만 정의하면 됨.

**위험도:**  
현재 설계와 동일하게 깡통 카드만 사용되므로 게임 동작에 영향 없음.  
JSON화는 데이터 주도 원칙 완성 및 코드 중복 제거를 위한 목적.

**해결 방향:**  
`Data/ResourceCards.json`에 자원 카드 템플릿 1종(`RES-01`) 추가 →
`GameDataManager.LoadResourceCards()` 메서드 신규 추가 →
`ConsoleRunner`·`BattleManager`의 `CreateResourceDeck()`은
`AllCards["RES-01"].Clone()`을 15회 반복하는 1줄로 단순화.

```json
// Data/ResourceCards.json (제안) — 깡통 카드 1종
{
  "ResourceCards": [
    {
      "id": "RES-01",
      "name": "자원 카드",
      "type": "Resource",
      "speed": 0,
      "cost": 0,
      "description": "코스트 지불에 사용되는 자원 카드. 모든 플레이어 동일."
    }
  ]
}
```

---

### 문제 E — BotBrain 전략 기초 수준 [우선순위: 낮음 — 밸런싱 단계에서 개선]

**현상:**  
현재 BotBrain의 4개 결정 메서드는 모두 단순 그리디 전략이다.

- `ChooseSetCard`: Speed 낮은 순, 같으면 Defense > Attack > Support
- `ChooseOpenOrAbandon`: 코스트 지불 가능하면 무조건 공개
- `ChooseStackActivation`: 상대 Attack/Support에 Defense 스택 자동 발동
- `ChooseCardsFromZone`: 조건 맞는 카드 앞에서 N장

**현실적 문제:**

- 코스트를 낭비하거나 불리한 타이밍에 카드를 공개하는 실수를 함
- 라이프 차이나 남은 자원 수를 전혀 고려하지 않음
- 상대 캐릭터·카드 타입에 따른 전략 변화 없음

**해결 방향:**  
`IPlayerBrain` 인터페이스가 이미 잘 설계되어 있으므로
`BotBrain`을 교체하거나 상속하는 `SmartBotBrain` 구현체를 추가하면 된다.
기존 `BotBrain`은 폴백용으로 유지.

---

### 문제 F — Human vs Human 동시 행동 미처리 [우선순위: 낮음]

**현상:**  
룰북에서 세트 페이즈와 오픈 페이즈는 "양측 동시 확정"이다.
현재 코드는 p1 완료 후 p2를 처리하는 순차 구조여서,
로컬 대전 시 p2가 p1의 선택을 미리 알 수 있는 정보 노출 문제가 있다.

```csharp
// BattleManager.cs — 순차 처리 (p1 → p2 순서)
yield return StartCoroutine(PerformSetCard(p1));
yield return StartCoroutine(PerformSetCard(p2));
```

**해결 방향:**  
두 플레이어의 선택을 병렬 코루틴으로 처리하거나,
"봉인 확인" 방식(양측 모두 선택 완료 후 동시 공개)을 구현해야 한다.
온라인 멀티플레이라면 서버 중재가 필요하다.
현재 로직팀 범위에서는 필요성이 발생할 때 해결하는 것이 적합하다.

---

### 문제 G — 룰북 검증 불일치 (최신 룰북 대비) [우선순위: 중간]

`전투!용병의 시대 룰북.txt` 대비 검증 결과. 대부분 일치하나 아래 4건은 수정 필요.

| 항목 | 룰북 | 현재 구현 | 조치 |
|------|------|-----------|------|
| 반격 + 무적 | 무적은 반격 피해도 막음 | `LoseLife()` 직접 호출로 무적 미검사 | 반격 피해도 `DamageResolver` 경유 또는 무적 체크 추가 |
| 다음턴 버프 | "다음턴 자원 페이즈부터" 적용 | 드로우 페이즈에서 `ApplyNextTurnBuffs()` 호출 | 자원 페이즈 시작 시점으로 이동 |
| 스택 강제 사용 | "대응되는 효과가 나온다면 강제로 사용" | Human이 패스 선택 가능 | 대응 시 발동 필수 로직 추가 검토 |
| "그 후" 효과 | 선행 효과 실패 시 후속 효과 불발 | `CompositeEffect`가 Step 성공 여부 미검사 | Step 실패 시 후속 중단 로직 추가 |

---

## 3. 권장 다음 작업 순서

**전체 순서:** Phase 13 → 14 → 15 → **16(듀얼 캐릭터 덱)** → **17(능력 1회 제한)** → **18(룰북 검증 불일치)**

Phase 16·17은 신규 게임 룰 적용. Phase 14(캐릭터 능력 분리)와 Phase 17(1회 제한)은 순서를 바꿔도 되며, 14를 먼저 하면 17 로직을 ICharacterAbility 내부에 통합하는 것이 깔끔함. Phase 18은 룰북 준수 강화.

---

### Phase 13 — 공유 로직 추출 (문제 A + 문제 B 통합 해결)

**목표:** ConsoleRunner와 BattleManager의 중복 제거.

**작업 내용:**

1. `Scripts/Systems/GameLogicHelpers.cs` 정적 클래스 신규 생성
   - `GetEffectiveCost(Card, Player)` — 전장 코스트 감소 반영 계산
   - `ApplyBattlefieldTurnEffects(Player, GameContext)` — 매 턴 전장 효과
   - `ApplyBattlefieldResourcePhaseEffects(Player, GameContext)` — 자원페이즈 전장 효과
   - `DrawCards(Player, int, GameContext)` — 덱 → 패 드로우 (이벤트 포함)

2. `CardSelector.cs`에 단일 카드 오버로드 추가

   ```csharp
   public static bool MatchesSingle(Card card, string filter)
       => ApplyFilter(new List<Card> { card }, filter).Count > 0;
   ```

3. ConsoleRunner, BattleManager, BotBrain에서 중복 구현 제거 후
   `GameLogicHelpers` / `CardSelector.MatchesSingle` 호출로 교체

**예상 난이도:** 중간 (로직 변경 없음, 이동 작업)  
**검증 기준:** `dotnet run` 동작 동일, 빌드 오류 0개

---

### Phase 14 — 캐릭터 능력 시스템 분리 (문제 C 해결)

**목표:** 5번째 캐릭터 추가 시 두 파일을 동시 수정하는 문제 제거.

**작업 내용:**

1. `Scripts/Abilities/` 폴더 신규 생성
2. `ICharacterAbility` 인터페이스 정의
   ```csharp
   public interface ICharacterAbility
   {
       string CharacterCardId { get; }
       // 각 페이즈 훅 — 해당 페이즈에서만 호출됨
       void OnDrawPhase(Player me, GameContext ctx);     // VERONICA
       void OnSetPhase(Player me, GameContext ctx);       // SONIA
       void OnOpenPhaseAbandon(Player me, GameContext ctx); // DAINA
       void OnMainPhaseAfterAttack(Player me, Card played, Player enemy, GameContext ctx); // ELLIE
   }
   ```
3. `EllieAbility`, `VeronicaAbility`, `DainaAbility`, `SoniaAbility` 구현 클래스 작성
4. `CharacterAbilityRegistry.cs` — ID → 인스턴스 딕셔너리
5. ConsoleRunner, BattleManager에서 `if (CharacterCardId == "ELLI-01")` 분기를
   `CharacterAbilityRegistry.Get(player.CharacterCardId)?.On*(...)` 호출로 교체

**예상 난이도:** 중간  
**검증 기준:** `dotnet run` 캐릭터 능력 4종 정상 동작, 5번째 캐릭터 추가 시 두 파일 수정 불필요

---

### Phase 15 — 자원 카드 JSON화 (문제 D 해결)

**목표:** 데이터 주도 원칙 완성.

**작업 내용:**

1. `Data/ResourceCards.json` 신규 생성 (RES-01 1종)
2. `GameDataManager.LoadResourceCards(basePath)` 추가
3. `ConsoleRunner.InitializeSingleGame()`, `BattleManager.InitializeSingleGame()`에서
   `LoadResourceCards()` 호출 추가
4. `CreateResourceDeck()`을 `AllCards["RES-01"].Clone()` 기반으로 단순화

**예상 난이도:** 쉬움 (1~2시간)  
**검증 기준:** 빌드 오류 0개, 게임 루프 동작 동일

---

### Phase 16 — 듀얼 캐릭터 덱 구축 룰 (신규 게임 룰 1)

**목표:** 메인덱 구축 규칙 변경 — 2종 캐릭터 선택, 10종류×2장=20장, 두 테마 비율 자유.

**룰 변경 요약:**

- 플레이어는 **서로 다른 용병(캐릭터) 카드 2종류**를 고른다.
- 각 용병 테마의 카드 전체 20종류 중에서 **총 10종류**를 골라, 각 2장씩 = **20장 메인덱**을 구축한다.
- 두 테마 간 비율은 자유 (예: 7:3, 5:5, 9:1 등).
- **봇 시뮬레이션**: 임의로 5:5 비율로 덱 생성 (캐릭터 A 5종×2장 + 캐릭터 B 5종×2장 = 20장).

**작업 내용:**

1. `Player`에 `CharacterCardIds` (또는 `PrimaryCharacterId` + `SecondaryCharacterId`) 필드 추가 — 기존 `CharacterCardId` 단일 값에서 2종 지원으로 확장
2. `DeckValidator` 확장
   - `ValidateDeck(mainDeck, characterCardId1, characterCardId2)` 오버로드 추가
   - 검증 규칙: 10종×2장=20장, 모든 카드가 두 캐릭터 중 하나에 소속, 각 캐릭터 풀 내 카드만 허용
   - `ValidateFullDeckSet(mainDeck, resourceDeck, charId1, charId2)` 오버로드 추가
3. `ConsoleRunner` / `BattleManager` 덱 생성 로직 수정
   - 봇: 2종 캐릭터 지정 (예: ELLI-01 + VERO-01), 5종+5종×2장=20장 생성
   - `CreateDeckFromIds()` → `CreateDualCharacterDeck(charId1, charId2, ratioA, ratioB)` 형태로 변경
4. `Data/RulebookCards.json` — 현재 캐릭터당 10종. 룰상 "20종류"는 향후 확장 시 20종으로 늘릴 수 있음. Phase 16에서는 기존 10종×2캐릭터 풀에서 10종 선택하는 구조로 구현

**예상 난이도:** 중간  
**검증 기준:** `dotnet run` 봇 5:5 덱으로 정상 매치 진행, DeckValidator 신규 API 호출 시 검증 통과

---

### Phase 17 — 캐릭터 능력 게임당 1회 제한 (신규 게임 룰 2)

**목표:** 모든 용병의 고유 능력은 게임당 1번만 사용 가능.

**룰 변경 요약:**

- ELLIE, VERONICA, DAINA, SONIA 등 모든 캐릭터의 고유 능력은 **단일 게임 내 1회만** 발동 가능.
- 매치(3판 2선승)의 각 게임마다 카운트 리셋. (게임 1에서 사용 → 게임 2에서 다시 사용 가능)

**작업 내용:**

1. `Player`에 `UsedCharacterAbilityThisGame` (또는 `HasUsedCharacterAbility`) 플래그 추가
   - `ResetForNewGame()` 호출 시 초기화
2. 캐릭터 능력 발동 직전 체크 로직 추가
   - `ConsoleRunner`: `DrawOrVeronica`, `ApplySoniaPreSetAbility`, `BotChooseOpenOrAbandon`(DAINA), `ApplyEllieExtraAttack` 각각에서 `player.UsedCharacterAbilityThisGame` 확인
   - `BattleManager`: 동일한 4개 훅에서 확인
3. 능력 사용 시 `player.MarkCharacterAbilityUsed()` (또는 `UsedCharacterAbilityThisGame = true`) 호출
4. **Phase 14(캐릭터 능력 시스템 분리)와 연계**: Phase 14에서 `ICharacterAbility`를 도입할 경우, `CanUse(Player)` / `MarkUsed(Player)` 메서드를 인터페이스에 포함하여 Phase 17 로직을 통합

**예상 난이도:** 쉬움  
**검증 기준:** `dotnet run` 실행 시 베로니카 능력 2회 연속 발동 불가, 엘리 추가 발동도 게임당 1회로 제한됨 확인

**Phase 14와의 순서:** Phase 14(캐릭터 능력 분리)를 먼저 진행할 경우, Phase 17의 "1회 제한" 로직을 `ICharacterAbility` 구현체 내부에 통합하는 것이 깔끔함. Phase 14를 나중에 할 경우 Phase 17을 독립적으로 먼저 적용 가능.

---

### Phase 18 — 룰북 검증 불일치 수정 (문제 G 해결)

**목표:** 최신 룰북(`전투!용병의 시대 룰북.txt`)과의 불일치 4건 수정.

**작업 내용:**

1. **반격 + 무적**: `DamageResolver` 반격 처리 시 `attacker.IsInvincible` 체크 추가. 무적이면 반격 피해 0.
2. **다음턴 버프 시점**: `ApplyNextTurnBuffs()` 호출을 드로우 페이즈 → **자원 페이즈** 시작으로 이동.
3. **스택 강제 사용**: `OnRequireStackResponse` 시 대응 가능한 스택이 있으면 Human도 발동 필수. (선택적, UI 연동 영향 있음)
4. **"그 후" 효과**: `CompositeEffect`에 Step 실패 시 후속 중단 옵션 또는 `"그후"` 시맨틱 지원.

**예상 난이도:** 중간  
**검증 기준:** 반격+무적 시나리오, SONI-06 다음턴 화력 시나리오로 수동 검증

---

## 4. 파일 구조 참조

```
TCG_Project/
├── Program.cs
├── ConsoleRunner.cs             ← Bot vs Bot 테스트 러너
├── HANDOFF.md                   ← 전체 인수인계 문서
├── EVENTMANAGER_CONTRACT.md     ← UI팀 이벤트 계약 명세
├── PROJECT_DIAGNOSIS.md         ← 이 파일
├── Data/
│   ├── CommonConfig.json
│   ├── RulebookCards.json        ← 룰북 카드 40종
│   └── Character.json            ← 캐릭터 카드 4종
├── Scripts/
│   ├── Core/         Card, Player, GameContext, Enums, Target, PendingEffect
│   ├── Systems/      BotBrain, HumanBrain, GameDataManager, GameRules,
│   │                 SpeedResolver, DamageResolver, ConditionEvaluator,
│   │                 FormulaEvaluator, TargetEvaluator
│   ├── Effects/      DamageEffect, BuffEffect, MoveEffect, BattlefieldEffect,
│   │                 SelfPlaceEffect, CompositeEffect, TemporaryCostEffect,
│   │                 PlayFromBufferEffect, CardSelector
│   ├── Conditions/   ComparePlayerStatCondition, HandCountCondition
│   ├── Interfaces/   ICardEffect, ICardCondition, IPlayerBrain
│   ├── Manager/      BattleManager (Unity), EventManager, MatchManager
│   ├── Utils/        DeckValidator
│   └── UI/           샘플 MonoBehaviour 4종 (UI팀 소유, 로직팀 수정 불가)
└── TCG_Project.csproj            ← .NET 10.0 + UnityEngine.dll 참조
```

---

## 5. 핵심 아키텍처 원칙 (변경 금지)

다음 원칙은 반드시 유지해야 한다. 위반 시 Unity 연동 및 UI팀 계약이 파괴된다.

1. **Zero Unity Dependency**: `Scripts/Core·Systems·Effects·Conditions` 내 파일에 `using UnityEngine` 금지
2. **이벤트 기반 통신**: 로직→UI 직접 호출 금지. `EventManager` 이벤트로만 방송
3. **EventManager 시그니처 불변**: 이벤트 파라미터 변경 시 UI팀 공지 필수
4. **DeckValidator 공개 메서드 불변**: 기능 추가는 새 오버로드로만
5. **데이터 주도**: 카드·효과·규칙은 `Data/*.json`에 선언, 코드 수정 없이 밸런싱 가능해야 함
6. **이중 환경 동작**: 모든 변경 후 `dotnet run`으로 콘솔 환경 검증 필수

---

## 6. 빌드 및 실행 방법

```powershell
# 빌드
dotnet build "./TCG_Project.csproj"

# 실행 (Bot vs Bot 시뮬레이션)
dotnet run --project "./TCG_Project.csproj"

# 정상 종료 판단 기준
# - 빌드: "오류 0개" 확인
# - 실행: "[MATCH SET] {플레이어명} 세트 승리!" 출력 확인
```

---

## 7. 관련 문서 참조

| 문서                     | 위치         | 용도                                          |
| ------------------------ | ------------ | --------------------------------------------- |
| HANDOFF.md               | TCG_Project/ | 전체 Phase 히스토리, 파일 구조, 아키텍처 원칙 |
| EVENTMANAGER_CONTRACT.md | TCG_Project/ | UI팀과의 이벤트 계약 명세                     |
| PROJECT_DIAGNOSIS.md     | TCG_Project/ | 이 파일 — 현재 상태 진단 및 다음 작업 추천    |

계획 파일 (Cursor Plans):

- Phase 12 계획: `phase_12_레거시_정리_0bb30c63.plan.md`
- Phase 11 계획: `phase_11_작업_계획_e9ca6344.plan.md`
- Phase 10 계획: `phase_10_effect_리팩터링_5f22a232.plan.md`
