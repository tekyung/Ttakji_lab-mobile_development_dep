# 카드 효과 시스템 재설계 제안서

작성일: 2026-03-02  
작성 배경: Phase 7 구현 후 기존 Effect 구조의 한계를 분석하고, 이상적인 구조를 논의한 결과를 정리

---

## 1. 현재 구조의 문제 진단

### 1-1. Effect 클래스 폭발

현재 프로젝트의 Effect 클래스 수:
- Phase 5 이전: 8종 (DamageEffect, ArmorEffect 등)
- Phase 7 이후: **26종**

카드가 추가될수록 클래스가 계속 늘어나는 구조.

### 1-2. 관심사 혼합 (Mixed Concerns)

각 Effect 클래스가 "무엇을 / 어떻게 / 언제" 세 가지를 동시에 담당한다.

```
DrawEffect         → from: Deck, to: Hand, count: N, mode: Top
RecoverFromDiscard → from: Graveyard + filter, to: Hand, count: N, mode: Random
DiscardFromHand    → from: Hand, to: Graveyard, count: N, mode: Random/All
```

이 세 클래스는 사실 **같은 연산(카드 이동)에 파라미터만 다른 것**이다.

### 1-3. 중복 로직

`FilterCards` 메서드가 `RecoverFromDiscardEffect`와 `SearchDeckEffect`에
**각각 독립적으로 중복 구현**되어 있다.

### 1-4. 기존 MoveCardEffect의 한계 (레거시와 공존 문제)

기존 `MoveCardEffect`를 확장하지 않고 새 클래스를 만든 이유:
- `TargetSelector.Select()` — 구 Unit/Field 패러다임용 비동기 타겟팅에 종속됨
- `ResourceDeck`, `ResourceZone`, `StackZone` 등 새 존을 TargetSelector가 처리하지 못함
- "character:ELLIE, type:Attack" 같은 필터 로직이 전혀 없음
- 기존 카드(Unit/Skill) 동작을 깨지 않기 위한 하위 호환 제약 (원칙 6)

---

## 2. 이상적인 구조 — Effect = 선택 × 연산 × 지속

핵심 원칙: **카드 효과는 세 관심사의 조합이다**

```
Effect
  ├── Selector  : 어떤 카드/대상을? (zone + filter + count + mode)
  ├── Operation : 어떻게 처리?      (move / buff / damage / place)
  └── Duration  : 언제까지?         (instant / thisTurn / nextTurn / permanent)
```

이 구조를 따르면 **Effect 구현 클래스 6개**로 현재 26개를 전부 표현할 수 있다.

---

## 3. 재설계된 클래스 구조

### 3-1. CardSelector — 공통 선택 로직

```csharp
class CardSelector
{
    ZoneType   From;     // Deck / Hand / Graveyard / ResourceDeck / ResourceZone ...
    string     Owner;    // "Self" / "Opponent"
    CardFilter Filter;   // character:ELLIE, type:Attack 등 (현재 string filter 방식 유지)
    int        Count;
    SelectMode Mode;     // Top / Bottom / Random / Choose / All
}
```

### 3-2. 6개의 핵심 Effect 클래스

```
MoveEffect         — 카드를 한 존에서 다른 존으로 이동
DamageEffect       — 상대(또는 자신)에게 데미지
BuffEffect         — 버프/디버프 적용 (ArmorBonus, FirepowerBonus 등)
SelfPlaceEffect    — 이 카드 자체를 특정 존에 배치 (자원존, 전장존)
BattlefieldEffect  — 전장 배치 + 주기 효과(매 턴/매 자원페이즈) 등록
CompositeEffect    — 위 효과들의 순서 있는 합성
```

---

## 4. MoveEffect 단일 클래스로 커버되는 범위

| 현재 클래스명 (26개)         | MoveEffect 파라미터                                              |
|------------------------------|------------------------------------------------------------------|
| DrawEffect                   | from:Deck, to:Hand, count:N, mode:Top                           |
| DiscardFromHandEffect         | from:Hand, to:Graveyard, count:N, mode:Random/All               |
| RecoverFromDiscardEffect      | from:Graveyard, filter:"char:ELLIE,type:Attack", to:Hand        |
| ShuffleReturnEffect           | from:Hand, to:Deck, count:N, mode:Random → shuffle              |
| TopDeckToDiscardEffect        | from:Deck, to:Graveyard, count:N, mode:Top                      |
| ResourceGainEffect            | from:ResourceDeck, to:ResourceZone, count:N, mode:Top           |
| ResourceFromDiscardEffect     | from:Graveyard, filter:"type:Resource", to:ResourceZone         |
| ReturnFromDiscardEffect       | from:Graveyard, filter:"type:Effect", to:Deck → shuffle         |
| SearchDeckEffect              | from:Deck, filter:"char:SONIA,type:Attack", to:Hand, mode:First |

> **9개의 별도 클래스 → MoveEffect 1개 + 파라미터**

---

## 5. BuffEffect 단일 클래스로 커버되는 범위

| 현재 클래스명           | BuffEffect 파라미터                                   |
|-------------------------|-------------------------------------------------------|
| ArmorEffect             | buffType:Armor, amount:N, duration:ThisTurn           |
| SuperArmorEffect        | buffType:SuperArmor, amount:N, duration:ThisTurn      |
| InvincibilityEffect     | buffType:Invincible, duration:ThisTurn                |
| FirepowerEffect         | buffType:Firepower, amount:N, duration:ThisTurn       |
| CounterAttackEffect     | buffType:CounterAttack, duration:ThisTurn             |
| NextTurnBuffEffect      | buffType:Firepower/Armor, amount:N, duration:NextTurn |

> **6개의 별도 클래스 → BuffEffect 1개 + duration 파라미터**

---

## 6. BattlefieldEffect 통합

```csharp
class BattlefieldEffect : ICardEffect
{
    ICardEffect OnPlaceEffect;            // 배치 시 즉시 실행 (nullable)
    ICardEffect PerTurnEffect;            // 매 드로우페이즈 실행 (nullable)
    ICardEffect PerResourcePhaseEffect;   // 매 자원페이즈 실행 (nullable)
    CardSelector CostReductionTarget;     // 코스트 감소 대상 필터 (nullable)
    int         CostReduction;
}
```

**JSON 표현:**

```json
// VERO-11 체크메이트: [전장] 매 턴 경감 +1
{
  "type": "BattlefieldEffect",
  "perTurnEffect": {
    "type": "BuffEffect",
    "buffType": "Armor", "amount": 1, "duration": "ThisTurn"
  }
}

// ELLI-11 무작위 노획: [전장] 매 자원페이즈 폐기존 공격카드 → 패
{
  "type": "BattlefieldEffect",
  "perResourcePhaseEffect": {
    "type": "MoveEffect",
    "selector": { "from": "Graveyard", "filter": "type:Attack", "count": 1, "mode": "Random" },
    "to": "Hand"
  }
}

// SONI-11 관제탑: [전장] 소니아 카드 코스트 -1
{
  "type": "BattlefieldEffect",
  "costReductionFilter": "character:SONIA",
  "costReduction": 1
}
```

> **4개의 별도 Battlefield 클래스 → BattlefieldEffect 1개 + 내부 Effect 합성**

---

## 7. CompositeEffect — 복합 효과 처리

단일 Effect로 표현할 수 없는 카드(DAIN-10 기뢰 등)는 CompositeEffect로 처리한다.

```csharp
class CompositeEffect : ICardEffect
{
    List<ICardEffect> Steps; // 순서대로 순차 실행
}
```

**DAIN-10 기뢰 표현:**
```json
{
  "type": "CompositeEffect",
  "steps": [
    {
      "type": "MoveEffect",
      "selector": { "from": "Graveyard", "filter": "type:Effect", "count": 1, "mode": "Choose" },
      "to": "PlayBuffer"
    },
    { "type": "TemporaryCostModifyEffect", "delta": -1 },
    { "type": "ReplayBufferedEffect" }
  ]
}
```

---

## 8. 전체 구조 요약

```
이전 구조 (26개 클래스)         →    이상적 구조 (6개 클래스)
─────────────────────────────────────────────────────────────
DrawEffect                      ┐
DiscardFromHandEffect            │
RecoverFromDiscardEffect         │
ShuffleReturnEffect              ├──  MoveEffect
ResourceGainEffect               │    (CardSelector + to: ZoneType + shuffle: bool)
TopDeckToDiscardEffect           │
ResourceFromDiscardEffect        │
ReturnFromDiscardEffect          │
SearchDeckEffect                 ┘

DamageEffect                    ┐
PiercingDamageEffect             ├──  DamageEffect
MultiHitDamageEffect             ┘    (isPiercing: bool, times: int, target: Self/Opponent)

ArmorEffect                     ┐
SuperArmorEffect                 │
InvincibilityEffect              ├──  BuffEffect
FirepowerEffect                  │    (buffType: enum, amount: int, duration: enum)
CounterAttackEffect              │
NextTurnBuffEffect               ┘

BattlefieldEffect               ┐
ArmorBattlefieldEffect           │
FirepowerBattlefieldEffect       ├──  BattlefieldEffect
PeriodicRecoveryBattlefield      │    (onPlace / perTurn / perResourcePhase / costReduction)
CostReductionBattlefield         ┘

SelfDamageEffect                 ──  DamageEffect (target: "Self", skipDefense: true)

SelfAsResourceEffect             ──  SelfPlaceEffect (to: ResourceZone, typeAs: Resource)

ReplayCardEffect                 ──  CompositeEffect (Select → CostModify → Replay)
```

---

## 9. 이 구조의 장점

1. **카드 추가 시 새 C# 파일 불필요** — JSON 파라미터만 추가하면 됨
2. **중복 코드 제거** — FilterCards 등 공통 로직이 한 곳에만 존재
3. **테스트 용이** — 클래스가 적어 단위 테스트 작성 범위가 줄어듦
4. **디자이너 친화적** — 기획자가 JSON만으로 새 효과 조합 가능
5. **확장성** — 새 SelectMode나 BuffType 추가만으로 다양한 효과 표현 가능

---

## 10. 현재 프로젝트에서의 적용 시점 제안

| 작업 | 시점 |
|------|------|
| 레거시(Unit/Skill) 카드 완전 제거 | Phase 8 Unity 연동 완성 후 |
| MoveEffect + BuffEffect 통합 리팩터링 | 레거시 제거와 동시 진행 |
| BattlefieldEffect 통합 | Phase 8 이후 |
| CompositeEffect 도입 | 기뢰(DAIN-10) 같은 복합 효과 정식 처리 시 |

> 현재(Phase 7)까지는 원칙 6(하위 호환 유지)으로 인해 두 시스템이 공존하는 것이 불가피하다.
> Phase 8에서 레거시 카드를 완전히 제거하는 시점이 이 리팩터링의 자연스러운 진입점이다.
