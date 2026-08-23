# TCG_Project 작업 인수인계 문서

최초 작성일: 2026-02-28  
최종 수정일: 2026-08-23 (HumanChoiceDialogUI 프리팹 전환 완료)
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

## 0. 지금 상태 한눈에 (2026-08-18)

**온라인 1대1 단판제 대전이 처음부터 끝까지 진행된다.** 다만 아래 제약이 남아 있다.

| 모드 | 엔진 | 화면·입력 |
| ---- | ---- | --------- |
| 봇 vs 봇 | ✅ | ✅ 회귀 기준선 |
| 사람 vs 봇 | ✅ | ✅ |
| **사람 vs 사람** | ✅ | 🔶 게스트 입력 절반(세트·오픈만) / 용병 능력 미작동 |

**가장 큰 공백은 덱·용병 주입이다** — 덱이 하드코딩이고 캐릭터 ID가 비어 있어 용병 능력 4종이 발동하지 않는다.
서버 담당 영역이다(섹션 11 F-1 #2·#3).

> 프로젝트 소개·역할별 남은 일·공통 규칙은 **저장소 루트 `README.md`**를 먼저 본다.
> 이 문서는 그다음에 읽는 상세 인수인계서다.

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

### [2026-08-23 후속 2] HumanChoiceDialogUI 프리팹 전환 — 2단계(카드 항목) ✅ **전환 완료**

1단계에서 패널을 옮겼고, 이번에 **요청마다 만들어지는 카드 칸**까지 옮겼다.
이로써 이 화면의 코드 생성은 끝났다.

#### 이번엔 순서를 지켰다

지난번엔 생성 코드를 먼저 지워 캡처할 원본이 사라졌다.
이번엔 **프리팹 확보 → 코드 정리** 순으로 갔다.

#### 만든 것

| 항목 | 설명 |
| ---- | ---- |
| `Assets/Art/UI/badge_circle.png` | 순서 배지용 원형 (128×128 RGBA, 흰색+알파) — 색은 코드가 입힌다 |
| `Assets/Resources/Build/ChoiceCardItem.prefab` | 카드 칸 템플릿 |
| `ChoiceCardItemView.cs` | 칸의 참조 모음 |

원형 스프라이트도 PIL 없이 직접 만들었고 **알파 채널을 검증**했다
(불투명 비율 0.787 ≈ π/4, 중앙 255 / 모서리 0).

#### 구조 — `CardHost`가 핵심

```
ChoiceCardItem   Image(테두리) + Button + LayoutElement + ChoiceCardItemView
├── CardHost     카드 그림이 들어갈 빈 자리 (테두리 두께만큼 안쪽)
└── OrderBadge   우상단 순서 배지 [꺼둔 상태]
    └── Number
```

예전에는 코드가 매번 `innerSize = cardSize - 두께×2`를 계산해 카드를 욱여넣었다.
이제 **그 계산이 프리팹의 여백**이다 — 두께를 바꾸려면 씬에서 `CardHost` 여백만 조절하면 된다.

#### 몫 나누기

| | 소유 |
| --- | --- |
| 프리팹 | 칸 크기 · 테두리 두께 · 배지 위치와 크기 · 글꼴 |
| 코드 | 어떤 카드를 넣을지 · **선택 상태 색** · 배지 번호와 표시 여부 |

`cardFrameNormal`/`cardFrameSelected`만 인스펙터에 남겼다 — **상태**에 따라 바뀌는 값이라
프리팹이 가질 수 없다. `cardSize` · `cardFrameThickness`는 프리팹으로 넘겼다.

#### 삭제한 것

`CreateOrderBadge` · `GetCircleSprite` · `_circleSprite` · 인라인 프레임 생성 ·
`OrderBadge` 내부 클래스 · `_frameByKey`/`_badgeByKey` 두 병렬 딕셔너리
(→ `_itemByKey` 하나로 합쳤다).

**`new GameObject` 14곳 → 2곳.** 남은 둘은 로직 싱글턴과,
카드 프리팹조차 없을 때 이름만 띄우는 최소 대체물이다 — 레이아웃 생성이 아니다.

> ⚠️ 작업 중 스프라이트 생성기를 잘라 낼 때 **`InstantiateCardVisual`까지 함께 지웠다.**
> 빌드가 잡아 줘서 복원했다. 코드 구간을 통째로 들어낼 때는 그 사이에 낀 것이 없는지 볼 것.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 3/3 /
프리팹 참조 5개·배지 스프라이트 GUID 대조 완료.
**⚠️ 콘솔 회귀는 이 UI를 타지 않는다 — 실기 확인이 본 검증이다.**

**사람이 확인할 것:**
1. 카드 선택창에 카드가 **원래 크기·간격으로** 늘어서는가
2. 카드를 누르면 **테두리 색**이 바뀌는가
3. 베로니카 순서 선택에서 **1·2 배지**가 뜨고, 1번 해제 시 2번이 1번이 되는가
4. '패 3장 버리기'처럼 순서가 무의미한 요청에는 **배지가 안 뜨는가**
5. 프리팹에서 `CardHost` 여백을 바꾸면 **테두리 두께가 따라 바뀌는가**

---

### [2026-08-23 후속] ★ 반복 실수 정리 — "씬에 놓인 UI는 켜진 채로 저장된다"

**같은 실수를 네 번 했다.** 개별 버그가 아니라 패턴이므로 규칙으로 남긴다.

#### 증상 (매번 똑같다)

1. 화면에 들어가자마자 **UI가 떠 있어 플레이를 방해한다**
2. 그 UI의 버튼을 눌러도 **아무 반응이 없다**
3. 정작 기능을 한 번 쓰고 나면 그때부터는 정상 동작한다

#### 원인 (매번 똑같다)

**준비 코드를 "쓸 때"만 부르고 "시작할 때"는 안 불렀다.**

씬에 놓인 UI는 기획자가 편집하기 좋도록 **켜진 상태로 저장된다.**
그런데 그것을 끄고 버튼에 동작을 걸어 주는 코드가 "요청이 왔을 때"에만 있으면,
그전까지는 **켜져 있고 리스너도 없는** 유령이 화면을 덮는다.
버튼이 안 먹는 것도 같은 이유다 — 리스너는 준비 코드가 건다.

| 언제 | 무엇 | 준비 코드가 있던 자리 |
| --- | --- | --- |
| 후속 5 | 용병 선택 팝업 | `EnsurePickerPanel()` — 팝업 열 때만 |
| 후속 9~ | 선택 다이얼로그 | `EnsureUI()` — 선택 요청이 올 때만 |

#### 규칙

> **씬에 놓일 수 있는 UI의 준비 코드는 `OnEnable`/`Start`와 `SceneManager.sceneLoaded`에서 부른다.**
> "쓸 때 부르면 된다"는 코드 생성 시절의 습관이다 — 그때는 쓰기 전까지 아예 존재하지 않았다.

프리팹으로 옮길 때 반드시 함께 넣어야 할 세 가지:

1. **시작 시 정리** — 씬 로드 직후 한 프레임 뒤에 UI를 찾아 끄고 리스너를 건다
2. **중복 방지** — 이미 화면에 있으면 새로 찍지 않는다
3. **소유권 구분** — 씬에서 빌려 온 것은 파괴하지 않는다

#### 이번에 한 조치

`HumanChoiceDialogUI`가 `SceneManager.sceneLoaded`를 구독하고, `OnEnable`에서도 한 번 돌린다.
씬에 다이얼로그가 없으면 **아무 일도 하지 않는다**(로비처럼 없는 화면도 있다) —
정말 필요한 순간에 `EnsureUI()`가 프리팹을 찍는다.

---

## 🔧 이런 증상을 직접 처리하는 법

"UI가 시작부터 떠 있고 버튼이 안 먹는다" 는 대개 위 패턴이다. 순서대로 확인한다.

**1단계 — 유령인지 확인한다.**
플레이 중 Hierarchy에서 그 UI를 찾는다. **같은 이름이 둘 있으면 중복**이고,
하나뿐인데 안 먹으면 **리스너가 안 걸린 것**이다.

**2단계 — 씬에 저장된 상태를 본다.**
플레이를 멈추고 씬에서 그 오브젝트를 찾는다. **체크박스가 켜져 있으면** 그것이 원인이다.

**3단계 — 급하면 껐다 쓴다.**
씬에서 그 오브젝트의 활성 체크박스를 **끄고 저장**한다.
코드가 필요할 때 켜 주므로 대개 이것만으로 증상이 사라진다.
(근본 해결은 아니다 — 준비 코드가 시작 시 돌게 해야 한다)

**4단계 — 로그로 확인한다.**
정상이라면 화면 진입 시 이런 줄이 뜬다.

```
[HumanChoiceDialogUI] 씬에 배치된 'HumanChoiceDialog'을 사용합니다.
[DeckBuilder] 씬에 배치된 슬롯 바 '...'를 사용한다
[DeckBuilder] 용병 선택 팝업 준비 완료 — 템플릿 Row
```

**이 줄이 안 보이면 준비 코드가 아직 안 돌았다는 뜻**이고, 그게 바로 이 버그다.

**5단계 — 아예 지워도 된다.**
씬의 그 UI를 지워도 코드가 프리팹을 찍어 준다. 배치를 직접 잡고 싶을 때만 씬에 두면 된다.

---

### [2026-08-23] 다이얼로그 프리팹 정착 — 씬 인스턴스 채택 + 잔재 정리 ✅

프리팹 캡처 결과를 점검하고 남은 것을 마무리했다.

#### 점검 결과 — 대부분 정상

`HumanChoiceDialog.prefab` 생성 완료. 계층·`HumanChoiceDialogView` 참조 **10개 전부**
올바른 컴포넌트를 가리키고, ▼▲ 스프라이트도 `Assets/Art/UI/`에 연결돼 있었다.
빌더 `.cs`·meta·csproj도 깨끗이 지워져 있었다.

#### ① 씬에 프리팹 인스턴스가 남아 있었다 → 코드가 받아들이게

프로젝트 창으로 드래그하면 유니티가 **씬 오브젝트를 그 프리팹의 인스턴스로 바꿔 놓는다.**
그래서 `GameScene`·`TestGameScene` 둘 다 인스턴스가 남았고, 프리팹의 패널이 `active=1`이라
**화면 진입부터 계속 떠 있고 버튼도 안 먹는** 상태가 된다(리스너는 런타임 사본에만 걸리므로).
게다가 런타임에 사본이 하나 더 찍혀 **둘이 겹친다.**

**`CharacterSlotBar`·`CharacterPicker`와 똑같은 실수를 세 번째로 반복했다** —
"프리팹이 있으면 찍는다"만 만들고 "이미 화면에 있으면 찍지 않는다"를 또 빠뜨렸다.

→ `EnsureUI()`에 **씬 인스턴스 채택**을 넣었다.

```
① 씬에 놓인 것  →  ② 프리팹을 찍는다
```

- 채택한 것은 `Validate()`를 통과한 경우에만 쓴다
- **`_ownsDialogRoot`로 소유권을 구분**한다. 씬에서 빌려 온 것은 절대 파괴하지 않는다 —
  지우면 기획자의 오브젝트가 사라진다
- 찍었든 빌려 왔든 `PrepareView()`로 **똑같이 준비**한다(자리 기억 · 리스너 · 패널 끄기).
  씬의 것은 편집하기 좋도록 켜진 채 저장되므로, 여기서 꺼 주지 않으면 계속 떠 있는다

> 💡 세 번 반복한 실수다. **"프리팹으로 옮긴다"는 곧 "씬에 놓일 수 있다"는 뜻**이고,
> 그러면 생성 코드에는 반드시 "이미 있는가" 검사가 따라와야 한다. 앞으로 옮길
> `DeckInfoPanelUI` 등에서는 처음부터 같이 넣을 것.

#### ② 삭제된 빌더의 미싱 스크립트 정리

프리팹 루트와 두 씬에 `HumanChoiceDialogBuilder` 컴포넌트 항목이 남아
유니티가 'Missing (Mono Script)' 경고를 띄우는 상태였다.

- MonoBehaviour 블록과 이를 가리키는 `- component:` 줄 제거
- 씬 쪽은 프리팹 인스턴스의 **`m_AddedComponents` 항목**으로 붙어 있었다 —
  그 3줄까지 지우고 빈 목록(`[]`)으로 되돌렸다. 안 지우면 **끊긴 참조가 남는다**
- 편집 전 백업했고, 편집 후 **문서 수·헤더·내부 고아 참조 0** 을 재파싱으로 확인했다

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 3/3 /
프리팹·씬 3개 재파싱 정상(고아 참조 0).
**⚠️ 유니티에서 실제로 열어 확인하는 것이 본 검증이다.**

---

### [2026-08-22 후속 9] 프리팹 캡처용 임시 빌더 ⚠️ 쓰고 나면 삭제

프리팹 전환에서 **순서를 잘못 잡았다** — 계층을 짓던 코드를 먼저 지워서
정작 캡처할 원본이 사라졌다(닭과 달걀).

→ `Assets/Scripts/InGameCard/HumanChoiceDialogBuilder.cs` — **예전과 같은 계층을 한 번만 만들어 주는 도구.**

#### 실행 모드가 아니라 에디터에서 만든다

앞서 두 번은 "플레이 중 하이어라키를 복사"했는데, 이번엔 `[ContextMenu]`로
**편집 모드에서 바로 생성**한다. 플레이 모드 왕복이 없고, 만들어진 것을 그대로 프리팹으로 끌면 된다.

캡처본과 달리 **처음부터 온전한 상태**로 나온다:
- 화살표가 런타임 텍스처가 아니라 **실제 스프라이트 에셋**(`Assets/Art/UI/`) — 프리팹에 저장된다
- `HumanChoiceDialogView`가 붙고 **참조가 이미 연결**돼 있다

#### ⚠️ 에디터에서 폰트 폴백을 부르지 않는다

처음엔 `UiFontResolver.Resolve()`를 쓰려 했는데, 그쪽은 폰트 에셋의
`fallbackFontAssetTable`에 런타임 폰트를 **기록**한다.
편집 모드에서 부르면 **프로젝트 폰트 에셋이 더러워져** 저장되지 않는 객체를 가리키는
깨진 참조가 남을 수 있다. 그래서 빌더는 씬에서 폰트만 찾아 쓰고, 폴백은 실행 중에 건다.

#### 쓰는 순서

1. 대전 씬을 연다 → 아무 오브젝트에 이 컴포넌트를 붙인다
2. 컴포넌트 우클릭 → **"선택 다이얼로그 만들기"**
3. 생긴 `HumanChoiceDialog`를 `Assets/Resources/Build/` 로 끌어 프리팹 저장
4. 씬의 오브젝트·컴포넌트와 **이 스크립트 파일까지 삭제**

> ⚠️ **내 빌드 검증은 `#if UNITY_EDITOR` 블록을 컴파일하지 않는다.**
> `AssetDatabase` · `Selection` · `EditorGUIUtility` 사용부는 유니티에서 처음 확인된다.

---

### [2026-08-22 후속 8] ▲▼ 아이콘 에셋 정리 ✅

프리팹 전환으로 코드가 그리던 삼각형 텍스처를 지웠으므로, 실제 스프라이트가 필요해졌다.
제공받은 아이콘 한 장(▲▼이 위아래로 붙어 있음)을 **두 장으로 나눴다.**

| 파일 | 내용 |
| ---- | ---- |
| `Assets/Art/UI/arrow_up.png` | ▲ (원본 위쪽 절반) 512×256 |
| `Assets/Art/UI/arrow_down.png` | ▼ (원본 아래쪽 절반) 512×256 |
| `Assets/Art/UI/free-icon-arrows-up-and-down.png` | 원본 (보관용) |
| `Assets/Art/UI/copyright.txt` | 출처 표기 — Magnific / Flaticon |

#### 왜 Resources가 아니라 Art인가

프리팹은 스프라이트를 **직접 참조**하므로 `Resources` 아래에 있을 필요가 없다
(빌드에 의존성으로 함께 포함된다). `Resources`는 **경로 문자열로 불러오는 것**만 두는 자리다
— 지금은 프리팹과 카드 이미지가 거기 있다.

#### 자른 방법

PIL이 없어 **PNG를 직접 파싱**했다. 다행히 8비트 팔레트(colortype 3)·비인터레이스라
픽셀당 1바이트여서 언필터링이 단순했다.
PLTE·tRNS를 그대로 물려주고 필터 0으로 재인코딩했으며, **CRC까지 검증**했다.

`.meta`는 원본의 임포트 설정(Sprite · alphaIsTransparency)을 물려받되
`spriteMode`만 Single로 바꿨고, GUID는 새로 발급해 충돌이 없음을 확인했다.

> 원본은 `spriteMode: 2`(Multiple)에 슬라이스가 없는 상태 그대로 뒀다.
> 통째로는 쓸 수 없으니 실수로 끌어다 쓰는 것을 막아 준다.

> 💡 잘라 낸 두 장은 **좌우에 여백이 있다**(삼각형이 가운데). 버튼에 넣었을 때 작아 보이면
> 프리팹에서 Image 크기를 키우거나, 여백을 잘라 달라고 하면 다시 처리한다.

---

### [2026-08-22 후속 7] HumanChoiceDialogUI 프리팹 전환 — 1단계(패널) ✅

**"모든 UI는 프리팹 우선"** 원칙의 첫 적용 대상.
대전 내내 쓰이고 손볼 곳이 가장 많은데 `new GameObject`가 14곳에서 계층을 코드로 짓고 있었다.

#### 원칙

| | 소유 |
| --- | --- |
| **프리팹** | 위치·크기·앵커·계층·폰트·기본 색 |
| **코드** | 텍스트 내용·활성 여부·상태 색·목록 개수·애니메이션 |

**이중 경로를 남기지 않는다.** 앞선 세 라운드의 버그가 전부
"코드 경로에만 있던 초기화가 프리팹 경로엔 없어서" 생겼다. 코드 생성 경로는 **지웠다.**

#### 한 일

- **신규 `HumanChoiceDialogView`** — 계층 참조만 들고 있는다.
  이름으로 자동 보완(`ResolveMissingReferences`)하고, 빠진 것은 **이름을 찍어 알린다**(`Validate`)
- `EnsureUI()`가 `Resources/Build/HumanChoiceDialog`를 찍어 씬 캔버스 아래에 붙인다.
  씬 캔버스의 자식이어야 CanvasScaler를 물려받아 카드 크기가 보드와 맞는다
- **삭제**: `BuildPanel` · `CreateText` · `CreateButton` · `CreateArrowButton` ·
  `GetTriangleSprite` · `panelHeightMultiplier` · `PanelHeight` · `FooterHeight` ·
  `panelColor` · `cardSpacing`
- `new GameObject` **14곳 → 5곳** (남은 건 로직 싱글턴 1 + 카드 항목 4 = 다음 라운드 범위)

#### ★ 슬라이드가 프리팹 자리를 존중하게

예전 `SetCollapsed`는 보이는 위치를 **`y=0`으로 못박고** x까지 0으로 덮어썼다.
프리팹에서 패널을 어디에 두든 튀어 오른다.

→ 찍은 직후 `anchoredPosition`을 **'보이는 위치'로 기억**하고,
   접힌 자리는 거기서 패널 높이만큼 아래로 잡는다. **x는 건드리지 않는다.**

#### 실패 안전망 (임계 경로라 중요)

이 UI는 엔진이 `WaitUntil`로 기다리는 자리다 — **화면이 안 뜨면 게임이 멈춘다.**
`_panel`을 뷰 참조를 따라가는 속성으로 바꿔, 프리팹이 없거나 참조가 비면
기존의 "기본값으로 자동 응답" 경로가 **그대로 발동**한다.
선택적 참조(`askerImage` · `declineButton` · `confirmLabel`)는 없어도 진행되도록 가드를 넣었다.

> ⚠️ **코드로 UI를 다시 짓는 폴백은 두지 않았다.**
> 조용히 다른 모양이 뜨는 것보다 명확히 실패하고 게임은 굴러가는 편이 낫다.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 3/3.
**⚠️ 콘솔 회귀는 이 UI를 전혀 타지 않는다 — 빌드 통과 이상은 증명하지 못한다.**

**아직 프리팹이 없다.** 0단계(캡처)를 마쳐야 화면이 뜬다.
그전까지는 안전망이 작동해 선택 요청이 자동 응답된다(에러 로그와 함께).

---

### [2026-08-22 후속 6] 기호 폰트 폴백을 다중 체인으로 ✅

앞 작업에서 `✓`가 깨져 `—`로 바꿔 뒀는데, **폴백으로 제대로 띄울 수 있느냐**는 물음이 나왔다.
할 수 있다. 다만 기존 폴백에는 구조적 결함이 있었다.

#### 왜 폴백이 있는데도 깨졌나

```csharp
Font.CreateDynamicFontFromOSFont(candidates, 48);   // 이름 배열
```

이 API는 **배열에서 먼저 발견된 폰트 하나**를 돌려준다. 윈도우에서는 `Malgun Gothic`이 먼저 잡히고
거기서 끝난다. 그런데 맑은 고딕에는 한글은 다 있어도 **`✓`(U+2713, Dingbats)는 없다.**
뒤에 적어 둔 `Segoe UI Symbol`은 시도조차 되지 않았다. 폴백이 하나뿐이라 그 폰트에 없으면 그걸로 끝이다.

#### 조치 — 여러 폰트를 체인으로

- 후보를 **한 개씩** 물어보고, 만들어진 것을 **모두** 폴백 표에 넣는다.
  TMP는 글자마다 앞에서부터 훑다가 없으면 다음으로 넘어가므로 한글 폰트와 기호 폰트가 서로를 메운다
- 순서 = 우선순위. 한글이 넓은 폰트 → 기호가 넓은 폰트(`Segoe UI Symbol`, `Segoe UI Emoji`) 순
- **`UiFontResolver.CanRender(char)`** 추가 — 실제로 그릴 수 있는지 묻는다.
  동적 폰트는 처음에 글리프 표가 비어 있어 "있는지"만 물으면 항상 false다.
  그래서 `HasCharacter(c, searchFallbacks: false, tryAddCharacter: true)`로 **넣어 보게** 한다

#### 그리고 표시 자체도 방어적으로

```csharp
private static string CheckMark => UiFontResolver.CanRender('✓') ? "✓" : "—";
```

폴백이 닿으면 `✓`, 못 닿는 환경(폰트가 없는 안드로이드 등)에서는 `—`로 물러난다.
**□가 보이는 것보다는 낫다.** 못 그리면 경고 로그도 남긴다.

> ⚠️ 폴백은 **폰트 에셋 단위**라, 나중에 화면에 등장한 폰트에는 다시 걸어 줘야 한다.
> 그래서 팝업을 만든 직후에도 `EnsureSymbolFallback()`을 한 번 더 부른다.
> (씬·프리팹 방식으로 바꾸면서 "UI가 나중에 등장한다"는 전제가 생겼다)

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 3/3.
**실기 확인 필요** — 시작 로그의 `기호 폴백 N종 준비` 목록에 `Segoe UI Symbol`이 있는지,
그리고 선택 중인 용병 줄에 `✓`가 보이는지.

---

### [2026-08-22 후속 5] 팝업이 시작부터 보이고 닫기가 안 먹던 문제 ✅

프리팹을 씬에 연결한 뒤 나온 3건. **1·2번은 뿌리가 같다.**

#### ①② 팝업이 바로 보이고, 첫 [닫기]가 먹지 않았다

`EnsurePickerPanel()`을 **팝업을 열 때만** 불렀다.
그러면 씬에 켜 둔 채 저장한 팝업이 화면 진입 즉시 보이고,
그 안에 남아 있는 **예시 줄의 [닫기]는 아무 동작도 걸려 있지 않다**(프리팹은 실행 화면을 캡처한 것이라
버튼만 있고 리스너가 없다). 슬롯을 한 번 눌러야 비로소 예시 줄이 정리되고 진짜 목록이 생겨서,
"두 번째 닫기부터 동작하는" 것처럼 보였다.

→ **`Start()`에서 미리 부른다.** 진입 시점에 예시 줄을 치우고 팝업을 꺼 둔다.
   준비는 한 번만 하고(`_pickerReady`) 이후에는 껐다 켜기만 한다.

> 💡 씬에 UI를 두는 방식으로 바꾸면 **"보이는 상태로 저장된다"**는 새 전제가 생긴다.
> 코드 생성 시절에는 없던 문제라 초기화 시점을 놓쳤다.

#### ③ 체크표시 `✓`가 □로 깨졌다

`UiFontResolver.EnsureSymbolFallback()`을 **코드 생성 경로에서만** 부르고 있었다.
씬·프리팹을 쓰면 그 줄을 지나가지 않아 폴백이 걸리지 않는다.

조치 두 가지:
- 폴백 호출을 `SetupCharacterSlots()`로 옮겨 **모든 경로에서 한 번** 걸리게 했다
- 그와 별개로 `✓`(U+2713)를 **`—`(U+2014)로 바꿨다.** 제목 줄에서 이미 잘 나오는 것이 확인된 기호라
  폰트 폴백이 실패해도 안전하다. 폰트에 의존하는 표시는 되도록 피하는 편이 낫다

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 3/3.
**실기 확인 필요** — 진입 시 팝업이 숨어 있는지, 첫 [닫기]가 바로 먹는지, 표시가 깨지지 않는지.

---

### [2026-08-22 후속 4] 씬에 배치한 UI가 코드 생성본과 겹치던 문제 ✅

프리팹을 **씬에 직접 올리자** 코드가 하나를 더 찍어 **둘이 겹쳐 보였다.**
전환을 반만 한 셈이다 — "프리팹이 있으면 찍는다"까지만 했고
"이미 화면에 있으면 찍지 않는다"가 빠져 있었다.

#### 슬롯 바 — 확보 순서에 '씬 검사'를 넣었다

```
① characterSlots(인스펙터)  →  ② 씬에 배치된 것  →  ③ 프리팹  →  ④ 코드 생성
                                  ↑ 이번에 추가
```

씬 검사는 `CharacterSlotView`를 찾아 **공통 부모(바)에서 다시 훑는다.**
`FindObjectsByType`의 순서는 하이어라키 순서가 아니라서, 그대로 쓰면 1번·2번이 뒤바뀔 수 있다.

#### 용병 선택 팝업 — 프리팹 연결 + 예시 줄 정리

받은 `CharacterPicker.prefab`은 **실행 화면을 그대로 캡처한 모양**이라
예시 줄이 6개(엘리·베로니카·다이나·소니아·비우기·닫기) 들어 있었다.
그대로 두면 실제 목록과 함께 보인다.

→ **첫 줄을 템플릿으로 삼아 꺼 두고, 나머지 예시 줄은 제거**한다.
   목록을 만들 때 템플릿을 복제해 쓴다.
   제목은 "줄에 속하지 않은 텍스트"로 자동으로 찾는다.

팝업도 슬롯 바와 같은 순서다: 인스펙터 → 씬(`CharacterPicker` 이름) → 프리팹 → 코드 생성.
한 번 만들면 껐다 켜기만 한다(`_pickerReady`).

> ⚠️ 줄 색은 프리팹이든 코드 생성이든 **상태별로 다시 칠한다.**
> 템플릿 색을 그대로 두면 모든 줄이 같은 색이 되어 "현재 이 슬롯 / 다른 슬롯 사용 중" 구분이 사라진다.
> 색 자체는 인스펙터(`pickerCurrentColor` 등)에서 바꿀 수 있다.

#### TouchTargetNormalizer 점검 결과

두 씬 모두 정상 배치돼 있고 설정값도 온전하다.
**다만 `normalizeEnabled`가 꺼져 있다** — 배치를 손으로 잡는 동안에는 그게 맞다.
배치가 끝나면 켜서 최소 크기·간격 규칙을 다시 적용할지 판단할 것.

| 씬 | normalizeEnabled |
| -- | ---------------- |
| `MainMenu.unity` | 0 (꺼짐) |
| `BuildDeck.unity` | 0 (꺼짐) |

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 3/3.
**겹침이 실제로 사라졌는지는 유니티에서 확인해야 한다.**
실행 시 콘솔에 `씬에 배치된 슬롯 바 '...'를 사용한다`가 뜨면 프리팹을 더 찍지 않은 것이다.

---

### [2026-08-22 후속 3] 용병 슬롯 바를 프리팹으로 ✅

기획자가 편집한 프리팹을 받아 **에셋으로 확정**했다.
이제 슬롯 바는 코드가 만드는 것이 아니라 **유니티에서 편집하고 저장하는 것**이다.

#### 프리팹 위치

`Assets/Scripts/BuildDeck/` → **`Assets/Resources/Build/CharacterSlotBar.prefab`**

스크립트 폴더에 에셋을 두면 찾기 어렵고, 이 프로젝트의 UI 프리팹은
이미 `Resources/Build/`에 모여 있다(`CardSlot`, `CardSlotInGame`).
Resources 아래라 **인스펙터 연결이 없어도 코드가 스스로 찾는다.**

#### 선택 순서

```
① characterSlots(씬에 직접 배치)  →  ② 프리팹  →  ③ 코드 생성
```

프리팹은 `characterSlotBarPrefab` 필드로 지정하거나, 비워 두면
`Resources.Load("Build/CharacterSlotBar")`로 자동으로 찾는다.
프리팹 안의 `CharacterSlotView`를 **하이어라키 순서대로** 1번·2번 슬롯으로 삼는다.

#### 받은 프리팹 점검 결과

| 항목 | 상태 |
| ---- | ---- |
| 구조 | `CharacterSlotBar`(HorizontalLayoutGroup) → 슬롯 2칸 + Label |
| `CharacterSlotView` | 두 슬롯 모두 부착됨. **초상·배경·버튼 연결 정상** |
| `TouchTargetExempt` | 두 슬롯 모두 부착됨 (원본 비율 유지) |
| `label` 필드 | 비어 있음 — 슬롯 안에 이름 텍스트가 없는 구성이다 |

> 처음에는 두 슬롯의 초상 참조가 **뒤바뀐 것처럼 보였다**.
> 실제로는 파일에 기록된 순서 때문이고, 부모-자식 관계를 따라가 보니 **각자 제 자식이 맞았다.**
> 프리팹을 텍스트로 읽을 때는 fileID 위치가 아니라 **hierarchy를 따라가야** 한다.

슬롯에 이름 텍스트가 없어도 동작한다(코드가 null을 걸러 낸다).
다만 **빈 슬롯일 때는 배경색만으로 구분**되므로, 안내 글자가 필요하면 슬롯 안에 텍스트를 넣고
`CharacterSlotView.label`에 연결하면 된다.

#### "편집 중: OO" 텍스트를 어디든 놓을 수 있게

프리팹 안 `Label`이 그 텍스트였다(`편집 중: MyDeck_4`).
바 안에 있으면 레이아웃에 묶여 다른 자리로 옮길 수 없다.

→ `editingDeckNameText`에 **씬 어디든** 텍스트를 만들어 연결하면 그것을 쓰고,
   **프리팹 안의 것은 자동으로 숨긴다**(중복 방지).
   연결하지 않으면 예전처럼 바 안의 것을 쓴다. **프리팹을 고칠 필요가 없다.**

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 3/3.
**프리팹이 실제로 화면에 어떻게 나오는지는 유니티에서 확인해야 한다.**

---

### [2026-08-22 후속 2] UI를 씬에서 직접 배치할 수 있게 전환 ✅

**왜:** 코드로 UI를 만드는 방식은 원래 임시방편이었다 — 씬 파일을 눈으로 볼 수 없어 택한 우회로다.
그 대가로 **기획자가 배치를 직접 못 만졌다.** 이제 씬 우선으로 뒤집는다.

#### 원칙 — "연결하면 씬, 비우면 코드"

인스펙터에 연결하면 그것을 쓰고, 비워 두면 예전처럼 코드가 만든다.
**기존 씬은 아무것도 안 해도 그대로 돈다.**

`DeckBuilderManager`에 추가된 자리:

| 필드 | 뜻 |
| ---- | -- |
| `characterSlots` | 용병 슬롯(1번, 2번 순서). `CharacterSlotView` 컴포넌트를 붙인 오브젝트 |
| `editingDeckNameText` | "편집 중: OO" 텍스트 |
| `characterPickerPanel` | 용병 선택 팝업 루트 (씬에 **비활성**으로 두면 된다) |
| `characterPickerContent` | 목록이 들어갈 부모. 비우면 팝업 루트에 직접 |
| `characterPickerRowPrefab` | 목록 한 줄 프리팹(Button + 자식 Text) |
| `characterPickerTitle` | 제목 텍스트 |

#### 신규: `CharacterSlotView`

슬롯 1칸의 겉모습(버튼·배경·초상·이름)을 묶는 작은 컴포넌트.
붙이기만 하면 같은 오브젝트/자식에서 알아서 찾아 채운다(`ResolveMissingReferences`).

덤으로 **코드 생성 경로도 이 컴포넌트를 쓰도록 통일**했다.
예전에는 버튼·배경·초상·이름을 네 개의 병렬 리스트로 들고 있어 인덱스가 어긋날 위험이 있었다.

> ⚠️ 씬에서 연결한 팝업은 닫을 때 **끄기만** 한다(`SetActive(false)`).
> 예전처럼 `Destroy`하면 두 번째부터 열리지 않는다.

#### `TouchTargetNormalizer`도 인스펙터로

상수를 전부 `[SerializeField]`로 바꾸고, **씬에 직접 올려 둔 것이 있으면 그 설정을 쓴다.**
안 올려 두면 예전처럼 기본값으로 자동 생성된다.

`normalizeEnabled`를 끄면 아무것도 보정하지 않는다 — **배치를 손으로 잡는 동안 꺼 두면 편하다.**
이게 없어서 "에디터에서 옮겨도 실행하면 되돌아간다"는 혼란이 있었다.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 3/3.
**연결 없이도 예전과 똑같이 동작하는 것까지가 이번 검증 범위다** —
씬에 실제로 연결해 보는 것은 유니티에서 해야 한다.

---

### [2026-08-22 후속] UI 배치·표시 수정 ✅

앞 작업의 실기 피드백 3건.

#### ① 용병 슬롯 위치 — 화면 맨 위 → 덱 버튼 왼쪽

처음엔 캔버스 최상단에 붙였는데 덱 리스트를 가렸다.
**[새 덱 만들기]·[덱 저장하기] 버튼의 왼쪽**, 즉 덱 리스트와 카드 리스트 사이 띠로 옮겼다.

씬 배선을 건드리지 않고 자리를 잡기 위해, **onClick에 걸린 메서드 이름으로 기준 버튼을 역추적**한다
(`FindButtonByMethod("OnClickNewDeckButton")`). 못 찾으면 예전처럼 상단으로 물러난다.

> ⚠️ `Start()` 시점에는 레이아웃이 아직 돌지 않아 버튼 크기가 제값이 아니다.
> (게다가 `TouchTargetNormalizer`가 뒤늦게 크기를 바꾸기도 한다)
> 그래서 **한 프레임 뒤에 한 번 더 재어** 자리를 확정한다.

#### ② 슬롯의 글자·이미지가 깨져 보이던 문제

두 가지가 겹쳐 있었다.

- **폰트 미지정** — 런타임에 만든 `TextMeshProUGUI`는 `font`를 직접 넣어야 한다.
  안 넣으면 기본 폰트에 한글 글리프가 없어 깨진다.
  프로젝트에 이미 있던 `UiFontResolver.Resolve()`를 쓰고 `EnsureSymbolFallback()`도 건다
- **전각 기호** `＋`(U+FF0B)는 프로젝트 폰트에 없다 → 평범한 `+`로
- **초상이 아예 없었다** — 배경 Image만 있어 빈 네모로 보였다.
  `Portrait` Image를 따로 두고 `CardImageLoader.LoadSprite(character.imagePath)`로 채운다.
  배경에 직접 스프라이트를 넣으면 비었을 때 흰 네모가 남아 더 깨져 보이므로 **있을 때만 켠다**

#### ③ 메인 화면 3버튼이 하나로 뭉쳐 보이던 문제

`TouchTargetNormalizer`가 **크기를 키우면서** 원래 딱 붙어 있던 버튼들이 겹쳤다.
키우기만 하고 다시 벌리지 않은 것이 원인이다.

→ 보정을 **3단계**로 바꿨다: `크기 → 겹침 풀기 → 화면 안으로`.
   같은 부모 아래 버튼끼리 최소 간격(16)을 두도록 밀어내되,
   **중심이 더 벌어져 있는 축으로만** 민다(가로 배치면 좌우로, 세로 배치면 위아래로).
   셋이 연달아 겹치면 한 번에 안 풀리므로 4회 반복한다.
   레이아웃·스트레치가 관리하는 버튼은 건드리지 않는다.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 3/3.
**여전히 UI라 실기 확인이 본 검증이다** — 특히 ③은 보정 로그(`겹침 해소 N쌍`)를 함께 볼 것.

---

### [2026-08-22] 메뉴·UI 사용성 개선 ✅

로직이 안정된 뒤 처음으로 **유저가 만지는 부분**을 손봤다.

> ⚠️ 씬·프리팹은 텍스트로 고치기 위험하므로 **UI는 전부 코드에서 생성·보정**한다
> (`HumanChoiceDialogUI`가 쓰던 방식).

#### ① 메인에서 고른 덱이 그대로 열린다

`DeckSelector`는 이미 `PlayerPrefs["SelectedDeckName"]`에 선택을 저장하고 있었고,
`RefreshDeckList(focusDeckName)`이라는 인자도 **원래 있었다.**
아무도 넘기지 않아 늘 목록 맨 위 덱이 열렸을 뿐이다. 그 길을 연결했고,
편집 화면에서 덱을 바꾸거나 저장할 때도 같은 키를 갱신해 **양쪽 선택이 항상 일치**한다.

#### ② 용병 슬롯 + 카드 목록 필터 ★

편집 화면 상단에 **용병 슬롯 2칸**을 코드로 만든다. 슬롯을 채우면 그 용병의 카드 10종만 아래에 뜬다.

- `CardDataManager`가 `Character.json`도 읽는다 (기존엔 `RulebookCards.json`만 읽어
  **용병의 이름·그림을 알 방법이 아예 없었다**)
- 목록 필터는 슬롯을 지우지 않고 `SetActive`만 토글해 **스크롤 위치가 유지**된다
- 슬롯을 비우면 그 용병 카드를 덱에서 함께 뺀다 — 안 그러면 목록에서 사라져 **뺄 방법이 없어진다**
- `AddCard`에도 마지막 방어선을 뒀다

**용병을 덱 파일에 명시적으로 저장한다.** 역산은 "용병 2명을 골랐지만 한쪽 카드만 넣은 덱"의 의도를 잃는다.

| 파일 | 변경 |
| ---- | ---- |
| `DeckBuilderManager.cs` | `DeckSaveData.characterIdList` |
| `session_game_data.cs` ⚠ | `GameData.MyCharacters` |
| `session_manage.cs` ⚠ | 덱 파일에서 읽어 담기 |
| `firebase_network.cs` ⚠ | `GetDeck`이 `List<string>` → **`DeckSaveData`** 반환 |
| `session_game_manage.cs` ⚠ | 업로드 + `ApplyCharactersFromDeck`가 명시값 우선 |

**구버전 덱과 호환된다** — 값이 비면 기존 역산으로 넘어간다.

#### ③ 덱 정렬 — 새 규칙이 필요 없었다

기획 요구(용병 엘리→베로니카→다이나→소니아, 카드 공격→방어→지원)가
**`RulebookCards.json`의 파일 순서와 정확히 같다는 걸 확인했다.**
그래서 `CardDataManager.GetSortOrder`(= JSON 인덱스) **하나로 두 조건이 모두 충족된다.**
정렬 규칙을 따로 만들면 JSON과 어긋나기 쉬우므로 이 함수만 쓴다.

#### ④ 버튼 터치 크기 자동 보정

신규 `Assets/Scripts/UI/TouchTargetNormalizer.cs`. 씬이 로드될 때마다 버튼을 훑어
**부족한 것만 키운다**(줄이지 않는다).

- 최소 높이(캔버스 높이의 5%, 하한 72) + **최소 4:3 비율**
- 캔버스 밖으로 나간 버튼은 안쪽으로 끌어당긴다 → 덱 편집의 잘린 [메인으로] 버튼
- `LayoutGroup` 아래 버튼은 `sizeDelta`를 직접 만져도 되돌아가므로 **`LayoutElement.min*`으로 요청**
- 무엇을 얼마나 바꿨는지 **로그로 남긴다**

> ⚠️ 화면 전체에 영향을 준다. 값은 보수적으로 잡았으니 실기를 보고 조정할 것.

#### ⑤ 온라인 항복이 안 되던 문제

`DeckInfoPanelUI`가 무조건 `BattleManager.SurrenderBy`를 불렀는데,
**온라인에서는 BattleManager가 매치를 돌리지 않아 `context`가 null**이라 그 메서드가 첫 줄에서 돌아왔다.
버튼 2개가 모두 같은 곳으로 모이므로 **둘 다 먹통**이었다.

**새 DTO는 필요 없었다** — `GameSetRequest`와 그 처리부가 이미 있었다.

- 호스트: `ServerGameManager.SurrenderBy` 신규 → `OnGameSet` 발행 →
  기존 `HandleGameOverFlow`가 결과 확정·board_state 동기화까지 처리
- 게스트: `session_game_manage.SendSurrender()` → `GameSetRequest{ WinnerName = 상대 }`

> 🔒 겸사겸사 **"보낸 사람이 자기를 승자로 지명하면 거부"**를 한 줄 추가했다.
> 이 경로에서 곧바로 악용될 수 있는 형태였다(전면적인 서버 인증은 여전히 별건).

#### ⑥ 조사 중 발견해 함께 고친 것

- **덱이 하나도 없으면 편집 화면이 터졌다** ★ — `CreateStarterDeck`이 채우던 `"11001"`은
  **`RulebookCards.json`에 없는 ID**다. 이어지는 `RefreshDeckUI`가 null을 그대로 역참조해 NRE.
  새 설치·저장 경로가 바뀐 복사본에서 **바로 만나는 상황**이었다.
  죽은 코드를 걷어내고 빈 상태 + 안내로 바꿨으며, 목록/컬렉션 양쪽에 **null 카드 가드**를 넣었다
- 덱 불러오기가 **두 번 실행**되던 것 → 한 번만
- **편집 중인 덱 이름**을 상단에 항상 표시
- 저장하지 않고 다른 덱을 불러오면 **경고**(한 번 더 누르면 진행)

---

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 4/4.

**정렬 규칙은 실제로 검증했다** — JSON 순서가 요구 순서와 일치하는지 확인하고,
뒤섞은 덱을 `GetSortOrder`로 정렬해 엘리→다이나→소니아, 각 용병 안에서 공격→방어→지원으로
나오는 것을 확인했다.

> ⚠️ **나머지는 전부 유니티 UI라 빌드·콘솔로는 검증되지 않는다. 실기 확인이 본 검증이다.**

**사람이 확인할 것:**
1. 메인에서 B덱 선택 → 편집 진입 시 **B덱이 열리는가**
2. **덱이 하나도 없는 상태**로 편집 진입 — 터지지 않고 안내가 뜨는가
3. 용병 슬롯이 비면 카드 목록도 비고, 1종 넣으면 **그 10종만** 뜨는가
4. 2종이면 20종, **3종째는 넣을 수 없는가**
5. 슬롯을 비울 때 그 용병 카드가 덱에서 함께 빠지는가
6. 저장 후 다시 열면 **룰북 순서**로 보이는가
7. 버튼이 잘리지 않고 누르기 충분한가 — **다른 화면이 망가지지 않았는지 보정 로그로 확인**
8. **항복** — 온라인 2인에서 버튼 2개 모두 즉시 패배 처리되고, 상대 화면에 승리로 뜨는가 (양방향)
9. 저장 없이 다른 덱을 불러올 때 경고가 뜨는가

---

### [2026-08-21 후속 4] 불발될 공격에 상대 스택이 소진되던 문제 ✅

바로 앞 작업(이행 원칙 정리)의 **뒷정리**다.
조건부 카드가 불발되도록 고쳤더니, 그보다 **먼저 일어나는** 스택 발동이 남아 있었다.

**증상:** 베로니카 "계획대로"(공격 카드 + 조건부)가 불발돼도
**상대 스택존의 방어 카드는 그대로 소진**됐다. 맞지도 않을 공격에 방어를 태우는 셈이다.

#### 원인 — 순서 문제

메인 페이즈의 카드 처리 순서가 이렇다 (`ResolveMainPhaseCard`):

```
코스트 지불 → HandleStackActivation(스택 소진) → card.Play(여기서 불발 판정)
```

`HandleStackActivation`은 `playedCard.Effects`에서 `DamageEffect`의 타격 횟수만 세고,
0이 아니면 곧바로 방어측에 스택 발동을 강제한다.
"계획대로"는 `DamageEffect(3)`를 갖고 있어 3타로 잡히지만,
그 효과엔 `requirePreviousSuccess`가 붙어 있어 **선행 조건이 무너지면 실행되지 않는다.**
스택 소진은 그 사실을 알기 **전에** 일어났다.

#### 조치

`Card`에 공개 질의를 하나 두고, 세 엔진의 `HandleStackActivation`이 **스택을 태우기 전에** 묻는다.

```csharp
// Card.cs — 직전 작업의 private 검사를 그대로 재사용한다 (판정이 두 곳으로 갈리면 또 어긋난다)
public bool WillResolve(GameContext context) => CanSatisfyPreconditions(context, out _);
```

가드는 이미 있던 `if (incomingHits == 0) → 스택을 아낀다` **바로 옆**에 붙였다. 성격이 같은 판단이다.

| 파일 | 비고 |
| ---- | ---- |
| `Scripts/Managers/BattleManager.cs` | 로컬 엔진 |
| `Scripts/Server Scripts/ServerGameManager.cs` | ⚠️ **서버 파일** |
| `ConsoleRunner.cs` | 동기 버전 |

확정한 룰:

- **코스트는 지불한 채로 둔다.** 순서는 그대로 두고 스택 소진만 고쳤다
  (쓴 비용은 돌려주지 않는 것이 일반적이고, 무작정 질러 보는 플레이도 막는다)
- **스택을 아낀 사실은 진행 로그로 남긴다** (`[스택 보류]`).
  온라인에서 방어측이 "왜 내 스택이 안 터졌지?" 하지 않도록. 별도 UI는 만들지 않았다

#### 이미 안전했던 것

**반격은 문제없다.** `DamageResolver`에서 실제로 데미지가 들어갈 때만 판정하므로
불발된 공격에는 애초에 반응하지 않는다. 확인하고 손대지 않았다.

#### 알아 둘 한계

`HandleStackActivation` 시점에는 `cardPlayer.PlayingCard`가 **아직 설정되기 전**이다
(`card.Play` 직전에 설정된다). `MoveEffect.CanFullySatisfy`는 `excludeSelf` 옵션에서
`PlayingCard`를 후보에서 빼므로, null이면 후보를 한 장 더 세게 된다.

즉 이 검사는 그 경우에 한해 **관대한 쪽으로만 틀린다** —
"불발인데 스택을 태운다"(= 예전 동작)는 남을 수 있어도,
**"멀쩡한 공격인데 스택을 안 태운다"는 일어나지 않는다.** 안전한 방향이고,
`excludeSelf`를 쓰면서 데미지를 주는 카드는 현재 룰북에 없다.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 **6/6**.

직전 작업과 같은 방식으로 **VERO-03의 폐기 장수를 3→99로 바꿔 항상 불발**시켜 확인했다(확인 후 원상복구):

| 항목 | 결과 |
| ---- | ---- |
| 불발 카드 | `[스택 보류]` 뜨고 **`스택 발동!`은 없음** → 이어서 `[불발]`, 페이즈 정상 진행 |
| **통제군** (조건 없는 공격) | 같은 판에서 **스택 발동 19건** — 정상 소진 유지 |
| 원상복구 후 | 스택 발동 19건 / 스택 보류 0건 (정상 데이터에선 불발이 없으므로 맞다) |

> 가드는 `StackZone.Count == 0`·`incomingHits == 0` 검사 **뒤에** 있으므로,
> `[스택 보류]` 로그가 찍혔다는 건 **실제로 태울 스택이 있었다**는 뜻이다. 빈 테스트가 아니다.

**사람이 확인할 것 (로컬 사람 vs 봇):**
1. 상대 스택존에 방어 카드가 깔린 상태에서 **패 2장으로 "계획대로"** 사용
2. **상대 스택이 그대로 남아 있는가** ← 이번 버그의 본체
3. 선택창이 뜨지 않고 `[스택 보류]` → `[불발]` 로그만 남는가
4. **회귀**: 패가 3장 이상이면 스택이 예전처럼 정상 발동하는가

**온라인 2인:**

5. 게스트가 불발 카드를 쓸 때 호스트 스택이 남는가 (반대 방향도)
6. `[스택 보류]` 로그가 양쪽 화면에 보이는가

---

### [2026-08-21 후속 3] "그 후," 연쇄의 이행 원칙 정리 ✅

**증상:** 베로니카 "계획대로"(패 3장 폐기 → 그 후 데미지 3)를 **패 2장**으로 쓰면,
2장짜리 선택창이 뜨고 고른 뒤 **진행이 막혔다.** 선택창이 뜰 이유조차 없는 상황이었다.

#### 정리한 규칙

기준은 **오직 `requirePreviousSuccess`의 유무** 하나다.

| 자리 | 원칙 | 못 지키면 |
| ---- | ---- | -------- |
| `requirePreviousSuccess` **앞**(= 선행 조건) | **100% 이행** | **카드 전체가 불발** (화면도 안 띄운다) |
| 그 외 (연쇄의 뒷효과 · 조건 없는 단일 효과) | **가능한 최대 이행** | 되는 만큼 하고 성공 처리 |

#### 무엇이 잘못돼 있었나

`MoveEffect`가 **존 조합을 보고 엄격성을 추측**하고 있었다 —
"덱→패거나 전부(All)면 유연, 나머지는 엄격". 같은 `MoveEffect`라도 카드마다 의미가 다른데
효과 하나만 봐서는 알 수가 없다. 그래서 조건이 없는 카드까지 엄격해져
"부족하면 통째로 실패"가 됐고, 반대로 진짜 선행 조건은 **일단 선택창부터 띄운 뒤** 실패시켰다.

#### 조치

- **판단 주체를 카드로 옮겼다.** `Card.Play`가 발동 직전에 효과 목록을 훑어
  각 효과에 "100% 이행이 필요한 자리인가"를 알려 준다(`ApplyExecutionPolicy`).
  효과 하나만으로는 알 수 없는 정보이므로 카드가 가진 게 맞다.
- **선행 조건 사전 검사**(`CanSatisfyPreconditions`). 만족할 수 없으면
  **효과를 하나도 실행하지 않고 카드가 불발된다.** 선택창이 뜨지 않는다.
- 새 인터페이스 `IConditionalEffect { RequireFullExecution, CanFullySatisfy }`.
  **모든 효과가 구현할 필요는 없다** — 구현하지 않은 효과는 검사에서 항상 통과한다.
  현재 룰북에서 선행 조건 자리에 오는 건 전부 `MoveEffect`다.
- `MoveEffect`는 이제 **후보보다 많이 요구하지 않는다**(`askFor = Min(요구, 후보)`).
  선택창이 있지도 않은 장수를 요구하는 일이 원천적으로 없어진다.
- JSON의 `isStrict` 오버라이드는 제거했다. 판단 기준이 둘이면 또 어긋난다
  (실제로 쓰는 카드도 없었다).

#### 멈춤(freeze)에 대한 정직한 기록

**정확한 정지 지점은 재현하지 못했다.** 다만 이 구조는 멈추기 쉽게 돼 있었다 —
효과는 `async void`인데 호출부는 `WaitUntil(effectDone)`로 **무기한** 기다린다.
그 사이 어디서든 예외가 나면 `async void`가 조용히 삼켜 콜백이 영영 오지 않고, 게임은 그대로 선다.

위 규칙 변경으로 **문제의 상황 자체가 사라졌지만**(선택창이 뜨지 않는다),
같은 함정이 또 생기지 않도록 안전망 둘을 함께 넣었다:

- `MoveEffect.Execute`를 try/catch로 감싸 **무슨 일이 있어도 `onComplete`를 돌려준다**
- `HumanChoiceDialogUI`의 확정/취소에서 **패널 정리가 실패해도 응답은 보낸다**

> ⚠️ `WaitUntil`에 시간 제한은 두지 않았다. 사람의 선택 대기(제한 없음 설정)를 잘라 버리기 때문이다.

#### 알려진 한계

선행 조건 검사는 **발동 시점** 기준이라, 앞선 효과가 상태를 바꾼 뒤에야 판정할 수 있는 선행 조건
(= 선행 조건이 첫 효과가 아닌 카드)까지는 완전히 보지 못한다.
**현재 룰북 40종은 모두 선행 조건이 첫 효과**라 문제가 없고, 그런 카드가 생기면
`ExecuteEffectsSequentially`의 런타임 검사가 받아 준다.
스택 카드의 트리거별 효과 분기도 같은 이유로 아직 가리지 않는다(해당 카드 없음).

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 **6/6**, 용병 능력 4종 정상.

두 갈래를 실제로 갈라 보기 위해 **카드 데이터를 임시로 극단값(count 99)으로 바꿔** 확인했다(확인 후 원상복구):

| 카드 | 조건 | 결과 |
| ---- | ---- | ---- |
| 계획대로 (`requirePreviousSuccess` 있음) | 3→99장 폐기 | **선택창 없이 즉시 불발**, 데미지도 없음, 페이즈는 정상 진행 |
| 수류탄 투척 (조건 없음) | 2→99장 폐기 | **데미지는 들어가고** 패에 있는 만큼만 폐기 (부분 이행) |

**사람이 확인할 것:**
1. **계획대로** — 패가 3장 미만일 때 **선택창이 아예 뜨지 않고** 불발 로그만 남는가
2. **약탈**(다이나) — 덱이 3장 미만일 때 같은 방식으로 불발되는가
3. **수류탄 투척·퀵 드로우** 등 조건 없는 카드 — 패가 모자라도 **되는 만큼 처리하고 진행**되는가
4. **회귀** — 정상 상황(패·덱이 충분)에서 기존과 똑같이 동작하는가
5. 온라인에서도 1~3이 게스트 쪽에서 동일한가

---

### [2026-08-21 후속 2] 온라인 테스트 피드백 4건 ✅

복사본 2인 테스트 결과. **1번은 코드 버그가 아니었고, 2·3번은 뿌리가 같았다** —
**구독을 떼어 내지 않아 핸들러가 쌓인다.**

#### ① 미러전 — 테스트 환경 충돌이었다 (코드 수정 없음) ★

프로젝트 폴더를 복사해도 `companyName`/`productName`이 같으면
**두 인스턴스가 같은 저장소를 쓴다.** 현재 값은 `DefaultCompany` / `My project`다.

| | 경로 | 결과 |
| --- | --- | --- |
| `persistentDataPath` | `AppData/LocalLow/DefaultCompany/My project` | **같은 `MyDeck` 폴더** |
| `PlayerPrefs` | `HKCU\Software\DefaultCompany\My project` | **같은 `SelectedDeckName`** |

`session_manage.cs`가 그 `SelectedDeckName`으로 덱을 읽어 올리므로 **양쪽이 같은 덱을 업로드**한다.
`HostGameSetupRoutine`은 HOST/GUEST를 각각 정확히 읽어 오고 있었다. **서버 코드는 정상이다.**

> 📌 **2인 테스트 준비물** — 복사본에서 `Edit > Project Settings > Player > Product Name`을 바꿀 것.
> 저장 경로가 갈라지므로 복사본에는 덱을 새로 만들어야 한다
> (어차피 서로 다른 덱으로 붙어야 의미 있는 테스트다).

#### ② 게스트 선택창이 안 닫히고 확정을 두 번 해야 했다

"적용은 되는데 창이 남고 두 번째 확정은 무의미"는 **같은 질문이 두 번 큐에 들어왔다**는 뜻이다.
첫 확정이 요청 A를 처리하고 창을 닫자마자 `ShowNext()`가 요청 B를 띄우니 안 닫힌 것처럼 보이고,
B의 응답은 호스트가 이미 지운 뒤라 무시된다.

**뿌리:** `EventService.SubscribeToNotifications()`의 알림 10개가 전부
**익명 람다 + `+=`이고 해제 코드도 중복 가드도 없었다.**
`EventManager`는 **정적 클래스**라 씬을 다시 로드해도 구독이 살아남는다
→ 재진입하면 **파괴된 옛 EventService의 람다가 그대로 붙어 있어** 질문 한 번에 알림이 두 번 방송된다.
RequestId가 서로 달라 게스트의 기존 중복 가드도 통과해 버린다.
호스트는 자기 창을 엔진 이벤트로 한 번만 띄우므로 **게스트에서만 두 번 보인다** — 증상과 일치.

**조치 — 뿌리 + 독립 방어선 2개:**
- `EventService`: 람다 10개를 **기명 메서드**(`Broadcast*`)로 바꾸고, 멱등 구독 가드 + `OnDestroy` 해제
- `OnlineGuestBoardAdapter`: 중복 판정을 RequestId에서 **내용 지문**(플레이어+후보 ID+장수+문구)으로 확대.
  **RequestId가 달라도 막힌다**
- `HumanChoiceDialogUI`: 마지막 방어선. 현재 띄운 것/대기 중인 것과 같은 내용이면 큐에 넣지 않는다

#### ③ 게스트가 느리고 깜빡이며 덱이 사라졌다 나타났다

**3-1. board_state 리스너가 계속 쌓였다 ★**

`firebase_network.ListenForBoardState`가 **익명 람다를 `+=`만 하고 뗄 수단이 없었다.**
`dbRef`는 static이라 씬이 바뀌어도 살아남는다. 부르는 곳은 둘 —
`session_game_manage.Start()`(씬 로드마다)와 `OnlineGuestBoardAdapter.TryAttach()`
(`ResetMirror()`가 `_listening`을 false로 되돌려 또 붙는다).

즉 **씬을 재진입할수록 리스너가 배로 늘고**, board_state 한 번 쓰면 같은 스냅샷이 N번 큐에 쌓인다.
그러면 `MaxPendingSnapshots = 30`이 **오래된 것부터 조용히 버려** 중간 상태가 유실됐다.

→ 해제 토큰(`BoardStateListener`)을 돌려주는 방식으로 바꾸고, 양쪽 모두 `OnDestroy`/`ResetMirror`에서 뗀다.

**3-2. `movesPerFrame = 1`이 처리량 한계였다**

예전 점등 때문에 1로 낮춘 값인데, 카드 20장 이동에 20프레임이 걸려 덱이 비었다 차는 게 보였다.
→ **밀린 만큼만** 예산을 늘리는 적응형(`baseline + 큐/4`, 상한 `maxMovesPerFrame = 8`).
   한가할 땐 그대로 1장이라 점등이 돌아오지 않는다.

**3-3.** 스냅샷을 버릴 때 **경고를 남긴다.** 조용히 버려서 원인을 추적할 수 없었다.

> ⚠️ 3-1을 고치면 큐가 애초에 넘치지 않을 것으로 본다.
> **깜빡임이 남으면 원인이 다른 곳(레이아웃 재구성)이므로 다음 라운드에 따로 잡는다.**

#### ④ 선택창 순서 배지 (1, 2)

`_selected`가 클릭 순서를 담은 리스트라 번호는 곧 `index + 1`이고,
**1번을 해제하면 2번이 1번이 되는 동작이 저절로 따라온다.**

순서가 무의미한 요청('패 3장 버리기')에 번호를 띄우면 오해를 주므로,
**요청하는 쪽이 플래그를 켜는** 방식으로 했다. `OnRequireCardPick`의 `string message` 자리를
`CardPickPrompt { Message, Ordered }` 구조체로 바꿨다 — 앞으로 항목이 늘어도
**이벤트 시그니처를 다시 손대지 않기 위해서다.** `RequireCardPickNotification`에도 `Ordered`를 실어 게스트까지 전달한다.

배지는 원형 스프라이트를 코드로 생성해 쓴다(쓸 만한 에셋이 없다).

> 💡 **교훈:** 이번 2·3번은 같은 실수의 두 얼굴이다 —
> **정적/외부 이벤트에 익명 람다를 `+=` 하면 뗄 방법이 사라진다.**
> 씬 하나짜리 프로토타입에선 안 드러나다가, 씬을 오가는 순간 조용히 배가된다.
> 구독을 만들 때는 **해제 경로를 같이 만들 것.**

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok /
콘솔 회귀 **5/5** `★ [MATCH SET] ★`, 용병 능력 4종 및 베로니카 되돌리기 정상.
**이번 건은 대부분 온라인·UI라 콘솔 회귀로는 거의 잡히지 않는다. 실기 확인이 본 검증이다.**

**사람이 확인할 것 (복사본 Product Name을 먼저 바꾸고 2인 테스트):**
1. **미러전 해소** — 양쪽이 서로 다른 덱·용병으로 시작하는가 (`[서버 초기화] HOST/GUEST` 로그 대조)
2. **게스트 선택창** — 확정 **한 번**으로 닫히는가. 안 닫히면 새 경고 로그
   (`중복 구독을 건너뛴다` / `내용이 같은 카드 선택 요청이 또 왔다`)로 원인이 갈린다
3. **게스트 속도** — 큰 이동에서 덱이 사라졌다 나타나지 않는가.
   `밀린 스냅샷 N건을 버렸다` 경고가 뜨면 아직 처리량이 모자란 것이다
4. **점등 재발 여부** — 한가할 때는 예전처럼 차분한가
5. **순서 배지** — 베로니카에서 1·2가 보이고, **1번을 해제하면 2번이 1번으로 바뀌는가**
6. **회귀** — '패 3장 버리기'에는 배지가 **뜨지 않는가**
7. **씬 재진입 회귀** — 로비→대전을 두 번 반복해도 2·3번이 재발하지 않는가
   (리스너 누적이 원인이라면 여기서 가장 잘 드러난다)

> 🔧 **콘솔 회귀 실행 요령** — `dotnet run`을 연달아 돌리면 빌드 전 sync 단계가
> 직전 실행과 파일을 두고 부딪쳐 가끔 실패한다(코드 문제가 아니다).
> 반복 실행할 땐 `dotnet build` 한 번 뒤 `TCG_Project/bin/Debug/net9.0/TCG_Project.exe`를 직접 부르는 편이 안정적이다.
> ⚠️ `bin/Debug/net10.0`은 **오래된 잔재 폴더**다(프로젝트 TFM은 net9.0). 그쪽 exe를 돌리면 옛 코드가 실행된다.
> 실행 시 `DOTNET_ROLL_FORWARD=LatestMajor`가 필요하다.

---

### [2026-08-21 후속] 베로니카 능력 완성 — 되돌릴 위치·순서 선택 ✅

룰북상 베로니카(VERO-01)는 4단계다.

> 메인덱 맨 위 3장을 자신만 보고 **원하는 카드 1장을 패로 가지고 온다.
> 그 후, 2장은 덱 맨 위 혹은 맨 아래로 원하는 순서로 되돌린다.**

**1·2단계만 있었다.** 고른 1장만 덱에서 빼고 나머지 2장은 아예 손대지 않아
원래 자리(덱 맨 위, 원래 순서)에 그대로 남았다. 3·4단계는 코드에 존재하지 않았다.

그래서 "쓸모없는 카드를 덱 맨 아래로 치운다"는 **능력의 핵심 절반이 죽어 있었다.**
실질적으로는 그냥 "3장 보고 1장 뽑기"였다.

#### 확정한 룰 해석

- 2장은 **같은 쪽**으로 간다 (한 장은 위, 한 장은 아래로 쪼갤 수 없다)
- 순서의 뜻은 하나로 통일 — **먼저 고른 카드를 덱에서 먼저 만난다**
  - 맨 위: 첫 카드가 덱 맨 위(다음 드로우)
  - 맨 아래: 첫 카드가 둘 중 위쪽(= 둘 중 먼저 뽑히는 쪽)

#### 선행 작업 ① 선택창에 문구를 전달할 수 없었다

`OnRequireCardPick`에 메시지 인자가 없어 선택창이 항상 `"카드를 N장 선택하세요."`만 띄웠다.
"순서대로 클릭하라"를 전할 방법이 아예 없었다.
DTO(`RequireCardPickNotification`)에는 `Message` 필드가 **이미 있었는데 배관이 그걸 버리고 있었다.**

```csharp
Action<Player, List<Card>, int, string, Action<List<Card>>> OnRequireCardPick;
```

**호환 규칙: `null`/공백이면 수신측이 기존 자동 문구를 만든다.**
덕분에 기존 발행처 7곳은 `null` 한 개 끼우는 한 줄 수정으로 끝났고 동작도 그대로다.
구독 5곳(`HumanChoiceDialogUI`·`CardSelectionPanel`·`GameStatusPanelUI`·`BattleManager` QA·`EventService` ⚠)은
문구를 받아 쓰도록 고쳤고, `OnlineGuestBoardAdapter`는 **버리던 `noti.Message`를 넘겨주도록** 했다.

#### 선행 작업 ② 덱 맨 위에 카드를 넣을 수단이 아예 없었다

`Player.InsertCard(ZoneType.Deck, card)`는 `Deck.Add` — **리스트 끝 = 덱 맨 아래**다.
드로우는 `Deck[0]`(맨 위)에서 가져가므로, 기존 API로는 맨 위에 넣을 방법이 없었다.

위치를 받는 오버로드 `InsertCard(zone, card, index)`를 추가했다(범위 밖은 클램프).
기존 2인자 버전은 그대로 둬 다른 호출부에 영향이 없다.

> ⚠️ 앞으로 덱 맨 위에 무언가를 놓는 효과를 만들 때 **반드시 3인자 버전**을 써야 한다.
> 2인자 버전은 이름과 달리 맨 아래로 간다.

#### 본 작업 — 순서에 함정이 있다

```
1. 고른 카드를 패로
2. MarkCharacterAbilityUsed        ← ★ 되돌리기보다 먼저!
3. 위/아래 선택 → 순서 선택 → 실제 이동
4. onComplete(true)
```

**2번이 3번보다 앞에 와야 한다.** 지난 세션의 버그가 정확히
"능력이 조용히 실패 → `MarkCharacterAbilityUsed` 미호출 → 매 턴 재발동"이었다.
되돌리기 단계에서 무슨 일이 생겨도 **능력은 이미 쓴 것으로 기록돼 있어야** 같은 함정을 피한다.

방어 처리: 봇·타임아웃·취소는 모두 **덱 맨 위 + 원래 순서**(= 기존 동작)로 떨어지고
**절대 중단하지 않는다** — 카드는 이미 패로 갔기 때문이다.
응답 구성이 원래 후보와 어긋나면 경고 후 원래 순서로 폴백한다
(`ValidateOnRequireCardPick`이 **개수 상한만** 보므로 능력 쪽에서 한 번 더 본다).

되돌린 로그에는 **카드 이름을 남기지 않는다.** 로그는 상대에게도 보이므로
"덱 맨 위에 무엇이 있는지"가 새면 능력의 의미가 사라진다.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 회귀 3판 `★ [MATCH SET] ★`.

콘솔은 봇 전용이라 사람 경로를 못 돌린다. 그래서 **덱 순서를 찍는 임시 로그를 넣어 두 갈래를 직접 확인**했다
(확인 후 원상복구):

| 경우 | 결과 |
| ---- | ---- |
| 맨 위(봇 기본) | 되돌리기 전후 덱이 **완전히 동일** → 기존 동작과 무회귀 |
| 맨 아래(강제) | 위 2장이 사라지고 덱 끝에 **고른 순서 그대로** 붙음 → 첫 카드가 먼저 뽑히는 위치 |

**미검증:** 사람이 직접 두 질문에 답하는 경로. 아래 체크리스트 참조.

**사람이 확인할 것 (베로니카 덱 1판):**
1. 1장 고른 뒤 **"맨 위로 되돌릴까요?"**가 뜨는가
2. 이어 순서 선택창이 뜨고 문구가 **"먼저 뽑고 싶은 순서대로"**로 보이는가
3. **맨 위** 선택 시 다음 턴 드로우가 **첫 번째로 고른 카드**인가
4. **맨 아래** 선택 시 다음 턴 드로우가 되돌린 2장이 **아닌** 카드인가
5. 능력은 한 판에 **한 번만** 발동하고 용병 카드가 180도 돌아가는가
6. 회귀: 엘리·소니아·다이나 능력과 다른 카드 선택창 문구가 그대로인가

**온라인 2인:**

7. 두 질문이 **자기 차례인 쪽에만** 뜨는가
8. 게스트가 고른 **순서대로** 호스트 덱에 반영되는가
9. ⚠️ 질문이 연달아 2개 뜨므로 **기존 게스트 이중 확인 문제**가 여기서 재현되는지 함께 볼 것
   (현재 중복 가드는 임시 조치다)

---

### [2026-08-21] 임시 랜덤 처리된 카드 효과를 실제 선택으로 전환 ✅

선택 UI가 없던 시절 "나중에 고치자"며 랜덤으로 넘겨 둔 곳들을 걷어냈다.
`HumanChoiceDialogUI`(다중 선택 N/M) + 온라인 중계가 둘 다 동작하므로 이제 가능하다.

**판정 기준:** 카드 설명에 **"랜덤"이라고 적힌 것만 랜덤.**
(ELLI-11 "랜덤으로", DAIN-08 "랜덤으로 1장" — 이 둘뿐이다)
40종을 전수 조사한 결과 대부분은 이미 `Choose`였고, 진짜 문제는 4건이었다.

#### ① ELLI-10 전략적 후퇴 — ★ JSON을 코드가 덮어쓰고 있었다

JSON은 `"mode": "choose"`라고 적혀 있고 카드 설명도 "2장을 **고르고**"인데,
`GameDataManager.BuildMoveParams`의 `ShuffleReturnEffect` 분기가 `raw.mode`를 읽지 않고
`p["mode"] = "Random"`을 박아 넣고 있었다. **데이터가 말하는 걸 코드가 묵살한 경우다.**

#### ② ELLI-09 리로드 — 랜덤 하드코딩

`RecoverFromDiscardEffect`도 같은 모양. 폐기존은 공개 정보이고 설명에 "랜덤"이 없으므로 선택이 맞다.
`SearchDeckEffect`가 쓰던 패턴(`raw.mode` 없으면 Choose)으로 통일했고, JSON에도 `"mode": "choose"`를 명시했다.

#### ③ ELLI-11 무작위 노획 — 랜덤은 맞지만 거부권이 없었다

"랜덤으로 가져올 **수 있다**"인데 무조건 가져왔다. `OptionalActionEffect`로 감싸 매 자원페이즈 의사를 묻는다.

> ⚠ **그런데 여기 함정이 있었다.** `GameLogicHelpers.ApplyBattlefieldResourcePhaseEffects`가
> `Execute(ctx, () => { })`로 **응답을 기다리지 않고** 있었다.
> 바로 앞 세션의 **다이나 능력 버그와 정확히 같은 모양**이다.
> 그대로 둔 채 사람에게 물으면 회수 카드가 **드로우 페이즈 도중에** 들어온다.

조치: 헬퍼가 `out int startedCount`로 **몇 개를 발동시켰는지**를 알려주고,
호출부가 그만큼 응답을 기다린다(상한 = `ChooseWaitTime`). 다이나 때 쓴 패턴 그대로다.

| 호출부 | 처리 |
| ------ | ---- |
| `BattleManager` | `WaitForBattlefieldEffects()` 코루틴 추가 |
| `ServerGameManager` | 같은 코루틴 추가 + `ExecuteResourcePhaseRoutine`을 **void → IEnumerator**로 전환 ⚠ 서버 파일 |
| `ConsoleRunner` | 인자 생략 오버로드. 봇은 동기 응답이라 그대로 동작 |

또 `OptionalActionEffect`에 **`Precondition`**을 넣었다. 폐기존이 비었으면 아예 묻지 않는다
— 얻을 것도 없는데 매 턴 팝업을 띄우면 그것만큼 해롭다.

#### ④ SONI-10 신재생에너지 — 구별되지 않는 카드에 선택을 요구했다

자원카드는 전부 `RES-01`의 복제본이라 무엇을 골라도 결과가 같다.
`ResourceFromDiscardEffect` 기본 모드를 `Choose` → `Top`으로 내렸다.
뒤따르는 "효과 카드 1장 되돌리기"는 그대로 선택이다.

#### ⑤ 재발 방지 — 조용한 폴백이 문제였다

이 버그들이 오래 살아남은 이유는 **`Choose`가 아무 말 없이 랜덤으로 떨어졌기 때문**이다.

- `CardSelector.SelectByMode`의 `Random or Choose => SelectRandom`을 분리해
  **경고 로그를 남긴다.** `MoveEffect`가 Choose를 먼저 가로채므로,
  여기 도달했다면 선택 UI를 우회한 것이다.
- `DiscardEffect.cs` **삭제**. `GameDataManager`가 `DiscardFromHandEffect`를 `MoveEffect`로
  보내므로 이 클래스는 어디서도 생성되지 않는 **죽은 코드**였는데,
  "UI 미구현으로 임시 랜덤 처리"라는 낡은 주석이 남아 다음 사람을 오도했다.
  (`Assembly-CSharp.csproj`의 `<Compile>` 항목도 같이 제거 — 유니티가 재생성한다)

#### 그대로 둔 것

DAIN-08 약탈의 `"mode": "random"`(설명이 "랜덤으로 1장"),
`MoveEffect`·`OptionalActionEffect`의 **봇 분기**(랜덤 선택 / 항상 수락).
봇 전략화는 강화학습 단계의 일이라 이번 범위 밖이다.

> 💡 **교훈:** 이번에 드러난 세 부류가 모두 같은 종류다 —
> ① 데이터(JSON)가 말하는 걸 코드가 묵살하고,
> ② 폴백이 **조용하게** 동작해 틀림을 숫자로만 남기고,
> ③ 비동기 후크를 fire-and-forget으로 불러 페이즈 경계가 무너진다.
> **임시 처리는 반드시 시끄럽게 실패하는 방향으로 놓아야** 나중에 찾을 수 있다.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok /
콘솔 회귀 `★ [MATCH SET] ★`. 추가로 **테스트 덱을 임시로 바꿔** ELLI-09/10/11·SONI-09/10을
직접 발동시켜 확인했고(확인 후 덱은 원래대로 복구), 그 동안 **`CardSelector` 경고는 한 번도 뜨지 않았다**
= 선택 UI를 우회하는 경로가 없다는 뜻이다.
**미검증:** 사람이 직접 고르는 경로(봇은 자동 응답이라 회귀로는 잡지 못한다). 아래 체크리스트 참조.

**사람이 확인할 것 (엘리 덱 1판):**
1. 전략적 후퇴 — 2장 드로우 후 **되돌릴 2장을 고르는 창**이 뜨는가
2. 리로드 — 폐기존 목록이 뜨고 **고른 그 카드가** 패에 오는가
3. 무작위 노획 — 자원페이즈마다 예/아니오를 묻고, "예"일 때 **자원페이즈 안에서** 카드가 들어오는가
   (드로우 페이즈로 넘어간 뒤면 대기 처리가 틀린 것)
4. **회귀**: 전장 카드가 없는 판에서 자원페이즈가 지연 없이 넘어가는가

---

### [2026-08-20 후속] 다이나 능력 점검 — 타이밍 결함 발견·수정 ✅

용병 4종 중 다이나만 점검이 안 돼 있어 확인했다. **카드 조회 버그(위 ③)와는 무관**하다 —
다이나는 후보 카드를 고르는 능력이 아니라 예/아니오만 묻기 때문이다.
대신 **다른 결함**이 있었다.

**문제:** `DainaAbility.OnOpenPhaseAbandon`은 사람에게 예/아니오를 묻는 **비동기(`async void`) 훅**인데,
두 엔진 모두 **응답을 기다리지 않고 던져 놓기만** 했다.

```csharp
// 즉발 효과라 대기 불필요   ← 묻지 않고 즉시 회복하던 구버전 기준의 낡은 주석
ability.OnOpenPhaseAbandon(player, context, _ => { });
```

`BattleManager` L747, `ServerGameManager` 오픈 페이즈 — 둘 다 같은 형태였다.
(`BattleManager` 하단에 `WaitUntil(() => done)`을 쓰는 **주석 처리된 코루틴 버전**이 남아 있다.
 누군가 이미 알아채고 시도했던 흔적으로 보인다)

**영향:** 오픈 페이즈가 답을 기다리지 않고 끝나므로 **라이프 회복이 메인 페이즈 도중에 적용된다.**
그 사이에 데미지를 맞으면 **회복 전에 죽을 수 있다** — 룰북 타이밍과 어긋난다.

**조치:** 발동한 '폐기 시 능력' 수를 세고(`_pendingAbandonAbilities`) 오픈 페이즈가 응답을 기다린다.
**두 엔진 모두 동일하게** 적용했다. 다만 응답이 없어도 멈추지 않도록
`GameRules.ChooseWaitTime` 상한을 두었다.

> 참고: 나머지 3종(엘리·베로니카·소니아)은 호출부가 이미 `WaitUntil`로 대기하고 있어 문제가 없다.
> 다이나만 "즉발"로 분류돼 있었다.

---

### [2026-08-20] 온라인 실전 버그 3종 수정 ✅

2인 실접속에서 나온 문제들. **세 번째가 이번 세션에서 가장 값진 발견이다.**

#### ① 이긴 쪽에 "패배"가 뜨던 문제

`GameStatusPanelUI.ResolveHuman()`이 `BattleManager.HumanPlayer`를 보고 있었다.
**온라인에서는 BattleManager가 매치를 돌리지 않아 이 값이 항상 null**이고, 그러면

- `HandleGameSet` → 제목이 "게임 종료" (게스트가 본 것)
- `HandleMatchSet` → `humanWon = false` → "매치 패배" (**이긴 호스트가 본 것**)

→ 매치 시작 때 받은 `p1`/`p2`를 저장해 `LocalPlayerContext.ResolveMine`으로 판정한다.
   로컬 플레이용 `BattleManager.HumanPlayer` 폴백은 남겼다.

#### ② 용병 카드가 안 돌아가던 문제

180도 회전은 `Player.MarkCharacterAbilityUsed` → `OnCharacterAbilityUsed` →
`CharacterFieldBroadcast.EmitSlotUpdate`로 일어난다. 게스트에는 그 이벤트를 쏘는 엔진이 없고,
**`board_state`에 "능력을 썼는가" 정보 자체가 없었다.**

→ `PlayerState.UsedCharacterCardIds` 신규(EventService가 채움) → 게스트가 미러에 반영하면
   엔진 규칙대로 이벤트가 나가 카드가 돌아간다.

#### ③ ★ 베로니카·소니아 능력만 매 턴 반복되던 문제 — 서버 카드 조회의 존 누락

**증상:** 엘리는 정상인데 베로니카·소니아만 게스트가 매 턴 다시 쓸 수 있고,
베로니카는 **무엇을 골라도 덱 맨 위 카드**를 가져왔다.

**원인:** `EventService.GetCardFromAnywhere`가 카드를 찾을 때
**패 · 스택존 · 전장 · 세트존만 훑고 덱과 폐기존을 빠뜨렸다.**

| 용병 | 후보가 있는 곳 | 결과 |
| ---- | ------------- | ---- |
| 엘리 | **패** | 찾힘 → 정상 |
| 베로니카 | **덱** 탑 3장 | 못 찾음 → 실패 |
| 소니아 | **폐기존** | 못 찾음 → 실패 |

못 찾으면 `pickedCards`가 **빈 목록**이 되는데, `ValidateOnRequireCardPick`은
"너무 많이 골랐는가"만 보므로 **빈 선택이 그대로 통과**한다. 그 결과 능력은
`chosenCard == null`로 조용히 실패하고 **`MarkCharacterAbilityUsed`가 호출되지 않는다**
→ 다음 턴에 또 물어본다(= 무한 사용처럼 보임).

베로니카가 "맨 처음 카드"를 가져온 것도 여기서 설명된다. 능력이 실패하면 드로우 대체가 무산돼
**평범한 드로우(덱 맨 위 1장)** 가 일어난다. 사용자가 본 "패 카드 점등"이 그 드로우다.

**조치 2가지:**
- `GetCardFromAnywhere`에 **덱 · 폐기존 · 자원덱 · 자원존** 추가
- `CardPickRequest` 처리에서 **보낸 ID 수와 해석된 카드 수가 다르면 거부**하고 경고를 남긴다.
  빈 목록이 조용히 통과하던 구멍을 막는다

> 💡 **교훈:** "전체에서 찾는다"는 이름의 함수가 실제로는 일부 존만 훑고 있었다.
> 그리고 검증 함수가 **하한(최소 개수)을 보지 않아** 빈 응답을 정상으로 취급했다.
> 조회 범위와 검증 범위는 이름만 믿지 말고 실제로 확인할 것.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok /
콘솔 봇 회귀에서 **베로니카·소니아 능력 발동 로그 확인**.
**미검증:** 2인 실접속에서의 재현 여부.

---

### [2026-08-18 후속 2] 덱·용병 주입 + 게스트 선택 UI (F-4 4단계) ✅

섹션 11 F-1 **#2·#3**과 게스트 입력 잔여분을 한꺼번에 해소했다. **서버 파일 3곳을 수정했다(담당자 공유 필요).**

#### A. 덱 주입 — 고른 덱으로 싸운다

| 파일 | 변경 |
| ---- | ---- |
| `firebase_network` | **`GetDeck(sessioncode, role)` 신규.** 쓰기(`UploadDeck`)만 있고 읽는 수단이 없었다 |
| `session_game_manage.HostGameSetupRoutine` | 하드코딩 ID 배열 2개 제거 → 업로드된 덱 사용. 고정 `WaitForSeconds(0.5f)` → **양쪽 덱이 올라올 때까지 폴링**(15초 한도) |

> ⚠️ **덱 장수 규약이 두 가지다.** 덱 빌더는 **중복 포함 20장**을 저장하고,
> 콘솔·구버전은 **유니크 10종**을 쓴다. `CreateDeckFromIds`는 **각 ID를 2장씩 전개**하므로
> 20장짜리를 그대로 넘기면 40장이 된다. `BuildMainDeck`이 개수를 보고 갈라 준다
> (20개 이상 → 1:1, 그 미만 → 2장씩). 결과가 20장이 아니면 폴백 덱으로 대체하고 경고를 남긴다.

#### B. 용병 능력 — 이제 발동한다

원인은 하나였다: 셋업이 `CharacterCardId` / `SecondaryCharacterId`를 넣지 않아
`CharacterAbilityRegistry.GetPlayerAbilities`가 빈 목록을 돌려주고 있었다.

| 파일 | 변경 |
| ---- | ---- |
| `GameDataManager` | **`TryResolveCharacterCardId` 신규.** `"ELLIE"` 또는 `"ELLI-05"` → `"ELLI-01"` |
| `session_game_manage` | 덱 카드에서 용병 2종 역산 후 주입(`ApplyCharactersFromDeck`) |
| `session_game_manage` | `CharacterFieldBroadcast.Register` + `SyncAll` 호출 → **용병 슬롯 4칸 표시** |
| `EventService` | `PlayerState.Character1_ID / Character2_ID` 대입 (선언만 있고 채우지 않았다) |
| `OnlineGuestBoardAdapter` | 게스트도 용병 슬롯 방송 |

> ★ **`ELLI` vs `ELLIE` 불일치는 데이터로 해결했다.** `Character.json`이 `id`(ELLI-01)와
> `characterId`(ELLIE)를 함께 갖고 있다. **문자열을 직접 조립하지 말고 `TryResolveCharacterCardId`만 쓸 것.**

#### C. 게스트 선택 UI — 카드 선택 · 예/아니오 · 스택

| 파일 | 변경 |
| ---- | ---- |
| `session_game_manage` | 공개 훅 3개 — `OnCardPickRequested` / `OnOptionalRequested` / `OnStackRequested`. 알림은 private 필드에만 담겨 있어 UI가 도착을 알 수 없었다 |
| `OnlineGuestBoardAdapter` | 훅 구독 → 후보 카드 복원 → `EventManager.OnRequireCardPick` / `OnRequireOptionalAction` **합성 발행** → 콜백에서 `SubmitCardPickFromUI` / `SendPendingOptionalResponse` / `SendPendingStackResponse` 전송 |

**새 UI를 만들지 않았다.** 기존 `HumanChoiceDialogUI`가 그대로 뜬다 (세트·오픈과 같은 방식).

> ⚠️ **스택은 시맨틱이 어긋난다.** 호스트 엔진은 `OnRequireCardPick`으로 묻는데
> 게스트에는 `RequireStackNotification`(스택 카드 1장 지정)으로 도착한다.
> 그래서 게스트에서는 **예/아니오로 바꿔 묻고** 응답은 스택 전용 API로 보낸다.

> 빈 응답 방지: 선택 결과가 비면 후보 앞쪽으로 채워 보낸다. 서버가 대기 중이라 빈 응답이 더 위험하다.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok /
콘솔 봇 회귀 `★ [MATCH SET] ★` + 용병 능력 3종 발동 로그 확인(로컬 경로 무영향).
**미검증:** 2인 실접속에서의 덱 주입·용병 능력·게스트 선택 창.

---

### [2026-08-18 후속] 문서 재정비 + 이동 속도 조정 ✅

| 항목 | 내용 |
| ---- | ---- |
| `movesPerFrame` 1로 | 게스트 카드 이동을 프레임당 1건만 반영 (화면 점등 최소화) |
| 루트 `README.md` 재작성 | 프로젝트 소개 / 현재 진행 상황 / **역할별 남은 일** / 모두가 알아야 할 규칙. 기존 내용은 `docs/개발_기록.md`로 이관 |
| `EVENTMANAGER_CONTRACT.md` | 죽은 계약 2개(`OnRequireStackResponse`·`OnRequireCardChoice`), 미발행 이벤트 3개(`OnCardDiscard`·`OnCardUnstacked`·`OnCardUnbattlefield`) 표기. **온라인에서 콜백이 null로 온다**는 주의 추가 |
| `CardManual.md` (정본+루트 사본) | `max_deck_count` 필드 문서화. 빠뜨리면 덱 빌더의 추가 버튼이 죽는다는 경고 포함 |
| 과거 문서 3종 | `PROJECT_DIAGNOSIS.md` · `EFFECT_SYSTEM_PROPOSAL.md` · `BattleManager-ServerGameManager-FunctionList.md`에 **"과거 기록" 머리말** 부착 |

---

### [2026-08-18] 2인 실접속 안정화 — 진행 로그·이탈·제한시간·방 정리 + 게스트 표시 버그 ✅

한 판이 끝까지 진행되는 것을 확인한 뒤의 다듬기 작업. **서버 스크립트는 건드리지 않았다.**

#### ★ 게스트 표시 버그의 진짜 원인 — 미러가 이벤트를 골라 쐈다 (2026-08-18 최종)

앞선 두 번의 수정(존 문자열, 앞뒷면 규칙)으로도 스택·전장 카드의 뒷면 표시와 화면 점등이 남았다.
서버까지 포함해 다시 훑은 결과, **서버는 정상이었고 어댑터가 이벤트 계약을 일부만 지키고 있었다.**

**서버는 문제 없다** — `ServerGameManager`는 존 전이마다 **쌍으로** 이벤트를 쏜다
(L848 `OnCardMove` + `OnCardStacked`, L857 `OnCardMove` + `OnCardBattlefield`). `board_state`도 정확하다.

**어댑터는 `OnCardMove`만 흉내 내고 있었다.** 그런데 보드 UI의 스택존 렌더링은 그 이벤트가 아니라
**`OnCardStacked`**에 걸려 있다:

```
OnCardStacked → PlayerUIManager.HandleCardStacked → SyncMyStack
              → StackZoneRowUI.SyncFromEngineStack   ← 줄 배치 + SetFaceDown(false)
```

그래서 내 스택 카드는 줄로 정렬되지도, 앞면으로 뒤집히지도 않았다.
(상대 스택은 `EnemyVisualTester.HandleEnemyCardMove` L265가 `OnCardMove`만으로 처리해 멀쩡했다 —
 한쪽만 깨져 "일부만 뒷면"으로 보인 이유다.)

**해결:** `EmitZoneArrivalEvents`로 **엔진과 같은 세트를 전부 발행**한다.

| 도착 존 | 함께 쏘는 이벤트 |
| ------- | ---------------- |
| SetZone | `OnCardSet` |
| StackZone | `OnCardStacked` |
| BattlefieldZone | `OnCardBattlefield` |
| ResourceZone | `OnCardResourceAdded` |
| Hand (from Deck) | `OnCardDraw` |

> ⚠️ **교훈: 미러는 엔진 이벤트를 골라 쏘면 안 된다.** 같은 전이에는 같은 세트를 전부 쏜다.
> 보드 UI가 어떤 이벤트에 무엇을 걸어 뒀는지는 이벤트마다 다르다.

#### 화면이 새로고침되듯 점등되던 문제

스냅샷 하나에 **한 페이즈치 이동(5~10건)**이 통째로 들어 있는데 그걸 한 프레임에 다 적용하고 있었다.
이동마다 트윈 + `DeckGraveyardStackUI.Sync`(덱·폐기존 전체 재배치)가 돌아 화면이 번쩍인다.
호스트는 엔진이 `ActionDelay`를 두고 진행해 이런 일이 없다.

→ **이동 큐**를 두고 `movesPerFrame`(기본 2)씩 나눠 반영한다. 큐를 다 비우기 전에는 다음 스냅샷을 읽지 않아
   순서도 보존된다. 인스펙터 값이 아니라 코드 상수이므로 조절은 `movesPerFrame`에서 한다.

#### 앞선 시도들 (참고 — 원인 2개는 실재했고 함께 고쳐져 있다)

| 원인 | 내용 |
| ---- | ---- |
| ① **스택존만 앞뒷면을 안 건드린다** | `CardBoardRegistry.ApplyZonePresentation`은 Hand·SetZone·Graveyard·Deck·Battlefield에서는 `SetFaceDown`을 부르는데 **`StackZone`에서는 부르지 않는다.** 그래서 뒷면으로 세트됐던 카드가 스택존에 올라가면 뒷면인 채로 남는다. 호스트는 오픈 페이즈에서 `OnCardStateChanged`로 이미 뒤집혀 있어 드러나지 않았다 |
| ② **어댑터가 중간 스냅샷을 버렸다** | `while (_pending.Count > 1) Dequeue()`로 "밀리면 최신 것만" 반영했다. 그래서 세트(뒷면) → **공개(앞면)** → 스택존으로 이어지는 흐름에서 가운데 '공개' 상태를 통째로 건너뛰면, 카드는 앞면으로 뒤집힐 기회를 영영 잃는다 |

**해결:**
- 스냅샷을 **순서대로 프레임당 하나씩** 반영한다(버리지 않는다. 병적으로 밀릴 때만 오래된 것을 흘린다)
- 이동할 때마다 `RefreshCardFace`로 **카드 GO의 면을 모델에 직접 맞춘다.** `ApplyZoneMove`의 존별 처리에 기대지 않는다
  (상대 손패·덱은 예외로 항상 뒷면)

> 같은 변경이 **"화면이 새로고침되듯 점등"** 증상도 줄인다. 한 페이즈치 이동이 한 프레임에 몰려
> 트윈과 덱·폐기존 재배치가 동시에 터지던 것이 프레임에 나뉘기 때문이다.

#### 함께 추가한 것

| 항목 | 내용 |
| ---- | ---- |
| 게스트 진행 로그 | 스냅샷 차이에서 읽어 로그로 남긴다 — 공개(이름·스피드·타입) / 세트 / 폐기 / 스택 대기 / 전장 배치 / 드로우 / **라이프 증감(색 구분)**. 호스트의 엔진 원본 로그와 같지는 않다(그건 `LogNotification` 발행이 필요 = 서버 담당) |
| 상대 이탈 감지 | `firebase_network.ListenForSessionExit`로 세션 삭제를 감시 → 로그 + `GameStatusPanelUI.ShowNotice("상대가 나갔습니다")`. ⚠️ **게스트가 나가면 세션은 남고 `guest` 칸만 비므로 잡히지 않는다** — 그때는 호스트가 제한 시간으로 자동 진행 |
| 입력 제한 시간 표시 | 헤더에 `ROUND 3 · 세트 페이즈   세트 12초`. 5초 이하 빨강. **내 요청일 때만** 뜨고, 카드 확정·페이즈 전환에 사라진다 |
| 끝난 방 정리 | `ClearSession()`에서 `ExitSession` 호출(메인 메뉴 복귀 시). 호스트면 세션 삭제, 게스트면 자리만 비움. ⚠️ **에디터 Play 정지·앱 강제 종료는 정리되지 않는다** — `onDisconnect`가 필요하고 그건 `firebase_network`에 API가 없다 |
| 카드 이동 속도 | `CardMoveTween.Duration` 0.18 → **0.36초** (눈으로 따라가기 위해) |
| 입력 대기 시간 | `choose_wait_time` 10초 → **2분**, `UnlimitedInputForTesting = true`. ⚠️ **임시 설정** — 되돌릴 지점은 `OnlineMatchStarter` 주석에 명시 |

> ⚠️ **`choose_wait_time`을 무한으로 두면 안 된다.** 게스트가 아직 답할 수 없는 요청(스택 발동·카드 선택)이
> 오면 그 시간만큼 양쪽이 멈춘다. 실제로 1시간으로 뒀다가 스택 상황에서 게임이 정지했다.

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 봇 회귀 정상.
**미검증:** 이 수정 이후의 2인 재접속.

---

### [2026-08-17 후속 9] 2인 실접속 1차 시도 — 막힌 지점 2개 해소 ✅

에디터 2개(프로젝트 복사본)로 처음 붙여 본 결과. **둘 다 코드가 아니라 "배선"이 빠져 있던 문제였다.**

#### ① 매칭 후 아무 일도 일어나지 않음 — 시작을 눌러 줄 주체가 없다

**증상:** 양쪽 다 "match found / entering battle soon" 팝업에서 영구 정지.

**원인:** 호스트·게스트 모두 `ListenForGameStart`로 **세션 state가 `PLAYING`이 되기를 기다린다.**
그런데 state를 `PLAYING`으로 올리는 것은 `session_manage.OnClickSessionStart()` 하나뿐이고,
**그 메서드에 연결된 버튼은 `server ui.unity`(디버그 씬)에만 있다.** 메인 메뉴의 랜덤 매칭 경로에는 없다.
→ 방이 `READY`까지 가고 멈춘다.

**해결:** 랜덤 매칭에는 로비가 없으므로(팝업 문구도 "곧 전투 시작") **호스트가 자동으로 시작을 건다.**

- `RandomMatchUI` — 매칭 성사 시 호스트면 `autoStartDelay`(기본 1.5초) 뒤 `OnClickSessionStart()` 호출
- `session_manage` — 호스트 여부를 읽는 `public bool IsHost => amIHost;` **한 줄**(읽기 전용, 동작 변화 없음)

> ⚠️ **즉시 호출하면 안 된다.** `HandleGuestJoined`는 콜백을 먼저 부르고 **그 뒤에** state를 `READY`로 쓴다.
> 바로 `PLAYING`을 쓰면 뒤이은 `READY`가 덮어써서 똑같이 멈춘다. 그래서 지연을 둔다.
>
> 비공개 방(방 만들기) 경로는 그대로 뒀다 — 거기는 호스트가 직접 시작을 누르는 흐름이 맞다.

#### ② 씬 전환은 됐는데 빈 필드 — GameManage가 꺼져 있다

**증상:** 양쪽 다 TestGameScene 진입, 그런데 `배틀 매니저 준비 완료. 매치 시작을 대기합니다...`만 뜨고
**`[OnlineMatchStarter]`·`[GuestBoard]` 로그가 하나도 없다.** 필드는 텅 비어 있다.

**원인:** `session_game_manage` + `firebase_network` + `session_ui`가 올라간 **`GameManage` 오브젝트가
씬에 비활성(`m_IsActive: 0`)으로 저장돼 있다**(로컬 봇전에서 파이어베이스를 건드리지 않으려던 조치로 보인다).

두 가지가 동시에 터진다:
- `session_game_manage.Start()`가 아예 안 돈다 → 덱 업로드·이벤트 구독·`HostGameSetupRoutine`이 전부 없다.
  **호스트가 매치를 시작조차 하지 않는다**
- `FindFirstObjectByType<T>()`는 **비활성 오브젝트를 건너뛴다** → 내 부트스트랩들이 조용히 물러난다(로그조차 없음)

**해결:**
- `OnlineMatchStarter` / `OnlineGuestBoardAdapter`의 탐색을 **`FindObjectsInactive.Include`**로 바꿨다
- 온라인 세션으로 들어왔으면 `GameManage`를 **켠다**(`SetActive(true)` + `enabled = true`). 로그를 남긴다
- 게스트 어댑터는 `isActiveAndEnabled`를 확인하고, `ListenForBoardState`가 파이어베이스 초기화 전이라
  실패할 수 있으므로 try/catch로 조용히 재시도한다

> 로컬 봇전은 영향 없다. 세션이 없으면 `OnlineMatchStarter`가 아무것도 하지 않아 `GameManage`는 꺼진 채 남는다.

**검증:** Assembly-CSharp 오류 0. **미검증:** 이 수정 이후의 2인 재접속.

---

### [2026-08-17 후속 8] 게스트 화면 복원 어댑터 (F-4 6단계) ✅ 1차 완료

섹션 11 F-1 **#6** 착수. **서버 스크립트는 읽기만 했고 한 줄도 고치지 않았다.**

**문제:** 호스트는 `ServerGameManager`가 돌고 보드 UI가 그 이벤트를 구독해 그려진다.
그런데 **게스트에는 엔진도 `Player` 객체도 없다.** 받는 것은 `board_state` 스냅샷뿐이라
손패 한 장도 그릴 수 없었고, 조작 수단은 인스펙터의 `Test_*` 17개뿐이었다.

**해결:** `Assets/Scripts/InGameCard/OnlineGuestBoardAdapter.cs`(신규, 게스트 전용).
스냅샷을 읽어 **게스트 쪽에 `Player` 두 개를 흉내 내어 만들고**, 스냅샷이 바뀔 때마다
직전 상태와 비교(reconcile)해 `EventManager` 이벤트로 되쏜다.
**보드 UI(PlayerUIManager / EnemyVisualTester / CardBoardRegistry)는 한 줄도 고치지 않았다.**

| 항목 | 방식 |
| ---- | ---- |
| 스냅샷 수신 | `firebase_network.ListenForBoardState`를 **독자적으로 하나 더** 붙인다. 파이어베이스 `ValueChanged`는 핸들러 다중 등록이 되므로 `session_game_manage`의 구독과 공존한다 |
| ⚠️ 금지 | `ListenForEventDTO`는 부르면 안 된다 — 그쪽은 `StopListeningEvents()`로 **기존 리스너를 떼어 낸다** |
| 스레드 | 콜백에서는 큐에만 넣고 `Update()`에서 처리한다. 밀리면 **최신 스냅샷 하나만** 반영 |
| 카드 복원 | `BattleManager.CardData`로 DataId → `Clone()` 후 **`InstanceId`를 호스트 값으로 덮어쓴다.** 이게 같아야 카드 GO가 매칭된다 |
| 풀 생성 | 중간 접속 대비로 **모든 존의 카드를 일단 덱에 담아** `OnGameStart`를 쏘고, 곧바로 reconcile이 제자리로 옮긴다 |
| 되쏘는 이벤트 | `OnGameStart` / `OnCardMove`(존 변화) / `OnLifeChange` / `OnResourceChange` / `OnTurnStart` / `OnLogMessage`(페이즈) / `OnGameSet` |
| 플레이어 순서 | `OnGameStart(내 플레이어, 상대)` — `owner == p1`을 하단으로 보는 규칙(CharacterFieldUI·CardZoomPopupUI)에 맞춘다 |

**설계에서 조심한 것:** 첫 배치 시 `_zoneOf`에 시작 위치(덱/자원덱)를 **미리 기록해 둔다.**
이게 없으면 첫 reconcile이 "원래 어디 있었는지"를 몰라 자원덱 카드를 자원덱에 **중복 삽입**한다.
정체를 모르는 카드는 `RemoveFromAnyZone`으로 전 존을 훑어 빼낸 뒤 넣는다.

#### 게스트 입력 연결 (같은 어댑터, 2026-08-17 후속)

**로컬 사람이 쓰는 UI를 그대로 재사용한다.** 보드 UI는 `OnRequireSetPhaseAction` /
`OnRequireOpenPhaseAction`을 구독해 손패 선택과 [공개]/[폐기] 흐름을 띄우는데,
게스트에는 그 이벤트를 쏠 엔진이 없다. 그래서 **어댑터가 board_state의 페이즈 전환을 보고
같은 이벤트를 합성**하고, 콜백이 오면 엔진 대신 호스트로 전송한다.

| 페이즈 | 합성 이벤트 | 전송 |
| ------ | ---------- | ---- |
| SetPhase | `OnRequireSetPhaseAction(_mine, ctx, cb)` | `session_game_manage.SendSetPhaseChoice(instanceId, true)` |
| OpenPhase | `OnRequireOpenPhaseAction(_mine, setCard, cost, ctx, cb)` | `SendOpenPhaseChoice(instanceId, "Open"/"Abandon")` |

- 세트 잔상 미리보기·버튼·확대까지 **로컬과 완전히 같은 UI**가 동작한다
- 같은 턴·같은 페이즈에서 창이 두 번 뜨지 않도록 `(phase, turn)`으로 잠근다.
  오픈 시점에 세트 카드가 아직 스냅샷에 없으면 잠금을 풀어 다음 스냅샷에서 재시도한다
- **`OnCardSet`도 함께 쏜다** — `PlayerUIManager`가 이 이벤트로 확정 전 잔상을 지우므로,
  빠뜨리면 잔상이 화면에 남는다
- 오픈 선택은 세트 시점에 이미 정해져 있어(`pendingIsReveal`) 합성 즉시 응답이 돌아간다

**아직 안 되는 것 (다음 작업):**

- **카드 선택·예/아니오·스택 응답은 여전히 `Test_*` 경로다.** 이 셋은 `events` 노드로 오는
  *알림*이고, `session_game_manage`가 `pendingCardPickNotification` 등 **private 필드**에 담아 둔다.
  전송 API(`SubmitCardPickFromUI`, `SendPendingOptionalResponse`, `SendPendingStackResponse`)는
  이미 공개돼 있으니, **알림이 도착했음을 알 방법만 생기면 바로 연결된다.**
  → 서버 담당자에게 요청할 것: `public event Action<RequireCardPickNotification> OnCardPickRequested`
     같은 공개 훅 하나. (대안인 `events` 재구독은 `ChildAdded`가 과거 이벤트를 전부 재생해 위험하다)
  → 다만 이 셋은 용병 능력·서치 카드에서 나오는데, **용병 능력은 F-1 #3 때문에 온라인에서 아직
     발동하지 않으므로** 당장 막히지는 않는다
- 용병 슬롯은 비어 있다 — 호스트가 `Character1_ID`를 채우지 않는 F-1 #3이 먼저 풀려야 한다
- 실제 2인 접속 미검증 (에디터를 띄울 수 없었다)

**검증:** Assembly-CSharp 오류 0 / 엔진 빌드 오류 0 / `sync -Check` ok / 콘솔 봇 회귀 정상.

---

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
| **루트 `README.md`** | **프로젝트 소개 · 현재 진행 상황 · 역할별 남은 일 · 공통 규칙. 신규 합류자는 여기부터** |
| `Assets/TCG_Project/EVENTMANAGER_CONTRACT.md` | UI팀 이벤트 계약 명세 (2026-08-18 갱신 — 죽은 계약 2개·미발행 이벤트 3개·온라인 null 콜백 주의 표기) |
| `docs/개발_기록.md` | 구 README (날짜별 개발 일지 + 초기 UI 연동 가이드). **과거 기록이며 현재 코드와 다른 서술이 있다** |
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
4. ~~**F-1 #2·#3 덱·용병 주입**~~ → ✅ **완료(2026-08-18).** 섹션 5 참조 — 원래 내용: — `decks/{role}` 읽기 + 용병 2종을 세션에 싣고 `PlayerSetupData` 경유로 통일(원칙 6-1)
5. ~~**F-1 #5 타임아웃**~~ → ✅ **완료(2026-08-17).** `AllowUnlimitedHumanInput` 스위치로 해결.
   `WaitUntil` 5곳은 손대지 않아도 타임아웃 콜백이 대기를 풀어 준다. 섹션 5 참조
6. **F-1 #6 게스트 화면** → 🔶 **주요 완료(2026-08-17).** `OnlineGuestBoardAdapter`가 화면 복원 + **세트/오픈 입력**까지 담당한다.
   남은 것은 카드 선택·예/아니오·스택 응답(알림 훅 필요 — 섹션 5 참조)
7. F-2 안정성 (끊김·이벤트 청소·항복·타이머 표시)

### E. 기타

- `ServerSenderManager.cs`(128줄)는 어떤 씬·프리팹에도 배치되어 있지 않다 (사실상 dead)
- `TestGameScene`의 `LocalMatchStarter`는 **`mode: 1`(HumanVsBot), `autoStart: 1`** (2026-08-17 재확인).
  봇 vs 봇 관전으로 회귀 테스트를 하려면 인스펙터에서 **Mode를 BotVsBot으로** 바꾼다.
  온라인으로 들어오면 이 컴포넌트는 스스로 물러나므로 값과 무관하다
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
