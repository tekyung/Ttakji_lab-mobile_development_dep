# TCG_Project 작업 인수인계 문서

최초 작성일: 2026-02-28  
최종 수정일: 2026-08-17 (Firebase 서버 실측 진단 / 손패 호버 미해결 기록 / 버튼 우선순위 / 서브 팝업 3배 / 폐기존 패널 바깥 클릭 / 소니아 OnCardMove 누락)
목적: 새 AI가 현재까지의 작업을 이어받아 계속 진행하기 위한 컨텍스트 제공 (로직 레이어 + Unity 보드 UI)

> **2026-08-16 갱신 범위**  
> 이 문서는 `Assets/TCG_Project/`와 `Assets/Scripts/`의 실제 파일을 전수 대조하여 갱신되었다.  
> 이전 판(2026-08-15)에는 `PlayerSetupData` 의존성 주입, `IJsonLoader` 로더 추상화, `TiebreakerResolver` 분리,  
> 용병 필드 슬롯 방송, `DiscardEffect`가 코드에만 있고 문서에 없었다. 섹션 4·5·6·7이 그에 맞춰 수정되었다.

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
8. **섹션 11 (모드별 진단)** — 사람 vs 봇 / 사람 vs 사람 상태가 바뀌면 갱신

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
   `DeckValidator.ValidateFullDeckSet(List<Card> mainDeck, List<Card> resourceDeck, string char1Id, string char2Id)`  
   반환: 중첩 클래스 `DeckValidator.DeckValidationResult` (`IsValid` / `ErrorMessage`)  
   → **공개 메서드는 이것 하나뿐이다.** 구 `ValidateDeck` / `ValidateResourceDeck` / `IsValidCharacterDeck` /  
   `IsValidRulebookDeckWithCharacter`는 현재 코드에 없다. 덱 빌딩 화면은 이 단일 API만 호출한다.

3. **GameDataManager** — 카드 데이터 로드 API  
   생성자에 `IJsonLoader`를 주입한다: `new GameDataManager(loader)`  
   `LoadRulebookCards()`, `LoadCharacterCards()`, `LoadResourceCards()` (basePath 인자 없음 — 로더가 경로를 안다)  
   Unity는 `UnityResourceLoader("GameData")`, 콘솔은 `ConsoleFileLoader("./Data")`

4. **PlayerSetupData** — 로비/매치메이킹 → 엔진 매치 시작 DTO (`Scripts/Core/PlayerSetupData.cs`)  
   `BattleManager.Instance.StartMatch(p1Setup, p2Setup)` 한 곳으로만 매치가 시작된다.  
   필드: `PlayerName`, `Type(UserType)`, `MainCharacterId`, `SubCharacterId`, `DeckCardIds(List<string> 20장)`

> `Scripts/UI/` 폴더의 4개 파일은 UI 팀에게 **이벤트 구독 참고 코드(샘플)** 로 제공된 것이며,  
> 로직팀이 직접 유지보수하지 않는다. 필요 시 UI팀이 덮어써도 된다.

### 프로젝트 경로

```
C:\Users\tekyung\.cursor\TTAckji-Game\Ttakji_lab-mobile_development_dep-M1_milestone\
```

현재 브랜치: `M2_milestone`

### 사본이 3벌 존재한다 — 반드시 이해할 것

| 위치 | 역할 | 편집 여부 |
| ---- | ---- | -------- |
| `Assets/TCG_Project/` | **SSOT(정본)**. Unity가 컴파일하는 로직 레이어 | ✅ 여기만 편집한다 |
| `TCG_Project/` (저장소 루트) | 콘솔 실행 미러. `sync-tcg-project.ps1`이 덮어쓴다 | ❌ 직접 편집 금지 (덮어써짐) |
| `Assets/Resources/GameData/*.json` | Unity 런타임이 `Resources.Load`로 읽는 JSON | ❌ 직접 편집 금지 (동기화 대상) |

`tools/sync-tcg-project.ps1`이 정본 → 나머지 2곳으로 복사한다. **미러에만 있는 편집은 다음 빌드에서 사라진다.**

**동기화에서 의도적으로 제외되는 파일** (스크립트 `$excludeRel` + `.csproj`의 `Compile Remove`):

- `Scripts/UI/**` — UI팀 샘플, 콘솔에서 컴파일 불가
- `Scripts/Managers/BattleManager.cs` — Unity MonoBehaviour
- `Scripts/Managers/UnityResourceLoader.cs` — `using UnityEngine`

> 그래서 `TCG_Project/Scripts/Managers/BattleManager.cs`는 **동기화도 컴파일도 되지 않는 옛 사본**이다.
> 정본(1199줄, `PlayerSetupData` 주입)과 내용이 다르지만 **정상이며, 맞출 필요가 없다.**
> 혼동을 피하려면 스크립트의 `$deleteFromMirror` 목록에 추가해 미러에서 지우는 방법이 있다 (선택 사항).

### 빌드 환경

- 콘솔 `.csproj`: **net9.0**, OutputType=Exe — 루트 `TCG_Project/TCG_Project.csproj`만 사용
- `.csproj`에 `SyncFromAssets` 타깃이 있어 **Restore/Build 직전 sync 스크립트가 자동 실행된다.** 별도 동기화 명령이 필요 없다
- `Assets/TCG_Project/` 안에 SDK식 `.csproj` / `obj` / `bin`을 두지 않는다. Unity가 `obj/**/AssemblyAttributes.cs`를 컴파일하면 TargetFramework 충돌이 난다
- Newtonsoft.Json 13.0.4 사용
- 빌드: `dotnet build ./TCG_Project/TCG_Project.csproj` → **2026-08-16 기준 오류 0개 / 경고 284개**
- 실행: `dotnet run --project ./TCG_Project/TCG_Project.csproj`
  - `ConsoleFileLoader`가 **`./Data` 상대 경로**를 읽는다. 반드시 `TCG_Project/` 안에서 실행할 것
  - ⚠️ **개발 PC에 .NET 9 런타임이 없다** (설치된 것은 6.0 / 8.0 / 10.0). 빌드는 참조 어셈블리로 통과하지만 실행은 실패한다.
    우회: `$env:DOTNET_ROLL_FORWARD = "LatestMajor"` 후 실행 (2026-08-16 이 방법으로 정상 완주 확인)
    항구적 해결: `.csproj`에 `<RollForward>LatestMajor</RollForward>` 추가하거나 TargetFramework를 `net8.0`/`net10.0`으로 변경
    → RL 학습 루프를 돌리기 전에 이 문제를 먼저 정리해야 한다
- 동기화 검증: `powershell -File tools/sync-tcg-project.ps1 -Check` (파일 해시 + `imagePath` 스프라이트 존재 + `ELLIE`/`GameDesign` 레거시 경로 검사) → **2026-08-16 기준 ok**

---

## 2. 아키텍처 원칙 (반드시 준수)

### 원칙 1 — 이중 실행 환경 (Dual-Environment)

- `ConsoleRunner.cs`: Unity 없이 단독 실행하는 테스트 러너
- `BattleManager.cs`: Unity MonoBehaviour 기반 진입점
- **게임 로직은 반드시 두 환경 모두에서 동작해야 한다**

> **⚠️ 콘솔 빌드를 "지금 안 쓰니까"라며 정리하지 말 것 — 존치 이유가 있다**
>
> 콘솔 빌드(`TCG_Project.csproj` + `ConsoleRunner.cs`)는 단순한 개발 편의 도구가 아니라
> **추후 강화학습(RL) 확장을 위한 기반**이다. 학습 루프를 돌리려면 Unity 에디터·렌더링·프레임 루프에
> 묶이지 않고 **헤드리스로 고속 반복 실행 가능한 게임 로직**이 필요하다.
>
> 그래서 원칙 2(Zero Unity Dependency)는 단순한 코드 위생 규칙이 아니라 **RL 확장의 전제 조건**이다.
> 로직 레이어에 `using UnityEngine`을 하나라도 들이면 그 순간 헤드리스 실행이 불가능해지고
> RL 경로가 막힌다. 편의를 위해 Unity API를 끌어오고 싶을 때 이 문단을 다시 읽을 것.

### 원칙 2 — Zero Unity Dependency (로직 레이어)

- `Scripts/Core/`, `Scripts/Systems/`, `Scripts/Effects/`, `Scripts/Conditions/`, `Scripts/Abilities/`, `Scripts/Interfaces/`, `Scripts/Utils/` 폴더의 파일은 `using UnityEngine`을 **사용하지 않는다**
- Unity 의존이 허용되는 파일은 **정확히 2개**: `Scripts/Managers/BattleManager.cs`, `Scripts/Managers/UnityResourceLoader.cs`
  (둘 다 콘솔 `.csproj`의 `Compile Remove`와 sync 스크립트의 `$excludeRel`에 등재되어 있다)
- 새로 Unity 의존 파일을 만들면 **두 곳 모두에 등록**해야 콘솔 빌드가 깨지지 않는다

### 원칙 2-0 — 모든 소스 파일은 UTF-8(BOM 없음)로 저장한다

- 저장소 루트의 `.editorconfig`가 `charset = utf-8`을 선언한다. VS / Rider / VS Code가 이를 따른다
- **CP949(EUC-KR)로 저장하지 말 것.** 한글 주석과 문자열이 전부 깨진다.
  실제로 `DeckValidator.cs`가 CP949로 저장돼 있어 **깨진 에러 메시지가 덱 빌딩 UI 팝업에 그대로 노출**됐다
- 2026-08-16에 저장소 전체를 UTF-8로 변환 완료 (Assets 10개 + 미러 5개). 현재 위반 파일 0개
- BOM은 붙이지 않는다 (기존 다수 관행과 일치)
- 검사 방법 — PowerShell에서 아래를 돌리면 위반 파일이 나열된다

  ```powershell
  $strict = New-Object System.Text.UTF8Encoding($false, $true)
  Get-ChildItem . -Recurse -Include *.cs,*.json,*.md |
    Where-Object { $_.FullName -notmatch '\\(Library|Temp|obj|bin)\\' } |
    ForEach-Object { try { [void]$strict.GetString([IO.File]::ReadAllBytes($_.FullName)) } catch { $_.FullName } }
  ```

- 줄바꿈(CRLF/LF)은 **아직 통일되어 있지 않다** (`.cs` 기준 CRLF 43 / LF 57).
  `.editorconfig`에서 일부러 규정하지 않았다. 강제하면 저장 시 파일 전체가 변경으로 잡혀 diff가 오염된다.
  통일하려면 별도 작업으로 `.gitattributes`와 함께 한 번에 처리할 것
- **씬·프리팹 YAML을 스크립트로 수정할 때 주의:** PowerShell `WriteAllLines`는 줄바꿈을 CRLF로 바꿔 버린다.
  `TestGameScene.unity`(LF, 11,347줄)에 쓰면 전 줄이 변경된 것으로 잡힌다.
  `ReadAllText` → 문자열 치환 → `WriteAllText`로 처리할 것

### 원칙 2-1 — 파일 입출력은 IJsonLoader를 경유한다

- 로직 레이어는 `System.IO`나 `Resources.Load`를 직접 호출하지 않는다. `Scripts/Interfaces/IJsonLoader.cs`의 `LoadJson(fileName)`만 쓴다
- 구현체 2종: `UnityResourceLoader`(Unity, `Resources/GameData`) / `ConsoleFileLoader`(콘솔, `./Data`)
- `GameDataManager`와 `GameRules.LoadRules`가 모두 이 인터페이스를 주입받는다

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

### 원칙 6 — 레거시 제거 완료 (Phase 9에서 종결)

- `CardType.Unit` / `CardType.Skill` / `Mana` 시스템 / `BattleSystem` / `TargetSelector`는 **모두 삭제되었다.**
  현재 `CardType`은 `None/Attack/Defense/Support/Character/Resource` 6종뿐이다
- 구 하위 호환 원칙은 더 이상 유효하지 않다. 새 코드에서 이들을 되살리지 않는다

### 원칙 6-1 — 매치 시작은 의존성 주입으로만

- 엔진 안에 덱·용병 ID를 **상수로 박지 않는다.** 매치 시작은 `StartMatch(PlayerSetupData, PlayerSetupData)` 한 경로뿐이다
- 테스트용 덱 구성은 **호출자 쪽**에 둔다: Unity는 `LocalMatchStarter`, 콘솔은 `ConsoleRunner.Run()` 상단, 서버는 `ServerGameManager`
- `BattleManager.InitializeSingleGame()` 안의 `p1TestIds`/`p2TestIds` 블록은 **주석 처리된 QA 참고용**이다.
  카드 조합 충돌을 재현할 때 참고하도록 남겨둔 것이므로 **지우지 않는다** (활성 경로는 `_p1Setup.DeckCardIds`)

### 원칙 7 — UI 팀과의 계약 유지 (로직팀 최우선 원칙)

- **EventManager의 이벤트 시그니처(파라미터 타입·순서)는 함부로 변경하지 않는다.**  
  변경 시 UI 팀의 구독 코드가 일제히 깨진다. 변경이 불가피하면 반드시 UI 팀에 공지한 뒤 진행한다.
- **새 게임 로직 이벤트를 추가할 때는 EventManager에 선언과 XML 문서화를 동시에 완료하고 `EVENTMANAGER_CONTRACT.md`를 갱신한다.**
- **DeckValidator의 공개 메서드 시그니처 변경 금지.** UI 팀이 호출하는 API다. 기능 추가는 새 오버로드로만 한다.
  > ⚠️ 이 원칙은 과거에 한 번 깨졌다. `ValidateDeck` / `ValidateResourceDeck` / `IsValidCharacterDeck`가
  > 오버로드 추가가 아니라 **삭제**되어 `ValidateFullDeckSet` 하나만 남았다. 덱 빌딩 화면이 구 API를 호출 중이면 컴파일이 깨진다.
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

> 아래는 **정본 `Assets/TCG_Project/`** 기준이다. 2026-08-16에 디스크와 전수 대조했다.

```
Assets/TCG_Project/
├── Program.cs                ← 콘솔 진입점 (Main → ConsoleRunner.Run)
├── ConsoleRunner.cs          ← 콘솔 러너. 상단에 PlayerSetupData 2개를 직접 조립(가상 로비 역할)
│                                세트/오픈 동시 처리(Gather→Execute), BotSingleGame 단판/3판 전환
├── ConsoleFileLoader.cs      ← ⭐ IJsonLoader 콘솔 구현체 (`./Data`에서 File.ReadAllText)
├── EVENTMANAGER_CONTRACT.md  ← EventManager.cs와 1:1 동기화 (2026-08-16 확인)
├── PROJECT_DIAGNOSIS.md      ← 로직팀 상태 진단서
├── CardManual.md             ← 카드 40종 상세 매뉴얼 (저장소 루트에도 동일 사본)
├── EFFECT_SYSTEM_PROPOSAL.md ← Effect 통합 설계 제안서 (Phase 10 근거 문서)
├── Scripts/
│   ├── Core/
│   │   ├── Card.cs                ← CharacterId, IsStack, Speed, ImagePath, IsFaceUp
│   │   ├── CharacterFieldState.cs ← ⭐ CharacterSlotType(Main/Sub) enum + CharacterSlotSnapshot struct
│   │   │                             (Owner/Slot/CharacterCardId/ImageResourcesPath/RotationZ/AbilityUsed)
│   │   ├── DebugHelper.cs         ← 색상 로그 유틸 (LogSpell/LogEffect/LogWarning, Rich Text 태그)
│   │   ├── Enums.cs               ← ZoneType 10종, GamePhase 8종, CardType 6종,
│   │   │                             CardSpeed / EffectDuration / OpenPhaseChoice / UserType
│   │   ├── GameContext.cs         ← CurrentTurn, CurrentPhase, LastEffectSucceeded, IsGameOver
│   │   ├── PendingEffect.cs
│   │   ├── Player.cs              ← 존 9종 + 라이프 + 전투버프 + 스택버프 리스트 5종
│   │   │                             (StackArmors/StackSuperArmors/StackCounterAttacks/
│   │   │                              StackInvincibilities/StackFirepowers)
│   │   │                             + NextTurn 버프 5종, PendingCounterRewards 큐
│   │   │                             + UseAndDiscardStack: Extract→Insert(Graveyard)→OnCardMove 순서
│   │   │                             + PayCost: ResourceZone[0] FIFO + OnCardMove(ResourceZone→Graveyard)
│   │   │                             + InitializeLifeTokens/GainLife/LoseLife 모두 OnLifeChange 발행
│   │   ├── PlayerSetupData.cs     ← ⭐ 로비→엔진 DTO. PlayerName/Type/Main·SubCharacterId/DeckCardIds
│   │   └── Target.cs
│   ├── Abilities/                 ← ICharacterAbility, CharacterAbilityBase, CharacterAbilityRegistry,
│   │                                 EllieAbility / VeronicaAbility / DainaAbility / SoniaAbility
│   │                                 4종 모두 AsyncTimeoutHelper로 인간 입력 타임아웃 처리
│   ├── Systems/
│   │   ├── BotBrain.cs            ← 6페이즈 AI 전략
│   │   ├── ConditionEvaluator.cs
│   │   ├── DamageResolver.cs      ← 화력→아머/슈퍼아머→무적→LoseLife→반격 파이프라인
│   │   ├── FormulaEvaluator.cs
│   │   ├── GameDataManager.cs     ← ⭐ 생성자에 IJsonLoader 주입. LoadRulebookCards/LoadCharacterCards/
│   │   │                             LoadResourceCards (basePath 인자 없음)
│   │   │                             CreateKeywordEffect()가 JSON → Effect 객체 팩토리
│   │   ├── GameLogicHelpers.cs    ← GetEffectiveCost / ApplyBattlefield*Effects / DrawCards
│   │   ├── GameRules.cs           ← ⭐ LoadRules(IJsonLoader, "CommonConfig")
│   │   │                             LifeTokens/ResourceDeckCount/StartingHands/ChooseWaitTime/
│   │   │                             BotDelayTime/BotSingleGame/DefaultCardBackPath
│   │   ├── HumanBrain.cs          ← EventManager 이벤트 위임
│   │   ├── SpeedResolver.cs       ← 9단계 스피드 해결
│   │   └── TargetEvaluator.cs
│   ├── Effects/
│   │   ├── CardSelector.cs         ← 존·필터·선택 방식 공통 로직 + SelectMode enum
│   │   ├── MoveEffect.cs           ← 카드 이동 9종 통합. 실패 시 LastEffectSucceeded=false
│   │   ├── DamageEffect.cs         ← 데미지 3종 통합 (isPiercing/times/targetSelf)
│   │   ├── DiscardEffect.cs        ← ⭐ 폐기 전용 효과. IsStackAction / RequirePreviousSuccess 지원
│   │   ├── BuffEffect.cs           ← 버프 6종 통합 + BuffType enum + rewardOnSuccess(반격 성공 보상)
│   │   ├── BattlefieldEffect.cs    ← 전장 5종 통합 (PerTurnEffect/PerResourcePhaseEffect/CostReduction)
│   │   ├── SelfPlaceEffect.cs      ← 이 카드를 자원존 등에 배치
│   │   ├── CompositeEffect.cs      ← "그 후" 시맨틱 (LastEffectSucceeded 검사 후 다음 Step)
│   │   ├── TemporaryCostEffect.cs  ← PlayBuffer 카드 임시 코스트 감소
│   │   ├── PlayFromBufferEffect.cs ← PlayBuffer 카드 발동 + 코스트 복원 + 폐기존 이동
│   │   └── OptionalActionEffect.cs ← 봇=자동 Yes, 인간=OnRequireOptionalAction 위임
│   ├── Conditions/
│   │   ├── ComparePlayerStatCondition.cs
│   │   └── HandCountCondition.cs
│   ├── Interfaces/
│   │   ├── ICardcondition.cs  (클래스명은 ICardCondition — 파일명 대소문자 주의)
│   │   ├── ICardEffect.cs
│   │   ├── IJsonLoader.cs     ← ⭐ string LoadJson(string fileName)
│   │   └── IPlayerBrain.cs    ← 6페이즈 메서드
│   ├── Managers/              ← namespace `TCG_Project.Scripts.Managers`
│   │   ├── BattleManager.cs   ← 🚫 Unity 전용(동기화·콘솔컴파일 제외). 1199줄
│   │   │                         StartMatch(PlayerSetupData×2) 단일 진입점, 6페이즈 코루틴 체인,
│   │   │                         세트/오픈/드로우 병렬 코루틴 + WaitUntil, ChooseWaitTime 타임아웃,
│   │   │                         QA 자동 응답기(HandleQA_CardPick/HandleQA_OptionalAction),
│   │   │                         CharacterFieldBroadcast 연동, InjectTestCard(QaInjection)
│   │   │                         ※ 구현이 끝난 옛 메서드가 /* */ 주석으로 남아 동명 메서드가 중복 보임
│   │   │                           (ExecuteDrawPhaseRoutine, ExecuteOpenPhaseRoutine 등)
│   │   ├── EventManager.cs    ← 이벤트 39개 = 로직→UI 발행 33 + UI→로직 콜백(`OnRequire*`) 6. XML 문서화 완료
│   │   ├── MatchManager.cs    ← 3판 2선승 (gamesToWin/maxGames 생성자 인자)
│   │   └── UnityResourceLoader.cs ← ⭐ 🚫 Unity 전용. IJsonLoader 구현 (Resources.Load<TextAsset>)
│   │                                 ※ 파일은 Managers 폴더인데 namespace는 `...Scripts.Systems`
│   ├── UI/                    ← 🚫 UI팀 샘플 4종. 동기화·콘솔컴파일 제외
│   │   ├── GameStatusUI.cs / PhaseInputPanel.cs / StackResponsePanel.cs / CardSelectionPanel.cs
│   └── Utils/
│       ├── AsyncTimeoutHelper.cs     ← ⭐ WaitForChoiceWithTimeout<T> — 코루틴 비의존 Task 타임아웃
│       ├── CharacterFieldBroadcast.cs ← ⭐ 용병 슬롯 스냅샷 생성·방송
│       │                                Register/GetRotationZ/BuildSnapshot/BuildAllSnapshots/
│       │                                SyncAll/EmitSlotUpdate
│       │                                회전 규칙: 하단(P1)=0°, 상단(P2)=180°, 능력 사용 시 +180°
│       ├── DeckValidator.cs          ← ⚠️ ValidateFullDeckSet 1개 + 중첩 DeckValidationResult
│       │                                + 미사용 ResolveTiebreaker (섹션 7 참조)
│       ├── QaInjection.cs            ← Inject(dataManager, player, cardId, zone) / ApplyDefaultScenario
│       └── TiebreakerResolver.cs     ← ⭐ 룰북 6단계 타이브레이커. 실제로 호출되는 쪽
├── Data/                      ← 편집은 여기서. sync가 미러 + Resources/GameData로 복사
│   ├── CommonConfig.json      ← life_tokens/resource_deck_count/starting_hands/choose_wait_time/
│   │                             Bot_delay_time/Bot_single_game/card_zoom_hold_duration_time/
│   │                             default_card_back
│   ├── Character.json         ← 캐릭터 4종
│   ├── ResourceCards.json     ← 자원 카드 1종 (RES-01)
│   └── RulebookCards.json     ← 실제 카드 40종. imagePath = Assets/Resources/card_image/{ELLI|VERONICA|DAINA|SONIA}/ID.png
```

⭐ = 이번(2026-08-16) 갱신에서 문서에 처음 기재된 파일 · 🚫 = 콘솔 동기화/컴파일 제외

**삭제되어 더 이상 없는 파일(참고):** `UnitCard.cs`, `BotSimulator.cs`, `BattleSystem.cs`, `TargetSelector.cs`,
`ReplayCardEffect.cs`, `DeckValidationResult.cs`(DeckValidator 내부 중첩 클래스로 흡수),
Phase 9~10에서 정리된 개별 Effect 30여 종

### Unity 보드 UI (로직 레이어 밖 — `Assets/Scripts/`)

> 2026-07~08 구현. `OnCardMove`는 reparent만, Instantiate는 매치 시작 1회.

```
Assets/Scripts/
├── BuildDeck/
│   ├── CardData.cs            ← JSON 필드명 `imagePath` (구 `skin_res`는 JsonUtility가 채우지 않음)
│   ├── CardUI.cs              ← BindEngineCard는 `card.ImagePath` 우선, 없으면 CardData.imagePath
│   ├── CardDataManager.cs / DeckBuilderManager.cs / CheckCard.cs
│   ├── CommonConfigManager.cs / LongPressTrigger.cs
├── Utils/
│   ├── CardImageLoader.cs     ← `Assets/Resources/`·확장자 제거 후 Resources.Load
│   └── UiFontResolver.cs      ← ⭐ 런타임 생성 TMP용 폰트 해석기 (캐싱)
│                                 코드로 만든 TMP는 기본 폰트(LiberationSans)라 한글이 ㅁ로 깨진다.
│                                 씬이 쓰는 폰트(CookieRun Black SDF)를 찾아 따라간다
│                                 + EnsureSymbolFallback(): CookieRun에는 기호가 하나도 없어서
│                                   → ★ ♬ · × 등이 전부 깨진다. OS 폰트를 동적 폴백으로 건다
├── InGameCard/
│   ├── CardBoardRegistry.cs   ← InstanceId → GO 풀. BuildPool / TryGet / ApplyZoneMove / ClearPool
│   │                            ZoneMoveContext.ResourceZone은 가시 스택이 아니라 트윈 출발점
│   │                            ResetVisualState가 CanvasGroup.alpha를 1로 되돌린다 (세트 흐림 자동 해제)
│   ├── GameStatusPanelUI.cs   ← ⭐ 턴·페이즈 표시 / 진행 로그 / 결과 오버레이 (자가 생성)
│   │                            6페이즈 이벤트와 OnMatchSet의 유일한 Unity 소비자
│   ├── DeckInfoPanelUI.cs     ← ⭐ 좌측 상단 톱니바퀴 → 덱 5×4 격자 + [항복] (자가 생성)
│   │                            덱 스냅샷은 OnGameStart(시작 드로우 이전) 시점의 player.Deck
│   │                            톱니바퀴 아이콘도 코드 생성 (⚙ 글리프가 폰트에 없음)
│   ├── CardZoomPopupUI.cs     ← ⭐ 공개 카드 확대(용병·스택·세트앞면·전장) + 폐기존 목록 패널
│   │                            클릭 감지는 레이캐스트가 아니라 Rect 포함 판정.
│   │                            (스택·폐기·전장은 CardBoardRegistry가 레이캐스트를 꺼 두기 때문)
│   │                            폐기존 패널은 화면 오른쪽 (왼쪽은 로그 패널 자리)
│   ├── HumanChoiceDialogUI.cs ← ⭐ 사람 전용 카드 선택 / 예·아니오 다이얼로그
│   │                            OnRequireCardPick / OnRequireCardChoice / OnRequireOptionalAction 구독
│   │                            씬 배선 불필요 (RuntimeInitializeOnLoadMethod 자가 생성, UI도 코드 생성)
│   │                            스크롤 뷰포트는 반드시 RectMask2D — Mask는 자식을 통째로 잘라 버린다
│   │                            ▼▲는 문자 대신 코드 생성 삼각형 스프라이트 (폰트에 글리프 없음)
│   ├── DeckGraveyardStackUI.cs ← 메인덱 뒷면 스택, 폐기존 앞면 스택 (엔진 리스트 SSOT)
│   │                            EnsureDeckCardAnchor: 덱 Rect 중앙(0.5,0.5) 전용 자식. 부모 피벗 사용 금지
│   ├── StackZoneRowUI.cs      ← 단일 스택 바 layout. registry 필수, Instantiate fallback 비활성
│   ├── CardMoveTween.cs       ← WorldToScreenPoint → ScreenPointToLocalPointInRectangle 후 anchored lerp
│   ├── CardBoardInvariantChecker.cs ← SetZone/Deck/Graveyard 엔진↔hierarchy 대조
│   ├── CharacterFieldUI.cs    ← ⭐ 용병 슬롯 4칸 표시. OnCharacterFieldSync / OnCharacterSlotUpdated 구독
│   │                            능력 사용 시 카드가 180° 회전 (CharacterFieldBroadcast 규칙)
│   ├── GameSceneBoardBinder.cs ← GameScene 서버 흐름 유지, 존 트랜스폼 자동 배선
│   ├── PlayerUIManager.cs     ← Human: 선택은 InstanceId만. reparent는 OnCardMove
│   │                            OnGameStart에서 myLifeText를 player.LifeTokens로 동기화
│   │                            ⭐ 세트 확정 전 잔상: 손패 카드 흐림 + 세트존 흐린 뒷면
│   │                            (공개=파랑 / 폐기=빨강 테두리 + 우측 상단 라벨)
│   │                            잔상은 CardUI를 붙이지 않는다 (CountCardObjects가 세어 교체를 막음)
│   │                            잔상 제거는 OnCardSet 시점 — [레디] 클릭 시점이 아니다
│   ├── EnemyVisualTester.cs   ← Bot: Hand→SetZone을 OnCardMove로 즉시 반영. OnPlayCard는 공개만
│   │                            OnGameStart에서 봇 라이프/자원 텍스트를 엔진 값으로 동기화
│   ├── MyHandManager.cs / CardInteraction.cs / DropZone.cs ← 손패 레이아웃·드래그·드롭
│   └── InGameUIManager.cs
├── MainMenu/                  ← DataManager.cs, DeckSelect.cs
├── LocalMatchStarter.cs       ← ⭐ 로컬 매치 진입점. BotVsBot / HumanVsBot
│                                P1UniqueCardIds / P2UniqueCardIds(각 10종) → 2장씩 20장으로 전개하여
│                                PlayerSetupData 조립 후 BattleManager.StartMatch 호출
│                                StartMatch 직전 ConfigureBotVsBotSpectator
├── GameUIManager.cs / SceneChanger.cs / CustomRoomUI.cs / OpponentIntroUI.cs
└── Server Scripts/            ← Firebase 온라인 매치
    ├── ServerGameManager.cs   ← 1183줄. 오픈 타이밍 BattleManager와 동일(동시 ApplyOpenChoice → ActionDelay 1회)
    │                            타이브레이커는 TiebreakerResolver 사용
    ├── ServerSenderManager.cs / firebase_network.cs
    ├── session_*.cs (data / game_data / game_manage / manage / ui)
    │   ※ LocalMatchStarter가 Awake에서 session_game_manage를 비활성화한다 (로컬 플레이 충돌 방지)
    └── EventScripts/          ← ActionValidator.cs, EventDTO.cs, EventService.cs
```

**씬 (`Assets/Scenes/`)**: `TestGameScene`(로컬 봇 매치 검증), `GameScene`(서버 매치), `BuildDeck`,
`MainMenu`, `MainMenuPopupUI`, `TestServerConnect`, `server ui`, `SampleScene`

**매치 진입점 3종 (모두 `PlayerSetupData` 경유)**

| 환경 | 진입점 | 덱 구성 위치 |
| ---- | ------ | ----------- |
| Unity 로컬 | `LocalMatchStarter.StartMatchAfterBattleManagerReady()` | `P1UniqueCardIds` / `P2UniqueCardIds` 상수 |
| 콘솔 | `ConsoleRunner.Run()` | 메서드 상단 `p1Setup` / `p2Setup` 리터럴 |
| 서버 | `ServerGameManager` | 세션 데이터 |

**표시 규칙**

| 모드 | Human 손패 | Bot 손패 | 세트존 |
| ---- | ---------- | -------- | ------ |
| Human vs Bot | 앞면 | 뒷면 (`SetFaceDown(true)`) | `SetFaceDown(!IsFaceUp)` |
| Bot vs Bot (관전 ON, 기본) | N/A | 양측 앞면 | 동일 (세트 시 뒷면) |

- 풀 프리팹: Human `myCardPrefab`, Bot `cardFrontPrefab` — 둘 다 `CardSlotInGame` (`CardUI` 필수).
- `cardBackPrefab`(`CardBackground`)은 Inspector 잔존, **풀 생성에 사용하지 않음**.
- 뒷면은 `CardUI.SetFaceDown`만. GO 전체에 `ApplyDefaultCardBack` 금지 (루트 `cardImage` 오염).
- 숨김: `CardType.Resource`(폐기존 제외) 또는 `ResourceDeck` / `ResourceZone` / `PlayBuffer` → hiddenPool, `SetActive(false)`.
- 메인덱: `Player.Deck` 순서, `Deck[0]`이 탑, 전 카드 뒷면, 작은 오프셋 적층, raycast 비활성.
- 폐기존: `Player.Graveyard` 추가 순서, 마지막 폐기 카드가 탑, 효과카드·비용 자원카드 모두 앞면 적층.
- HiddenPool은 Canvas 하위 off-screen `RectTransform` (`anchoredPosition.x = 8000`). 카드 이동은 `anchoredPosition` 트윈.
- 메인덱 스택 부모는 존 Rect의 **피벗이 아니라** 런타임 `DeckCardAnchor`(중앙). `PlayerUIManager.myDeckTransform` / `EnemyVisualTester.enemyDeckTransform`은 풀 초기화 때 이 앵커로 교체한다.
- `ApplyZoneMove` 트윈: 재부모 **전** 월드 좌표 캡처 → 손패 LayoutGroup 잠금 → `SetParent` → 도착 프레임/손패 간격 적용 → `WorldToAnchored` → lerp. 덱/폐기 `Sync`는 이동 중인 인스턴스의 **도착 좌표만** 건너뛰고(`SkipLayoutInstanceId`) `PrepareStackedCard`는 적용한다.
- 라이프 TMP 씬 기본값은 `"0"`. `OnLifeChange`만으로는 시작 표기가 갱신되지 않았으므로 `OnGameStart`에서 `LifeTokens`를 직접 쓴다.

---

## 5. Phase별 완료 현황

### [2026-08-17 후속 7] 온라인 무한 대기 교착 해소 (F-4 5단계) ✅ 완료

섹션 11 F-1 **#5** 해결. **서버 스크립트를 건드리지 않고** 끝냈다.

**문제:** 온라인에서 용병 능력·카드 선택 프롬프트는 **제한 시간이 없었다.**
`GameLogicHelpers.GetChooseTimeoutMs`가 Human이면 무조건 `NoTimeout(-1)`을 돌려주고,
`AsyncTimeoutHelper`는 `Task.Delay(-1)`로 영원히 기다린다.
호스트 코루틴은 `WaitUntil(done || IsGameOver)`에 걸려 있는데 응답이 없으면 둘 다 성립하지 않아
**양쪽 모두 영구 정지**한다. (`ServerGameManager` L400·424·464·819·869 — 타임아웃이 있는 곳은 세트·오픈·스택 3곳뿐)

**해결:** 정책 스위치 하나를 엔진에 두고 Unity 쪽에서 전환한다.

```csharp
// GameLogicHelpers (Zero Unity Dependency 유지 — 그냥 static bool이다)
public static bool AllowUnlimitedHumanInput { get; set; } = true;   // 로컬 기본

public static int GetChooseTimeoutMs(Player player)
{
    bool isHuman = player != null && player.Type == UserType.Human;
    return isHuman && AllowUnlimitedHumanInput ? NoTimeout : GameRules.ChooseWaitTime;
}
```

| 전환 지점 | 값 |
| --------- | -- |
| `OnlineMatchStarter.PrepareOnlineRuntime` (온라인 진입) | **false** → `ChooseWaitTime`(10초) 적용 |
| `OnlineMatchStarter.ClearSession` (메인 메뉴 복귀) | true |
| `LocalMatchStarter` 매치 시작 | true (정적 값이 이전 세션에서 남는 경우 대비) |

> **`ServerGameManager`를 고치지 않아도 되는 이유:** 타임아웃이 걸리면 `AsyncTimeoutHelper`가
> 기본값 콜백을 호출하고, 그러면 `done`이 true가 되어 호스트의 `WaitUntil`이 자연히 풀린다.
> 즉 **타임아웃 값 하나만 유한하게 만들면 5곳의 무한 대기가 모두 해소된다.**
> 기본 동작은 이미 코딩되어 있다(카드 선택=무작위, 용병 능력=미발동).

**로컬 동작 변화 없음:** 봇은 종전처럼 `ChooseWaitTime`, 로컬 사람은 여전히 무제한이다.
콘솔 봇 회귀도 `★ [MATCH SET] ★` 정상 완주했다.

**남은 구멍 1개 (지금은 무해):** `OptionalActionEffect`는 `AsyncTimeoutHelper`를 쓰지 않고
`EventManager.OnRequireOptionalAction`을 직접 부른다 → **타임아웃이 아예 없다.**
다만 **현재 `RulebookCards.json`에서 이 타입을 쓰는 카드가 0장**이라 실제로는 도달하지 않는다.

> ⚠️ **이 클래스는 지우지 말 것.** 앞으로 추가할 용병의 특수 기믹을 위해 미리 만들어 둔 것이다
> (2026-08-17 확인). "쓰는 카드가 없으니 dead code"라며 정리하면 안 된다.
> 다만 이 효과를 쓰는 카드를 **실제로 추가할 때는 먼저 `AsyncTimeoutHelper` 경유로 바꿔야 한다.**
> 그러지 않으면 온라인에서 그 카드가 나오는 순간 양쪽이 영구 정지한다.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 봇 회귀 정상.
**미검증:** 실제 2인 온라인에서 한쪽이 응답하지 않을 때 10초 뒤 진행되는지.

---

### [2026-08-17 후속 6] 덱 저장 — 덮어쓰기 안 됨 + 용병 3종 이상 통과 ✅ 완료

#### 1. [덱 저장하기]가 매번 새 덱을 만들던 문제

**증상:** 기존 덱을 편집하고 저장하면 덮어쓰지 않고 `my_deck_1`, `_2` … 가 계속 생겼다(실제로 9번까지 늘어남).

**원인:** `SaveDeckToJson`이 **같은 이름의 파일이 있으면 무조건 자동 번호를 붙였다.**
"지금 편집 중인 덱"이라는 개념이 없어서, 방금 불러온 그 덱조차 남으로 취급했다.

**해결:** `_editingDeckName` 필드로 편집 대상을 추적한다.

| 시점 | 처리 |
| ---- | ---- |
| 덱을 불러왔을 때(`LoadDeckFromJson`) | 그 이름으로 설정 → 이후 저장은 **덮어쓰기** |
| 저장 성공 | 방금 저장한 이름으로 갱신 |
| [새 덱](`OnConfirmNewDeck`) | **비운다** — 아직 파일이 없으므로 첫 저장은 새로 만드는 게 맞다 |
| 편집 중이던 덱 삭제 | 비운다 |

이름을 바꿔 저장했는데 그게 **다른** 덱과 겹칠 때만 종전처럼 자동 번호가 붙는다(남의 덱을 덮어쓰지 않기 위해).
로그도 `저장 완료(덮어쓰기)` / `저장 완료(새 덱)`으로 구분된다.

> 씬 시작 시 `RefreshDeckList`가 첫 덱을 자동으로 불러오므로(L153) 편집 대상은 자동으로 잡힌다.

#### 2. 용병 3종 이상이 섞여도 저장되던 문제

`OnClickSaveDeck`은 **20장인지만** 봤다. 룰북은 용병 2종을 골라 그 테마로 덱을 짜는데 아무 제한이 없었다.
카드의 `characterId`로 테마 수를 세어 **2종 초과면 저장을 막고** 어떤 용병이 섞였는지 팝업으로 알린다.

```
용병은 최대 2종까지만 섞을 수 있습니다.
현재 3종: ELLIE, DAINA, SONIA
```

> **카드당 장수는 강제하지 않았다.** 룰북은 "10종류 × 2장"이지만 **현재 1장도 허용하는 방침**이다(2026-08-17 확인).
> `AddCard`의 상한 2장은 그대로 두고(상한일 뿐 강제가 아님), 저장 시 "각 2장" 검사는 넣지 않았다.
> 이는 `DeckValidator`에서 같은 검사가 주석 처리된 것과 같은 맥락이다 (섹션 6 참조).
>
> 용병 **선택 UI**는 아직 없다. 지금은 덱에 담긴 카드로부터 테마를 역산한다.
> 선택 UI가 생기면 `DeckValidator.ValidateFullDeckSet(mainDeck, resourceDeck, char1Id, char2Id)`로 넘길 것.

**검증:** Assembly-CSharp 오류 0. **미검증:** 에디터에서 덮어쓰기·용병 3종 차단 실동작.

---

### [2026-08-17 후속 5] 덱 편집 불가 — 카드 추가 버튼이 죽어 있던 문제 ✅ 완료

**증상:** 덱 빌더에서 **카드 제거는 되는데 추가 버튼이 아무 반응이 없다.** 그래서 20장을 채울 수 없고 저장도 막힌다.

**원인:** `RulebookCards.json`에 **`max_deck_count` 필드가 아예 없었다.**
덱 빌더는 `Resources/GameData/RulebookCards`를 `JsonUtility`로 읽는데, 없는 필드는 **0으로 채워진다.**
그래서 `DeckBuilderManager.AddCard`의

```csharp
if (currentCount < data.max_deck_count)   // 0 < 0 → false
```

가 항상 거짓이 되어 **로그 한 줄 없이 조용히 반환**했다. `RemoveCard`에는 같은 검사가 없어서 제거만 됐다.
(경로 이전 작업과는 무관한 기존 버그다.)

**조치 2가지:**

| 대상 | 내용 |
| ---- | ---- |
| `Data/RulebookCards.json` (정본) | 카드 **40장 전부**에 `"max_deck_count": 2` 추가. 룰북대로 10종류 × 각 2장 = 20장이므로 상한은 2다. `"cost": N,` 뒤에 끼워 넣어 기존 손 정렬을 유지했다(diff 최소화). sync로 미러 2곳에 반영 |
| `DeckBuilderManager.AddCard` | 값이 없거나 0이면 **룰북 기본값 2**로 보고, 상한에 걸리면 **로그를 남기고** 반환한다. 카드 데이터를 못 찾을 때도 경고를 남긴다 — 다시는 조용히 죽지 않게 |

> **왜 둘 다 했나:** JSON 쪽이 정석(데이터 주도)이지만, 필드 하나 빠졌다고 버튼이 말없이 죽는 구조를
> 그대로 두면 같은 사고가 반복된다. 코드에 기본값과 로그를 함께 넣었다.

**엔진 영향 없음:** 엔진(`GameDataManager`)은 Newtonsoft로 읽고 모르는 필드는 무시한다.
콘솔 봇 회귀도 `★ [MATCH SET] ★` 정상 완주했다.

**검증:** JSON 40장 파싱 확인 / `Resources/GameData`에 40건 반영 / Assembly-CSharp 오류 0 /
엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 정상.
**미검증:** 에디터에서 실제 카드 추가·20장 채우기·저장.

---

### [2026-08-17 후속 4] 덱 저장 경로를 persistentDataPath로 이전 ✅ 완료

**문제:** 덱 저장·읽기가 전부 `Application.dataPath/MyDeck`을 쓰고 있었다(**8곳**).
에디터에서는 그게 `Assets/MyDeck`이라 잘 됐지만 **안드로이드에서 `Application.dataPath`는 APK 내부**다.
읽기 전용인 데다 일반 디렉터리도 아니라서 **실기에서는 덱 빌더 자체가 동작하지 않는다.**

**해결:** `Assets/Scripts/Utils/DeckStorage.cs`(신규)를 단일 창구로 두고 8곳을 전부 경유시켰다.

| API | 용도 |
| --- | ---- |
| `EnsureFolder()` | 폴더 보장 + 경로 반환 (최초 1회 구 폴더에서 덱 이전) |
| `GetDeckPath(name)` | 덱 파일 경로. 확장자는 있어도 없어도 된다 |
| `GetDeckFiles()` | 저장된 덱 목록 |
| `DeckExists(name)` | 존재 확인 |

교체한 곳: `DeckBuilderManager` 5곳 / `DeckSelect` 1곳 / `MyHandManager` 1곳 /
**`session_manage` 1곳(서버 파일 — 안 바꾸면 온라인 덱 업로드가 파일을 못 찾는다)**.

**기존 덱은 자동으로 옮겨 온다.** `Assets/MyDeck`에 커밋돼 있는 덱 5개(`MyDeck`, `MyDeck_1`, `MyDeck_2`,
`TestDeck`, `new`)가 경로 변경만으로 사라진 것처럼 보이므로, 새 폴더에 같은 이름이 없을 때만 한 번 복사한다.
안드로이드에서는 구 폴더를 읽을 수 없어 조용히 건너뛴다(예외를 삼키고 로그만 남긴다).

> 새 코드에서 `Application.dataPath`를 직접 부르지 말 것. 현재 이 경로를 아는 곳은 `DeckStorage` 하나뿐이다.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok.
**미검증:** 에디터에서 덱 저장·불러오기·삭제 실동작, 실기 빌드.

---

### [2026-08-17 후속 3] 온라인 단판제 적용 (F-4 3단계) ✅ 완료

섹션 11 F-1 **#1** 해결. **서버 스크립트를 이번에만 한 곳 손댔다**(사용자 승인).

`ServerGameManager.StartMultiplayerGame`이 `new MatchManager(gamesToWin: 2, maxGames: 3)`을
**하드코딩**하고 있어 온라인만 3판 2선승으로 돌았다. `ConsoleRunner`·`BattleManager`와 같은 관용구로 맞췄다:

```csharp
int maxGames = 3, gamesToWin = 2;
if (GameRules.BotSingleGame == 1) { maxGames = 1; gamesToWin = 1; }
```

확인한 것:

- `CommonConfig.json`의 `Bot_single_game = 1` → **단판제로 동작**
- `GameRules.LoadRules`는 `BuildServerDataManager`(L889)에서 호출되고 이는 `StartMultiplayerGame`(L871)보다
  **먼저** 실행된다 → 값을 읽는 시점에 룰이 이미 로드돼 있다
- `MatchManager.IsMatchOver()`는 `_gamesPlayed >= _maxGames`도 보므로 `maxGames: 1`이면 **첫 게임 종료 즉시 매치 종료**.
  `HandleGameOverFlow`가 `OnMatchSet`을 쏘고 끝나며 2게임째로 넘어가지 않는다 (L230~249)

> 단판제는 기획 결정이다. "룰북은 3판 2선승인데?"라며 되돌리지 말 것 (섹션 6 참조).
> 3판으로 되돌리려면 코드가 아니라 `CommonConfig.json`의 `Bot_single_game`을 0으로 바꾼다.

**검증:** Assembly-CSharp 오류 0. **미검증:** 실제 2인 온라인 접속.

---

### [2026-08-17 후속 2] 역할 필터 — "내 플레이어" 판정 일원화 (F-4 2단계) ✅ 완료

섹션 11 F-1 **#4** 해결. 서버 스크립트는 건드리지 않았다.

**문제:** 로컬은 사람이 한 명뿐이라 UI 전 계층이 `UserType.Human`으로 "나"를 판정해 왔다.
그런데 온라인은 **호스트·게스트 둘 다 `UserType.Human`**이다(`session_game_manage` L839~840). 그래서

- 호스트 화면에 **게스트에게 물어야 할 다이얼로그가 뜨고 호스트가 대신 답해 버린다**
- 상대 보드(`EnemyVisualTester`)는 `Type != UserType.Bot`으로 걸러서 **아무것도 그리지 않는다**

**해결:** 판정 창구를 `Assets/Scripts/Utils/LocalPlayerContext.cs`(신규) 하나로 모았다.

| API | 판정 |
| --- | ---- |
| `IsMine(player)` | 온라인이면 `player.Name == GameData.MyRole`("HOST"/"GUEST"), 로컬이면 `Type == Human` |
| `IsOpponent(player)` | `player != null && !IsMine(player)` — 봇 vs 봇 관전에서는 **양쪽 다 true**(종전 `Type == Bot`과 동일) |
| `ResolveMine(p1, p2)` | 내가 조작하는 플레이어. 없으면 null(봇 vs 봇) |

적용 범위:

| 파일 | 변경 |
| ---- | ---- |
| `PlayerUIManager` | 소유 판정 12곳 → `IsMine`. `ResolveHumanPlayer`는 시그니처를 유지한 채 `ResolveMine`에 위임(호출처 4곳이 자동으로 역할 인식) |
| `EnemyVisualTester` | 상대 보드 판정 4곳 → `IsOpponent`. **봇 vs 봇 관전 판정(`IsBotVsBot`, L190)은 진짜 '봇' 이야기라 그대로 뒀다** |
| `HumanChoiceDialogUI` | `IsHuman` → `IsMine` 위임 |
| `CardBoardInvariantChecker` | 5곳 → `IsMine` |

> ★ **로컬 모드의 판정 결과는 종전과 완전히 같다** (사람=나 / 봇=상대 / 봇vs봇=조작 주체 없음).
> 그래서 봇 vs 봇 회귀 기준선에 영향이 없다. 실제로 콘솔 회귀도 그대로 통과했다.

**부수 효과(개선):** 호스트의 **상대 보드가 처음으로 그려진다.** 종전에는 게스트가 봇이 아니라는 이유로
`EnemyVisualTester`가 통째로 무시하고 있었다.

**진단:** 온라인인데 `GameData.MyRole`이 비어 있으면 내 보드·입력이 전부 죽으므로
`[LocalPlayerContext] 온라인 세션인데 GameData.MyRole이 비어 있다` 경고를 한 번 남긴다.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 봇 회귀 `★ [MATCH SET] ★` 정상.
**미검증:** 실제 2인 온라인 접속.

---

### [2026-08-17 후속] 온라인 대전 진입 경로 확보 (F-4 1단계) ✅ 완료

섹션 11 F-4의 **1번(온라인 대전 씬)** 작업. 서버 스크립트(`Assets/Scripts/Server Scripts/**`)는
담당자가 따로 있어 **한 줄도 건드리지 않았다.** 씬 진입·부트스트랩 쪽만 손봤다.

#### ★ 씬을 새로 파지 않았다 — 이유

원안은 "온라인 대전 씬 신설"이었지만 **보드 UI가 통째로 든 `TestGameScene`(약 11,000줄)을 복제하면
보드를 고칠 때마다 두 벌을 맞춰야 한다.** 게다가 `LocalMatchStarter`는 `BattleManager`·`CharacterFieldUI`와
**같은 GameObject**에 붙어 있어 오브젝트를 통째로 끌 수도 없었다(컴포넌트만 도려내는 YAML 수술이 필요).

대신 **같은 씬이 세션 유무에 따라 로컬 봇전 / 온라인 대전으로 갈리게** 했다.
씬을 나중에 분리하더라도 아래 컴포넌트는 그대로 쓸 수 있다.

| 파일 | 변경 |
| ---- | ---- |
| `LocalMatchStarter` (수정) | `GameData.SessionCode`가 있으면 **Awake에서 즉시 물러난다.** 이 가드가 F-1의 핵심 블로커를 없앤다 — 종전에는 매칭 후 이 씬에 오면 `session_game_manage`를 꺼 버려 온라인 세션이 죽고 봇전이 시작됐다 |
| `OnlineMatchStarter` (신규) | 온라인 쪽 짝. 세션이 있을 때만 동작하며 **호스트에 한해** `ServerGameManager` / `EventService`를 붙이고 `EventService.networkService`를 씬의 `firebase_network`로 이어 준다. 준비 상태를 한 줄 로그로 남긴다 |
| `GameStatusPanelUI` (수정) | [메인 메뉴로] 시 `OnlineMatchStarter.ClearSession()` — `GameData`는 static이라 안 지우면 같은 실행에서 로컬 봇전이 계속 막힌다. 온라인에서는 [다시 하기]가 **씬 재로드 대신 메인 메뉴로** 간다(같은 세션 재입장은 덱 재업로드 + 과거 이벤트 재생을 부른다) |
| `BattleManager` (수정) | `HandleGameSet`/`HandleGameDraw`에 **null 컨텍스트 가드**. `context`는 `StartMatch` 때만 생기는데 온라인 씬에서는 이 매니저가 카드 데이터 제공자(`CardData`)로만 올라가므로, `ServerGameManager`가 쏘는 `OnGameSet`에 얹혀 **매치 종료 시 NullReferenceException**이 났다 |

**부트스트랩 타이밍:** `RuntimeInitializeOnLoadMethod`는 게임 시작 때 한 번만 돈다.
매칭 후 씬을 갈아탈 때도 준비해야 하므로 `SceneManager.sceneLoaded`를 계속 구독한다.
이 콜백은 씬 오브젝트의 Awake 뒤 · 첫 `Start()` 앞에 오므로
`session_game_manage.Start()`(호스트면 `HostGameSetupRoutine`)가 도는 시점에는 준비가 끝나 있다.

**`MainMenu.nextSceneName`은 그대로 `TestGameScene`이다.** 그 씬이 이제 온라인에서도 안전해졌으므로 바꿀 필요가 없다.
`session_game_manage`의 `networkService`/`uiManager`도 이미 인스펙터에 배선돼 있음을 확인했다.

#### 이번 작업으로 기대할 수 있는 것 / 없는 것

`ServerGameManager`의 이벤트 발행을 세어 보면 호스트 보드가 어디까지 살아날지 가늠할 수 있다:

| 이벤트 | 발행 | 의미 |
| ---- | ---- | ---- |
| `OnGameStart` / `OnTurnStart` / `OnPlayCard` | 각 1회 | 카드 GO 풀 생성·턴 표시가 뜬다 |
| `OnCardMove` | 4회 | 존 이동 트윈이 돈다 |
| `OnLifeChange` / `OnCardSet` / `OnCharacterFieldSync` | **0회** | 라이프 표기·세트 연출·용병 슬롯은 **호스트에서도 안 뜬다** |
| `CharacterFieldBroadcast` | **미연동** | 용병 필드가 비어 있다 (F-1 #3과 같은 뿌리) |

즉 **1단계는 "온라인 진입이 봇전으로 새지 않게 만든 것"까지**다. 실제 화면이 제대로 도는지는
F-4 2~6번(역할 필터 → 단판제 → 덱·용병 주입 → 타임아웃 → 게스트 화면)을 마쳐야 판가름 난다.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 봇 회귀 `★ [MATCH SET] ★` 정상.
**미검증:** 실제 온라인 접속(서버 담당자와 2인 테스트 필요).

### [2026-08-17] 손패 호버 미리보기 + 버튼 우선순위 + 폐기존 패널 UX + 소니아 능력 이벤트 누락 ✅ 완료

| 항목 | 파일 | 내용 |
| ---- | ---- | ---- |
| 손패 호버 미리보기 | `CardZoomPopupUI.UpdateHandHover` + `CardInteraction.OnPointerEnter` | ⚠️ **미해결 — 아래 ★ 참조.** 커서만 올렸을 때는 아직 안 뜬다(버튼을 누른 채 지나가야 뜸). 클릭·드래그 미리보기는 정상. 손패를 벗어나면 닫되 **손패에서 띄운 경우만**(`_subFromHand`) — 스택·폐기존 열람은 유지 |
| 버튼이 용병 슬롯보다 우선 | `CardZoomPopupUI.IsPointerOverInteractiveUI` | 손패 좌측 카드의 [공개]/[폐기] 버튼이 용병 슬롯과 겹치면 **버튼과 용병 팝업이 같이** 반응했다. Rect 판정은 레이캐스트를 거치지 않아 버튼에 가려진 줄 모르기 때문. 이제 커서 아래 맨 위 히트에 상호작용 가능한 `Selectable`이 있거나 손패 카드가 있으면 Rect 판정을 **양보**한다. 카드 본체(`CardUI`)까지 올라오면 버튼이 아니므로 계속 진행 — 세트존·스택 확대는 그대로 동작 |
| 서브 팝업 3배 확대 | `CardZoomPopupUI.subZoomWidthScale` | 2배 → **3배**. 화면 높이의 92%를 넘으면 비율을 지킨 채 자동 축소 |
| 폐기존 패널 바깥 클릭 닫기 | `CardZoomPopupUI.Update` | [닫기] 버튼 없이도 패널 바깥 클릭 시 닫힘. 닫은 뒤 그 클릭은 아래 존 판정으로 이어진다(다른 폐기존 클릭 시 즉시 전환) |
| 소니아 능력 `OnCardMove` 누락 | `SoniaAbility` | `Graveyard.Remove` 직접 조작 → `ExtractCard` + **`OnCardMove(Graveyard→Hand)` 발행**. 증상: 봇 소니아가 "곡예 비행"을 회수하면 엔진 폐기존에서는 빠지는데 이벤트가 없어 **보드 폐기존 스택에 카드 GO가 유령으로 잔류** → 엔진 리스트(SSOT) 기반 폐기존 패널에는 안 보여 "패널이 카드를 누락한다"로 오인됨. 봇은 폐기존에 소니아 카드가 생기면 능력을 자동 발동하고 `validCards.First()`를 집으므로, 코스트 0이라 일찍 버려지는 곡예 비행이 거의 항상 회수 대상이었다 |

#### ★ 손패 호버 — ⚠️ **미해결. 4차까지 실패했다** (2026-08-17)

**증상:** 커서만 올리면 미리보기가 뜨지 않는다. **마우스 왼쪽 버튼을 누른 채** 다른 카드 위를 지나야만 뜬다.
클릭·드래그 미리보기는 정상이므로 **기능 자체는 동작하고, 커서 위치 감지만 실패**한다.
플레이에 지장은 없어 보류했다. 아래는 시도 이력이다 — **같은 방식을 다시 시도하지 말 것.**

| 시도 | 방식 | 결과 |
| ---- | ---- | ---- |
| 1차 | `CardInteraction`에 `IPointerEnterHandler` | **마우스 버튼을 누른 채** 다른 카드 위를 지날 때만 떴다. 드래그 중에는 잡은 카드의 `blocksRaycasts`가 꺼져 가려져 있던 카드가 **최상위 히트**가 되므로 그때만 Enter가 배달된 것 |
| 2차 | 매 프레임 **`myHandTransform`의 자식**을 Rect 판정 | 무반응 |
| 3차 | **엔진 손패(SSOT) → `CardBoardRegistry.TryGet`으로 GO**를 얻어 Rect 판정 | 여전히 무반응 (씬 트랜스폼 가정은 없앴지만 좌표 판정 자체가 안 맞았다) |
| 4차(현재 코드) | **`EventSystem.RaycastAll` 결과를 전부 훑어** 그중 손패 카드를 찾기 + Rect 판정 보루 | ❌ **여전히 실패.** 진단 로그 3종은 정상 출력되는데 증상 그대로 |

> **왜 4차도 실패하는지가 핵심 수수께끼다.** 이벤트는 최상위 히트 하나에만 배달되지만
> `RaycastAll`은 그 좌표의 모든 히트를 정렬해 돌려주므로 더 관대해야 한다.
> 그런데도 안 된다는 것은 **`Update`에서 읽는 커서 좌표 기준으로는 카드가 안 잡힌다**는 뜻이다.

**다음에 시도할 것 (우선순위 순):**

1. ★ **에디터 Game 뷰 포커스를 의심할 것 (가장 유력, 2026-08-17 추가 조사)**

   조사해 보니 입력 스택은 완전한 레거시다 — `TestGameScene`의 EventSystem은 `StandaloneInputModule`,
   새 Input System 패키지는 **설치되어 있지 않다**(`Packages/manifest.json`에 `com.unity.inputsystem` 없음.
   `ProjectSettings.activeInputHandler: 2`는 "Both"지만 패키지가 없어 사실상 레거시로 동작).

   그런데 레거시 스택에는 두 가지 포커스 의존성이 있다:
   - `StandaloneInputModule.Process()`는 **Game 뷰가 포커스를 잃으면 통째로 건너뛴다**
     (`if (!eventSystem.isFocused && ShouldIgnoreEventsOnNoFocus()) return;`) → 포인터 Enter가 안 온다
   - 에디터에서 **`Input.mousePosition`은 Game 뷰가 포커스를 가질 때만 갱신된다** → 좌표 판정·`RaycastAll`도 같이 죽는다

   **이 하나로 4차까지의 모든 실패가 설명된다.** 감지 방식을 뭘로 바꿔도 좌표가 옛날 값이면 소용없다.
   버튼을 누르면 Game 뷰가 포커스를 얻으므로 "누른 채로만 된다"는 증상과도 정확히 맞는다.

   **확인 방법:** Game 뷰를 한 번 클릭해 포커스를 준 뒤(다른 패널을 건드리지 말고) 커서만 움직여 볼 것.
   그래도 안 되면 `Debug.Log(Input.mousePosition)`을 매 프레임 찍어 값이 실제로 변하는지 본다.
   **실기·스탠드얼론 빌드에서는 이 문제가 없을 수 있으므로 빌드로도 한 번 확인할 것.**

2. 카드 GO의 화면 사각형(`RectTransformUtility.WorldToScreenPoint` 4모서리)과 커서 좌표를 같이 찍어
   실제로 겹치는지 확인. 겹치는데 판정이 false면 캔버스/카메라 인자 문제다

**남겨 둔 것:** 진단 로그 3종(아래)과 감지 2단, 표시 3경로는 그대로 둔다. 다음 사람이 바로 이어서 파도록.

배경: `CardInteraction.Awake`가 카드마다 `Canvas` + `GraphicRaycaster`를 붙여 중첩 캔버스를 만들고,
`CardBoardRegistry`가 존마다 `CanvasGroup.blocksRaycasts` / `GraphicRaycaster.enabled`를 껐다 켠다.
그래서 **클릭은 되는데 호버만 안 먹는** 상태가 나온다 (에디터를 띄울 수 없어 정확한 히트 순서까지는 확인하지 못했다).

현재 설계 (`CardZoomPopupUI.FindHandCardUnder`):

- **감지 2단** — ① `RaycastAll` 전체 훑기 → ② 실패 시 엔진 손패 + `CardBoardRegistry.TryGet` GO의 Rect 판정
- **표시 경로 3개** — 호버 판정 / `OnPointerEnter` / 클릭. `ShowSubByInstanceId`는 같은 카드면 즉시 반환하는 **멱등** 함수라 중복 호출이 안전하다
- **닫기 경로 2개** — 판정이 카드를 잡고 있으면 판정이 전환·닫기를 맡고, 못 잡을 때만 `OnPointerExit`가 닫는다(`CloseSubOnPointerExit`가 `_hoveredHandInstanceId`로 구분)
- 마우스 다운에 손패 미리보기를 닫지 않는다(`if (!_subFromHand) CloseSub()`). 닫으면 방금 누른 카드의 미리보기가 사라진다
- 드래그 게이트는 `bool`이 아니라 **자기 검증하는 참조**다(`CardInteraction.CurrentDragging`).
  단순 static bool은 드래그 도중 플레이를 멈추거나 카드 GO가 풀로 비활성화되면 true로 굳어 호버가 영영 죽는다
  (에디터에서 도메인 리로드를 끄면 **플레이 세션을 넘어서까지** 살아남는다)

**진단 로그 3종** — 호버가 안 먹으면 콘솔에서 이 순서로 본다:

| 로그 | 의미 |
| ---- | ---- |
| `손패 호버 상태: …` | 어느 관문에서 멈췄는지. `판정 동작 중`이 아니면 그 사유가 원인이다 (상태가 바뀔 때만 찍힘) |
| `손패 호버 준비 — 대상 'Player' 손패 5장 / 카드 GO 연결 5장` | 대상이 봇이거나 GO 연결이 0이면 원인이 거기다 |
| `손패 호버 감지 성공 — 레이캐스트 / Rect 판정` | 어느 수단이 잡았는지. **이 줄이 없으면 두 수단 모두 커서 아래에서 카드를 못 찾은 것** |

> ⚠️ 되돌리지 말 것: 감지 수단이나 표시 경로를 "중복이니까"라며 하나로 줄이면 위 증상이 재발한다.
> 레이캐스트 **설정을 다시 켜서**(존별 `SetRaycast`) 해결하려는 것도 금물이다 —
> 끈 데는 이유가 있다(섹션 [2026-08-16 후속 2] 3번). `RaycastAll`로 **읽기만** 하는 것은 그 설정을 건드리지 않는다.

**검증:** Assembly-CSharp 빌드 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok /
콘솔 봇 회귀 `★ [MATCH SET] ★` 정상 + 소니아 회수 로그(`폐기존 '곡예 비행' → 패`) 확인.
**미검증:** 실제 플레이 화면(호버 반응, 버튼 우선순위, 3배 크기, 패널 닫힘, 회수 트윈).

---

### [2026-08-16] 사람 vs 봇 입력 UI 착수 + 인코딩 정리 ✅ 완료

Bot vs Bot을 마무리하고 **사람 vs 봇**으로 넘어가는 첫 작업. 사람 vs 사람(서버)은 의도적으로 손대지 않았다.

#### 사전 검증 — 사람 vs 봇 / 사람 vs 사람 현황 진단

이벤트 발행처·구독처를 전수 매칭하고 스크립트 GUID로 실제 씬 배치를 대조한 결과. 상세는 섹션 11 참조.

- **사람 vs 봇**: 엔진은 거의 완성. 막힌 곳은 UI였다. 인간 결정 6종 중 **정상 동작 1종**뿐
- **사람 vs 사람**: 프로토콜·검증·호스트 로직은 상당하나 **GameScene에 게임 컴포넌트가 하나도 배치되어 있지 않다**

#### 1. QA 자동 응답기를 봇 전용으로 제한

| 파일 | 내용 |
| ---- | ---- |
| `BattleManager.HandleQA_CardPick` / `HandleQA_OptionalAction` | `player.Type != UserType.Bot`이면 즉시 반환하는 가드 추가. 구독 자체는 봇 경로 대비로 유지 |

> 조사 중 확인: `OnRequireCardPick` / `OnRequireOptionalAction`의 **모든 발행처가 이미 봇/사람을 분기**해
> 사람일 때만 이벤트를 쏜다. 즉 이 응답기는 그동안 **사람의 선택만 가로채고 있었다.**

#### 2. 사람 입력 제한 시간 해제 (사람 vs 봇 한정)

| 경로 | 파일 | 내용 |
| ---- | ---- | ---- |
| 코루틴 3곳 | `BattleManager` 세트/오픈/스택 | 타이머 루프 → `WaitUntil(() => done)`. 뒤따르던 타임아웃 폴백 블록도 제거 |
| async 8곳 | 용병 능력 4종(7) + `MoveEffect`(1) | `GameRules.ChooseWaitTime` 직접 참조 → **`GameLogicHelpers.GetChooseTimeoutMs(player)`** |

`GetChooseTimeoutMs`는 Human이면 `-1`(무한), 그 외는 JSON 값을 반환한다. 정책 변경 지점을 한 곳으로 모았다.
**온라인에서는 이 헬퍼를 쓰면 안 된다** — 상대를 무한정 기다리게 된다. XML 주석에 명시해 두었다.

#### 3. 카드 선택 다이얼로그 신규 — `HumanChoiceDialogUI`

`Assets/Scripts/InGameCard/HumanChoiceDialogUI.cs` (신규). 씬 배선 불필요 —
`RuntimeInitializeOnLoadMethod`로 자가 생성하고 UI도 코드로 만든다.

- 화면 하단, 높이 = **카드 세로 × 1.4**(392px), 반투명 회색
- 상단은 가로 스크롤 카드 영역, 하단은 안내 문구 + 확정 버튼
- 카드 클릭 시 **테두리 색**으로 선택 표시. 필요 장수를 채워야 확정 활성화 (`확정 (1/2)` 진행도 표시)
- 우측 상단 **▼** → 패널이 아래로 슬라이드, 화면 우측 하단에 **▲**만 남음 (선택 전 보드 확인용).
  ▲를 누르면 패널 복귀
- 담당 이벤트: `OnRequireCardPick`, `OnRequireCardChoice`, `OnRequireOptionalAction`

> **예/아니오 UI는 스톱갭이다.** 사양이 정해지지 않았지만, 자동 응답기를 봇 전용으로 막은 이상
> `OnRequireOptionalAction`에 답할 주체가 없으면 용병 능력 4종과 `OptionalActionEffect`에서
> **게임이 영구 정지**한다(`OptionalActionEffect`는 타임아웃도 없다). 같은 패널을 카드 없이 재사용해
> `[예]`/`[아니오]` 두 버튼만 띄운다. 디자인 확정 시 교체할 것.

**구현 중 잡은 버그 2건**

| 증상 | 원인 | 대응 |
| ---- | ---- | ---- |
| 창이 뜬 직후 다음 요청이 덮어씀 | 용병 능력이 "발동할까?"→"어떤 카드?"를 묻는데 첫 콜백이 **동기적으로** 다음 요청을 띄운다 | 요청 큐 + `ShowNext` 재진입 가드 |
| 후보 0장일 때 영구 정지 | 확정 버튼이 켜지지 않아 콜백이 나가지 않음 | UI를 띄우지 않고 빈 응답 즉시 반환 |

**UI가 안 보이던 문제 2건 (초기 구현 오류)**

| 증상 | 원인 | 대응 |
| ---- | ---- | ---- |
| 카드가 하나도 렌더링되지 않음 | 스크롤 뷰포트에 `Mask` + 알파 0.001 Image. **`Mask`는 그래픽 알파를 스텐실 모양으로 쓰므로 자식이 통째로 잘린다** | `RectMask2D`로 교체 (사각 영역만으로 자름, 알파 무관) |
| 모든 글자가 ㅁ로 깨짐 | 런타임 생성 TMP는 기본 폰트 `LiberationSans SDF`를 쓰는데 한글 글리프가 없음 | `UiFontResolver`로 씬이 쓰는 폰트(`CookieRun Black SDF`)를 따라감 |
| ▼▲도 ㅁ로 깨짐 | `CookieRun Black SDF`는 **정적 아틀라스**(`m_AtlasPopulationMode: 0`)이고 U+25BC/U+25B2가 없음. 런타임 추가도 안 됨 | 문자 대신 **코드로 삼각형 스프라이트 생성**, ▲는 180° 회전 |

#### 4. 세트 페이즈 — 확정 전 잔상 미리보기

오픈 페이즈는 **세트 시 확정 방식을 유지**하되, 선택 상태를 보드에 표현한다.
**카드 GO는 손패를 떠나지 않는다** — 엔진 존과 hierarchy가 계속 일치하고 `OnCardMove`도 발행되지 않아
온라인 확장 시 확정 전 정보가 새지 않는다.

| 동작 | 표현 |
| ---- | ---- |
| [공개]/[폐기] 클릭 | 손패 카드가 흐려짐(alpha 0.4) + 세트존에 **흐린 뒷면 잔상** |
| 공개 / 폐기 구분 | 잔상 테두리 **파랑 / 빨강** + **우측 상단 라벨**("공개"/"폐기") |
| 다른 카드 선택 | 이전 카드 흐림 해제, 잔상은 색·라벨만 갱신 |
| [레디] | 잔상을 진하게(alpha 0.8) = "확정 대기". **잔상을 여기서 지우지 않는다** |
| 엔진 실제 세트 | `OnCardSet` 수신 시 잔상 제거 |

- **잔상 제거는 [레디] 클릭이 아니라 `OnCardSet` 시점이다.** 엔진의 실제 세트는 양측이 모두 준비된 뒤에
  일어나므로, 클릭 즉시 지우면 그 사이 세트존이 비어 보인다. 온라인에서 상대를 기다릴 때도 그대로 맞다
- 흐림 해제는 **자동**이다. `ApplyZoneMove`가 맨 앞에서 `ResetVisualState`를 호출하고 그것이 `alpha`를 1로 되돌린다
- **잔상에 `CardUI`를 붙이지 않는다.** `CardBoardRegistry.CountCardObjects`가 `CardUI`로 카드를 세므로,
  붙이면 `CanPlaceCardInSetZone()`이 "세트존이 찼다"고 판단해 **카드 교체가 막힌다**.
  이 덕분에 `CanPlaceCardInSetZone`과 `CardInteraction`은 한 줄도 고치지 않았다
- 되돌리기("아무것도 안 낸 상태")는 보류. 매 턴 1장 세트가 강제이고 `GetSetCardChoice`는 **손패가 빈 경우에만**
  `null`을 반환하므로 도달할 수 없는 상태다

#### 5. 버튼 가독성

| 항목 | 내용 |
| ---- | ---- |
| 흐린 카드의 버튼까지 흐려짐 | `CardInteraction.Awake`에서 `actionButtonPanel`·`actionButtonPanelBottom`에 `CanvasGroup.ignoreParentGroups = true` 부여. 프리팹 수정 없이 코드에서 보장 |
| 버튼 높이 +40% | 공개/폐기 4개(`CardSlotInGame.prefab`) `26.6326 → 37.2856`, 레디·상대레디 2개(`TestGameScene.unity`) `42.5811 → 59.6135` |

> `ignoreParentGroups`는 드래그 중 `blocksRaycasts = false`도 무시하지만,
> `OnBeginDrag`가 먼저 `DeselectCard()`로 패널을 끄므로 충돌하지 않는다.

#### 6. `ChooseWaitTime` 단위 버그

`ServerGameManager` L512·L652가 `ChooseWaitTime / 100f`였다. 밀리초(10000) 기준이므로 10초가 아니라
**100초**를 기다렸다. 같은 파일 L948과 `BattleManager`는 모두 `/ 1000f`였다. 세 곳 모두 `/ 1000f`로 통일.

#### 7. 소스 인코딩 UTF-8 통일

저장소 전체에서 CP949 파일 **15개**(Assets 10 + 미러 5)를 UTF-8(BOM 없음)로 변환했다.

- 변환 전 드라이런 검증: 10개 모두 **U+FFFD 0개**, **CP949 재인코딩이 원본 바이트와 일치** → 손실 없는 복원
- 복원된 한글 2,585자. 최다는 `session_game_manage.cs`(1,296자)
- `.editorconfig` 신규 — `charset = utf-8` 선언 (VS/Rider/VS Code가 읽는다)
- 자세한 규칙은 **원칙 2-0** 참조

**검증:** 엔진 빌드 오류 0 / Unity 어셈블리 clean rebuild 오류 0 / `sync -Check` ok /
콘솔 봇 회귀 `★ [MATCH SET] ★` 정상 / UTF-8 위반 0개

**미검증:** 실제 플레이 화면. 에디터를 띄울 수 없어 배치·크기감·조작감은 확인하지 못했다.

---

### [2026-08-16 후속 3] 종료 후 이탈 경로 + 마무리 UI ✅ 완료

| 항목 | 내용 |
| ---- | ---- |
| **결과 오버레이에 [다시 하기] / [메인 메뉴로]** | `GameStatusPanelUI`. 이게 없어서 **한 판 끝나면 화면이 멈춰** 에디터를 껐다 켜야 했다 |
| 용병 능력 질문에 용병 이미지 표시 | `HumanChoiceDialogUI`. 예/아니오 요청일 때 물어본 용병의 카드 이미지를 띄운다 |
| 손패 카드 클릭 확대 | 이미 선택된(액션 버튼이 뜬) 카드를 **다시 클릭**하면 확대 팝업 |
| 죽은 [Surrender] 버튼 연결 | 씬에 남아 있던 코드 참조 0건 버튼 → 덱/항복 패널 열기로 연결 |

**[다시 하기]는 씬 재로드 방식이다.** `BattleManager.StartMatch`를 다시 부르면 카드 GO 풀·레지스트리·엔진 상태를
일일이 되돌려야 해서 위험하다. `SceneManager.LoadScene(현재 씬)`이 가장 확실하다.
`MainMenu` / `TestGameScene` 모두 Build Settings에 포함되어 있음을 확인했다.

**용병 판별 방법:** `OnRequireOptionalAction`에는 "어느 용병이 묻는지"가 오지 않는다(메시지 문자열뿐).
이벤트 시그니처는 UI팀과의 계약이라 바꾸지 않고(원칙 7), **요청자의 용병 2종 이름을 데이터에서 가져와
메시지와 대조**한다. 용병 능력 메시지는 모두 `"엘리 능력을…"`처럼 이름으로 시작한다.
카드 효과(`OptionalActionEffect`)의 메시지는 어느 이름과도 맞지 않아 자연히 이미지가 숨겨진다.

> ⚠️ **손패 확대는 "두 번째 클릭"이다.** 첫 클릭은 기존 선택 동작(확대 + 공개/폐기 버튼)에 양보한다.
> 다른 존은 한 번 클릭이라 조작이 완전히 통일되지는 않았다. 손패 클릭에 선택 의미가 이미 있어서다.
> 두 번째 클릭은 확대와 동시에 선택 해제도 일어난다. 불편하면 더블클릭이나 액션 패널의 확대 버튼으로 바꿀 것.

> `Surrender` 버튼은 즉시 항복이 아니라 **패널을 연다.** 항복 확인 절차(1차 `정말 항복?` → 2차 실행)를
> 한 곳으로 모으기 위해서다. `DontDestroyOnLoad` 컴포넌트라 bool 플래그로 연결을 막으면 씬 재로드 후
> 다시 연결되지 않으므로, **버튼 참조가 바뀔 때마다** 재연결한다.

---

### [2026-08-16 후속 2] 특수문자 폰트 폴백 + 공개 카드 확대 / 폐기존 열람 ✅ 완료

#### 1. 특수문자가 전부 ㅁ로 깨지던 문제

`CookieRun Black SDF`의 글리프를 세어 보니 **11,318자가 전부 한글(11,172) + ASCII**다.
**기호가 하나도 없다.** 실제 사용 현황(엔진 소스 + 카드 JSON 전수 조사):

| 문자 | 횟수 | 문자 | 횟수 |
| ---- | ---- | ---- | ---- |
| `─` | 2,816 | `·` | 8 |
| `→` | 87 | `✨` | 6 |
| `★` | 78 | `×` | 4 |
| `—` | 54 | `♬` | 2 |
| `▶` | 10 | `⚔` `♥` `※` `°` `←` | 각 1~3 |

이모지(🤖 🛡 💚 🚫 🎲)도 30회 이상. `♬`는 **소니아 카드 이름("내 선물이야♬")** 에 들어 있다.

**해결:** `UiFontResolver.EnsureSymbolFallback()` — OS 폰트(맑은 고딕 → Noto Sans CJK → …)로
**동적 아틀라스** TMP 폰트를 런타임 생성해 폴백 체인에 건다.

- TMP 폴백은 **텍스트 컴포넌트가 아니라 폰트 에셋 단위**다. 그래서 씬에서 쓰이는 폰트 에셋 전부에 건다.
  덕분에 우리가 만든 UI뿐 아니라 **카드 프리팹의 이름 텍스트까지 함께 고쳐진다**
- 폴백 폰트에 `HideFlags.DontSave`를 줘서 에디터에서 프로젝트 에셋으로 저장되지 않게 했다
- `UiFontResolver.Resolve()`가 호출될 때 자동으로 함께 보장된다

> ⚠️ **OS 폰트 의존이다.** 안드로이드 실기에서 후보 목록에 없는 폰트만 있으면 다시 깨질 수 있다.
> 그 경우 후보 목록에 기기 폰트명을 추가하거나 프로젝트에 기호용 폰트 에셋을 넣을 것.
> 컬러 이모지 폰트는 TMP가 렌더하지 못할 수 있다(로그의 이모지만 영향, 기능 문제 없음).

#### 2. 공개 카드 확대 + 폐기존 열람 — `CardZoomPopupUI` (신규)

| 대상 | 동작 |
| ---- | ---- |
| 용병 카드 (양측 4슬롯) | 확대 팝업 |
| 스택 카드 (양측) | 확대 팝업 |
| 세트존 카드 | **앞면일 때만** 확대. 뒷면은 무반응 |
| 전장 카드 (양측) | 확대 팝업 |
| 폐기존 (양측) | **화면 오른쪽** 세로 패널 열기 (카드가 없어도 영역만 누르면 열림) |
| 손패 | 무시 — 기존 드래그 확대(`InGameUIManager`) 유지 |

- 폐기존 패널: 버려진 순서대로 세로 스크롤. 항목 클릭 시 확대.
  **자원 카드는 목록에 보이되 클릭 대상에서 제외**(확대할 내용이 없음)
- 왼쪽은 `GameStatusPanelUI`의 진행 로그 자리라 **패널은 오른쪽에 둔다**
- 확대 이미지는 `preserveAspect = true`로 원본 비율 유지. 기존 `InGameUIManager.ShowCardZoom`에도
  같은 한 줄을 넣어 늘어나던 문제를 고쳤다
- 엔진 변경은 `BattleManager.CardData`(읽기 전용 `GameDataManager` 접근자) 하나뿐 — 용병 카드 이름·설명 조회용

#### 3. ★ 클릭 감지는 레이캐스트가 아니라 Rect 포함 판정으로 한다

처음에 `EventSystem.RaycastAll`로 구현했더니 **내 세트존과 스택존만 동작**했다. 원인:

`CardBoardRegistry.ApplyZoneMove`가 상호작용이 필요 없는 존의 레이캐스트를 **의도적으로 끈다.**

| 존 | `SetRaycast` |
| -- | ------------ |
| Hand / SetZone | `!UseCardBackInHand` |
| **StackZone / Graveyard / BattlefieldZone / Deck** | **false** |

- 내 세트존은 `UseCardBackInHand = false`라 켜져 있어서 동작했다
- 내 스택존은 `PlayerUIManager.HandleCardMove`가 `ApplyZoneMove`를 **거치지 않는 별도 경로**를 타서
  레이캐스트가 살아 있었다 — 설계가 아니라 우연이었다
- 상대 보드는 `EnemyVisualTester`가 `UseCardBackInHand = true`라 세트존까지 꺼져 있었다

**레이캐스트를 되살리면 드래그·클릭 동작이 엉킨다(끈 데는 이유가 있다).**
그래서 감지 쪽을 `RectTransformUtility.RectangleContainsScreenPoint`로 바꿨다.
`raycastTarget` / `blocksRaycasts` 설정과 무관하게 동작하며, **기존 파일은 한 줄도 고치지 않았다.**

검사 순서: 내 폐기존 패널 위 → 폐기존 영역(양측) → 스택·세트·전장(양측) → 용병 슬롯 4개.

존 Transform은 양쪽 다 public이라 그대로 쓴다(`PlayerUIManager.my*` / `EnemyVisualTester.enemy*`).
**인스펙터에 미할당이면 `null`이라 판정을 건너뛴다** — 어떤 존이 반응하지 않으면 여기부터 확인할 것.

용병 슬롯 매핑(`character1/2`, `EnemyCharacter1/2`)은 `CharacterFieldUI.ResolveSlot`과 동일하게
`owner == _p1` 기준으로 맞췄다. 씬에서 오브젝트 이름을 바꾸면 양쪽 다 고쳐야 한다.

**검증:** Unity 어셈블리 clean rebuild 오류 0 / 엔진 빌드 오류 0 / 콘솔 봇 회귀 정상.

---

### [2026-08-16 후속] 상태 표시 패널 + 덱 정보/항복 패널 ✅ 완료

사람 vs 봇이 "동작은 하지만 정보가 없는" 상태였다. 엔진은 이미 필요한 이벤트를 전부 쏘고 있었고
**표시할 UI만 없었다.** 두 패널을 추가해 소비자를 붙였다. 둘 다 씬 배선 불필요(자가 생성).

#### `GameStatusPanelUI` (신규) — 턴·페이즈 / 로그 / 결과

| 영역 | 구독 이벤트 | 표시 |
| ---- | ---------- | ---- |
| 상단 중앙 | `OnTurnStart` + 6페이즈 이벤트 | `ROUND 3 · 메인 페이즈` |
| 좌측 | `OnLogMessage` | 최근 10줄. 엔진의 `<color>` 리치 텍스트 그대로 |
| 전체 오버레이 | `OnGameSet` / `OnGameDraw` / `OnMatchSet` / `OnMatchDraw` | 승리·패배·무승부 (사람 기준으로 판정) |

- 6페이즈 이벤트는 그동안 **Unity에서 아무도 구독하지 않았다**(`OnEndPhase`만 정리용으로 사용)
- `OnMatchSet` / `OnMatchDraw`도 ConsoleRunner만 구독 중이었다
- 로그 패널은 톱니바퀴 버튼 자리를 피해 `logPanelTopOffset`만큼 내려서 시작한다

#### `DeckInfoPanelUI` (신규) — 톱니바퀴 → 덱 리스트 + 항복

- 화면 **좌측 상단 톱니바퀴** 버튼 → 패널 토글
- 패널 안에 내 덱 **20장을 5 × 4 격자**로 표시. **덱에 남아 있지 않은 카드는 흐리게**(alpha 0.28).
  헤더에 `내 덱 (12 / 20장 남음)`
- 격자 아래 **패널 우측에 [항복] 버튼**. 오조작 방지로 1차 클릭은 `정말 항복?` 확인, 2차 클릭에 실행

**덱 구성 스냅샷 방법:** `OnGameStart`는 엔진의 시작 드로우보다 **먼저** 발행된다
(`InitializeSingleGame`: `OnGameStart` → `CharacterFieldBroadcast` → `DrawCards`).
그래서 그 시점의 `player.Deck`이 곧 온전한 20장이다. **엔진에 덱 조회 API를 뚫지 않아도 된다.**

**카드 축소:** `CardUI` 자식들이 고정 크기라 `sizeDelta`로는 줄어들지 않는다. `localScale`로 축소한다.

#### 항복 지원 (엔진)

| 파일 | 변경 |
| ---- | ---- |
| `BattleManager.SurrenderBy(Player quitter)` | 신규 public API. 상대를 승자로 `OnGameSet` 발행 |
| `BattleManager.HumanPlayer` | 신규 public 프로퍼티. 사람 플레이어 조회 (없으면 null) |
| 인간 입력 대기 3곳 | `WaitUntil(() => done)` → **`WaitUntil(() => done \|\| context.IsGameOver)`** |

> **대기 지점에 `IsGameOver`를 함께 감시하는 것이 핵심이다.** 사람 입력은 제한 시간이 없으므로,
> 세트 페이즈 입력 대기 중에 항복하면 그 코루틴이 영원히 풀리지 않아 **교착 상태**가 된다.
> 이제 게임이 끝나면 즉시 빠져나오고 기본값(세트=null / 오픈=폐기 / 스택=미발동)으로 진행한다.

#### 아이콘 처리

톱니바퀴 ⚙(U+2699)도 ▼▲와 마찬가지로 프로젝트 한글 폰트(정적 아틀라스)에 글리프가 없다.
**코드로 톱니바퀴 스프라이트를 생성**한다 (`GetGearSprite`, 극좌표 코사인으로 톱니 8개).

#### 정렬 순서 (sortingOrder)

겹침 사고를 막기 위해 고정해 두었다.

| 값 | 대상 |
| -- | ---- |
| 500 | `HumanChoiceDialogUI` 선택 패널 |
| 501 | 선택 패널 펼치기(▲) 버튼 |
| 600 | 톱니바퀴 버튼 |
| 700 | 덱 정보 패널 |
| 800 | 폐기존 목록 패널 |
| 850 | 카드 확대 팝업 |
| 900 | 결과 오버레이 (항상 최상단) |

**검증:** Unity 어셈블리 clean rebuild 오류 0 / 엔진 빌드 오류 0 / 콘솔 봇 회귀 정상.
**미검증:** 실제 플레이 화면.

---

### [현행 코드 재점검] 문서에 없던 완료 항목 (2026-08-16 확인)

이 문서의 이전 판에 누락되어 있던 구현들. 저장소 루트 `README.md`의 진행 기록(26.03.31 / 26.04.04 항목)이
1차 출처이며, 아래는 실제 코드에서 존재를 확인한 결과다. 공식 Phase 번호는 없다.

| 항목 | 파일 | 내용 | 상태 |
| ---- | ---- | ---- | ---- |
| 매치 시작 의존성 주입 | `Core/PlayerSetupData.cs`, `BattleManager.StartMatch`, `LocalMatchStarter.cs`, `ConsoleRunner.Run` | BattleManager 내부의 하드코딩 덱·용병 상수(`P1_CHAR1` 등) 제거. 로비/매치메이킹이 조립한 DTO를 주입받아 매치 시작. 테스트 덱은 호출자(LocalMatchStarter / ConsoleRunner)로 이동 | ✅ 완료 |
| JSON 로더 추상화 | `Interfaces/IJsonLoader.cs`, `Managers/UnityResourceLoader.cs`, `ConsoleFileLoader.cs` | Unity(`Resources.Load`)와 콘솔(`System.IO`)의 경로 충돌 방지. `GameDataManager`·`GameRules.LoadRules`가 인터페이스를 주입받음 | ✅ 완료 |
| 타이브레이커 분리 | `Utils/TiebreakerResolver.cs` | DeckValidator에서 분리 + 룰북 6단계 순서 버그 수정(폐기존 적은 쪽 → 자원존 → 메인덱 → 자원덱 → 라이프 → 코인토스). ConsoleRunner / BattleManager / ServerGameManager 3곳 모두 이쪽을 호출 | ✅ 완료 |
| 용병 필드 슬롯 방송 | `Core/CharacterFieldState.cs`, `Utils/CharacterFieldBroadcast.cs`, `InGameCard/CharacterFieldUI.cs` | 용병 카드 4슬롯 스냅샷(`CharacterSlotSnapshot`)을 `OnCharacterFieldSync` / `OnCharacterSlotUpdated`로 방송. 능력 사용 시 카드 180° 회전으로 표시 | ✅ 완료 |
| 범용 입력 타임아웃 헬퍼 | `Utils/AsyncTimeoutHelper.cs` | 코루틴 비의존 `Task` 기반 `WaitForChoiceWithTimeout<T>`. 용병 능력 4종 + `MoveEffect`가 사용. 타임아웃 시 기본값으로 자동 콜백 | ✅ 완료 |
| 폐기 효과 분리 | `Effects/DiscardEffect.cs` | `IsStackAction` / `RequirePreviousSuccess` 지원 | ✅ 완료 |
| 카드 뒷면 상태화 | `Card.IsFaceUp`, `GameRules.DefaultCardBackPath`, `CommonConfig.json`의 `default_card_back` | 뒷면을 별도 카드가 아니라 카드의 bool 상태로 관리. 기본값 앞면(true) | ✅ 완료 |
| 콘솔 미러 자동 동기화 | `TCG_Project.csproj`의 `SyncFromAssets` 타깃 | Restore/Build 직전 sync 스크립트 자동 실행. 수동 동기화 명령 불필요 | ✅ 완료 |

**검증(2026-08-16):** `dotnet build ./TCG_Project/TCG_Project.csproj` → 오류 0 / 경고 284,
`tools/sync-tcg-project.ps1 -Check` → ok

---

### [재점검] 코드 전수 검토 결과 (2026-03-18)

Phase 18 이후 HANDOFF.md가 업데이트되지 않은 상태에서 아래 기능들이 코드에 추가되어 있었음. 공식 Phase 번호는 없으나 구현은 완료된 상태:

| 항목 | 파일 | 내용 | 상태 |
| ---- | ---- | ---- | ---- |
| 세트 페이즈 동시 처리 | `ConsoleRunner.cs`, `BattleManager.cs` | Gather(결정 수집) → Execute(일괄 적용) 패턴. ConsoleRunner는 봇 결정 2개 수집 후 동시 배치. BattleManager는 `StartCoroutine` × 2 + `WaitUntil(p1Done && p2Done)` | ✅ 구현 완료 |
| 오픈 페이즈 동시 처리 | `ConsoleRunner.cs`, `BattleManager.cs` | Gather→Execute. 2026-08: ApplyOpenChoice p1/p2 동일 프레임 후 ActionDelay 1회 (p1/p2 사이 delay 제거) | ✅ 구현 완료 |
| 드로우 페이즈 병렬 처리 | `BattleManager.cs` | `ExecuteDrawForPlayerParallel` 코루틴 양측 동시 실행 | ✅ 구현 완료 |
| Human 입력 타임아웃 | `BattleManager.cs` | `ChooseWaitTime`(=`GameRules.ChooseWaitTime`) 초과 시 봇 결정으로 자동 대체 | ✅ 구현 완료 |
| QA 자동 응답기 | `BattleManager.cs` | `OnRequireCardPick`, `OnRequireOptionalAction` 구독하여 Human 없이 봇처럼 자동 응답 | ✅ 구현 완료 |
| `GameRules` JSON 확장 | `GameRules.cs`, `CommonConfig.json` | `BotDelayTime`, `BotSingleGame`, `WaitTime` 추가 — QA/배포 시 코드 수정 없이 JSON으로 제어 | ✅ 구현 완료 |
| `DebugHelper` 색상 로그 | `Scripts/Core/DebugHelper.cs` | `LogSpell/LogEffect/LogWarning` — Unity Rich Text 태그 삽입 → `EventManager.OnLogMessage` 경유 | ✅ 구현 완료 |
| `OptionalActionEffect` | `Scripts/Effects/OptionalActionEffect.cs` | 봇=자동 Yes, 인간=`OnRequireOptionalAction` 이벤트 위임. "A를 할 수 있다. 했다면 B" 패턴 | ✅ 클래스 + JSON 팩토리(`GameDataManager` L360, `optional_action`/`OptionalActionEffect`) 연동 완료. **단, 현재 `RulebookCards.json`에서 이 타입을 쓰는 카드는 0장** |
| `DeckValidationResult` | `Scripts/Utils/DeckValidator.cs` 내부 | 별도 파일이 아니라 `DeckValidator`의 **중첩 클래스**로 존재한다. sync 스크립트가 미러의 구 `DeckValidationResult.cs`를 삭제 대상으로 관리 | ✅ 사용 중 |

> **JSON `type` 문자열에 대한 주의**  
> `RulebookCards.json`은 `DrawEffect`, `ArmorEffect`, `ReplayCardEffect` 같은 **구 클래스명을 그대로** 쓴다.
> 이 파일들은 Phase 9~11에서 삭제되었지만, 문자열은 `GameDataManager.CreateKeywordEffect()`가
> 통합 Effect(`MoveEffect`/`BuffEffect`/`CompositeEffect` 등)로 매핑하는 **키워드**로 계속 유효하다.
> JSON의 `type` 값을 클래스 이름으로 착각해 "없는 파일"이라며 지우지 말 것.

---

### [2026-08] 오픈 동시 적용 + 스택 폐기 순서 + Unity 카드 GO 풀 ✅ 완료

로직과 Unity 보드 UI를 함께 고친 작업. Phase 번호는 없음.

#### 로직

| 항목 | 파일 | 내용 |
| ---- | ---- | ---- |
| 오픈 페이즈 타이밍 | `BattleManager.ExecuteOpenPhaseRoutine`, `ServerGameManager.ExecuteOpenPhaseRoutine` | 선택 수집은 기존 병렬 유지. **적용 전 ActionDelay 제거**, **p1/p2 사이 ActionDelay 제거**. `ApplyOpenChoice(p1)`와 `ApplyOpenChoice(p2)`를 같은 프레임에 실행한 뒤 `WaitForSeconds(ActionDelay)` 1회만 |
| 스택 폐기 순서 | `Player.UseAndDiscardStack` | `ExtractCard(StackZone)` → `InsertCard(Graveyard)` → `OnCardMove`. 이벤트 시점에 엔진 존이 이미 폐기존이도록 맞춤 (스택 GO 잔류/유령 방지) |
| 세트 페이즈 | `BattleManager` / `ServerGameManager` | p1/p2 `SetCard`는 같은 프레임 유지. p1/p2 사이 delay 없음 |

#### Unity 보드 UI

| 항목 | 파일 | 내용 |
| ---- | ---- | ---- |
| GO 풀 | `CardBoardRegistry` | 매치 시작 `OnGameStart`(드로우 전): `Deck + ResourceDeck` 전부 Instantiate 1회. 이후 존 이동은 **reparent만**. 매치 종료 `ClearPool`에서만 Destroy |
| Human | `PlayerUIManager` | `InitializeHumanCardPool`. `HandleCardMove`는 `ApplyZoneMove` 통합. Hand Instantiate 경로 삭제 |
| Bot | `EnemyVisualTester` | `TryBuildBotPool`에 `cardFrontPrefab`(CardSlotInGame). `cardBackPrefab`(CardBackground, CardUI 없음)으로 풀을 만들면 카드가 안 보임 — 수정됨 |
| 스택 바 | `StackZoneRowUI` | registry `TryGet` 우선. miss 시에만 adopt / Instantiate fallback + warning |
| Bot vs Bot 관전 | `EnemyVisualTester` + `LocalMatchStarter` | `revealAllHandsInBotVsBot` 기본 true. `StartMatch` 직전 `ConfigureBotVsBotSpectator(mode == BotVsBot && flag)`. Human vs Bot이면 false → Bot 손패 뒷면 유지 |

**범위 밖 (의도적):** 자원존 가시 UI, 자원덱 물리 스택, DOTween. 폐기된 자원카드는 폐기존 스택에 표시.

**검증 (TestGameScene Play, 수동):** Graveyard→Hand 동일 GO 1장, 오픈 p1/p2 동시 후 0.5s, BotVsBot 양측 손패 앞면 / HumanVsBot Human 앞면+Bot 뒷면, 세트존 뒷면, 자원 카드는 hiddenPool 비활성.

---

### [2026-08-14~15] 보드 트윈/스택 앵커 + 라이프 시작 표기 ✅ 완료

로직은 그대로 두고 보드 표기만 맞춘 작업. Phase 번호는 없음.

| 항목 | 파일 | 내용 |
| ---- | ---- | ---- |
| 덱 스택 위치 | `DeckGraveyardStackUI.EnsureDeckCardAnchor` | 덱 Rect가 stretch + pivot `(1,0)`이라 카드가 우측 하단으로 쏠림. 중앙 `(0.5,0.5)` 전용 자식 `DeckCardAnchor`를 만들고 스택 부모로 사용. **부모 피벗에 쌓지 말 것** |
| 자원 FIFO | `Player.PayCost` | `ResourceZone[0]`부터 소비. `OnCardMove(ResourceZone→Graveyard)` 발행 |
| 자원 폐기 출발점 | `ZoneMoveContext.ResourceZone`, `PlayerUIManager.myResourceZoneTransform`, `EnemyVisualTester.enemyResourceZoneTransform` | HiddenPool(`x=8000`)이 아니라 플레이어 자원 아이콘에서 출발. 자원존 GO는 계속 hidden |
| 드로우/이동 트윈 | `CardBoardRegistry.ApplyZoneMove`, `CardMoveTween.WorldToAnchored` | 재부모 전 월드 좌표 캡처. `InverseTransformPoint` 사용 금지. 손패 LayoutGroup은 재부모 전에 잠근다. 덱/폐기 Sync는 이동 중 카드의 dest만 skip |
| 라이프 시작 표기 | `Player.InitializeLifeTokens` / `GainLife`, `PlayerUIManager`, `EnemyVisualTester` | 씬 `Life`/`EnemyLife` 기본 문구 `"0"`. `OnLifeChange`는 원래 `LoseLife`만 발행되어 시작 값이 안 나옴. 초기화·회복도 이벤트 발행 + `OnGameStart`에서 `LifeTokens` 직접 기입. 엔진 라이프는 원래 정상이었음 (표기만) |

**하지 말 것:** `DeckCardAnchor`를 제거하고 덱 부모 피벗으로 되돌리기. 자원존/자원덱을 가시 스택으로 켜기.

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

## 6. 남은 작업

> Phase 18까지 로직팀 범위의 핵심 구현이 완료되었고, 이후 동시 처리·오픈 타이밍·Unity GO 풀이 추가되었다.

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
| 추가   | 오픈 p1/p2 동시 ApplyOpenChoice 후 ActionDelay 1회 (BattleManager + ServerGameManager) | ✅ 완료 |
| 추가   | `UseAndDiscardStack` Extract→Insert→OnCardMove 순서                                | ✅ 완료 |
| 추가   | Unity 카드 GO 풀 + Bot vs Bot 관전 손패 앞면                                       | ✅ 완료 |
| 추가   | 세트존 뷰어 단일화 (InstanceId + OnCardMove), OptionalAction JSON, 이벤트 계약 동기화 | ✅ 완료 |
| 추가   | Unity `imagePath` 매핑 + 스프라이트 폴더 `ELLI` + 콘솔 net9.0 통일                     | ✅ 완료 |
| 추가   | 보드 `anchoredPosition` 트윈 + 메인덱/폐기존 물리 스택 + PayCost/전장 OnCardMove     | ✅ 완료 |
| 추가   | 덱 중앙에 전용 `DeckCardAnchor` 배치 + 자원존 FIFO + 자원 폐기 출발점을 플레이어 자원 아이콘으로 | ✅ 완료 |
| 추가   | 라이프 UI 시작 동기화 (`OnLifeChange` 초기화/회복 + `OnGameStart`에서 `LifeTokens` 기입) | ✅ 완료 |
| 추가   | 매치 시작 DI (`PlayerSetupData`), `IJsonLoader` 로더 추상화, `TiebreakerResolver` 분리 | ✅ 완료 |
| 추가   | 용병 필드 슬롯 방송 + 회전 표시, `AsyncTimeoutHelper` 범용 타임아웃, 카드 뒷면 상태화 | ✅ 완료 |

### 잔여 기술 부채 (로직, 우선순위 순)

| 항목 | 위치 | 내용 | 우선순위 |
| ---- | ---- | ---- | -------- |
| 예/아니오 UI 사양 미정 | `HumanChoiceDialogUI` | 카드 선택 패널을 재사용한 **스톱갭**. 디자인 확정 후 교체 필요 | 중 |
| .NET 9 런타임 부재 | 개발 PC | 콘솔 빌드는 통과하나 실행 불가. `DOTNET_ROLL_FORWARD=LatestMajor`로 우회 중. **RL 확장 전에 `RollForward` 속성 추가 또는 TFM 변경으로 정리 필요** | 중 |
| 구 API 참조 위험 | 덱 빌딩 화면 | `ValidateDeck` / `ValidateResourceDeck` / `IsValidCharacterDeck`가 삭제되었다. 구 API를 호출하는 UI 코드가 남아 있으면 컴파일 실패 | 중 |
| 특수문자 폴백이 OS 폰트 의존 | `UiFontResolver.EnsureSymbolFallback` | 안드로이드 실기에서 후보 목록에 없는 폰트만 있으면 다시 ㅁ로 깨진다. 확실히 하려면 기호용 폰트 에셋을 프로젝트에 포함할 것 | 중 |
| 미커밋 산출물 | 작업 트리 | `CardBoardRegistry.cs`, `DeckGraveyardStackUI.cs`, `CardMoveTween.cs`, `GameSceneBoardBinder.cs`, `StackZoneRowUI.cs`, `QaInjection.cs`, `HumanChoiceDialogUI.cs`, `UiFontResolver.cs`, `GameStatusPanelUI.cs`, `DeckInfoPanelUI.cs`, `CardZoomPopupUI.cs`, `.editorconfig`, `tools/` 등이 미추적/미커밋 상태 | 중 |
| 미러의 유령 BattleManager | `TCG_Project/Scripts/Managers/BattleManager.cs` | 동기화·컴파일 모두 제외된 옛 사본이 남아 혼란을 준다. sync 스크립트 `$deleteFromMirror`에 추가하면 정리됨 | 낮음 |
| 줄바꿈 혼재 | 전체 | `.cs` 기준 CRLF 43 / LF 57. `.editorconfig`에서 일부러 규정하지 않음(강제 시 diff 오염). 정리하려면 `.gitattributes`와 함께 별도 작업 | 낮음 |

### ⏸ 안드로이드 실기 대응 — 보류 중 (2026-08-17 결정)

**지금은 손대지 않는다.** 나중에 몰아서 처리할 것. 착수할 때 아래부터 보면 된다.

| 항목 | 내용 |
| ---- | ---- |
| 패키지명 불일치 | `google-services.json` = `com.Ttakji.server` ↔ `ProjectSettings`에 Android 항목 없음(`Standalone: com.DefaultCompany.2DProject`). 이대로 빌드하면 **Firebase 초기화 실패** |
| 기호 폰트 OS 의존 | `UiFontResolver.EnsureSymbolFallback`가 OS 폰트(맑은 고딕 등)를 찾아 폴백한다. 후보에 없는 기기에서는 `★ ♬ →` 등이 다시 ㅁ로 깨진다. 확실히 하려면 기호용 폰트 에셋을 프로젝트에 포함 |
| 입력 방식 | 현재 UI는 마우스 전제(호버 미리보기, 두 번째 클릭 확대). 터치에서는 호버가 없으므로 손패 열람 동선을 다시 정해야 한다 |
| 해상도·세이프에어리어 | 코드 생성 UI(패널·다이얼로그)가 고정 픽셀값을 쓴다. 노치/다양한 종횡비 검증 필요 |
| ✅ 덱 저장 경로 | **해결됨** — `DeckStorage`가 `persistentDataPath`를 쓴다 (2026-08-17) |

**✅ 2026-08-16 해결됨**

| 항목 | 처리 |
| ---- | ---- |
| `DeckValidator.cs` 인코딩 깨짐 | CP949 → **UTF-8(BOM 없음)** 재저장. 주석·`ErrorMessage` 한글 전부 복원 |
| 죽은 `DeckValidator.ResolveTiebreaker` | 삭제. `TiebreakerResolver`로 안내하는 주석만 남김 |
| 저장소 전체 CP949 파일 15개 | UTF-8 변환 + `.editorconfig`로 규칙 고정. 현재 위반 0개 |
| QA 자동 응답기가 사람 선택을 가로챔 | 봇 전용으로 제한. 사람은 `HumanChoiceDialogUI`가 응답 |
| 사람 입력에 제한 시간이 걸림 | 사람 vs 봇에서 해제(`GetChooseTimeoutMs`). 온라인은 서버 제한 시간 유지 |
| `OnRequireCardPick` 인간 UI 부재 | `HumanChoiceDialogUI` 신규 |
| 세트 선택 상태가 화면에 안 보임 | 손패 흐림 + 세트존 잔상(공개/폐기 색·라벨) |
| 흐린 카드의 [공개]/[폐기] 버튼까지 흐려짐 | `CanvasGroup.ignoreParentGroups` |
| 버튼이 너무 작음 | 공개/폐기/레디/상대레디 높이 +40% |
| `ServerGameManager` 세트·오픈 타임아웃 100초 | `/ 100f` → `/ 1000f` (3곳 모두 `1000f`로 통일) |

**의도적으로 유지 중인 항목 (버그 아님)**

| 항목 | 위치 | 이유 |
| ---- | ---- | ---- |
| **단판제 (`Bot_single_game: 1`)** | `Data/CommonConfig.json` | 룰북은 3판 2선승이지만 **기획상 게임 피로도 관리를 위해 단판제를 유지**한다. `MatchManager`의 3판 로직은 그대로 살아 있으므로 값만 0으로 바꾸면 복원된다. "룰북과 다르다"며 되돌리지 말 것 |
| "10종류 × 2장" 검증 주석 처리 | `DeckValidator.ValidateFullDeckSet` | 룰북상으로는 맞지만 **팀 내 협의가 진행 중**이라 의도적으로 강제하지 않는 상태. 협의 종료 후 주석 해제. 임의로 지우지 말 것 |
| `BattleManager`의 `p1TestIds` 주석 블록 | `InitializeSingleGame()` | 카드 조합 충돌 재현용 QA 참고 자료 |
| 자원존 가시 UI 미구현 | `CardBoardRegistry` hiddenPool | GO는 풀에 있으나 화면에 표시하지 않는다 |
| **`OptionalActionEffect` (사용 카드 0장)** | `Scripts/Effects/OptionalActionEffect.cs` | 앞으로 추가할 **용병 특수 기믹용**으로 미리 만들어 둔 클래스다. 미사용이라며 지우지 말 것 (2026-08-17 확인) |

### Unity 보드 UI 잔여 (TestGameScene 이후)

| 항목 | 내용 | 우선순위 |
| ---- | ---- | -------- |
| 자원 카드 가시 UI | ResourceZone GO는 풀에 있으나 hiddenPool 비활성. 화면에 표시하지 않음 (의도적) | 중 |
| 용병 필드 UI 실동작 확인 | `CharacterFieldUI`의 4슬롯 배치·180° 회전이 씬에서 정상인지 미검증 | 중 |

### UI팀 이관 항목 (로직팀 샘플)

- `Scripts/UI/*.cs` 4개 파일 — UI팀에 샘플 코드로 전달 완료
- 실제 인게임 보드는 `Assets/Scripts/InGameCard/` (PlayerUIManager / EnemyVisualTester / CardBoardRegistry)가 담당
- 덱 빌딩 화면 — `DeckValidator.ValidateFullDeckSet()` API 호출. Phase 16 후 2캐릭터 파라미터 오버로드 사용
- 이벤트 구독 — `EVENTMANAGER_CONTRACT.md`가 `EventManager.cs`와 동기화됨

---

## 7. 알려진 버그 및 기술 부채

| 위치                                    | 내용                                                                                             | 우선순위        |
| --------------------------------------- | ------------------------------------------------------------------------------------------------ | --------------- |
| 손패 호버 미리보기                      | 커서만 올리면 안 뜨고 **버튼을 누른 채 지나가야** 뜬다. 감지 방식 4가지 모두 실패. 섹션 5 [2026-08-17] ★ 참조 — 다음 시도 후보와 진단 로그 사용법이 거기 있다. 클릭·드래그는 정상이라 플레이 지장 없음 | 중 (보류)       |
| 콘솔 실행 환경                          | 개발 PC에 .NET 9 런타임 없음. `DOTNET_ROLL_FORWARD=LatestMajor` 없이는 `dotnet run` 실패           | **높음**        |
| `DeckValidator.ValidateFullDeckSet`     | "10종류" / "카드당 2장" 검증이 주석 처리 — **팀 협의 중이라 의도적**. 버그로 오인하지 말 것        | 참고            |
| `BattleManager.cs`                      | 옛 구현이 `/* */` 주석으로 남아 동명 메서드가 2벌씩 보인다 (`ExecuteDrawPhaseRoutine` 등). 활성 버전 판별 시 주의. 목록은 루트 `BattleManager-ServerGameManager-FunctionList.md` 참조 | 참고 |
| 소스 파일 인코딩                        | `DeckValidator.cs`가 CP949로 저장돼 한글이 깨진 전례가 있다. **새 파일은 UTF-8(BOM 없음)로 저장**할 것 | 참고            |
| `UnityResourceLoader.cs`                | 파일 위치는 `Scripts/Managers/`인데 namespace는 `TCG_Project.Scripts.Systems`                     | 낮음            |
| `Scripts/UI/*.cs`                       | 로직팀 샘플. 실제 인게임 보드는 `Assets/Scripts/InGameCard/`                                    | 참고            |
| 자원존 가시화                           | GO는 풀에 있으나 hidden. 의도적 미구현                                                          | 중              |

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

### 자원 카드 (1종) — `Data/ResourceCards.json`

| ID     | 이름   | 종류 | 설명                                          |
| ------ | ------ | ---- | --------------------------------------------- |
| RES-01 | 자원   | 자원 | 특수 효과 없음. 코스트 지불 전용. 자원덱 15장 |

> **삭제됨:** 구 4페이즈 시절의 `Card.json`(유닛 7종 + 스킬 4종)과 `GameDataManager.LoadAllData()`는
> 현재 코드베이스에 존재하지 않는다. 2026-08-16 확인.

---

## 9. 다음 단계 참고사항

2026-08-15: 카드 이동은 재부모 전 월드 좌표를 시작점으로 저장하고, 도착 레이아웃·스택 프레임을 맞춘 뒤에 `anchoredPosition`으로 변환해 트윈한다. HiddenPool은 Canvas 하위. 메인덱은 덱 Rect **중앙**의 전용 `DeckCardAnchor` 아래에 뒷면 물리 스택으로 표시하고, 폐기존은 앞면 물리 스택으로 표시한다. **덱 부모 피벗에 쌓지 않는다.** 자원덱/자원존 GO는 계속 hidden. `PayCost`는 `ResourceZone[0]` FIFO. 자원 폐기는 플레이어별 자원 아이콘에서 출발한다. 라이프 라벨은 `OnGameStart`와 `OnLifeChange`(초기화·회복·감소)로 `LifeTokens`를 표시한다. 씬 TMP 기본값 `"0"`만 보면 안 된다. ELLI-08 보급 전달은 자원존 맨 뒤에 enqueue. DAIN-09 리벤지는 폐기 후 반격 성공 시에만 자원존으로 이동.

완성도 (2026-08-16 전수 검증 기준. 섹션 11 참조)

| 모드 | 엔진 | UI / 씬 통합 | 비고 |
| ---- | ---- | ----------- | ---- |
| Bot vs Bot | 100% | 100% | 완료. 회귀 테스트 기준선으로 사용 |
| Human vs Bot | ~95% | ~70% | 입력 UI 6종 중 5종 대응. 플레이 검증 미완 |
| Human vs Human | ~80% | ~15% | 프로토콜·검증·호스트 로직은 있으나 **씬 배선이 끊겨 있음** |

> 이전 판의 "Human vs Bot 95%"는 엔진만 본 수치였다. UI를 포함하면 착수 시점 기준 35% 수준이었고,
> 2026-08-16 작업으로 70%대까지 올라왔다.

**다음으로 작업한다면 (2026-08-16 기준 우선순위):**

1. **남은 플레이 검증** — 상태 패널(턴·페이즈, 로그, 결과 오버레이), 톱니바퀴 → 5×4 격자,
   특수문자 표시(`내 선물이야♬`, 로그의 `→ ★ ─`), 확대 팝업 각 존, 폐기존 패널(우측).
   특히 **입력 대기 중 항복**(세트 페이즈에서 톱니바퀴 → 항복)이 교착 없이 끝나는지 확인할 것
2. **예/아니오 UI 사양 확정** — 지금은 카드 선택 패널 재사용 스톱갭 (용병 이미지는 표시됨)
3. **미커밋 파일 정리** — 보드 UI 신규 스크립트와 `tools/`가 미추적이다. 커밋하지 않으면 다음 사람이
   "문서에는 완료인데 파일이 없다"를 겪는다
4. **죽은 계약 2개 정리** — ⏸ **회의 중.** `OnRequireStackResponse` / `OnRequireCardChoice`를
   되살릴지 폐기할지 결정된 뒤 `EVENTMANAGER_CONTRACT.md`를 코드와 맞춘다 (섹션 11-A)
5. **덱 빌딩 → 대전 연결** — ⏸ **보류.** `DataManager.selectedDeckList`를 덱빌더가 쓰지만 **읽는 곳이 없다**.
   `LocalMatchStarter`가 이를 읽어 `PlayerSetupData`를 만들면 "내 덱으로 봇과 대전"이 된다.
   용병 2종 선택 UI도 필요. QA 편의(하드코딩 덱)를 해치므로 **사람 vs 봇 검증이 끝난 뒤에** 착수할 것
6. **사람 vs 사람 씬 통합** — 섹션 11 참조. `ServerGameManager` + `EventService` + 보드 UI를 한 씬에 배치하고
   MainMenu의 `nextSceneName`을 GameScene으로. `LocalMatchStarter`는 그 씬에 두지 말 것
7. **콘솔 실행 환경 정리 (RL 확장 선행 조건)** — `.csproj`에 `<RollForward>LatestMajor</RollForward>` 추가 또는
   TFM을 설치된 런타임에 맞춤. 지금은 환경변수 우회 없이는 `dotnet run`이 실패한다
8. **자원존 가시 UI** — hiddenPool의 Resource GO를 ResourceZone에 표시 (자원덱은 계속 숨김). 지금은 의도적 hidden

**로직팀의 일반 작업 가이드:**

- **모든 편집은 `Assets/TCG_Project/`(정본)에서 한다.** 루트 `TCG_Project/`는 빌드 시 덮어써진다
- 새 카드 데이터 추가 → `Assets/TCG_Project/Data/RulebookCards.json` 또는 `Character.json` 편집 (`Resources/GameData`는 sync가 갱신)
- 새 효과 키워드 추가 → `Scripts/Effects/` 신규 파일 + `GameDataManager.CreateKeywordEffect()` case 추가
- 새 게임 규칙 변경 → `Scripts/Systems/GameRules.cs` + `Data/CommonConfig.json`
- 새 이벤트 추가 → `EventManager.cs` 선언 + XML 문서화 + `EVENTMANAGER_CONTRACT.md` 갱신
- 새 캐릭터 능력 추가 → `Scripts/Abilities/`에 `CharacterAbilityBase` 상속 클래스 + `CharacterAbilityRegistry.Register()`
- 새 Unity 의존 파일 추가 → `.csproj`의 `Compile Remove` + sync 스크립트 `$excludeRel` **양쪽에 등록**
- 인간 입력 대기가 필요한 로직 → `AsyncTimeoutHelper.WaitForChoiceWithTimeout<T>` 사용. 타임아웃 시 기본값 콜백 필수
- 동기화 검증 → `powershell -File tools/sync-tcg-project.ps1 -Check`

**Unity 보드 UI 가이드:**

- 카드 GO는 `CardBoardRegistry`만 생성/파괴. 존 이동은 `ApplyZoneMove`
- Human `ConfirmSetCard`는 선택만. reparent는 `OnCardMove(Hand→SetZone)`
- 봇 풀은 `cardFrontPrefab`(CardSlotInGame). 뒷면은 `CardUI.SetFaceDown`만
- Bot vs Bot 손패 공개는 `LocalMatchStarter.revealAllHandsInBotVsBot`
- Human 입력 대기 시간은 `Data/CommonConfig.json`의 `choose_wait_time`
- 손패 이동은 LayoutGroup을 재부모 전에 잠그고, 도착 레이아웃 후에만 다시 켠다. 덱/폐기 Sync는 이동 중인 카드의 dest 위치만 덮지 않는다 (`SkipLayoutInstanceId`).
- `GameSceneBoardBinder`는 `MyHand`/`set`/`delete`/`MyDeck`/`EnemyDeck`/`stack1`/`middle`/`resource`/`EnemyResource` 자동 배선. TestGameScene은 `LocalMatchStarter`가 있으면 binder가 early-return하므로 Inspector 참조를 쓴다. 라이프 TMP 이름은 `Life`/`EnemyLife` (binder가 찾는 `TxtMyLife`/`TxtEnemyLife`와 다를 수 있음).
- 메인덱 스택은 `EnsureDeckCardAnchor`가 만든 **중앙 앵커** 아래. 덱 부모 피벗 `(1,0)`에 쌓으면 양쪽 모두 우측 하단으로 밀린다. 이 앵커를 유지할 것.
- 자원 비용 폐기는 HiddenPool(+8000)이 아니라 `myResourceZoneTransform` / `enemyResourceZoneTransform`에서 출발
- 라이프 라벨은 `OnGameStart`에서 `player.LifeTokens`로 기입. Human은 `PlayerUIManager.myLifeText`, Bot은 `EnemyVisualTester` (`BotVsBot` P1은 `myLifeText`). `GainLife`도 `OnLifeChange`를 발행한다.
- 카드 앞면: JSON `imagePath` (`Assets/Resources/card_image/ELLI/...`). 폴더명은 **ELLI** (ELLIE는 오타). `characterId` 값 ELLIE는 그대로
- 카드 뒷면 경로는 `CommonConfig.json`의 `default_card_back` (`GameRules.DefaultCardBackPath`). 뒷면은 별도 카드가 아니라 `Card.IsFaceUp` 상태다
- 용병 필드는 `CharacterFieldUI`가 `OnCharacterFieldSync`(전체) / `OnCharacterSlotUpdated`(단일) 두 이벤트를 구독한다. 회전각은 로직이 `CharacterFieldBroadcast.GetRotationZ`로 계산해 스냅샷에 담아 보내므로 UI가 직접 계산하지 않는다

**참고 문서 (저장소 내)**

| 문서 | 내용 |
| ---- | ---- |
| `Assets/TCG_Project/EVENTMANAGER_CONTRACT.md` | UI팀 이벤트 계약 명세 (EventManager.cs와 동기화) |
| `Assets/TCG_Project/CardManual.md` (루트에도 사본) | 카드 40종 상세 매뉴얼 |
| `Assets/TCG_Project/PROJECT_DIAGNOSIS.md` | 로직팀 상태 진단서 |
| `Assets/TCG_Project/EFFECT_SYSTEM_PROPOSAL.md` | Effect 통합 설계 제안서 (Phase 10 근거) |
| `BattleManager-ServerGameManager-FunctionList.md` (루트) | 두 매니저의 활성/비활성(주석) 메서드 전수 목록 |
| `README.md` (루트) | 날짜별 진행 기록 + UI팀용 이벤트 연동 가이드 |

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
| 70장 GO 풀 + 오픈 동시 적용 + 스택 유령 수정           | `32fc4162-4d06-44ed-a243-9fea4c66e12f` |
| Bot vs Bot 카드 표시 (CardSlotInGame 풀 + 손패 앞면)   | 동일 세션 후속 (2026-07~08)            |
| 세트존 뷰어 단일화 + 계약/JSON/미러 동기화 (2026-08-14) | 현재 세션                              |
| imagePath 단일화 + ELLI 폴더 + net9.0 (2026-08-14)   | `0d6a81e1-2671-4183-a5f8-808440ff5b33` |
| 보드 위치 수정 + 물리 덱/폐기존 스택 (2026-08-14)     | 현재 세션                              |
| 전용 덱 카드 앵커 + 자원 FIFO + 자원 폐기 출발점 (2026-08-14) | `0d6a81e1-2671-4183-a5f8-808440ff5b33` |
| 드로우 트윈 경로 정정 + 라이프 시작 표기 0 버그 수정 (2026-08-15) | `0d6a81e1-2671-4183-a5f8-808440ff5b33` |
| 현행 코드 전수 대조 + HANDOFF 갱신 (2026-08-16) — DI/IJsonLoader/TiebreakerResolver/용병 필드 반영 | Claude Code 세션 |
| DeckValidator UTF-8 복원 + 데드 타이브레이커 제거 + 콘솔 빌드 존치 이유(RL) 명문화 (2026-08-16) | Claude Code 세션 |
| 사람 vs 봇 / 사람 vs 사람 현황 진단 (2026-08-16) | Claude Code 세션 |
| 사람 입력 UI 착수 — QA 응답기 봇 전용화, 타임아웃 해제, 선택 다이얼로그, 세트 잔상, 버튼 크기, 인코딩 UTF-8 통일 (2026-08-16) | Claude Code 세션 |
| 상태 표시 패널(턴·페이즈/로그/결과) + 덱 5×4 격자 + 항복 (2026-08-16) | Claude Code 세션 |
| 특수문자 폰트 폴백 + 공개 카드 확대 / 폐기존 열람, 클릭 감지를 Rect 판정으로 전환 (2026-08-16) | Claude Code 세션 |
| 손패 호버 미리보기(Rect 판정) + 버튼 우선순위 + 서브 팝업 3배 + 폐기존 패널 바깥 클릭 + 소니아 OnCardMove 누락 수정 (2026-08-17) | Claude Code 세션 |

---

## 11. 사람 vs 봇 / 사람 vs 사람 진단 (2026-08-16)

이벤트 발행처·구독처 전수 매칭 + 스크립트 GUID로 실제 씬 배치를 대조한 결과.
**"코드가 있다"와 "씬에서 동작한다"는 다르다**는 점이 이 진단의 핵심이다.

### A. 사람 입력 이벤트 6종의 실제 상태

| 이벤트 | 발행처 | 응답자 | 상태 |
| ------ | ------ | ------ | ---- |
| `OnRequireSetPhaseAction` | `BattleManager` | `PlayerUIManager.HandleRequireSet` | ✅ |
| `OnRequireOpenPhaseAction` | `BattleManager` | `PlayerUIManager.HandleRequireOpen` | ⚠️ 별도 선택 UI 없음 — **세트 시 확정 방식(의도된 설계)**. `pendingIsReveal` 재사용 |
| `OnRequireCardPick` | 스택 발동, 용병 능력 4종, `MoveEffect` | `HumanChoiceDialogUI` | ✅ 2026-08-16 신규 |
| `OnRequireOptionalAction` | 용병 능력 3종, `OptionalActionEffect` | `HumanChoiceDialogUI` | ⚠️ 스톱갭 UI |
| `OnRequireStackResponse` | **발행처 0곳** | — | ❌ 죽은 계약. 스택 경로는 `OnRequireCardPick`으로 대체됨 |
| `OnRequireCardChoice` | **발행처 0곳** | `HumanChoiceDialogUI`(대비) | ❌ 죽은 계약 |

> `EVENTMANAGER_CONTRACT.md`의 B-3(`OnRequireStackResponse`)·B-5(`OnRequireCardChoice`)는
> **현재 코드와 맞지 않는다.** 되살릴지 폐기할지 결정한 뒤 계약 문서를 정리할 것.

### A-2. Firebase 서버 실측 진단 (2026-08-17) — ⭐ 실제 DB에 접속해 확인

**결론: 서버는 살아 있고 프로토콜은 실전에서 검증됐다. 막힌 곳은 서버가 아니라 씬 배선과 클라이언트 UI다.**

#### 실제 접속 확인

| 항목 | 결과 |
| ---- | ---- |
| Firebase SDK | **13.7.0** 설치됨. App / Auth / Database, **x86_64 데스크톱 플러그인 포함 → 에디터 플레이 가능** |
| `Assets/google-services.json` | 있음. project `ttakji-server`, RTDB `https://ttakji-server-default-rtdb.firebaseio.com/` |
| DB 응답 | **HTTP 200, 살아 있음** (REST로 직접 확인) |
| 남아 있는 세션 | **7개** (모두 2026-08-13 기록, `state: PLAYING`인 채 방치) |

#### 실전 대전 기록이 남아 있다 (session 8041, 2026-08-13 14:13)

프로토콜이 **양방향으로 실제 동작했다는 증거**다. 이벤트 **99건**:

| 방향 | 이벤트 |
| ---- | ---- |
| 호스트→클라 알림 | `VisualEventNotification` 66, `RequireSetPhaseNotification` 8, `RequireOpenPhaseNotification` 6, `RequireOptionalNotification` 6, `RequireCardPickNotification` 2, `LifeChangeNotification` 1 |
| 클라→호스트 요청 | `OpenPhaseActionRequest` 6, `SetPhaseActionRequest` 4 |
| 발신자 분포 | ALL 67 / HOST 18 / GUEST 14 |

`board_state`도 정상 동기화됐다 — `CurrentTurn: 4`, `CurrentPhase: SetPhase`, `FieldCards`(스택의 VERO-05),
`HostState`/`GuestState`에 덱·패·폐기·자원의 **DataId와 InstanceId 목록 + 라이프·아머·화력·무적·반격**까지 들어 있다.
즉 **4턴까지 온라인 대전이 실제로 진행됐다.** (`IsGameOver: False` — 끝까지 가지 않고 중단)

#### ⚠️ 그대로 두면 안 되는 문제

| 문제 | 내용 | 심각도 |
| ---- | ---- | ---- |
| **인증이 아예 없다** | `Firebase.Auth.dll`은 있는데 **코드에서 사용처 0건**. 익명 로그인조차 없다. 그래서 DB 규칙이 전면 개방 상태이고, **URL만 알면 누구나 전체 세션을 읽고 쓰고 지울 수 있다**(읽기는 실제로 확인함). 테스트 모드 규칙이라면 **만료일이 지나는 순간 전부 차단**되어 접속이 통째로 끊긴다 | **높음** |
| **Android 패키지명 불일치** | `google-services.json`은 `com.Ttakji.server`인데 `ProjectSettings`에는 Android 항목이 없고 `Standalone: com.DefaultCompany.2DProject`뿐이다. **실기 빌드에서 Firebase 초기화가 실패한다** | ⏸ **보류**(2026-08-17 결정 — 안드로이드 작업은 나중에 몰아서) |
| ~~**덱 저장 경로가 `Application.dataPath`**~~ | ✅ **해결(2026-08-17).** `DeckStorage`로 일원화하고 `persistentDataPath`로 이전 + 기존 덱 자동 이관. 섹션 5 참조 | — |
| 세션 청소 없음 | 끝나거나 끊긴 방이 `PLAYING`인 채 영구히 남는다. 로비 목록은 `WAITING`만 보여 줘 눈에 안 띌 뿐 계속 쌓인다 | 중 |
| `dbRef` null 가드 부재 | `GetPublicSession`만 확인한다. `Initialize()` 실패 후 다른 메서드를 부르면 `NullReferenceException` | 중 |
| `ServerSenderManager.cs` | 어떤 씬·프리팹에도 없다 (dead code, 128줄) | 낮음 |

### B. 사람 vs 사람 — 계층별 완성도

| 계층 | 파일 | 상태 |
| ---- | ---- | ---- |
| DTO / 프로토콜 | `EventDTO.cs` (310줄) | ✅ 알림·요청 타입 완비 |
| 서버 검증 | `ActionValidator.cs` (686줄) | ✅ 20종 이상. `EventService`가 수신 이벤트마다 호출 |
| 호스트 시뮬레이션 | `ServerGameManager.cs` (1183줄) | ✅ 6페이즈 + 타임아웃 |
| 이벤트 브릿지 | `EventService.cs` (985줄) | ✅ 알림 발행 + 수신 처리 |
| 클라이언트 수신 | `session_game_manage.cs` (954줄) | ⚠️ 알림 → 상태 텍스트 + `Debug.Log`. 전송 API 6종은 완비 |
| 실제 게임 UI | — | ❌ 없음. 구동은 `Test_*` 디버그 메서드 |
| 씬 배선 | — | ❌ 끊김 (아래) |

**아키텍처:** 호스트 권위 방식. `EventService.Awake`가 `GameData.MyRole != "HOST"`면 자신을 비활성화한다.

### C. 씬 배선이 끊긴 지점 (사람 vs 사람의 실제 블로커)

> 2026-08-17에 스크립트 GUID로 다시 대조했다. **아래 배선 상태는 그대로다.**

| 컴포넌트 | 배치된 씬 |
| -------- | --------- |
| `ServerGameManager` | `TestServerConnect.unity` **뿐** |
| `EventService` | `GameMatchingManager.prefab`, `server ui`, `TestServerConnect` |
| `PlayerUIManager` / `EnemyVisualTester` / `CharacterFieldUI` / `BattleManager` / `LocalMatchStarter` | `TestGameScene` **뿐** |
| **GameScene이 가진 것** | `GameSceneBoardBinder`, `firebase_network`, `session_game_manage` **뿐** |

**보드를 그리는 코드와 게임을 돌리는 코드가 서로 다른 씬에 있고, 온라인 대전을 구성한 씬은 없다.**

**희망적인 사실:** `ServerGameManager`는 `EventManager` 이벤트를 **85곳에서 발행한다.**
보드 UI는 이 이벤트만 구독하면 그려지므로, **호스트 쪽 보드는 같은 씬에 컴포넌트를 모으는 것만으로 상당 부분 살아난다.**
반면 게스트(`session_game_manage`)는 `EventManager` 발행이 **3곳뿐**(`OnCardMove`/`OnCardDraw`/`OnPlayCard`)이고
보드 컴포넌트 참조가 아예 없다. **게스트 화면이 진짜 빈 곳이다.**

현재 게스트를 움직이는 수단은 인스펙터에서 누르는 **`Test_*` 디버그 메서드 17개**뿐이다
(`Test_RequestSetHandCardByIndex`, `Test_RequestOpenPhaseChoice`, `Test_ChooseFirstPendingCardPick` 등).

또한 MainMenu 씬에 `nextSceneName: TestGameScene`이 저장되어 있는데(2026-08-17 재확인),
`LocalMatchStarter.Awake`가 `session_game_manage`를 **비활성화**한다.
→ 매칭이 성사돼 TestGameScene으로 넘어가면 **온라인 세션이 꺼지고 로컬 봇전이 자동 시작된다.**

**매칭까지의 흐름은 완성되어 있다** (`session_manage.HandleGameStarted`):
게스트 입장 감지 → `READY` → 3·2·1 카운트다운 → `GameData`에 `SessionCode`/`MyID`/`MyRole`/`MyDeck` 적재 → 씬 로드.
즉 **바통은 제대로 넘어오는데 받는 씬이 없다.** 온라인 대전용 씬을 만들고 `nextSceneName`을 그쪽으로 돌리는 것이 첫 걸음이다.

### D. 사람 vs 봇 UI 현황 (2026-08-16 작업 후)

| 항목 | 상태 |
| ---- | ---- |
| 세트 카드 선택 + 공개/폐기 확정 | ✅ 잔상 미리보기 포함 |
| 카드 N장 선택 (스택·용병 능력·효과) | ✅ `HumanChoiceDialogUI` |
| 예/아니오 | ⚠️ 동작하나 사양 미확정(스톱갭) |
| 턴·페이즈 표시 / 진행 로그 / 승패 결과 | ✅ `GameStatusPanelUI` |
| 덱 리스트 확인 / 항복 | ✅ `DeckInfoPanelUI` |
| 공개 카드 확대 (용병·스택·세트앞면·전장) | ✅ `CardZoomPopupUI` |
| 손패 카드 확대 | ✅ 두 번째 클릭 (첫 클릭은 선택) |
| 폐기존 열람 | ✅ 우측 세로 패널 + 스크롤 |
| 특수문자·이모지 표시 | ✅ OS 폰트 폴백 (안드로이드 실기 확인 필요) |
| 게임 종료 후 [다시 하기] / [메인 메뉴로] | ✅ 결과 오버레이 |
| 항복 (톱니바퀴 패널 + 씬의 기존 버튼) | ✅ 2단계 확인 |
| 덱 빌딩 → 대전 연결 | ❌ `DataManager.selectedDeckList`를 읽는 곳이 없다. 용병 선택 UI도 없다 |
| 죽은 계약 2개 정리 | ⏸ 회의 중 (`OnRequireStackResponse` / `OnRequireCardChoice`) |
| 자원존 가시화 | ❌ 의도적 미구현 |

### F. 목표 "서버 경유 실시간 온라인 1:1 단판제"까지 남은 것 (2026-08-17 분석)

전송 계층은 실전 검증됐다(A-2 참조). 남은 것은 **Unity·게임 로직 쪽**이며, 아래 6개가 직접 블로커다.

#### F-1. 반드시 고쳐야 하는 것

| # | 결함 | 근거 (코드 위치) | 영향 |
| - | ---- | ---------------- | ---- |
| 1 | ~~**온라인이 단판제가 아니다**~~ ✅ **해결(2026-08-17)** | `ServerGameManager.StartMultiplayerGame`이 `GameRules.BotSingleGame`을 참조하도록 수정 | — |
| 2 | **덱·용병이 하드코딩** | `session_game_manage.HostGameSetupRoutine` L846~855에 ConsoleRunner 테스트 ID가 그대로 박혀 있다. `UploadDeck`으로 올라간 `decks/{role}`을 **읽는 코드가 0곳**, `GameData.MyDeck`도 업로드에만 쓰인다 | 누가 어떤 덱을 골라도 항상 같은 덱으로 대전한다 |
| 3 | **온라인에서 용병 능력이 전부 미작동** | 위 L839~840이 `new Player { Name, Type }`만 세팅하고 **`CharacterCardId`/`SecondaryCharacterId`를 넣지 않는다.** `CharacterAbilityRegistry.GetPlayerAbilities`는 ID가 비면 **빈 리스트**를 반환. `BoardState.Character1_ID`/`2_ID`도 DTO에 선언만 있고 **대입하는 코드가 없다** | 엘리·베로니카·다이나·소니아 능력 4종이 전부 죽는다. 게스트는 상대 용병도 알 수 없다 |
| 4 | ~~**호스트 UI가 게스트 몫까지 응답한다**~~ ✅ **해결(2026-08-17)** — `LocalPlayerContext`로 판정 일원화 | `PlayerUIManager.HandleRequireSet` L307과 `HumanChoiceDialogUI.IsHuman` L202는 **`UserType.Human`만 확인**한다. 온라인은 호스트·게스트 **양쪽 다 `UserType.Human`**(L839~840) | 호스트 화면에 게스트에게 물어야 할 다이얼로그가 뜨고, 호스트가 대신 답해 버린다. **UI 전 계층에 `GameData.MyRole` 기준 "내 것인가" 판정이 필요하다** |
| 5 | ~~**인간 입력 무한 대기 → 교착**~~ ✅ **해결(2026-08-17)** — `GameLogicHelpers.AllowUnlimitedHumanInput` | 타임아웃이 있는 곳은 세트(L512)·오픈(L652)·스택(L948) **3곳뿐**. 용병 능력(L400·424·464·869)과 카드 효과(L819)는 `WaitUntil(done \|\| IsGameOver)`로 **무한 대기**. 그 안에서 쓰는 `GameLogicHelpers.GetChooseTimeoutMs`는 **Human이면 -1(무제한)**을 돌려준다(주석에 "온라인에서 쓰지 말 것"이라 적혀 있으나 강제 장치 없음) | 상대가 카드 선택 창을 안 닫거나 끊기면 **양쪽 모두 영구 정지**. 실제 08-13 로그에도 `RequireCardPickNotification` 2건이 남아 있다 |
| 6 | **게스트에게 게임 화면이 없다** | 게스트에는 `Player` 객체 자체가 없다(`HostGameSetupRoutine`은 호스트 전용). 보드 UI는 `EventManager` 구독으로 도는데 `session_game_manage`의 발행은 **3곳뿐**(`OnCardMove`/`OnCardDraw`/`OnPlayCard`). 조작 수단은 인스펙터 `Test_*` 17개 | **작업량이 가장 큰 항목.** 게스트는 `board_state` 스냅샷에서 로컬 `Player`를 복원하고 이벤트를 되쏘는 어댑터가 필요하다 |

#### F-2. 온라인이라 새로 필요한 것 (지금 0%)

| 항목 | 현황 |
| ---- | ---- |
| 연결 끊김 / 재접속 | `OnDisconnect`·재접속 처리 **코드 0건**. 상대가 앱을 끄면 방이 `PLAYING`인 채 영원히 남는다(실제로 7개 방치) |
| `events` 누적 | `Push()`만 하고 **지우는 코드가 없다.** 게다가 수신은 `ChildAdded`라 리스너를 다시 붙이면 **과거 이벤트가 전부 재생된다**. 씬 재로드·재접속·2게임째에서 사고 난다 |
| 항복 | `BattleManager.SurrenderBy`는 로컬 전용. 온라인 경로에 대응 DTO·처리 없음 |
| 턴 제한 시간 표시 | 서버는 `ChooseWaitTime`으로 자르는데 **남은 시간을 보여 주는 UI가 없다** |

#### F-3. 이미 갖춰져 재사용 가능한 것 (다시 만들지 말 것)

- `ServerGameManager` 6페이즈 루프 + `EventManager` **85곳 발행** → **호스트 보드는 컴포넌트만 한 씬에 모으면 상당 부분 살아난다**
- `ActionValidator` 20종 이상 검증, `EventDTO` 요청·알림 타입 완비, `EventService` 브리지 10개 이벤트
- 매칭·세션·카운트다운·`GameData` 인계까지 완성 (`session_manage`)
- `TiebreakerResolver`는 온라인에서도 이미 호출된다

#### F-4. 권장 착수 순서

1. ~~**온라인 대전 씬 신설**~~ → ✅ **완료(2026-08-17, 방식 변경).** 씬을 복제하는 대신 `LocalMatchStarter`에
   "온라인이면 비켜라" 가드를 넣고 `OnlineMatchStarter`가 호스트 런타임을 붙이도록 했다. 섹션 5 참조
2. ~~**F-1 #4 역할 필터**~~ → ✅ **완료(2026-08-17).** `LocalPlayerContext`로 판정 일원화. 섹션 5 참조
3. ~~**F-1 #1 단판제**~~ → ✅ **완료(2026-08-17).** `GameRules.BotSingleGame` 참조로 통일. 섹션 5 참조
4. **F-1 #2·#3 덱·용병 주입** — `decks/{role}` 읽기 + 용병 2종을 세션에 싣고 `PlayerSetupData` 경유로 통일(원칙 6-1)
5. ~~**F-1 #5 타임아웃**~~ → ✅ **완료(2026-08-17).** `AllowUnlimitedHumanInput` 스위치로 해결.
   `WaitUntil` 5곳은 손대지 않아도 타임아웃 콜백이 대기를 풀어 준다. 섹션 5 참조
6. **F-1 #6 게스트 화면** — `board_state` → 로컬 `Player` 복원 어댑터. 가장 오래 걸린다
7. F-2 안정성 (끊김·이벤트 청소·항복·타이머 표시)

### E. 기타

- `ServerSenderManager.cs`(128줄)는 어떤 씬·프리팹에도 배치되어 있지 않다 (사실상 dead)
- `TestGameScene`의 `LocalMatchStarter`는 `mode: 0`(BotVsBot), `autoStart: 1`.
  사람 vs 봇을 보려면 인스펙터에서 **Mode를 HumanVsBot으로** 바꿔야 한다
| 현행 코드 전수 대조 + HANDOFF 갱신 (2026-08-16) — DI/IJsonLoader/TiebreakerResolver/용병 필드 반영 | Claude Code 세션 |
| DeckValidator UTF-8 복원 + 데드 타이브레이커 제거 + 콘솔 빌드 존치 이유(RL) 명문화 (2026-08-16) | Claude Code 세션 |

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
- 카드 GO 풀 + 오픈 동기화: `C:\Users\tekyung\.cursor\plans\card_pool_and_open_sync_3836ae50.plan.md`
- Bot vs Bot 카드 표시: `C:\Users\tekyung\.cursor\plans\bot_bvb_card_fix_93a4b19b.plan.md`
