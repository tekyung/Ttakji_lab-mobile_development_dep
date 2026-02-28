# 📡 TCG 시스템 연동 API 및 공유 사항 가이드 (UI & 클라이언트 담당자용)

본 TCG 프로젝트는 코어 로직(Backend)과 뷰(Unity UI)가 완벽히 분리된 **이벤트 주도형(Event-Driven) 아키텍처**를 사용합니다. 유니티 클라이언트 담당자는 코어 시스템을 직접 수정할 필요 없이, `EventManager`의 신호를 **구독(Subscribe)**하여 UI 갱신 및 애니메이션 연출을 구현하면 됩니다.

---

## 🛠 1. 이벤트 구독 및 해지 방법 (메모리 누수 방지 필수)

유니티 스크립트에서 이벤트를 연결할 때는 반드시 `OnEnable`에서 구독(`+=`)하고, `OnDisable`에서 해지(`-=`)해야 합니다. 이를 지키지 않으면 씬 전환 시 치명적인 에러와 메모리 누수가 발생합니다.

```csharp
private void OnEnable()
{
    EventManager.OnManaChange += UpdateManaUI;
    EventManager.OnUnitSummoned += PlaySummonEffect;
}

private void OnDisable()
{
    EventManager.OnManaChange -= UpdateManaUI;
    EventManager.OnUnitSummoned -= PlaySummonEffect;
}

// 연결된 함수 구현
private void UpdateManaUI(Player player, int currentMana)
{
    if (player.Name == "Player1") manaText.text = currentMana.ToString();
}

private void PlaySummonEffect(Card summonedCard)
{
    // 소환 파티클 재생 로직
}
```

## 📜 2. 전체 이벤트 목록 (Event List)

[1] 턴 진행 및 페이즈 (Phase)

| 이벤트 명 | 전달 데이터 | 발동 시점 |
| :--- | :--- | :--- |
| OnGameStart | "Player (P1), Player (P2)" | 게임이 최초 시작될 때 |
| OnGameSet | Player (승리자) | 누군가 승리 조건을 달성해 게임이 끝났을 때 |
| OnGameDraw | "Player (P1), Player (P2), int (종료 턴)" | 제한 턴을 초과하여 무승부 처리되었을 때 |
| OnTurnStart / OnTurnEnd | "int (현재 턴수), string (진행자 이름)" | 새로운 턴이 시작/종료될 때 |
| OnDrawPhase / OnMainPhase  OnBattlePhase / OnEndPhase | "string (진행자 이름), int (현재 턴수)" | 각 페이즈 진입 시 |

[2] 플레이어 상태 (HUD 연동)
참고: 플레이어의 HP 개념이 삭제되고 승점(Prize) 룰로 변경되었습니다.
| 이벤트 명 | 전달 데이터 | 발동 시점 |
| :--- | :--- | :--- |
| OnManaChange | Player (대상), int (현재 마나) | 마나를 소모하거나 회복했을 때 |
| OnPrizeChange | Player (대상), int (현재 승점) | 유닛을 파괴하여 승점을 획득했을 때 |

[3] 전투 및 카드 연출 (VFX/Animation)
| 이벤트 명 | 전달 데이터 | 발동 시점 |
| :--- | :--- | :--- |
| OnPlayCard | "Player (사용자), Card (사용된 카드)" | 패에서 카드를 필드로 냈을 때 |
| OnUnitSummoned | Card (소환된 유닛) | 유닛이 필드에 정상적으로 배치되었을 때 |
| OnAttack | "Card (공격 유닛), Card (타겟 유닛)" | 유닛이 타겟을 향해 공격을 시도할 때 (돌진 연출) |
| OnUnitTakeDamage | "Card (맞은 유닛), int (데미지량)" | 유닛이 데미지를 입었을 때 (피격/데미지 폰트 연출) |
| OnUnitDeath | Card (죽은 유닛) | 유닛이 파괴되었을 때 (사망 파티클/카드 파괴 연출) |
| UnitStatusChange | "Card (대상 유닛), string (상태)" | "지침, 기절 등 유닛의 상태 이상이 적용되었을 때" |
| OnCardPowerChanged | "Card (대상 유닛), int (변화량)" | 버프/디버프로 인해 파워가 변경되었을 때 (+/- 기호 포함 가능) |

[4] 카드 단순 이동
| 이벤트 명 | 전달 데이터 | 발동 시점 |
| :--- | :--- | :--- |
| OnCardDraw | "Card, Player, ZoneType" | 카드를 단순 드로우 할 때(패로 이동) |
| OnCardMove | "Card, Player(출발), ZoneType(출발), Player(도착), ZoneType(도착)" | 필드->묘지 등 카드의 구역이 변경될 때 |


## 🚨 3. [중요] 비동기 유저 입력 처리 (Human Input)

코어 시스템이 봇(Bot)의 자동 연산에서 사람의 조작을 기다리는 비동기 시스템으로 진화했습니다. 사용자가 카드를 조작하거나 타겟을 지정해야 할 때 아래 이벤트가 호출됩니다.

UI에서는 반드시 유저 입력이 끝난 뒤 매개변수로 넘어온 Action 콜백을 호출해야 합니다. 호출하지 않으면 게임 루프가 영원히 정지됩니다.

| 이벤트 명 | 설명 및 UI 처리 지침 |
| :--- | :--- |
| OnRequireMainPhaseAction | "유저에게 턴 통제권이 넘어왔습니다. 패의 카드를 드래그해서 내거나, [턴 종료] 버튼을 누를 수 있게 UI를 활성화하세요. 액션이 끝나면 onComplete()를 호출하세요." |
| OnRequireBattlePhaseAction | "배틀 페이즈입니다. 아군 유닛을 적에게 드래그해 공격하게 하거나, [배틀 종료] 버튼을 활성화하세요. 끝나면 onComplete()를 호출하세요." |
| OnRequireTargetSelection | 파이어볼 같은 타겟팅 스펠을 썼을 때 발동합니다. 매개변수로 넘어온 candidates 리스트에 있는 카드들의 테두리를 붉게 빛나게 처리하세요. 유저가 그중 하나를 클릭하면 onTargetSelected(선택된타겟리스트)를 호출하여 백엔드에 알려주세요. |


## 🛡️ 4. 덱 구축 시 리미트 레귤레이션 검증 방법

유저가 로비 화면에서 자신만의 덱을 짰을 때, 게임 시작 전 해당 덱이 룰(최소/최대 장수 및 카드별 최대 포함 매수)에 어긋나지 않는지 검사해야 합니다. 직접 계산할 필요 없이 DeckValidator 유틸리티를 사용하세요.

```
using TCG_Project.Scripts.Utils;

// 유저가 구성한 List<Card> 객체
List<Card> customDeck = GetPlayerDeck(); 

// 덱이 규칙을 위반했는지 검사
if (DeckValidator.IsValidDeck(customDeck, out string errorMsg))
{
    // 검사 통과 -> 서버에 저장 또는 게임 시작
    StartGame(customDeck);
}
else
{
    // 검사 실패 -> UI 팝업으로 사용자에게 안내
    ShowWarningPopup(errorMsg); 
    // 예: " '레드슬라임' 카드는 덱에 최대 2장까지만 넣을 수 있습니다. "
}
```


## 📁 5. 데이터 로드 폴백(Fallback) 시스템 안내

게임의 근간이 되는 JSON 데이터(.json)는 유니티 프로젝트 내 Assets/Resources/GameData 폴더에서 1차로 읽어옵니다.

만약 유니티 환경이 아니거나 해당 경로에 폴더가 없다면, 시스템이 자동으로 실행 파일 위치의 ./Data 폴더를 2차로 탐색하여 로드합니다. 경로 에러로 인한 팅김을 방지하기 위함입니다.



# 개발 진행 사항 기록

## 26.01.30 진행사항

Card // Effect 간 분리, Card는 Effect의 id를 호출

Cards.json 에서 각종 수식 파싱 후, Eval 하게 하여 게임 내 변수 활용

Rules.json 으로 게임 내 세부 수치 관리

각 카드별 발동 조건 설정 -> 사용 가능 여부 판별

카드의 효과 분기 설정 -> 한 카드가 다른 타입의 효과를 2개 이상 포함 가능


## 26.01.31 진행사항

Targeting 시스템 구현(N개의 데이터베이스에서 M개 지정)

MoveCardEffect(카드 이동 효과)으로 각종 카드 이동 효과 구현 및 대체

ModifyCardEffect(카드 수정 효과)으로 각종 카드 스탯 수정 효과 구현

현재 과도기로 사용하지 않는 Effect.cs 들이 혼재되어 있음. 추후 정리 필요.


## 26.02.10 진행사항

유닛의 소환 시 효과 발동 조건을 json에 기입

애벌레(1 드로우) : "on_play_condition_type": "DeckNotEmpty",

폭탄벌(1 파괴) : "on_play_condition_type": "EnemyUnitExist"

픽시드래곤(200 너프) : "on_play_condition_type": "EnemyUnitExist"

변경된 룰로 시뮬레이션 테스트 성공

최신화된 json사용 중, 이전 버전 사용 시 오류 발생


## 26.02.15 진행사항

unity에 사용될 이벤트 로직 초안 구성 (노트북 사양 문제로 테스트 못함_아직 작동 안될 수 있음)

데이터 파일 경로 수정했으나 추후 재검 필요

룰 파일 CommonConfig.json 로딩 구현 예정 (현재 임시로 고정값 사용 중)

당장은 이벤트 로그와 콘솔 로그를 동시 송출하게 구성, 진도에 맞춰 콘솔은 차차 없앨 예정


## 26.02.27 진행사항

### ✅ 1. 완료된 핵심 시스템
페이즈 체인 & 비동기 엔진: 코루틴과 콜백(Action) 기반으로 턴 흐름이 재설계되어, 카드 효과 처리가 끝날 때까지 시스템이 스스로 대기(WaitUntil)합니다.

아키텍처 분리: 카드의 '소환 조건(PlayCondition)'과 '효과 발동 조건(EffectCondition)'이 완벽히 분리되어 동작합니다.

안전한 스탯 파이프라인: 모든 스탯 변동과 파괴 로직이 ModifyPower라는 단일 창구로 통합됩니다.

디스플레이 디커플링: 코어 로직은 EventManager로 신호와 Rich Text(색상 태그)만 던지며, 유니티와 콘솔 양쪽에서 완벽히 호환됩니다.


### ⚠️ 2. 유니티 팀 인계 전/후 확인 사항 (TODO)
[ ] JSON 데이터 최신화 (가장 중요)

사용자의 직접 선택(HumanChoice), 가장 높은 공격력(HighestPower), 랜덤(Random) 의 타겟팅 모드를 지원하기 위해 CardEffectData 클래스에 target_mode 필드가 추가되었습니다.
코드는 target_mode을 파싱하는데 기본값으로 "HighestPower"를 사용하도록 되어 있습니다. (즉, JSON에 target_mode이 명시되지 않으면 자동으로 가장 높은 공격력을 가진 적을 타겟으로 삼습니다.)


[ ] 수동 타겟팅(HumanChoice) 임시 봉인 (Soft-Lock 주의)

현재 백엔드는 target_mode가 Manual일 때 UI의 선택을 무한정 기다리도록 설계되었습니다.

그래서 유니티 UI 쪽에 타겟을 클릭해서 콜백을 돌려주는 로직이 필요합니다.

조치: 당장은 JSON 데이터의 target_mode를 일괄적으로 HighestPower로 설정해 두어 게임이 멈추지 않게 했습니다.


[ ] 유니티 리소스 경로(Path) 검증

1차적으로 유니티 프로젝트 내의 폴더(Assets/Resources/GameData)가 존재하고 JSON 파일들이 그 안에 들어있는지 점검합니다.
없다면 2차로 이 폴더의 Data 폴더의 백업 파일을 사용합니다. (로그 출력으로 어느 경로의 데이터인지 알 수 있습니다.)

[ ] UI의 이벤트 메모리 누수(Leak) 방지

유니티에서 EventManager를 구독하여 연출을 만들 때, 반드시 유니티의 OnEnable()에서 +=로 구독하고, OnDisable()이나 OnDestroy()에서 -=로 해지하도록 해야 합니다. (이거 안 하면 씬 전환 시 100% 에러가 터집니다.)


## 26.02.28 진행사항

LegacyEffect 제거 및 코드 정리

파일 로딩 시스템 롤백

MoveCardEffect 역할 분리: 단발성 카드 이동 효과는 MoveCardEffect로, 조건부 기간 이동은 RevertControllerEffect로 분리

RevertStatusEffect 추가: 유닛의 상태 이상을 일정 턴 후 자동으로 해제하는 효과 구현 (구현 중)

SwapCardEffect 추가: 카드의 위치를 동시에 서로 바꾸는 효과 구현 (복원 중)
