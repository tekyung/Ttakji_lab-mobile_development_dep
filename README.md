# 26.01.30 진행사항

Card // Effect 간 분리, Card는 Effect의 id를 호출

Cards.json 에서 각종 수식 파싱 후, Eval 하게 하여 게임 내 변수 활용

Rules.json 으로 게임 내 세부 수치 관리

각 카드별 발동 조건 설정 -> 사용 가능 여부 판별

카드의 효과 분기 설정 -> 한 카드가 다른 타입의 효과를 2개 이상 포함 가능


# 26.01.31 진행사항

Targeting 시스템 구현(N개의 데이터베이스에서 M개 지정)

MoveCardEffect(카드 이동 효과)으로 각종 카드 이동 효과 구현 및 대체

ModifyCardEffect(카드 수정 효과)으로 각종 카드 스탯 수정 효과 구현

현재 과도기로 사용하지 않는 Effect.cs 들이 혼재되어 있음. 추후 정리 필요.


# 26.02.10 진행사항

유닛의 소환 시 효과 발동 조건을 json에 기입

애벌레(1 드로우) : "on_play_condition_type": "DeckNotEmpty",

폭탄벌(1 파괴) : "on_play_condition_type": "EnemyUnitExist"

픽시드래곤(200 너프) : "on_play_condition_type": "EnemyUnitExist"

변경된 룰로 시뮬레이션 테스트 성공

최신화된 json사용 중, 이전 버전 사용 시 오류 발생
