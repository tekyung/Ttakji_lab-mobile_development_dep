# 수정 및 보완 내역 정리

## 개요
- 본 문서는 이번 대화에서 진행한 코드 수정/보완 사항을 요약한 기록입니다.
- 기준 저장 경로: `GameDesign/수정 및 보완 MD파일 경로 파일.md`

## 1) 컴파일 오류(CS0246) 수정
- `Assets/TCG_Project/Scripts/Interfaces/ICardEffect.cs`
  - `Action` 타입 인식 오류 해결을 위해 `using System;` 추가.
- `Assets/TCG_Project/Scripts/Core/GameContext.cs`
  - `List<>`, `Dictionary<>` 인식 오류 해결을 위해 `using System.Collections.Generic;` 추가.

## 2) 덱 빌더 이미지/카드 연결 보완
- `Assets/Scripts/BuildDeck/CardData.cs`
  - JSON 키와 매칭되는 `imagePath` 필드 추가.
- `Assets/Scripts/BuildDeck/CardUI.cs`
  - 이미지 경로 우선순위: `imagePath` 우선, `skin_res` 보조(fallback).
  - `LoadCardImage()` 경로 정규화 보강:
    - `Assets/Resources/`, `GameDesign/` 프리픽스 제거.
    - DAINA 리네이밍(`DAIN-xx` -> `DAINA-xx`) 대응.
    - 캐릭터 폴더(`엘리`, `베로니카`, `소니아`, `다이나`) 기준 fallback 매칭 추가.

## 3) 덱 리스트 카드 표시 문제 수정
- `Assets/Scripts/BuildDeck/DeckBuilderManager.cs`
  - 옛 숫자 ID 덱(예: `11001`)과 현재 문자열 ID(예: `ELLI-02`) 간 불일치로 카드/이미지 미표시 발생.
  - `LoadDeckFromJson()`에 정규화 로직 적용:
    - `NormalizeDeckIds()` 추가.
    - `MigrateLegacyCardId()` 추가.
    - legacy -> new ID 매핑:
      - `11xxx -> ELLI-xx`
      - `21xxx -> VERO-xx`
      - `31xxx -> DAIN-xx`
      - `41xxx -> SONI-xx`
      - `001` 기준은 `02`로 보정(`+1`).

## 4) 폐기존 상호작용/위치 고정 수정
- `Assets/Scripts/InGameCard/PlayerUIManager.cs`
  - `Hand -> Graveyard` 이동 UI 처리 누락 보완.
  - `LockCardInGraveyard()` 추가:
    - 폐기존 카드의 앵커/피벗/좌표 고정.
    - `CardInteraction` 비활성화.
    - 액션 버튼 패널 비활성화.
  - `SendPendingCardToGraveyard()`, `MoveCardToGraveyardFromZone()`에서 공통 적용.
- `Assets/Scripts/InGameCard/EnemyVisualTester.cs`
  - 적 카드 `Hand -> Graveyard` 연출 추가.
  - 폐기존 이동 시 `SetParent(..., false)` 및 상호작용 비활성화 반영.

## 5) 원복/정리 이력
- 요청에 따라 "버리기(폐기) 이외 변경 원복" 작업을 수행한 이력이 있음.
- 이후 재요청된 항목(컴파일 오류, 이미지 미표시, 덱 리스트 미표시, 폐기존 잠금)에 대해 필요한 수정을 재적용함.

## 참고
- Unity에서 변경사항 반영을 위해 재컴파일 후 플레이 테스트 필요.
- 남은 이슈 발생 시 콘솔의 첫 에러/경고 1~2개를 기준으로 후속 보완 가능.

