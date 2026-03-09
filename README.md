---

# 🎮 TCG 프로젝트: Core 로직 연동 가이드 (UI 개발팀용)

TCG 코어 로직과 UI 연출을 연결하기 위한 가이드라인입니다.
우리 게임은 코어 엔진과 화면 연출이 완벽하게 분리된 **이벤트 기반 아키텍처(Event-Driven Architecture)**를 사용합니다.

UI 팀은 게임의 상태(HP, 덱의 남은 장수 등)를 직접 수정해서는 안 되며, 오직 `EventManager`가 쏘아주는 방송(Event)을 구독(Subscribe)하여 애니메이션과 이펙트를 재생하는 역할만 담당합니다.

---

## 📌 1. 이벤트 구독 및 해제 기본 수칙 (중요)

유니티에서 이벤트를 다룰 때 가장 중요한 것은 **메모리 누수(Memory Leak) 방지**입니다.
이벤트를 구독할 때는 반드시 `OnEnable`에서 등록(`+=`)하고, `OnDisable`이나 `OnDestroy`에서 해제(`-=`)해야 합니다.

```csharp
using UnityEngine;
using TCG_Project.Scripts.Managers;

public class BattleUIManager : MonoBehaviour
{
    private void OnEnable()
    {
        // 구독: 방송국 주파수를 맞춥니다.
        EventManager.OnLifeChange += UpdateHealthBar;
        EventManager.OnCardMove += AnimateCardFly;
    }

    private void OnDisable()
    {
        // 해제: 오브젝트가 꺼지거나 파괴될 때 반드시 주파수를 끊어야 합니다! (누수 방지)
        EventManager.OnLifeChange -= UpdateHealthBar;
        EventManager.OnCardMove -= AnimateCardFly;
    }

    // 실제 연출 함수들
    private void UpdateHealthBar(Player player, int currentLife) 
    { 
        /* 체력바 게이지가 줄어드는 애니메이션 재생 */ 
    }

    private void AnimateCardFly(Card card, Player fromPlayer, ZoneType fromZone, Player toPlayer, ZoneType toZone) 
    { 
        /* 카드가 A 위치에서 B 위치로 스르륵 날아가는 연출 */ 
    }
}

```

---

## 📡 2. 이벤트 리스트 (Event Dictionary)

UI 연출을 위해 `EventManager`에서 제공하는 핵심 이벤트 목록입니다. 용도에 맞게 구독하여 사용하세요.

### ⚔️ A. 게임 흐름 및 상태 변경

| 이벤트명 | 파라미터 (전달받는 값) | 호출 시점 및 UI 연출 권장 사항 |
| --- | --- | --- |
| `OnGameStart` | `Player p1, Player p2` | 게임 시작. 양측 플레이어 프로필 스폰, 초기 체력바/덱 UI 세팅 |
| `OnTurnStart` | `int turn, string subject` | 매 라운드 시작. 화면 중앙에 "ROUND 1" 텍스트 애니메이션 |
| `OnGameSet` | `Player winner` | 누군가의 HP가 0이 되어 게임 종료. 승리/패배 결과창 팝업 |
| `OnGameDraw` | `Player p1, Player p2, int turn` | 동시 타격 등으로 무승부 처리 시 호출. 무승부 연출 |
| `OnLifeChange` | `Player p, int newValue` | 체력 변동 시 호출. HP바 애니메이션 및 피격 화면 이펙트 재생 |

### 🎴 B. 카드 이동 및 액션 (가장 중요)

카드가 화면에서 움직이거나 효과가 터질 때 호출됩니다.
| 이벤트명 | 파라미터 (전달받는 값) | 호출 시점 및 UI 연출 권장 사항 |
|---|---|---|
| `OnCardMove` | `Card c, Player p1, Zone z1, Player p2, Zone z2` | 카드가 존을 이동할 때. (예: 세트존 -> 전장존). 궤적 이동 애니메이션 |
| `OnCardDraw` | `Card c, Player p, Zone z` | 덱에서 카드를 뽑을 때 호출. 덱에서 카드가 튀어나와 패로 들어가는 연출 |
| `OnPlayCard` | `Player p, Card c` | 메인 페이즈나 스택 반응으로 카드가 '발동'될 때. 카드 일러스트 컷인 및 타격 이펙트 |

### 🔄 C. 페이즈 전환 알림

화면 상단의 "현재 페이즈 UI"를 빛나게 하거나 갱신할 때 사용합니다.
| 이벤트명 | 파라미터 (전달받는 값) | 호출 시점 및 UI 연출 권장 사항 |
|---|---|---|
| `OnResourcePhase` | `string subject, int turn` | 자원 페이즈 시작 시. 자원 코인/에너지 UI 갱신 준비 |
| `OnDrawPhase` | `string subject, int turn` | 드로우 페이즈 시작 시 |
| `OnSetPhase` | `string subject, int turn` | 세트 페이즈 시작 시 |
| `OnOpenPhase` | `string subject, int turn` | 오픈 페이즈 시작 시 |
| `OnMainPhase` | `string subject, int turn` | 메인 페이즈 시작 시. 전투 시작 연출 (VS 마크 등) |

---

## ✋ 3. 플레이어 입력 처리 (Human Input)

코어 엔진은 봇(Bot)의 행동은 스스로 결정하지만, **사람(Human)**의 차례가 오면 UI 쪽에 **"유저가 버튼을 누를 때까지 기다릴게!"** 라며 콜백(Callback)을 던져줍니다.
UI 팀은 해당 이벤트를 구독하여 화면에 버튼을 띄우고, 유저가 선택을 마치면 **반드시 콜백 함수를 실행(`Invoke`)하여 엔진에 답을 돌려줘야 합니다.** (돌려주지 않으면 게임이 멈춥니다!)

| 이벤트명 | 파라미터 (전달받는 값) | 호출 시점 및 UI 연출 권장 사항 |
| --- | --- | --- |
| `OnRequireSetPhaseAction` | `Player, GameContext, Action<Card>` | **세트 페이즈:** 내 패를 클릭할 수 있게 활성화. 유저가 카드를 고르면 `콜백(고른카드)` 호출 |
| `OnRequireOpenPhaseAction` | `Player, Card, int, GameContext, Action<OpenPhaseChoice>` | **오픈 페이즈:** '공개' / '폐기' 2개 버튼 UI 팝업. 선택 시 `콜백(OpenPhaseChoice.Open 또는 Abandon)` 호출 |
| `OnRequireStackResponse` | `Player, Card(내카드), Card(상대카드), Action<bool>` | **스택 발동:** 상대가 날 때렸을 때! 내 스택 카드를 발동할지 '예/아니오' 버튼 팝업. 선택 시 `콜백(true/false)` 호출 |

### 📝 입력 처리 구현 예시 (오픈 페이즈)

```csharp
private void OnEnable()
{
    EventManager.OnRequireOpenPhaseAction += ShowOpenAbandonUI;
}

private void ShowOpenAbandonUI(Player p, Card c, int cost, GameContext ctx, Action<OpenPhaseChoice> callback)
{
    // 1. 화면에 [공개(비용: cost)] / [폐기] 버튼 패널을 띄웁니다.
    uiPanel.SetActive(true);

    // 2. 버튼에 임시로 이벤트를 달아줍니다. (유저 클릭 대기)
    btnOpen.onClick.AddListener(() => 
    {
        uiPanel.SetActive(false);
        callback.Invoke(OpenPhaseChoice.Open); // ★ 코어 엔진으로 대답 전송! (게임 진행 재개)
    });

    btnAbandon.onClick.AddListener(() => 
    {
        uiPanel.SetActive(false);
        callback.Invoke(OpenPhaseChoice.Abandon); // ★ 코어 엔진으로 대답 전송!
    });
}

```

---

## ⚠️ 4. 기타 주의사항 (Caveats)

1. **절대 데이터 직접 수정 금지:** UI 스크립트에서 `Player.LifeTokens -= 1` 처럼 코어 데이터를 직접 조작하지 마세요. 모든 로직 연산은 이미 엔진이 끝마친 상태입니다. UI는 전달받은 값(`newValue`)을 화면의 텍스트 컴포넌트에 반영하기만 하면 됩니다.
2. **연출 딜레이 (코루틴 대기):** 로직은 눈 깜짝할 새에 연산되지만 시각적 연출은 시간이 필요합니다. `BattleManager.cs` 내부에 페이즈 전환이나 카드 사용 시 `ActionDelay` 셋팅값이 있으니, UI 애니메이션 시간에 맞춰 유니티 인스펙터에서 이 시간을 조절해 주세요.
3. **이펙트 종류 확인:**
`OnPlayCard` 가 호출될 때, 해당 카드의 `Type` 이나 이름(`Name`)을 읽어서 공격 카드면 총알 이펙트, 방어 카드면 방패 이펙트 등 어셋을 분기하여 스폰하시면 됩니다.


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


## 26.03.09 진행사항

이전 버전 코드 완전 제거 및 리펙토링 (전투! 용병의 시대 룰로 재이식)

현재까지 발매된 카드 4+40장 모두 구현 완료 (검증은 공격, 방어만 완료)

EventManager 설명서 업데이트

DeckValidator.cs 새 룰에 맞춰 업데이트 (덱 유효성 검사)

Scripts/UI 폴더 안에 유니티 전용 임시 파일들 생성. 덮어써도 무방함.