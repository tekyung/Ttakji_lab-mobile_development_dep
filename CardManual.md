***

# 📖 RulebookCards.json 카드 기획 및 추가 가이드

본 문서는 TCG 엔진의 핵심 데이터인 `RulebookCards.json` 파일에 새로운 카드를 추가하거나 기존 카드를 수정하기 위한 기획자용 매뉴얼입니다. 개발 지식이 없어도 정해진 규칙(레고 블록)에 맞춰 옵션들을 조립하면 게임 내에 즉시 카드가 구현됩니다.

---

## 1. 파일의 기본 구조
`RulebookCards.json` 파일은 `"Card"`라는 큰 상자 안에 여러 개의 카드 정보(블록 `{ }`)가 나열된 형태입니다. 새로운 카드를 추가할 때는 기존 카드의 `{ ... }` 블록을 복사해서 맨 아래에 붙여넣고 내용을 수정하는 것이 가장 안전합니다.

```json
{
  "Card": [
    {
      "id": "ELLI-02", 
      "characterId": "ELLIE", 
      "name": "퀵 드로우",
      // ... (카드 속성들) ...
      "effects": [
        // ... (이 카드가 발동할 효과들) ...
      ]
    },
    {
       // 다음 카드 정보...
    }
  ]
}
```
*(⚠️ 주의: 카드 블록과 블록 사이에는 반드시 쉼표(`,`)가 있어야 하며, 맨 마지막 카드 뒤에는 쉼표가 없어야 합니다!)*

---

## 2. 카드 기본 속성 (머리말)
카드 한 장을 정의하는 기본 스펙입니다. 따옴표(`""`)와 대소문자, 쉼표(`,`) 규칙을 엄격히 지켜주세요.

| 속성명 | 타입 | 설명 및 작성 예시 |
| :--- | :--- | :--- |
| `"id"` | 문자열 | 카드의 고유 식별자. **절대 중복되면 안 됩니다.** (예: `"ELLI-02"`) |
| `"characterId"` | 문자열 | 이 카드를 소유한 캐릭터. (예: `"ELLIE"`, `"VERONICA"`) |
| `"name"` | 문자열 | 게임 내에 표시될 카드 이름. (예: `"미니건 난사"`) |
| `"type"` | 문자열 | 카드의 종류. `"Attack"`, `"Defense"`, `"Support"` 중 택 1 |
| `"speed"` | 숫자 | 카드의 스피드(발동 우선순위). `1` (가장 빠름) ~ `3` (가장 느림) |
| `"cost"` | 숫자 | 사용 시 지불할 자원의 개수. (예: `2`) |
| `"isStack"` | 불리언 | 스택존에 깔리는 방어/스택 카드인가? (`true` / `false`) |
| `"isBattlefield"` | 불리언 | 전장존에 깔리는 카드인가? (`true` / `false`) |
| `"cannotBePlayedByEffect"` | 불리언 | (선택) 다른 카드 효과로 간접 발동할 수 없는 카드인가? (예: 신재생에너지. 기본값 `false`) |
| `"description"` | 문자열 | UI에 표시될 카드 설명. 엔진은 이 텍스트를 읽지 않으므로 자유롭게 작성하세요. |

---

## 3. 효과(Effects) 조립 가이드
카드의 심장부인 `"effects"` 배열입니다. 한 카드에 여러 효과를 순서대로 넣을 수 있습니다.

### 🌟 핵심 규칙 1: "그 후," (조건부 연계 발동)
카드 설명에 **"A를 한다. 그 후 B를 한다."** 라고 적혀있다면, A를 실패했을 때 B가 발동하면 안 됩니다.
이때 B 효과에 `"requirePreviousSuccess": true` 를 추가해 주면 엔진이 알아서 A의 성공 여부를 검사합니다.

### 🌟 핵심 규칙 2: 스택 카드의 효과 분리
스택 카드를 낼 때 즉시 발동하는 효과(예: 패 버리기)와, 스택존에 올라가서 대기하는 효과(예: 아머 획득)를 구분해야 합니다. 스택존에 대기할 효과에만 `"isStackAction": true`를 적어주세요.

```json
// [스택] 자신은 [슈퍼아머 1]을 획득한다. 자신의 패에서 1장을 랜덤으로 버린다.
"effects": [
  { "type": "SuperArmorEffect", "amount": 1, "duration": "Stack", "isStackAction": true },
  { "type": "DiscardFromHandEffect", "count": 1, "mode": "random", "isStackAction": false }
]
```

---

## 4. 효과(Effect) 타입 사전
기획 의도에 맞춰 아래의 타입들을 `"type": "..."` 에 적어주세요.

### ⚔️ 데미지 계열
* **`"DamageEffect"`**: 일반 데미지
  * 필요 파라미터: `"amount": 2` (데미지 수치)
* **`"PiercingDamageEffect"`**: 방어막을 무시하는 관통 데미지
  * 필요 파라미터: `"amount": 2`
* **`"MultiHitDamageEffect"`**: 다단 히트 데미지
  * 필요 파라미터: `"amount": 1`, `"times": 3` (1데미지를 3번)
* **`"SelfDamageEffect"`**: 자신에게 주는 데미지 (자해, 코인 잃음)
  * 필요 파라미터: `"amount": 1`

### 🛡️ 버프 및 방어 계열 (Duration 설정 필수!)
*※ `duration` (지속 시간) 옵션: `"ThisTurn"`(이번 턴), `"NextTurn"`(다음 턴), `"Stack"`(스택존 1회성)*
* **`"ArmorEffect"`**: 일반 아머 획득
  * 필요 파라미터: `"amount": 1`, `"duration": "ThisTurn"`
* **`"SuperArmorEffect"`**: 관통 공격도 일부 막는 슈퍼 아머
  * 필요 파라미터: `"amount": 1`, `"duration": "Stack"`
* **`"InvincibilityEffect"`**: 데미지 완벽 무효화 (무적)
  * 필요 파라미터: `"duration": "ThisTurn"` (무적은 amount가 필요 없습니다)
* **`"FirepowerEffect"`**: 다음 공격 데미지 증가 (화력)
  * 필요 파라미터: `"amount": 1`, `"duration": "ThisTurn"`
* **`"NextTurnBuffEffect"`**: 다음 턴에 특정 버프 획득
  * 필요 파라미터: `"buffType": "Firepower"`, `"amount": 1` (buffType에 Armor 등을 넣을 수 있음)
* **`"CounterAttackEffect"`**: 반격 상태 획득
  * 필요 파라미터: `"duration": "ThisTurn"`, `"rewardOnSuccess": "SelfToResource"` (성공 시 이 카드를 자원으로)

### 🔀 카드 이동 계열
* **`"DrawEffect"`**: 덱에서 패로 카드 뽑기
  * 필요 파라미터: `"count": 2` (2장 드로우)
* **`"DiscardFromHandEffect"`**: 패에서 카드 폐기
  * 필요 파라미터: `"count": 1`, `"mode": "random"` (mode는 random, all, choose 가 가능)
* **`"ResourceGainEffect"`**: 자원덱에서 자원존으로 카드 이동
  * 필요 파라미터: `"count": 2`
* **`"RecoverFromDiscardEffect"`**: 묘지에서 패로 회수 (리로드)
  * 필요 파라미터: `"count": 1`, `"filter": "character:ELLIE,type:Attack"` (특정 카드만 건져올 때 필터 사용)
* **`"ShuffleReturnEffect"`**: 패에서 덱으로 넣고 섞기
  * 필요 파라미터: `"count": 2`, `"mode": "random"`
* **`"TopDeckToDiscardEffect"`**: 덱 위에서 묘지로 (밀링)
  * 필요 파라미터: `"count": 4`
* **`"SearchDeckEffect"`**: 덱에서 찾아 패로 (서치)
  * 필요 파라미터: `"count": 1`, `"filter": "character:SONIA,type:Attack"`
* **`"ResourceFromDiscardEffect"`**: 묘지에 있는 자원 카드를 다시 자원존으로
  * 필요 파라미터: `"count": 1`

### 🚩 전장(Battlefield) 전용 계열
* **`"ArmorBattlefieldEffect"`**: 매 턴 아머 획득 (체크메이트)
  * 필요 파라미터: `"amount": 1`
* **`"FirepowerBattlefieldEffect"`**: 매 턴 화력 획득 (조선소)
  * 필요 파라미터: `"amount": 1`
* **`"PeriodicRecoveryBattlefieldEffect"`**: 매 자원페이즈마다 묘지에서 회수 (무작위 노획)
  * 필요 파라미터: `"filter": "type:Attack"`
* **`"CostReductionBattlefieldEffect"`**: 특정 카드 코스트 상시 할인 (노을지는 활주로)
  * 필요 파라미터: `"filter": "character:SONIA"`, `"reduction": 1`

### 🌀 특수 계열
* **`"SelfAsResourceEffect"`**: 사용한 자기 자신을 즉시 자원존에 배치 (보급 전달)
* **`"ReplayCardEffect"`**: 묘지에 있는 효과 카드를 즉시 할인해서 발동 (기뢰)
  * 필요 파라미터: `"costReduction": 1`

---

## 5. 자주 묻는 질문 (FAQ) & 디버깅 팁

**Q. 카드를 추가했는데 게임이 안 켜져요!**
A. 십중팔구 **JSON 문법 오류**입니다. 가장 흔한 실수는 다음과 같습니다.
1. `}` 뒤에 쉼표(`,`)를 빼먹은 경우.
2. 맨 마지막 카드인데 `}` 뒤에 쉼표(`,`)를 붙인 경우.
3. 숫자를 적어야 하는 곳(예: `amount`)에 `"1"` 처럼 따옴표를 붙인 경우.

**Q. 필터(Filter)는 어떻게 적나요?**
A. 여러 조건을 쉼표로 연결해서 적습니다. 띄어쓰기 없이 적어야 합니다.
* 공격 카드만: `"filter": "type:Attack"`
* 엘리 캐릭터 카드만: `"filter": "character:ELLIE"`
* 엘리의 공격 카드만: `"filter": "character:ELLIE,type:Attack"`

**Q. "그 후," 가 있는 효과인데 전제 조건이 '전부 버린다' 입니다. 0장을 버렸을 때도 성공인가요?**
A. 네! `"mode": "all"` 로 설정된 효과(예: 전술 핵)는 손패가 0장이라도 "패를 전부 버리는 행위" 자체를 완수한 것으로 보아 논리적 성공(`true`)으로 간주하여 다음 효과가 이어집니다.