# 전투! 용병의 시대 — 모바일 TCG

실물 보드게임 **"전투! 용병의 시대"**를 Unity 모바일 온라인 게임으로 구현하는 프로젝트입니다.

최종 갱신: 2026-09-03 · 현재 브랜치: `M2_milestone`

---

## 1. 이 프로젝트가 무엇인가

용병 2명을 골라 20장짜리 덱을 짜고, 6페이즈로 진행하며 상대의 라이프 5개를 먼저 0으로 만들면 이기는 카드 게임입니다.

**목표는 서버를 경유하는 실시간 온라인 1대1 단판제 대전입니다.**

### 구조 한눈에 보기

```
Assets/TCG_Project/          게임 로직 (엔진). Unity 없이도 콘솔에서 돌아간다
  Scripts/Core                카드·플레이어·존·게임 상태
  Scripts/Systems             6페이즈 해결, 데미지, 봇 AI, 데이터 로딩
  Scripts/Effects             카드 효과 (JSON 키워드 → 효과 객체)
  Scripts/Abilities           용병 고유 능력 4종
  Scripts/Managers            BattleManager(Unity 진입점) · EventManager(UI 계약)
  Data/*.json                 카드 40종 + 용병 4종 + 규칙값  ← 정본(SSOT)

Assets/Scripts/              Unity 표현 계층
  InGameCard/                 인게임 보드 UI (카드 GO 풀, 손패, 존, 팝업)
  BuildDeck/                  덱 빌더
  MainMenu/                   메뉴·덱 선택
  Server Scripts/             Firebase 온라인 대전 (별도 담당자)

TCG_Project/                 콘솔 실행 미러. 빌드 시 자동 동기화된다 (직접 편집 금지)
```

### 세 가지 실행 경로

| 실행 | 방법 | 용도 |
| ---- | ---- | ---- |
| 콘솔 (헤드리스) | `cd TCG_Project && dotnet run` | 엔진 회귀 테스트. 강화학습 확장의 기반 |
| Unity 로컬 | `TestGameScene` 재생 | 사람 vs 봇 / 봇 vs 봇 |
| Unity 온라인 | 메인 메뉴 → 랜덤 매칭 | 사람 vs 사람 (같은 씬이 세션 유무로 갈린다) |

---

## 2. 지금 어디까지 왔나

### 모드별 상태

| 모드 | 상태 |
| ---- | ---- |
| 봇 vs 봇 | ✅ 완성. 회귀 테스트 기준선으로 쓴다 |
| 사람 vs 봇 | ✅ 플레이 가능. 입력·표시·항복·결과까지 동작 |
| **사람 vs 사람 (온라인)** | 🔶 **한 판이 끝까지 진행된다.** 아래 제약이 남아 있다 |

### 온라인 대전 계층별

| 계층 | 상태 |
| ---- | ---- |
| 전송·프로토콜 (Firebase) | ✅ 실전 검증됨 |
| 호스트 진행(엔진) · 화면 | ✅ 동작 |
| 게스트 화면 | ✅ 보드 미러링·진행 로그·용병 슬롯 (`OnlineGuestBoardAdapter`) |
| 게스트 입력 | ✅ 세트·오픈·카드 선택·예/아니오·스택 응답 모두 연결됨 |
| 덱·용병 주입 | ✅ 고른 덱과 용병이 그대로 올라간다 (`session_manage` → `UploadDeck` → `AssignCharacters`) |
| 끊김·재접속 | 🔶 이탈 감지·방 정리·20초 시작 감시까지. **재접속은 없다** |

### 지금 상태에서 "정상인데 이상해 보이는 것"

- 게스트의 진행 로그는 호스트와 내용이 다르다 (호스트 엔진 로그를 중계하지 않는다)
- **한 판으로 끝난다** — 단판제가 기획 결정이다
- 게스트 화면은 호스트보다 **한 박자 늦게, 뭉텅이로** 움직인다 —
  스냅샷이 네트워크 간격으로 오기 때문이다. `OnlineGuestBoardAdapter`가 이동 사이에
  간격(`moveIntervalSeconds`)을 두어 호스트와 비슷한 리듬을 만든다
- 새 덱을 만들면 카드 목록이 **비어 보인다.** 용병을 먼저 골라야 그 용병 카드가 나타난다

---

## 3. 남은 일 (역할별)

### 서버 담당

1. `LogNotification` 발행 — 게스트에게 엔진 로그 중계 (지금 게스트 로그는 어댑터가 따로 만든다)
2. 항복 DTO
3. **재접속** — 지금은 끊기면 그대로 끝난다
4. **인증** — 아래 "공통·출시 전" 참조. 서버 쪽에서 가장 급한 항목이다

> 📌 **서버 파일이 여러 번 수정됐습니다. 담당자 확인이 필요합니다.**
>
> | 파일 | 무엇 |
> | ---- | ---- |
> | `ServerGameManager` | 단판제 · `OnGameStart`를 시작 드로우 **앞으로** 이동 |
> | `session_manage` | 덱·용병 업로드 경로 · `IsHost` 접근자 · 20초 시작 감시(`WatchReadyStart`) |
> | `session_game_manage` | 덱·용병 주입 · 게스트 입력 배선 · **연출 재생을 어댑터에 양보**(`IsDrivingBoard`) |
> | `firebase_network` | `onDisconnect` 기반 방 정리 (`ArmDisconnectCleanup` 등) |
> | `EventService` | `board_state`에 용병 ID 기입 |

### 클라이언트·UI 담당

1. **안드로이드 실기 확인** — 가로 고정·세이프에어리어·터치 경로까지 코드는 들어갔으나 기기 검증 전이다
2. `Canvas/Buttons`가 UI 오브젝트가 아니다 (일반 `Transform`) — 16:9가 아닌 화면에서 어긋날 수 있다.
   `HANDOFF.md` 2026-09-03 항목 참조
3. `Sync`의 낭비 — 카드가 한 장 움직일 때마다 그 존의 카드를 전부 다시 바인딩한다.
   데스크톱에서는 안 보이지만 **모바일에서는 다르다**
4. 자원존 가시화 (현재 의도적 미구현)
5. 예/아니오 다이얼로그 사양 확정 — 지금은 카드 선택 패널을 재사용한 스톱갭

### 로직 담당

1. 콘솔 실행 환경 정리 — `.csproj`에 `<RollForward>LatestMajor</RollForward>` 추가 또는 TFM 변경.
   지금은 환경변수 없이 `dotnet run`이 실패한다 (**강화학습 확장의 선행 조건**). **아직 그대로다**
2. 죽은 계약 2개 정리 — `OnRequireStackResponse` / `OnRequireCardChoice` (발행처 0곳)

### 공통 · 출시 전

| 항목 | 내용 |
| ---- | ---- |
| **Firebase 인증** | 현재 **인증이 전혀 없다.** DB가 전면 공개 상태이고, 테스트 규칙이면 만료 시 접속이 통째로 끊긴다. **가장 급하다** |
| **임시 설정 되돌리기** | `choose_wait_time` 2분(원래 10초), `OnlineMatchStarter.UnlimitedInputForTesting = true` |
| 안드로이드 실기 | 코드는 준비됨(가로 고정 · `SafeAreaFitter` · 터치 탭 경로 · CanvasScaler 1920×1080). **기기 검증만 남았다** |
| ~~Android 패키지명~~ | ✅ 해결 — `applicationIdentifier.Android: com.Ttakji.server`가 `google-services.json`과 일치한다 |

---

## 4. 모두가 알아야 할 규칙

### 4-1. 편집 위치 — 사본이 3벌이다

| 위치 | 역할 | 편집 |
| ---- | ---- | ---- |
| `Assets/TCG_Project/` | **정본(SSOT)** | ✅ 여기만 편집 |
| `TCG_Project/` | 콘솔 미러 | ❌ 빌드 시 덮어써짐 |
| `Assets/Resources/GameData/*.json` | Unity 런타임용 | ❌ 동기화 대상 |

`tools/sync-tcg-project.ps1`이 정본 → 나머지로 복사하며, 콘솔 빌드 시 자동 실행됩니다.
검증: `powershell -File tools/sync-tcg-project.ps1 -Check`

### 4-2. 로직 레이어에 `using UnityEngine` 금지

`Core / Systems / Effects / Conditions / Abilities / Interfaces / Utils`는 Unity에 의존하지 않습니다.
예외는 정확히 2개(`BattleManager.cs`, `UnityResourceLoader.cs`)이며, 둘 다 콘솔 빌드에서 제외돼 있습니다.

이건 코드 위생이 아니라 **강화학습 확장의 전제 조건**입니다. 헤드리스로 고속 반복 실행이 가능해야 합니다.

### 4-3. UI는 이벤트만 구독한다

게임 상태를 UI에서 직접 고치지 마세요. `EventManager` 이벤트를 구독해 화면에 반영만 합니다.
구독은 `OnEnable`에서 `+=`, `OnDisable`에서 `-=` (누수 방지).

**이벤트 시그니처는 팀 간 계약입니다.** 바꾸면 구독 코드가 일제히 깨지므로 공지 후 진행하고,
`EVENTMANAGER_CONTRACT.md`를 함께 갱신하세요.

> ⚠️ **엔진 이벤트를 흉내 내는 코드는 "골라 쏘면" 안 됩니다.** 같은 존 전이에는 같은 세트를 전부 발행하세요.
> 실제로 `OnCardMove`만 쏘고 `OnCardStacked`를 빠뜨려 게스트 화면의 스택존이 깨진 적이 있습니다.

### 4-4. 소스는 UTF-8(BOM 없음)

`.editorconfig`가 선언합니다. CP949로 저장하면 한글이 전부 깨지고, 실제로 깨진 에러 메시지가
덱 빌딩 팝업에 그대로 노출된 적이 있습니다.

### 4-5. 지우면 안 되는 것들

| 대상 | 이유 |
| ---- | ---- |
| 콘솔 빌드(`ConsoleRunner`, `TCG_Project.csproj`) | 강화학습 확장 기반 |
| `OptionalActionEffect` | 앞으로 추가할 **용병 특수 기믹용**. 쓰는 카드가 0장이라고 dead code가 아니다 |
| `DeckValidator`의 주석 처리된 "10종류 × 2장" 검증 | 팀 협의 중. 현재 1장도 허용하는 방침 |
| `BattleManager`의 `p1TestIds` 주석 블록 | 카드 조합 충돌 재현용 QA 자료 |
| 단판제(`Bot_single_game: 1`) | 룰북은 3판 2선승이지만 **기획상 단판제 유지** |

### 4-6. 변경 후 확인 절차

```bash
dotnet build ./TCG_Project/TCG_Project.csproj        # 엔진
dotnet build ./Assembly-CSharp.csproj                # Unity 스크립트
powershell -File tools/sync-tcg-project.ps1 -Check   # 사본 동기화
cd TCG_Project && DOTNET_ROLL_FORWARD=LatestMajor dotnet run   # 봇 회귀 (★ MATCH SET ★ 확인)
```

---

## 5. 문서 지도

| 문서 | 내용 |
| ---- | ---- |
| **`Assets/TCG_Project/HANDOFF.md`** | **가장 중요.** 상세 인수인계 — 아키텍처 원칙, 작업 이력, 알려진 버그, 남은 작업 |
| `Assets/TCG_Project/EVENTMANAGER_CONTRACT.md` | 이벤트 계약 정본 (UI 팀 온보딩) |
| `Assets/TCG_Project/CardManual.md` | 카드 40종 상세 |
| `Assets/TCG_Project/PROJECT_DIAGNOSIS.md` | 2026-03 시점 로직팀 진단서 (과거 기록) |
| `Assets/TCG_Project/EFFECT_SYSTEM_PROPOSAL.md` | Effect 통합 설계 제안서 (과거 기록) |
| `BattleManager-ServerGameManager-FunctionList.md` | 두 매니저의 함수 전수 목록 |
| `docs/개발_기록.md` | 구 README — 날짜별 개발 일지 + 초기 UI 연동 가이드 (과거 기록) |

새로 합류하셨다면 **이 README → `HANDOFF.md` 섹션 1~2(개요·원칙) → 담당 영역** 순서를 권합니다.
