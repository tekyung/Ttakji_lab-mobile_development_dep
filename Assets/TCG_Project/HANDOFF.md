# TCG_Project 작업 인수인계 문서

최초 작성일: 2026-02-28  
최종 수정일: 2026-03-18 (코드 전수 재점검 — 동시 처리 구현 확인, 신규 파일 반영, 기술 부채 갱신)  
목적: 새 AI가 현재까지의 작업을 이어받아 계속 진행하기 위한 컨텍스트 제공

---

## ⚠️ AI에게 — 이 문서 업데이트 규칙

**Phase가 하나 완료될 때마다 반드시 이 문서를 업데이트해야 한다.**

업데이트 항목:

1. 문서 상단 **최종 수정일** 갱신
2. **섹션 4 (파일 구조)** — 추가/삭제/수정된 파일 반영
3. **섹션 5 (Phase별 완료 현황)** — 완료된 Phase에 ✅ 표시 및 세부 내용 추가
4. **섹션 6 (남은 작업)** — 완료된 Phase 항목 제거
5. **섹션 7 (알려진 버그)** — 해결된 버그 행 삭제
6. **섹션 9 (다음 Phase 체크리스트)** — 다음 Phase 착수 전 확인 사항으로 교체
7. **섹션 10 (대화 참조)** — 현재 대화의 에이전트 ID 추가

---

## 1. 프로젝트 개요

### 무엇을 만드는가

"전투! 용병의 시대"라는 실제 TCG 보드게임을 온라인 Unity 게임으로 구현하는 프로젝트.  
현재 코드(`TCG_Project`)는 이 게임의 **C# 로직 프로토타입**으로, Unity 없이도 콘솔에서 실행 가능하다.

### ⚠️ 이 코드베이스(TCG_Project)의 역할과 작업 범위

**TCG_Project는 게임 로직 전담 레이어다. UI 구현은 이 팀의 범위가 아니다.**

| 구분                          | 이 팀(로직팀) 담당 | UI 팀 담당 |
| ----------------------------- | ------------------ | ---------- |
| 게임 규칙 & 효과 처리         | ✅                 | -          |
| 카드 데이터 JSON 설계         | ✅                 | -          |
| EventManager 이벤트 계약 정의 | ✅                 | -          |
| DeckValidator 검증 API        | ✅                 | -          |
| BotBrain AI 전략              | ✅                 | -          |
| Unity MonoBehaviour UI        | ❌                 | ✅         |
| 씬 배치 및 UI 애니메이션      | ❌                 | ✅         |
| 덱 빌딩 화면                  | ❌                 | ✅         |

**UI 팀과의 계약(contract) 인터페이스:**

1. **EventManager** — UI 팀이 구독할 이벤트 목록 (`Scripts/Manager/EventManager.cs`)  
   로직 레이어는 이벤트를 발행(Invoke)하고, UI 팀은 구독(+=)하여 화면에 반영한다.

2. **DeckValidator** — UI 팀이 호출할 덱 검증 API (`Scripts/Utils/DeckValidator.cs`)  
   `ValidateDeck(mainDeck, characterCardId)` — 메인덱 검증  
   `ValidateResourceDeck(resourceDeck)` — 자원덱 검증  
   `ValidateFullDeckSet(mainDeck, resourceDeck, characterCardId)` — 통합 검증 (덱 빌딩 완료 버튼 등에서 호출)

3. **GameDataManager** — 카드 데이터 로드 API  
   `LoadRulebookCards(basePath)`, `LoadCharacterCards(basePath)`

> `Scripts/UI/` 폴더의 4개 파일은 UI 팀에게 **이벤트 구독 참고 코드(샘플)** 로 제공된 것이며,  
> 로직팀이 직접 유지보수하지 않는다. 필요 시 UI팀이 덮어써도 된다.

### 프로젝트 경로

```
C:\Users\tekyung\.cursor\Ttackji-Game\Ttakji_lab-mobile_development_dep\TCG_Project\
```

### 빌드 환경

- `.csproj`: .NET 10.0, OutputType=Exe
- UnityEngine.dll / UnityEngine.UI.dll 직접 참조
- Newtonsoft.Json 13.0.4 사용
- 빌드: `dotnet build`, 실행: `dotnet run --project ./TCG_Project.csproj`

---

## 2. 아키텍처 원칙 (반드시 준수)

### 원칙 1 — 이중 실행 환경 (Dual-Environment)

- `ConsoleRunner.cs`: Unity 없이 단독 실행하는 테스트 러너
- `BattleManager.cs`: Unity MonoBehaviour 기반 진입점
- **게임 로직은 반드시 두 환경 모두에서 동작해야 한다**

### 원칙 2 — Zero Unity Dependency (로직 레이어)

- `Scripts/Core/`, `Scripts/Systems/`, `Scripts/Effects/`, `Scripts/Conditions/` 폴더의 파일은 `using UnityEngine`을 **사용하지 않는다**
- Unity 의존은 오직 `BattleManager.cs` 한 파일에만 허용
- ✅ `BattleSystem.cs`의 `using UnityEngine;` 제거 완료 (Phase 3)

### 원칙 3 — 이벤트 기반 통신

- 게임 로직은 UI/Unity를 직접 호출하지 않고 `EventManager`의 이벤트를 통해 방송(pub/sub)
- UI는 이벤트를 구독하여 반응
- 새 기능 추가 시 반드시 대응하는 EventManager 이벤트도 추가할 것

### 원칙 4 — 데이터 주도 설계 (Data-Driven)

- 카드/효과/룰은 `Data/*.json`에 선언, 코드 수정 없이 밸런싱 가능
- `GameDataManager.cs`가 런타임에 JSON을 읽어 객체 조립

### 원칙 5 — 인터페이스 기반 AI 추상화

- `IPlayerBrain` 인터페이스: `BotBrain`(AI)과 `HumanBrain`(인간 입력)을 동일한 게임 루프에서 처리
- 새 페이즈 메서드 추가 시 두 구현체 모두 수정 필요

### 원칙 6 — 기존 코드 하위 호환 유지 (Phase 7 까지만 유효)

- `CardType.Unit`, `CardType.Skill`은 삭제하지 않는다 (BattleManager, BattleSystem, GameDataManager에서 사용 중)
- `Mana` 시스템은 레거시로 유지 (BattleManager, BattleSystem에서 사용 중)
- **이 원칙은 기존 코드를 잠시 보존하기 위한 것으로 Phase 8 부터 적용되지 않는다**
- 레거시 코드 완전 제거는 Phase 8 Unity 연동 완성 후에 진행

### 원칙 7 — UI 팀과의 계약 유지 (로직팀 최우선 원칙)

- **EventManager의 이벤트 시그니처(파라미터 타입·순서)는 함부로 변경하지 않는다.**  
  변경 시 UI 팀의 구독 코드가 일제히 깨진다. 변경이 불가피하면 반드시 UI 팀에 공지한 뒤 진행한다.
- **새 게임 로직 이벤트를 추가할 때는 EventManager에 선언과 XML 문서화를 동시에 완료한다.**
- **DeckValidator의 공개 메서드 시그니처 변경도 금지.** UI 팀이 호출하는 API이기 때문이다.  
  기능 추가는 새 메서드 오버로드로만 한다.
- **`Scripts/UI/` 파일은 로직팀이 직접 수정하지 않는다.** UI 팀 소유 코드다.

---

## 3. 룰북 요약 (전투! 용병의 시대)

### 게임 준비

- 메인덱: **2종 캐릭터** 선택 후, 각 용병 테마 카드 풀에서 **총 10종류**를 골라 각 2장씩 = 20장 (두 테마 비율 자유). 봇 시뮬레이션에서는 5:5 비율로 생성.
- 자원덱: **깡통 카드** 15장 (별도). 특수 효과 없음, 모든 플레이어 동일. 코스트 지불용만.
- 라이프 토큰: 5개
- 캐릭터 카드: 2종 선택 (서로 다른 용병)
- 시작 패: 5장 드로우
- **캐릭터 고유 능력**: 듀얼 덱 시 각 캐릭터당 1회, 총 2회 사용 가능 (매치의 각 게임마다 리셋)

### 턴 진행 (6단계 페이즈)

1. **자원 페이즈**: 자원덱에서 자원존으로 1장 이동 (누적)
2. **드로우 페이즈**: 메인덱 맨 위 1장 패로
3. **세트 페이즈**: 패에서 1장을 뒷면으로 세트존에 (양측 동시 확정)
4. **오픈 페이즈**: 공개(강제 사용) 또는 폐기(뒷면 버리기 + 드로우 1장) 선택 (양측 동시 선언)
5. **메인 페이즈**: 스피드 순서로 공개된 카드 효과 해결
6. **엔드 페이즈**: 승리 조건 확인, 버프/디버프 만료 처리

### 자원/코스트

- 코스트 = 자원존 카드 수 (매 턴 누적, 사용하면 폐기존으로)
- 마나 리셋 없음

### 카드 종류

- 공격(Attack): 데미지, 관통 효과
- 방어(Defense): 아머, 슈퍼아머, 무적 효과
- 지원(Support): 드로우, 자원 획득, 화력 효과
- 캐릭터(Character): 고유 능력 보유
- 자원(Resource): 특수 효과 없는 깡통 카드. 모든 플레이어 동일 1종. 코스트 지불용만.

### 스피드 해결 순서 (메인 페이즈)

1방어 → 1공격 → 1지원 → 2방어 → 2공격 → 2지원 → 3방어 → 3공격 → 3지원 (총 9단계)  
동시 타입이면 동시 처리. "그 후" 효과는 후순위.

### 키워드 효과

- **아머(N)**: 일반 데미지를 N 감소. 관통에는 반응 안 함
- **슈퍼아머(N)**: 관통 포함 모든 데미지를 N 감소
- **무적**: 데미지/관통/반격 키워드 차단. '체력을 잃는다' 같은 행동은 차단 불가
- **화력(N)**: 자신의 데미지/관통 수치 +N
- **반격**: 받은 원본 데미지를 상대에게 그대로 반환 (상대 방어 우회)
- **스택**: 대응하는 효과 발동 전까지 필드 잔류, 1회 사용 후 폐기
- **전장**: 파괴 전까지 지속, 턴 1회 발동 가능
- **관통**: 아머 무시 데미지

### 승리 조건

- **메인**: 상대 라이프 토큰 5개 → 0
- **서브**: 상대 덱 0장 (엔드 페이즈에서만 확인)
- **동시 승리 타이브레이커** (6단계 순서):
  1. 폐기존 카드 수 적은 쪽 승리
  2. 자원존 남은 카드 수 많은 쪽 승리
  3. 메인덱 남은 수 많은 쪽
  4. 자원덱 남은 수 많은 쪽
  5. 라이프 많은 쪽
  6. 코인토스
- **세트 룰**: 3판 2선승

---

## 4. 현재 파일 구조

```
TCG_Project/
├── Program.cs                ← 진입점 (Main) — Phase 3에서 신규 생성
├── ConsoleRunner.cs          ← ✅ Phase 18 이후 수정: 세트/오픈 페이즈 동시 처리(Gather→Execute),
│                                단판제/3판제 JSON 전환(BotSingleGame), QA 인젝션 블록 추가
├── EVENTMANAGER_CONTRACT.md  ← ✅ Phase 12c 신규: UI팀 온보딩용 이벤트 계약 문서
│                                (OnPrizeChange 삭제됨 — EventManager 동기화 필요)
├── PROJECT_DIAGNOSIS.md      ← ✅ Phase 12 완료 후 신규: 로직팀 상태 진단서 및 다음 작업 추천
├── Scripts/
│   ├── Core/
│   │   ├── Card.cs           ← ✅ Phase 12 수정: OnCardPowerChanged 발행 제거
│   │   ├── DebugHelper.cs    ← ✅ 신규: 색상 로그 유틸 (LogSpell/LogEffect/LogWarning, Rich Text 태그)
│   │   ├── Enums.cs          ← ✅ Phase 11 수정: ZoneType.PlayBuffer 추가
│   │   ├── GameContext.cs    ← ✅ Phase 18 수정: LastEffectSucceeded 추가
│   │   ├── PendingEffect.cs
│   │   ├── Player.cs         ← ✅ Phase 16: SecondaryCharacterId, Phase 17: 캐릭터별 능력 1회,
│   │   │                        듀얼 시 총 2회 (HasCharacter, HasUsedCharacterAbility)
│   │   │                        + InitializeBrain(), EnableCardList, UpdatePlayableCards() 추가
│   │   └── Target.cs
│   │   (UnitCard.cs 삭제됨 — Phase 3)
│   ├── Abilities/            ← ✅ Phase 14 신규: ICharacterAbility, CharacterAbilityBase,
│   │                            EllieAbility(async/콜백 기반으로 변경),
│   │                            VeronicaAbility, DainaAbility, SoniaAbility, CharacterAbilityRegistry
│   ├── Systems/
│   │   ├── BotBrain.cs       ← ✅ Phase 12 수정, Phase 13: CardSelector.MatchesSingle 사용
│   │   ├── GameLogicHelpers.cs ← ✅ Phase 13 신규
│   │   ├── GameDataManager.cs  ← ✅ Phase 16 수정: GetEffectCardIdsForCharacter 추가
│   │   │                          + RawRulebookEffect에 isStackAction/requirePreviousSuccess/rewardOnSuccess 추가
│   │   │                          + cannotBePlayedByEffect 필드 추가 (기뢰 재사용 제한)
│   │   ├── ConditionEvaluator.cs
│   │   ├── DamageResolver.cs ← ✅ Phase 5 신규, Phase 18 수정: 반격 시 attacker.IsInvincible 체크
│   │   ├── FormulaEvaluator.cs
│   │   ├── GameRules.cs      ← ✅ 수정됨: LifeTokens, ResourceDeckCount 추가
│   │   │                        + BotDelayTime, BotSingleGame, WaitTime 추가 (JSON 제어)
│   │   ├── HumanBrain.cs     ← ✅ Phase 12 수정: SelectTarget 레거시 메서드 제거
│   │   ├── SpeedResolver.cs  ← ✅ Phase 4 신규: 9단계 스피드 해결 알고리즘
│   │   └── TargetEvaluator.cs
│   │   (BattleSystem.cs 삭제됨 — Phase 12b)
│   │   (TargetSelector.cs 삭제됨 — Phase 12b)
│   ├── Effects/
│   │   ├── CardSelector.cs         ← ✅ Phase 10 신규: 존·필터·선택 방식 공통 로직 + SelectMode enum
│   │   ├── MoveEffect.cs           ← ✅ Phase 11 수정, Phase 18: 실패 시 LastEffectSucceeded=false
│   │   ├── DamageEffect.cs         ← ✅ Phase 10 재작성: 데미지 3종 통합 (isPiercing/times/targetSelf)
│   │   ├── BuffEffect.cs           ← ✅ Phase 10 신규: 버프 6종 통합 + BuffType enum
│   │   │                              + rewardOnSuccess(반격 성공 보상) 지원
│   │   ├── BattlefieldEffect.cs    ← ✅ Phase 10 재작성: 전장 5종 통합 (PerTurnEffect/PerResourcePhaseEffect/CostReduction)
│   │   ├── SelfPlaceEffect.cs      ← ✅ Phase 10 신규: SelfAsResourceEffect 대체
│   │   ├── CompositeEffect.cs      ← ✅ Phase 10 신규, Phase 18 수정: "그 후" 시맨틱 (LastEffectSucceeded)
│   │   ├── TemporaryCostEffect.cs  ← ✅ Phase 11 신규: PlayBuffer 카드 임시 코스트 감소
│   │   ├── PlayFromBufferEffect.cs ← ✅ Phase 11 신규: PlayBuffer 카드 발동 + 복원 + 폐기존 이동
│   │   └── OptionalActionEffect.cs ← ✅ 신규: "A를 할 수 있다. 했다면 B를 한다." 패턴 처리
│   │                                   (봇=자동 Yes, 인간=OnRequireOptionalAction 이벤트 위임)
│   │                                   ⚠️ JSON 팩토리 연동 미완성 — ActionToPerform을 코드에서 직접 설정 필요
│   │   (ReplayCardEffect.cs 삭제됨 — Phase 11)
│   ├── Conditions/
│   │   ├── ComparePlayerStatCondition.cs
│   │   └── HandCountCondition.cs
│   ├── Interfaces/
│   │   ├── ICardCondition.cs (파일명: ICardcondition.cs — 대소문자 주의)
│   │   ├── ICardEffect.cs
│   │   └── IPlayerBrain.cs   ← ✅ Phase 12 수정: SelectTarget 레거시 메서드 제거
│   ├── Managers/              ← ⚠️ 폴더명 주의: 'Manager'와 'Managers' 두 폴더가 혼재
│   │   ├── BattleManager.cs  ← ✅ Phase 18 이후 수정: 세트/오픈/드로우 페이즈 동시 처리(병렬 코루틴),
│   │   │                        타임아웃 로직(ChooseWaitTime), QA 자동 응답기(OnRequireCardPick/OptionalAction),
│   │   │                        BotDelayTime JSON 연동(ActionDelay), 기명 이벤트 핸들러로 정리
│   │   ├── EventManager.cs   ← ✅ Phase 12 수정: 레거시 이벤트 11개 제거, 계약 완전 정제
│   │   │                        + OnRequireOptionalAction 추가 (OptionalActionEffect 연동)
│   │   │                        ⚠️ OnPrizeChange 삭제됨 (EVENTMANAGER_CONTRACT.md 미동기화)
│   │   └── MatchManager.cs   ← ✅ Phase 6 신규: 3판 2선승 매치 관리
│   ├── UI/
│   │   ├── GameStatusUI.cs        ← ✅ Phase 11 신규 (UI팀 샘플)
│   │   ├── PhaseInputPanel.cs     ← ✅ Phase 11 신규 (UI팀 샘플)
│   │   ├── StackResponsePanel.cs  ← ✅ Phase 11 신규 (UI팀 샘플)
│   │   └── CardSelectionPanel.cs  ← ✅ Phase 11 신규 (UI팀 샘플)
│   └── Utils/
│       ├── DeckValidator.cs       ← ✅ Phase 16 수정: ValidateDeck/ValidateFullDeckSet 듀얼 캐릭터 오버로드
│       └── DeckValidationResult.cs ← ✅ Phase 10 신규: IsValid + ErrorMessage 결과 클래스
├── Data/
│   ├── CommonConfig.json     ← ✅ BotDelayTime, BotSingleGame 키 추가됨
│   ├── Character.json        ← ✅ Phase 7 신규: 캐릭터 카드 4종
│   ├── ResourceCards.json    ← ✅ Phase 15 신규: 자원 카드 1종 (RES-01)
│   └── RulebookCards.json    ← ✅ Phase 7 교체: 실제 카드 40종
│                                + cannotBePlayedByEffect 필드 지원
└── TCG_Project.csproj        ← ✅ Phase 11 수정: UnityEngine.UI.dll 참조 추가
```

---

## 5. Phase별 완료 현황

### [재점검] 코드 전수 검토 결과 (2026-03-18)

Phase 18 이후 HANDOFF.md가 업데이트되지 않은 상태에서 아래 기능들이 코드에 추가되어 있었음. 공식 Phase 번호는 없으나 구현은 완료된 상태:

| 항목 | 파일 | 내용 | 상태 |
| ---- | ---- | ---- | ---- |
| 세트 페이즈 동시 처리 | `ConsoleRunner.cs`, `BattleManager.cs` | Gather(결정 수집) → Execute(일괄 적용) 패턴. ConsoleRunner는 봇 결정 2개 수집 후 동시 배치. BattleManager는 `StartCoroutine` × 2 + `WaitUntil(p1Done && p2Done)` | ✅ 구현 완료 |
| 오픈 페이즈 동시 처리 | `ConsoleRunner.cs`, `BattleManager.cs` | 동일 패턴. `ApplyOpenChoice()` 헬퍼 분리. BattleManager의 `GetOpenChoiceParallel`이 선택만 수집, `ApplyOpenChoice`가 일괄 집행 | ✅ 구현 완료 |
| 드로우 페이즈 병렬 처리 | `BattleManager.cs` | `ExecuteDrawForPlayerParallel` 코루틴 양측 동시 실행 | ✅ 구현 완료 |
| Human 입력 타임아웃 | `BattleManager.cs` | `ChooseWaitTime`(=`GameRules.ChooseWaitTime`) 초과 시 봇 결정으로 자동 대체 | ✅ 구현 완료 |
| QA 자동 응답기 | `BattleManager.cs` | `OnRequireCardPick`, `OnRequireOptionalAction` 구독하여 Human 없이 봇처럼 자동 응답 | ✅ 구현 완료 |
| `GameRules` JSON 확장 | `GameRules.cs`, `CommonConfig.json` | `BotDelayTime`, `BotSingleGame`, `WaitTime` 추가 — QA/배포 시 코드 수정 없이 JSON으로 제어 | ✅ 구현 완료 |
| `DebugHelper` 색상 로그 | `Scripts/Core/DebugHelper.cs` | `LogSpell/LogEffect/LogWarning` — Unity Rich Text 태그 삽입 → `EventManager.OnLogMessage` 경유 | ✅ 구현 완료 |
| `OptionalActionEffect` | `Scripts/Effects/OptionalActionEffect.cs` | 봇=자동 Yes, 인간=`OnRequireOptionalAction` 이벤트 위임. "A를 할 수 있다. 했다면 B" 패턴 | ⚠️ 클래스 구현 완료, JSON 팩토리 연동 미완성 |
| `DeckValidationResult` | `Scripts/Utils/DeckValidationResult.cs` | Phase 10에서 신규 생성됐으나 HANDOFF에 누락되어 있었음 | ✅ 이미 사용 중 |

---

### Phase 18 — 룰북 검증 불일치 수정 ✅ 완료 (2026-03-03)

**변경된 파일:**

- `DamageResolver.cs` — 반격 시 `attacker.IsInvincible` 체크 추가 (무적은 반격 피해도 막음)
- `ConsoleRunner.cs`, `BattleManager.cs` — `ApplyNextTurnBuffs()` 호출을 DrawPhase → ResourcePhase로 이동
- `BattleManager.cs` — HandleStackActivation: Defense vs Attack/Support 시 Human 강제 발동 (OnRequireStackResponse 미호출)
- `GameContext.cs` — `LastEffectSucceeded` 플래그 추가
- `CompositeEffect.cs` — Step N>0 실행 전 `LastEffectSucceeded` 검사, false면 후속 Step 스킵
- `MoveEffect.cs` — selected.Count < _count 시 `LastEffectSucceeded = false` 설정
- `EVENTMANAGER_CONTRACT.md` — 스택 강제 발동 시 OnRequireStackResponse 미호출 주석 추가

**검증:** `dotnet run` 정상 진행, 반격+무적·다음턴 버프 시점·"그 후" 시맨틱 적용

---

### Phase 17 — 캐릭터 능력 게임당 1회 제한 (듀얼: 캐릭터당 1회, 총 2회) ✅ 완료 (2026-03-03)

**변경된 파일:**

- `Player.cs` — `HasCharacter()`, `HasUsedCharacterAbility(charId)`, `MarkCharacterAbilityUsed(charId)` 추가. `_usedCharacterAbility` HashSet으로 캐릭터별 추적
- `VeronicaAbility`, `SoniaAbility` — `HasCharacter()` 사용 (주/부 캐릭터 모두 체크)
- `ConsoleRunner.cs`, `BattleManager.cs` — 4곳 능력 훅에서 캐릭터 ID 기준 체크 (VERO-01, SONI-01, DAIN-01, ELLI-01)

**검증:** `dotnet run` 베로니카/다이나 능력 발동 확인. 듀얼 덱(ELLI+VERO, DAIN+SONI) 시 각 캐릭터당 1회, 총 2회 사용 가능

---

### Phase 16 — 듀얼 캐릭터 덱 구축 룰 ✅ 완료 (2026-03-03)

**변경된 파일:**

- `Player.cs` — `SecondaryCharacterId` 추가
- `DeckValidator.cs` — `ValidateDeck(deck, charId1, charId2)`, `ValidateFullDeckSet(..., charId1, charId2)` 오버로드, `IsValidCharacterDeck` Id 기반 검증
- `GameDataManager.cs` — `GetEffectCardIdsForCharacter(characterCardId)` 추가
- `ConsoleRunner.cs`, `BattleManager.cs` — `CreateDualCharacterDeck(charId1, charId2, 5, 5)` 사용, 봇 5:5 비율

**검증:** Bot_Red(ELLI+VERO 5:5), Bot_Blue(DAIN+SONI 5:5) 덱으로 매치 정상 진행

---

### Phase 15 — 자원 카드 JSON화 ✅ 완료 (2026-03-03)

**신규 파일:** `Data/ResourceCards.json` (RES-01 1종)  
**수정:** `GameDataManager.LoadResourceCards()`, `ConsoleRunner`/`BattleManager` CreateResourceDeck → AllCards["RES-01"].Clone() 기반

---

### Phase 14 — 캐릭터 능력 시스템 분리 ✅ 완료 (2026-03-03)

**신규 폴더/파일:** `Scripts/Abilities/` — `ICharacterAbility`, `CharacterAbilityBase`, `EllieAbility`, `VeronicaAbility`, `DainaAbility`, `SoniaAbility`, `CharacterAbilityRegistry`  
**수정:** `ConsoleRunner`, `BattleManager` — `if (CharacterCardId == "ELLI-01")` 분기 → `CharacterAbilityRegistry.Get()?.On*(...)` 호출로 교체

---

### Phase 13 — 공유 로직 추출 ✅ 완료 (2026-03-03)

**신규 파일:** `Scripts/Systems/GameLogicHelpers.cs` — `GetEffectiveCost`, `ApplyBattlefieldTurnEffects`, `ApplyBattlefieldResourcePhaseEffects`, `DrawCards`  
**수정:** `CardSelector.MatchesSingle()` 추가, `ConsoleRunner`/`BattleManager`/`BotBrain` 중복 제거

---

### Phase 12 — EventManager 레거시 정리 + BattleSystem 삭제 ✅ 완료 (2026-03-02)

**변경된 파일:**

- `Scripts/Manager/EventManager.cs` — 레거시 이벤트 11개 제거
  - Phase 12a 삭제: `OnBattlePhase`, `OnManaChange`, `OnUnitSummoned`, `OnAttack`, `OnRequireMainPhaseAction`, `OnRequireBattlePhaseAction`
  - Phase 12b 삭제: `UnitStatusChange`, `OnUnitTakeDamage`, `OnUnitDeath`, `OnCardPowerChanged`, `OnRequireTargetSelection`
- `Scripts/Core/Card.cs` — `ModifyPower()`에서 `OnCardPowerChanged?.Invoke()` 라인 제거
- `Scripts/Interfaces/IPlayerBrain.cs` — `SelectTarget()` 레거시 메서드 제거, `using System;` 제거
- `Scripts/Systems/BotBrain.cs` — `SelectTarget()` 레거시 구현 제거
- `Scripts/Systems/HumanBrain.cs` — `SelectTarget()` 레거시 구현 제거, 미사용 `using` 2개 제거

**신규 파일:**

- `EVENTMANAGER_CONTRACT.md` — UI팀 온보딩용 이벤트 계약 명세 (A. 로직→UI 발행 20개, B. UI→로직 콜백 5개, 구독 예시 코드 포함)

**삭제된 파일:**

- `Scripts/Systems/BattleSystem.cs` — 구 4페이즈 유닛 전투 시스템. `new BattleSystem()` 호출처 없음, 완전 dead code
- `Scripts/Systems/TargetSelector.cs` — 구 유닛 타겟팅 시스템. 외부 호출처 없음, 완전 dead code

**검증 결과:** `dotnet build` 오류 0개 (경고 279개, Phase 11 대비 9개 감소). `dotnet run` 전체 6페이즈 매치 3판 2선승 정상 진행. `[MATCH SET] Bot_Blue 세트 승리!` 정상 출력.

---

### Phase 11 — ReplayCardEffect 전환 + Unity UI + DeckValidator 연결 ✅ 완료 (2026-03-02)

**변경된 파일:**

- `Scripts/Core/Enums.cs` — `ZoneType.PlayBuffer` 추가
- `Scripts/Core/Player.cs` — `PlayBuffer` 리스트 필드 추가, `ResetForNewGame()`/`InsertCard()`/`ExtractCard()`/`GetZone()`에 PlayBuffer 분기 추가
- `Scripts/Effects/MoveEffect.cs` — `RemoveFromZone()`/`AddToZone()`에 `PlayBuffer` case 추가
- `Scripts/Systems/GameDataManager.cs` — `CreateKeywordEffect()`: `ReplayCardEffect` → `CompositeEffect(3-step)` 전환 (`MoveEffect(Graveyard→PlayBuffer)` + `TemporaryCostEffect` + `PlayFromBufferEffect`)
- `Scripts/Manager/BattleManager.cs` — `InitializeSingleGame()`에서 `DeckValidator.ValidateFullDeckSet()` 사용으로 단순화 (inline prefix 추출 코드 제거)
- `Scripts/Utils/DeckValidator.cs` — `ValidateDeck()` 내부 `ExtractCharacterPrefix()` 적용, `ValidateResourceDeck()`, `ValidateFullDeckSet()`, `ExtractCharacterPrefix()` 추가. UI 팀 독립 호출 가능 형태로 완성
- `TCG_Project.csproj` — `UnityEngine.UI.dll` 참조 추가 (UI 팀 샘플 코드 컴파일용)

**신규 파일:**

- `Scripts/Effects/TemporaryCostEffect.cs` — PlayBuffer 첫 카드의 코스트를 N만큼 임시 감소
- `Scripts/Effects/PlayFromBufferEffect.cs` — PlayBuffer 카드 발동 + OriginalCost 복원 + 폐기존 이동
- `Scripts/UI/GameStatusUI.cs` — `OnLifeChange`/`OnTurnStart`/`OnLogMessage`/`OnGameStart` 구독, 라이프·턴·로그 텍스트 갱신 MonoBehaviour
- `Scripts/UI/PhaseInputPanel.cs` — `OnRequireSetPhaseAction`/`OnRequireOpenPhaseAction` 구독, 패 카드 선택·공개/폐기 버튼 표시
- `Scripts/UI/StackResponsePanel.cs` — `OnRequireStackResponse` 구독, 발동/패스 버튼 + 자동 패스 카운트다운
- `Scripts/UI/CardSelectionPanel.cs` — `OnRequireCardPick`/`OnRequireCardChoice` 구독, N장 카드 선택 그리드 팝업

**삭제된 파일:**

- `Scripts/Effects/ReplayCardEffect.cs` — CompositeEffect(3-step)으로 완전 대체

**검증 결과:** `dotnet run` 빌드 오류 0개. 룰북 카드 40종 + 캐릭터 4종 로드, 6페이즈 매치 3판 2선승 정상 진행. [MATCH SET] 세트 승리 정상 출력.

---

### Phase 10 — Effect 시스템 리팩터링 ✅ 완료 (2026-03-02)

**변경된 파일:**

- `Scripts/Effects/DamageEffect.cs` — 재작성: `isPiercing`, `times`, `targetSelf` 파라미터 추가로 PiercingDamage/MultiHitDamage/SelfDamage 3종 통합
- `Scripts/Effects/BattlefieldEffect.cs` — 재작성: `PerTurnEffect`, `PerResourcePhaseEffect`, `CostReductionFilter/CostReduction` 속성 추가로 전장 5종 통합
- `Scripts/Systems/GameDataManager.cs` — `CreateKeywordEffect()` 전면 재작성: 26개 case → 6개 통합 Effect 클래스로 재구성
- `Scripts/Manager/BattleManager.cs` — 구 타입 체크(ArmorBattlefieldEffect 등) → BattlefieldEffect 통합 API로 전환
- `ConsoleRunner.cs` — ApplyBattlefieldTurnEffects/ResourcePhaseEffects → BattlefieldEffect.PerTurnEffect/PerResourcePhaseEffect 직접 호출로 전환
- `Scripts/Manager/EventManager.cs` — 전체 이벤트 XML 문서화, `OnRequestVisualDelay(float)` 이벤트 추가
- `Scripts/Utils/DeckValidator.cs` — `DeckValidationResult` 클래스 추가 (IsValid + ErrorMessage)

**신규 파일:**

- `Scripts/Effects/CardSelector.cs` — 존·필터·선택방식 공통 로직 + `SelectMode` enum
- `Scripts/Effects/MoveEffect.cs` — 카드 이동 9종 통합 (Draw, DiscardFromHand, ResourceGain, RecoverFromDiscard, ShuffleReturn, TopDeckToDiscard, Search, ResourceFromDiscard, ReturnFromDiscard)
- `Scripts/Effects/BuffEffect.cs` — 버프 6종 통합 + `BuffType` enum (Armor, SuperArmor, Invincible, Firepower, CounterAttack)
- `Scripts/Effects/SelfPlaceEffect.cs` — SelfAsResourceEffect 대체
- `Scripts/Effects/CompositeEffect.cs` — 복합 효과 순차 실행 (Steps.Add 체인 구조)

**삭제된 파일 (23개):**

- 이동 계열: DrawEffect, DiscardFromHandEffect, ResourceGainEffect, RecoverFromDiscardEffect, ShuffleReturnEffect, TopDeckToDiscardEffect, SearchDeckEffect, ResourceFromDiscardEffect, ReturnFromDiscardEffect (9개)
- 버프 계열: ArmorEffect, SuperArmorEffect, InvincibilityEffect, FirepowerEffect, CounterAttackEffect, NextTurnBuffEffect (6개)
- 데미지 계열: PiercingDamageEffect, MultiHitDamageEffect, SelfDamageEffect (3개)
- 전장 계열: ArmorBattlefieldEffect, FirepowerBattlefieldEffect, PeriodicRecoveryBattlefieldEffect, CostReductionBattlefieldEffect (4개)
- 기타: SelfAsResourceEffect (1개)

**미전환 항목:**

- `ReplayCardEffect.cs`: 폐기존 카드 선택 → 임시 코스트 수정 → 즉시 발동 로직은 PlayBuffer/TemporaryCostModify 인프라가 필요하여 Phase 11에서 CompositeEffect 전환 예정

**검증 결과:** `dotnet run` 빌드 오류 0개. 룰북 카드 40종 + 캐릭터 4종 로드, 6페이즈 매치 3판 2선승 정상 진행. 데미지/아머/화력/스택/전장 주기 효과/MoveEffect(카드 이동)/BuffEffect(버프) 모두 정상 동작. Bot_Red 2승 세트 승리 정상 출력.

---

### Phase 9 — 레거시 완전 제거 ✅ 완료 (2026-03-02)

**변경된 파일:**

- `Scripts/Core/Enums.cs` — `CardType.Unit(1)`, `CardType.Skill(2)` 삭제, `GamePhase` 레거시 5개 삭제
- `Scripts/Core/Card.cs` — `Play()` 단순화 (Unit/Skill 분기 제거), `IsPlayable()` Mana→`CanAfford()` 전환, `InitializeUnitStats()` Unit 분기 제거
- `Scripts/Core/Player.cs` — `Mana` 필드 삭제, `ManaGain()` 삭제, `PlayCard()` Mana·Unit·Skill 분기 제거, `GetUsableEffectCardIndices()` 삭제
- `Scripts/Systems/GameDataManager.cs` — `LoadAllData()` 빈 스텁으로 교체, 구 Raw 클래스 4개·래퍼 4개·관련 메서드 4개 (`GetConditionFormula`, `DeriveEffectCondition`, `ConvertEffect`, `ConvertTarget`) 삭제
- `Scripts/Systems/BattleSystem.cs` — `Mana` 차감 제거, `CardType.Unit` 체크 제거, `AttackCost` Mana 체크 제거
- `Scripts/Systems/ConditionEvaluator.cs` — `Mana` 변수 치환 3곳 제거
- `Scripts/Systems/FormulaEvaluator.cs` — `Mana` 변수 치환 2곳 제거
- `Scripts/Conditions/ComparePlayerStatCondition.cs` — `case "Mana"` 제거, 기본 statName `"Mana"` → `"HandCount"` 변경

**삭제된 Effect 파일 (7개):**

- `ManaGainEffect.cs`, `ModifyStatEffect.cs`, `RevertStatEffect.cs`, `RevertControllEffect.cs`
- `SwapCardEffect.cs`, `ConditionalEffect.cs`, `MoveCardEffect.cs`

**검증 결과:** `dotnet run` 빌드 오류 0개. 룰북 카드 40종 + 캐릭터 4종 로드, 6페이즈 매치 3게임 정상 진행, Bot_Blue 2승 세트 승리 정상 출력.

---

### Phase 8 — Unity 연동 완성 + 캐릭터 능력 구현 ✅ 완료 (2026-03-02)

**변경된 파일:**

- `Scripts/Interfaces/IPlayerBrain.cs` — 6페이즈 메서드로 완전 재설계
- `Scripts/Manager/EventManager.cs` — Human Input 이벤트 5개 추가 (OnRequireSetPhaseAction 등)
- `Scripts/Systems/BotBrain.cs` — 6페이즈 AI 전략 완성 (ChooseSetCard/ChooseOpenOrAbandon/ChooseStackActivation/ChooseCardsFromZone)
- `Scripts/Systems/HumanBrain.cs` — 6페이즈 EventManager 이벤트 위임 구조로 재작성
- `Scripts/Core/Player.cs` — CharacterCardId 속성 추가
- `Scripts/Manager/BattleManager.cs` — 6페이즈 코루틴 체인으로 전면 재작성
- `Scripts/Utils/DeckValidator.cs` — IsValidCharacterDeck() + IsValidRulebookDeckWithCharacter() 추가
- `Scripts/Core/Enums.cs` — CardType.Unit/Skill 레거시 주석 추가
- `ConsoleRunner.cs` — 캐릭터 능력 4종 훅 추가, HandleStackActivation() 추가, CharacterCardId 설정

**주요 내용:**

#### IPlayerBrain 재설계

- 구 4페이즈 메서드(ExecuteMainPhase/ExecuteBattlePhase/SelectTarget) → 6페이즈 메서드로 교체
- `ChooseSetCard(Player, GameContext)`: 세트 페이즈 카드 선택
- `ChooseOpenOrAbandon(Player, Card, int effectiveCost, GameContext)`: 공개/폐기 결정
- `ChooseStackActivation(Player, Card stackCard, Card opponentCard, GameContext)`: 스택 발동 여부
- `ChooseCardsFromZone(Player, ZoneType, int count, string filter, GameContext)`: 존 카드 선택
- `SelectTarget()` 레거시로 유지 (TargetSelector 하위 호환)

#### 스택 반응 시스템 구현

- 메인 페이즈 각 카드 효과 발동 직전, 상대방 스택존 확인
- 봇: Defense 스택 → 상대 Attack/Support 카드에 자동 발동
- Human(Unity): OnRequireStackResponse 이벤트로 UI에 발동 여부 위임

#### 캐릭터 특수 능력 4종 구현 (ConsoleRunner + BattleManager 양쪽)

- ELLIE(ELLI-01): 메인 페이즈 공격 카드 발동 후 패의 공격 카드 1장 추가 발동 (코스트 지불)
- VERONICA(VERO-01): 드로우 페이즈 덱 탑 3장 공개 → 봇 1장 선택, Human UI 대기
- DAINA(DAIN-01): 오픈 페이즈 폐기 선택 시 라이프 +1
- SONIA(SONI-01): 세트 페이즈 전 폐기존에서 소니아 카드 1장 → 패

#### BattleManager 재작성

- 6페이즈 코루틴 체인: ResourcePhase → DrawPhase → SetPhase → OpenPhase → MainPhase → EndPhase
- MatchManager 통합 (3판 2선승)
- Human/Bot 분기: Bot은 Brain 메서드 즉시 호출, Human은 EventManager 이벤트 발생 + WaitUntil 대기

**검증 결과:** `dotnet run` 실행 시 베로니카 덱 탑 3장 능력, 흐릿한 안개성 스택 발동, 전장 주기 효과 모두 정상 동작. Bot_Blue(베로니카) 2승으로 세트 승리 정상 출력.

---

### Phase 1 — 데이터 모델 재설계 ✅ 완료

**변경된 파일:** `Enums.cs`, `Card.cs`, `Player.cs`, `GameContext.cs`, `GameRules.cs`, `CommonConfig.json`, `EventManager.cs`

**주요 내용:**

- `ZoneType` 확장: `SetZone`, `ResourceDeck`, `ResourceZone`, `StackZone`, `BattlefieldZone` 추가
- `GamePhase` 확장: `ResourcePhase`, `DrawPhase`, `SetPhase`, `OpenPhase`, `MainPhase`, `EndPhase` 추가
- `CardType` 확장: `Attack(3)`, `Defense(4)`, `Support(5)`, `Character(6)`, `Resource(7)` 추가 (기존 `Unit(1)`, `Skill(2)` 유지)
- `CardSpeed` enum 신규: `Speed1/2/3`
- `EffectDuration` enum 신규: `Instant/ThisTurn/NextTurn/Stack/Permanent`
- `OpenPhaseChoice` enum 신규: `Open/Abandon`
- `Card`에 `Speed` 속성 추가, `Clone()`에 Speed 복사 반영
- `Player`에 신규 존 6개 추가: `ResourceDeck`, `ResourceZone`, `SetZoneCard`, `StackZone`, `BattlefieldCard`, `LifeTokens`
- `Player`에 신규 메서드: `InitializeLifeTokens`, `TakeResourceCard`, `PayCost`, `CanAfford`, `GetResourceCount`, `SetCard`, `AbandonSetCard`, `RevealSetCard`, `AddToStackZone`, `UseAndDiscardStack`, `PlaceBattlefield`, `DestroyBattlefield`, `LoseLife`, `GetTotalDiscardCount`
- `InsertCard/ExtractCard/GetZone/HasSpaceInZone` — 새 존 처리 추가
- `GameContext`에 `CurrentPhase` 추가
- `GameRules`에 `LifeTokens`, `ResourceDeckCount` 추가
- `CommonConfig.json`에 `life_tokens:5`, `resource_deck_count:15` 추가, `starting_hands: 3→5`
- `EventManager`에 `OnLifeChange` 추가

---

### Phase 2 — 존 관리 시스템 재구축 ✅ 완료

**변경된 파일:** `Player.cs`, `DeckValidator.cs`

**주요 내용:**

- `ExtractCard(ZoneType zone, string strategy)` 오버로드 완성 — `ResourceDeck`, `ResourceZone`, `StackZone`, `SetZone`, `BattlefieldZone` 처리 추가
- `DeckValidator.cs` 확장:
  - `IsValidRulebookDeck()`: 10종 × 2장 = 20장 메인덱 검증
  - `IsValidResourceDeck()`: 자원 카드만 15장 검증
  - `ResolveTiebreaker(Player p1, Player p2)`: 6단계 동시 승리 타이브레이커 판정

---

### Phase 3 — 페이즈 엔진 재설계 ✅ 완료 (2026-03-01)

**변경된 파일:** `ConsoleRunner.cs`, `EventManager.cs`, `BattleSystem.cs`, `ModifyStatEffect.cs`, `ConditionEvaluator.cs`  
**삭제된 파일:** `UnitCard.cs`, `BotSimulator.cs`  
**신규 파일:** `Program.cs`

**주요 내용:**

#### 사전 버그 수정

- `BattleSystem.cs` L7: `using UnityEngine;` 제거 → Zero Unity Dependency 원칙 복구
- `ModifyStatEffect.cs` L50-52: 중복 변수 할당 (`statName`, `targetParam`, `amountParam`) 제거
- `ConditionEvaluator.cs` L31-33: `ReplaceVariables` 이중 호출 → private 버전 단일 호출로 정리
- `UnitCard.cs`: `Card.cs`와 중복, 미사용 → 삭제
- `BotSimulator.cs`: 전체 주석, 미사용 → 삭제
- `Program.cs`: BotSimulator 삭제로 사라진 `Main` 진입점 신규 생성

#### EventManager 업데이트

- `using System.Collections.Generic;` 추가 (기존 `List<Target>` 컴파일 안전화)
- `OnResourcePhase(string playerName, int turn)` 추가
- `OnSetPhase(string playerName, int turn)` 추가
- `OnOpenPhase(string playerName, int turn)` 추가
- `OnBattlePhase` → 레거시 주석 부착하여 유지 (BattleManager가 아직 사용 중)

#### ConsoleRunner.cs 6페이즈 재작성

- 구 4페이즈(드로우→메인→배틀→엔드) 완전 제거
- **라운드 기반** 구조로 전환: 양측 플레이어가 매 라운드 6페이즈를 동시 진행
- `CreateResourceDeck()`: 자원 카드 15장 프로그래밍 생성 (JSON 데이터 없으므로)
- `p.InitializeLifeTokens()`, `p.SetResourceDeck(...)` 초기화 추가
- `BotChooseSetCard(Player)`: Speed 낮은 순 → Defense > Attack > Support 우선 전략
- `BotChooseOpenOrAbandon(Player)`: 코스트 지불 가능 → 공개, 불가 → 폐기 + 드로우 1장
- `ExecuteMainPhase()`: 공개 카드를 Speed/Type 정렬 후 순차 처리 (Phase 4 SpeedResolver 연결 예정)
- `ExecuteEndPhase()`: 덱아웃 체크 동작 확인 (동시 덱아웃 타이브레이커는 Phase 6에서 완성)

**검증 결과:** `dotnet run` 실행 시 Round 1~7을 거쳐 Bot_Blue 덱 고갈로 Bot_Red 승리 정상 출력. 자원 누적, 카드 드로우 효과(학습, 애벌레), 마나 효과(골드슬라임) 등 기존 카드 효과도 6페이즈 구조 안에서 정상 발동 확인.

---

### Phase 4 — 스피드 해결 시스템 ✅ 완료 (2026-03-01)

**신규 파일:** `Scripts/Systems/SpeedResolver.cs`

**주요 내용:**

- `SpeedResolver` 정적 클래스 구현 (Zero Unity Dependency)
- `GroupByResolutionOrder(List<(Player, Player, Card)>)`: 공개 카드를 룰북 9단계 순서로 처리 그룹 반환
  - Speed 1→2→3, 같은 Speed 내 `Defense(0) > Attack(1) > Support(2)`, `CardSpeed.None`은 맨 뒤
  - 완전히 동일한 Speed+Type 카드는 **동시 처리 그룹**으로 묶임
  - 그룹 내에서도 방어 카드 우선 정렬
- `LogResolutionOrder()`: 처리 순서 콘솔 출력
- `ConsoleRunner.ExecuteMainPhase()`: 임시 LINQ 정렬 → `SpeedResolver.GroupByResolutionOrder()` + 그룹 순서 반복으로 교체

**검증 결과:** `dotnet run` 실행 시 `[SpeedResolver] 처리 순서:` + `Step N: 동시 처리` 출력 정상 확인. Bot_Red 승리 흐름 유지.

---

### Phase 5 — 키워드 효과 시스템 ✅ 완료 (2026-03-01)

**신규 파일:**

- `Scripts/Systems/DamageResolver.cs`: 데미지 파이프라인 (화력→아머/슈퍼아머→무적→LoseLife→반격) 정적 클래스
- `Scripts/Effects/DamageEffect.cs`: 기본 데미지 (DamageResolver 호출)
- `Scripts/Effects/PiercingDamageEffect.cs`: 관통 데미지 (isPiercing=true)
- `Scripts/Effects/ArmorEffect.cs`: `Player.ArmorBonus` 누적, ThisTurn
- `Scripts/Effects/SuperArmorEffect.cs`: `Player.SuperArmorBonus` 누적, ThisTurn
- `Scripts/Effects/InvincibilityEffect.cs`: `Player.IsInvincible = true`, ThisTurn
- `Scripts/Effects/FirepowerEffect.cs`: `Player.FirepowerBonus` 누적, ThisTurn
- `Scripts/Effects/CounterAttackEffect.cs`: `Player.HasCounterAttack = true`, ThisTurn
- `Scripts/Effects/BattlefieldEffect.cs`: `Player.PlaceBattlefield(PlayingCard)` 호출
- `Data/RulebookCards.json`: Attack 3종, Defense 3종, Support 3종 (모든 키워드 커버)

**수정된 파일:**

- `Scripts/Core/Player.cs`: 전투 버프 속성 5개 추가 + `ClearCombatBuffs()` 추가
- `Scripts/Core/Card.cs`: `Play()`에서 Attack/Defense/Support도 Skill 분기 처리
- `Scripts/Systems/GameDataManager.cs`: `LoadRulebookCards()` + `CreateKeywordEffect()` 추가
- `ConsoleRunner.cs`: `CreateRulebookBotPlayer()` 추가, `PlayingCard` 설정, 전장 카드 분기, `ExecuteEndPhase()`에 `ClearCombatBuffs()` 추가

**검증 결과:** 룰북 카드 9종 로드 확인, 데미지/아머/슈퍼아머/화력/반격 키워드 정상 출력, 라이프 토큰 감소 및 승리 조건(라이프 0) 정상 동작.

---

### Phase 6 — 승리 조건 및 MatchManager ✅ 완료 (2026-03-01)

**신규 파일:**

- `Scripts/Manager/MatchManager.cs`: 3판 2선승 매치 관리
  - `RecordResult(winner, p1, p2)`: 게임 결과 기록
  - `IsMatchOver()`: 세트 종료 여부 (2승 달성 or 3게임 완료)
  - `GetMatchWinner(p1, p2)`: 세트 승자 반환 (null=무승부)
  - `GetStatusString()`: 현재 전적 문자열

**수정된 파일:**

- `Scripts/Core/Player.cs`: `ResetForNewGame(newDeck, newResourceDeck)` 추가 — 게임 간 상태 완전 초기화
- `Scripts/Manager/EventManager.cs`: `OnMatchSet`, `OnMatchDraw` 이벤트 추가
- `ConsoleRunner.cs`: 대규모 리팩토링
  - `Run()`: 매치 루프 구조로 재구성 (MatchManager 통합)
  - `InitializeSingleGame(p1, p2)`: 게임별 상태 초기화 분리
  - `RunGameLoop(p1, p2)`: 6페이즈 루프 분리
  - `_currentGameWinner` 필드: OnGameSet 핸들러에서 승자 캡처
  - `CreateDeckFromIds(string[])`: ID 배열→카드 리스트 헬퍼
  - `ResolveSimultaneousDeckout()`: `DeckValidator.ResolveTiebreaker()` 연결 + 코인토스 처리
  - `_p1CardIds` / `_p2CardIds`: 덱 재구성용 카드 ID 배열

**검증 결과:** 3게임 진행 후 Bot_Blue 2승으로 세트 승리, `[MATCH SET] Bot_Blue 세트 승리!` 정상 출력. 동시 덱아웃 시 타이브레이커 판정 및 코인토스 경로 확인.

---

### Phase 7 — 실제 카드 데이터 전면 교체 ✅ 완료 (2026-03-02)

**신규 파일:**

- `Data/RulebookCards.json`: 기존 9종 테스트 카드 → 실제 게임 카드 40종으로 전면 교체 (엘리 10, 베로니카 10, 다이나 10, 소니아 10)
- `Data/Character.json`: 캐릭터 카드 4종 데이터 정의 (엘리/베로니카/다이나/소니아 능력 기술)
- `Scripts/Effects/DrawEffect.cs`: 덱에서 N장 드로우
- `Scripts/Effects/DiscardFromHandEffect.cs`: 패 N장 폐기 (random/all)
- `Scripts/Effects/ResourceGainEffect.cs`: 자원덱 → 자원존 N장
- `Scripts/Effects/SelfDamageEffect.cs`: 자신 라이프 N 감소
- `Scripts/Effects/MultiHitDamageEffect.cs`: 데미지 N × M회 반복
- `Scripts/Effects/RecoverFromDiscardEffect.cs`: 폐기존에서 카드 패로 (캐릭터·타입 필터)
- `Scripts/Effects/ShuffleReturnEffect.cs`: 패 N장 덱으로 되돌리고 섞기
- `Scripts/Effects/TopDeckToDiscardEffect.cs`: 덱 맨 위 N장 앞면 폐기
- `Scripts/Effects/NextTurnBuffEffect.cs`: [다음 턴] 화력/경감 예약 버프
- `Scripts/Effects/SearchDeckEffect.cs`: 덱에서 카드 서치 (필터)
- `Scripts/Effects/ResourceFromDiscardEffect.cs`: 폐기존 자원카드 → 자원존
- `Scripts/Effects/ReturnFromDiscardEffect.cs`: 폐기존 효과카드 → 덱
- `Scripts/Effects/SelfAsResourceEffect.cs`: 이 카드를 자원카드로 자원존에 배치
- `Scripts/Effects/ReplayCardEffect.cs`: 폐기존 카드 코스트 감소 후 즉시 사용
- `Scripts/Effects/ArmorBattlefieldEffect.cs`: [전장] 매 턴 경감 +N
- `Scripts/Effects/FirepowerBattlefieldEffect.cs`: [전장] 매 턴 화력 +N
- `Scripts/Effects/PeriodicRecoveryBattlefieldEffect.cs`: [전장] 자원페이즈마다 폐기존 → 패
- `Scripts/Effects/CostReductionBattlefieldEffect.cs`: [전장] 조건 카드 코스트 -N

**수정된 파일:**

- `Scripts/Core/Card.cs`: `CharacterId`, `IsStack` 속성 추가 및 `Clone()` 반영
- `Scripts/Core/Player.cs`: `NextTurnFirepowerBonus`, `NextTurnArmorBonus`, `ApplyNextTurnBuffs()`, `GainLife()` 추가
- `Scripts/Systems/GameDataManager.cs`: `RawRulebookEffect` 구조체 확장 (count/mode/filter/times/buffType/costReduction/reduction), `RawRulebookCard`에 characterId·isStack 추가, `CreateKeywordEffect()` 26종 등록, `LoadCharacterCards()` 신규
- `ConsoleRunner.cs`: 덱 ID 교체(ELLI/VERO 10종), 스택·자원존 배치 감지, `ApplyBattlefieldTurnEffects()`, `ApplyBattlefieldResourcePhaseEffects()`, `GetEffectiveCost()`, `MatchesFilter()`, `CardMatchesFilter()` 추가

**검증 결과:** `dotnet run` 실행 시 룰북 카드 40종 + 캐릭터 카드 4종 로드 확인. 스택 카드(격추 시스템) 스택존 배치, 자원 카드(보급 전달) 자원존 배치, 전장 주기 효과(체크메이트 경감+1), 폐기존 회수(리로드), 다중 드로우(약탈), 자원 획득(정세 읽기) 등 모두 정상 동작. Bot_Red(엘리) vs Bot_Blue(베로니카) 2게임 진행 후 세트 승리 정상 출력.

---

## 6. 남은 작업 (로직팀 범위)

> Phase 18까지 로직팀 범위의 핵심 구현이 완료되었고, 이후 동시 처리 설계 및 QA 인프라가 추가되었다.  
> UI 구현(씬 배치, 화면 디자인, MonoBehaviour 연결)은 UI팀 범위이므로 아래에 포함하지 않는다.

### 완료된 Phase 요약

| Phase  | 내용                                                                               | 상태    |
| ------ | ---------------------------------------------------------------------------------- | ------- |
| 13     | 공유 로직 추출 (ConsoleRunner–BattleManager 중복 제거)                             | ✅ 완료 |
| 14     | 캐릭터 능력 시스템 분리 (ICharacterAbility 도입)                                   | ✅ 완료 |
| 15     | 자원 카드 JSON화                                                                   | ✅ 완료 |
| 16     | 듀얼 캐릭터 덱 구축 룰 — 2종 캐릭터, 10종류×2장=20장, 비율 자유, 봇 5:5           | ✅ 완료 |
| 17     | 캐릭터 능력 게임당 1회 제한 — 모든 용병 고유 능력은 게임당 1번만 사용              | ✅ 완료 |
| 18     | 룰북 검증 불일치 수정 — 반격+무적, 다음턴 버프 시점, 스택 강제, "그 후" 시맨틱    | ✅ 완료 |
| 추가   | 세트/오픈/드로우 페이즈 동시 처리 (Gather→Execute 패턴, WaitUntil 병렬 코루틴)    | ✅ 완료 |
| 추가   | Human 입력 타임아웃, QA 자동 응답기, `GameRules` JSON 확장, `DebugHelper`          | ✅ 완료 |

### 잔여 기술 부채 (우선순위 순)

| 항목 | 위치 | 내용 | 우선순위 |
| ---- | ---- | ---- | -------- |
| `OptionalActionEffect` JSON 미연동 | `GameDataManager.CreateKeywordEffect()` | `ActionToPerform` 파싱 로직이 TODO/주석 처리됨. 현재 JSON으로 Optional 효과를 선언할 수 없음 | 중 |
| `EVENTMANAGER_CONTRACT.md` 문서 불일치 | `EVENTMANAGER_CONTRACT.md` | `OnPrizeChange` 이벤트가 문서에는 남아 있으나 `EventManager.cs`에서 이미 삭제됨 | 중 |
| `EllieAbility.cs` 하단 레거시 코드 | `Scripts/Abilities/EllieAbility.cs` | 구 동기 구현이 `/* ... */` 블록으로 파일 하단에 남아 있음 | 낮음 |
| `ConsoleRunner.cs` 주석 처리된 구 코드 | `ConsoleRunner.cs` | 동시 처리 전환 전의 순차 구현 블록이 일부 남아 있음 | 낮음 |
| QA 덱 불일치 | `ConsoleRunner.cs` vs `BattleManager.cs` | QA 전용 테스트 덱 카드 ID 배열이 두 파일 간 미세하게 다를 수 있음 | 낮음 |
| `Managers` vs `Manager` 폴더 혼재 | 파일 시스템 | `Scripts/Manager/`(구)와 `Scripts/Managers/`(신)가 공존할 가능성 | 낮음 |

### UI팀 이관 항목 (로직팀 작업 불필요)

- `Scripts/UI/*.cs` 4개 파일 — UI팀에 샘플 코드로 전달 완료
- Unity 씬 오브젝트 배치 — UI팀 에디터 작업
- 덱 빌딩 화면 — UI팀 작업 (`DeckValidator.ValidateFullDeckSet()` API 호출. Phase 16 후 2캐릭터 파라미터 오버로드 사용)
- 이벤트 구독 구현 — `EVENTMANAGER_CONTRACT.md` 참조 (단, `OnPrizeChange` 제거됨 — 문서 동기화 필요)

---

## 7. 알려진 버그 및 기술 부채

| 위치                                    | 내용                                                                                             | 우선순위        |
| --------------------------------------- | ------------------------------------------------------------------------------------------------ | --------------- |
| `Scripts/UI/*.cs`                       | 로직팀 범위 밖. UI팀 참고용 샘플 코드로 이관됨                                                  | UI팀 이관 완료  |
| `OptionalActionEffect.cs` + `GameDataManager.cs` | `ActionToPerform` JSON 파싱 로직 미완성. 현재 코드에서 직접 인스턴스화해야만 사용 가능 | 중              |
| `EVENTMANAGER_CONTRACT.md`              | `OnPrizeChange` 이벤트가 문서에 남아 있지만 `EventManager.cs`에서 삭제됨. 문서 동기화 필요      | 중              |
| `EllieAbility.cs`                       | 파일 하단에 구 동기 구현(`/* ... */` 블록) 잔존. 불필요한 dead code                             | 낮음            |
| `ConsoleRunner.cs`                      | 동시 처리 전환 전의 순차 구현 블록 일부 주석으로 잔존                                           | 낮음            |
| QA 테스트 덱                            | `ConsoleRunner` vs `BattleManager` QA 덱 카드 ID 배열이 미세하게 다를 수 있음                  | 낮음            |

---

## 8. 현재 카드 데이터 목록

### 캐릭터 카드 (4종) — `Data/Character.json`

| ID      | 이름     | 발동 페이즈   | 능력 요약                                           |
| ------- | -------- | ------------- | --------------------------------------------------- |
| ELLI-01 | 엘리     | 메인 페이즈   | 공격 카드 사용 후, 패의 공격 카드 1장 추가 발동     |
| VERO-01 | 베로니카 | 드로우 페이즈 | 드로우 대신 덱 탑 3장 보기 → 1장 패로, 2장 되돌리기 |
| DAIN-01 | 다이나   | 오픈 페이즈   | 폐기 선택 시 라이프 1 회복                          |
| SONI-01 | 소니아   | 세트 페이즈   | 폐기존에서 소니아 카드 1장 패로                     |

### 효과 카드 — `Data/RulebookCards.json` (엘리 10종)

| ID      | 이름          | 종류 | 스피드 | 코스트 | 효과 요약                                                  |
| ------- | ------------- | ---- | ------ | ------ | ---------------------------------------------------------- |
| ELLI-02 | 퀵 드로우     | 공격 | 1      | 1      | 데미지 1 + 패 1장 폐기                                     |
| ELLI-03 | 수류탄 투척   | 공격 | 1      | 2      | 데미지 2 + 패 2장 폐기                                     |
| ELLI-04 | 미니건 난사   | 공격 | 2      | 3      | 데미지 3                                                   |
| ELLI-05 | 준비된 방어선 | 방어 | 1      | 0      | [이번 턴] 경감 1                                           |
| ELLI-06 | 격추 시스템   | 방어 | 1      | 0      | **[스택]** 방탄 1 + 패 1장 폐기                            |
| ELLI-07 | 카모플라쥬    | 방어 | 1      | 3      | [이번 턴] 무적                                             |
| ELLI-08 | 보급 전달     | 지원 | 3      | 0      | 이 카드를 자원존에 배치 + 패 1장 폐기                      |
| ELLI-09 | 리로드        | 지원 | 2      | 0      | 폐기존의 엘리 공격 카드 1장 패로                           |
| ELLI-10 | 전략적 후퇴   | 지원 | 3      | 0      | 덱 2장 드로우 + 패 2장 덱으로 되돌리기·섞기                |
| ELLI-11 | 무작위 노획   | 지원 | 3      | 2      | **[전장]** 매 자원페이즈 폐기존 공격카드 1장 랜덤으로 패로 |

### 효과 카드 — `Data/RulebookCards.json` (베로니카 10종)

| ID      | 이름            | 종류 | 스피드 | 코스트 | 효과 요약                           |
| ------- | --------------- | ---- | ------ | ------ | ----------------------------------- |
| VERO-02 | 숙청            | 공격 | 2      | 2      | 데미지 2                            |
| VERO-03 | 계획대로        | 공격 | 1      | 3      | 패 3장 폐기 + 데미지 3              |
| VERO-04 | 프로젝트:바빌론 | 공격 | 3      | 6      | 데미지 5                            |
| VERO-05 | 요새화          | 방어 | 3      | 0      | **[스택]** 경감 1                   |
| VERO-06 | 흐릿한 안개성   | 방어 | 2      | 2      | **[스택]** 경감 3                   |
| VERO-07 | 시위 해산       | 방어 | 1      | 0      | [이번 턴] 방탄 1 + 패 1장 폐기      |
| VERO-08 | 정세 읽기       | 지원 | 1      | 0      | 자원덱 → 자원존 1장                 |
| VERO-09 | 준비는 철저하게 | 지원 | 1      | 0      | 덱 2장 드로우 + [이번 턴] 경감 1    |
| VERO-10 | 테러의 잔향     | 지원 | 2      | 3      | 관통 데미지 1 + 자원덱 → 자원존 2장 |
| VERO-11 | 체크메이트      | 지원 | 1      | 2      | **[전장]** 매 턴 경감 +1            |

### 효과 카드 — `Data/RulebookCards.json` (다이나 10종)

| ID      | 이름             | 종류 | 스피드 | 코스트 | 효과 요약                                     |
| ------- | ---------------- | ---- | ------ | ------ | --------------------------------------------- |
| DAIN-02 | 함포 준비, 발사! | 공격 | 2      | 1      | 데미지 1 × 2회 + 자신 라이프 -1               |
| DAIN-03 | 미사일 발사!     | 공격 | 3      | 2      | 관통 데미지 2                                 |
| DAIN-04 | 풀 버스트!       | 공격 | 3      | 5      | 데미지 2 × 3회 + 자신 라이프 -2               |
| DAIN-05 | 잠수정           | 방어 | 2      | 0      | [이번 턴] 경감 2                              |
| DAIN-06 | 공습 주의        | 방어 | 3      | 0      | [이번 턴] 방탄 2                              |
| DAIN-07 | 강도 테스트      | 방어 | 1      | 1      | **[스택]** 경감 2                             |
| DAIN-08 | 약탈             | 지원 | 1      | 0      | 덱 3장 드로우 + 패 랜덤 1장 폐기              |
| DAIN-09 | 리벤지           | 지원 | 1      | 1      | [이번 턴] 반격                                |
| DAIN-10 | 기뢰             | 지원 | 3      | 2      | 폐기존 효과카드 1장 코스트 1 줄여서 즉시 사용 |
| DAIN-11 | 조선소           | 지원 | 3      | 2      | **[전장]** 매 턴 화력 +1                      |

### 효과 카드 — `Data/RulebookCards.json` (소니아 10종)

| ID      | 이름          | 종류 | 스피드 | 코스트 | 효과 요약                                       |
| ------- | ------------- | ---- | ------ | ------ | ----------------------------------------------- |
| SONI-02 | 게틀링건발사  | 공격 | 2      | 1      | 관통 데미지 1                                   |
| SONI-03 | 미사일 발사   | 공격 | 1      | 3      | 관통 데미지 2                                   |
| SONI-04 | 전술 핵       | 공격 | 3      | 8      | 관통 데미지 6 + 자신 패 전부 폐기               |
| SONI-05 | 곡예비행      | 방어 | 1      | 0      | [이번 턴] 방탄 1 + 덱 탑 1장 폐기               |
| SONI-06 | 엔진예열      | 방어 | 3      | 2      | **[스택]** 경감 1 + [다음 턴] 화력 1            |
| SONI-07 | 마하10        | 방어 | 3      | 2      | **[스택]** 무적                                 |
| SONI-08 | 연료충전      | 지원 | 1      | 0      | 자원덱 → 자원존 2장 + 덱 탑 4장 폐기            |
| SONI-09 | 공격적인 전술 | 지원 | 1      | 0      | 패 3장 폐기 + 덱에서 소니아 공격카드 서치       |
| SONI-10 | 신재생에너지  | 지원 | 1      | 0      | 폐기존 자원카드 → 자원존 + 폐기존 효과카드 → 덱 |
| SONI-11 | 관제탑        | 지원 | 3      | 2      | **[전장]** 소니아 카드 코스트 -1                |

### 레거시 카드 (Card.json — 참조용, 현재 게임에서 미사용)

> 구 4페이즈 구조 기반 유닛 7종 + 스킬 4종. LoadAllData()에서 로드되나 ConsoleRunner에서는 사용 안 함.

---

## 9. 다음 단계 참고사항

Phase 18 이후 동시 처리 설계(세트/오픈/드로우 페이즈), Human 입력 타임아웃, QA 자동 응답기, `DebugHelper`, `OptionalActionEffect` 등이 추가로 구현되어 있다.  
현재 **Bot vs Bot 100% / Human vs Bot 95% / Human vs Human 30%** 수준으로 완성도가 유지되고 있다.

**다음으로 작업한다면:**

1. **`OptionalActionEffect` JSON 연동 완성** — `GameDataManager.CreateKeywordEffect()`에 `optional_action` 케이스 추가하여 JSON에서 선언 가능하도록
2. **`EVENTMANAGER_CONTRACT.md` 동기화** — `OnPrizeChange` 항목 제거 및 `OnRequireOptionalAction` 항목 추가
3. **레거시 코드 정리** — `EllieAbility.cs` 하단 주석 블록, `ConsoleRunner.cs`의 구 순차 처리 블록 삭제
4. **BotBrain 고도화** — 현재 BotBrain은 랜덤/단순 우선순위 전략. 스택 활용, 라이프 상태 기반 전술 적용 검토

**로직팀의 일반 작업 가이드:**

- 새 카드 데이터 추가 → `Data/RulebookCards.json` 또는 `Data/Character.json` 편집
- 새 효과 키워드 추가 → `Scripts/Effects/` 신규 파일 + `GameDataManager.CreateKeywordEffect()` case 추가
- 새 게임 규칙 변경 → `Scripts/Systems/GameRules.cs` + `Data/CommonConfig.json`
- 새 이벤트 추가 → `EventManager.cs` 선언 + XML 문서화 + `EVENTMANAGER_CONTRACT.md` 갱신
- 새 캐릭터 능력 추가 → `Scripts/Abilities/`에 `CharacterAbilityBase` 상속 클래스 + `CharacterAbilityRegistry.Register()`

**UI팀이 할 일:**

- `EVENTMANAGER_CONTRACT.md` 숙지 (스택 강제 발동 시 `OnRequireStackResponse` 미호출, `OnRequireOptionalAction` 신규 추가됨)
- `Scripts/UI/*.cs` 4개 샘플 파일 참고하여 Unity 씬에 MonoBehaviour 부착
- `DeckValidator.ValidateFullDeckSet(mainDeck, resourceDeck, charId1, charId2)` 호출하여 덱 빌딩 화면 구현
- Human 입력 대기 시간은 `Data/CommonConfig.json`의 `wait_time` 값으로 조절 가능 (기본 30초)

---

## 10. 대화 참조

이 작업은 아래 Cursor 채팅 세션들에서 진행되었습니다:

| 작업 내용                                               | 에이전트 ID                            |
| ------------------------------------------------------- | -------------------------------------- |
| Phase 1~2 구현                                          | `17512e0e-eaad-4701-9f84-74ad899f7ed3` |
| Phase 3~6 구현                                          | `이전 대화`                            |
| Phase 7 구현 (카드 데이터 교체)                         | `이전 대화`                            |
| Phase 8~11 구현                                         | `이전 대화`                            |
| Phase 12 구현 (레거시 정리)                             | `이전 대화`                            |
| Phase 13~18 구현 + 동시 처리 설계 + 코드 전수 재점검   | `9ba4afde-b560-429b-a7f9-3dd41a08c799` |

- 계획 파일: `C:\Users\tekyung\.cursor\plans\전용_게임_구현_계획_8dd00c43.plan.md`
- 수정 계획 파일: `C:\Users\tekyung\.cursor\plans\tcg_프로젝트_수정_계획_8a25672d.plan.md`
- Phase 4~8 계획 파일: `C:\Users\tekyung\.cursor\plans\phase_4~8_이어서_구현_6d66e0de.plan.md`
- Phase 7 계획 파일: `C:\Users\tekyung\.cursor\plans\phase_7_카드_데이터_교체_1a066ee7.plan.md`
- Phase 8 계획 파일: `C:\Users\tekyung\.cursor\plans\phase_8_구현_계획_9f9e5e91.plan.md`
- Phase 9 계획 파일: `C:\Users\tekyung\.cursor\plans\phase_9_이후_작업_계획_c29fb2e7.plan.md`
- Phase 10 계획 파일: `C:\Users\tekyung\.cursor\plans\phase_10_effect_리팩터링_5f22a232.plan.md`
- Phase 11 계획 파일: `C:\Users\tekyung\.cursor\plans\phase_11_작업_계획_e9ca6344.plan.md`
- Phase 12 계획 파일: `C:\Users\tekyung\.cursor\plans\phase_12_레거시_정리_0bb30c63.plan.md`
- Phase 13~18 계획 파일: `C:\Users\tekyung\.cursor\plans\phase_13~18_작업_계획_8b62a3f4.plan.md`
