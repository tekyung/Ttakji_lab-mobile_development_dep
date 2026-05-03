# ⚔️ 전투! 용병의 시대 (BAOM) - Digital TCG Core Engine

## 📖 프로젝트 개요 (Project Overview)

본 프로젝트는 오프라인 인디 보드/카드 게임인 **[전투! 용병의 시대 (Battle! Age of Mercenaries, 이하 BAOM)]**를 디지털 환경으로 이식하기 위해 개발된 **1v1 TCG 코어 엔진**입니다.

실물 카드(Tabletop) 환경에서 플레이어들이 직접 계산하고 진행해야 했던 복잡한 페이즈 진행, 자원(코스트) 관리, 그리고 용병 카드(엘리, 베로니카 등)들의 다채로운 스택/체인 능력을 **순수 C# 기반의 논리 엔진**으로 100% 자동화 및 시스템화하는 것을 목적으로 합니다. 

이 엔진은 단순한 디지털 클라이언트를 넘어, 향후 실시간 PVP 서버의 검증 로직 및 강화학습(RL) AI 훈련을 위한 독립적인 시뮬레이터로 활용됩니다.

### 🔗 원작 게임 레퍼런스 (Original Game Info)
이 프로젝트가 구현하고 있는 원작 보드게임의 상세한 룰, 캐릭터 세계관, 그리고 실물 게임의 역사는 아래의 링크에서 확인하실 수 있습니다.

* **[나무위키: 전투! 용병의 시대]**(https://namu.wiki/w/%EC%A0%84%ED%88%AC!%20%EC%9A%A9%EB%B3%91%EC%9D%98%20%EC%8B%9C%EB%8C%80) - 게임의 기본 룰 및 용병 상세 정보
* **[텀블벅 크라우드 펀딩 페이지]**(https://tumblbug.com/baom) - 원작 게임의 시작과 세계관 소개
* **[인디 보드게임 마켓 (IBGM) 소개글]**(https://ibgm-mo.imweb.me/46/?bmode=view&idx=167339005) - 실물 보드게임 페스티벌 출품 기록

---

## ⚙️ TCG Core Engine Architecture (순수 C# 기반 독립 코어)

본 프로젝트의 심장부인 TCG 코어 엔진(Core Logic)은 **"유니티(Unity) 의존도 0%"**라는 엄격한 아키텍처 대원칙 아래 설계되었습니다. 게임의 모든 규칙 판정, 턴 흐름, 카드 효과 처리는 그래픽 엔진과 완벽하게 분리된 **순수 C# 환경**에서 작동합니다.

### 🎯 설계 철학 및 비전
단순한 오프라인 패키지 게임을 넘어, **안전한 실시간 PVP 서버 구축**과 **고속 딥러닝 AI 학습(Reinforcement Learning)**을 최종 목표로 설계되었습니다.
* **Server-Authoritative PVP (서버 권한 검증):** 클라이언트 측 조작(핵)을 원천 차단하기 위해, 코어 로직 수정 없이 그대로 경량화된 .NET 백엔드 서버(AWS, Docker 등)에 올려 매치 검증기로 사용합니다.
* **초고속 AI 시뮬레이션:** 유니티의 그래픽 렌더링 오버헤드를 완벽히 제거하여, 고성능 GPU(RTX 4090) 및 CPU 멀티코어 환경에서 초당 수천 판의 가상 대전을 시뮬레이션하며 강화학습 AI를 훈련시킬 수 있는 기반을 마련했습니다.

---

### 🚀 핵심 아키텍처 특징

#### 1. 프레임워크 독립성 (Engine Agnostic)
* `MonoBehaviour`, `Coroutine`, `UniTask`, `UniRx` 등 유니티 및 외부 종속성을 철저히 배제했습니다.
* 순수 C# 표준 라이브러리(`async/await`, `Task`)만을 사용하여 턴의 선형적 대기와 비동기 흐름을 제어합니다.
* 엔진 독립성이 보장되어 향후 언리얼(Unreal), 고도(Godot), 웹(Blazor) 등 어떠한 환경으로도 코어 수정 없이 즉각적인 이식이 가능합니다.

#### 2. 의존성 주입(DI) 기반 데이터 파이프라인
* 플랫폼(Unity vs Console) 간의 파일 입출력(`System.IO`) 경로 충돌을 방지하기 위해 `IJsonLoader` 인터페이스를 도입했습니다.
* 코어 엔진은 데이터 출처(Unity Resources, 로컬 디스크, 웹 API 등)를 알 필요 없이, 외부에서 주입된 로더를 통해 `Rulebook`, `Character`, `CommonConfig` 등 JSON 데이터를 일관되게 파싱합니다.

#### 3. 관심사의 완벽한 분리 (Separation of Concerns)
* **Model과 View의 분리:** 카드의 뒷면 슬리브 이미지 적용 등 시각적 처리(View)는 데이터 모델에 하드코딩하지 않습니다. 엔진 코어는 오로지 카드의 상태(`IsFaceUp = true/false`)만 조작하며, 유니티 UI 시스템이 이를 관찰(Observe)하여 화면을 렌더링합니다.
* 내부 로직과 외부 진입점의 분리: 매치메이킹 시스템 등 외부에서 조립된 `PlayerSetupData` DTO를 주입받아 게임을 초기화하여 시스템 간 결합도를 최소화했습니다.

#### 4. 안정적인 비동기 타임아웃 제어
* 자체 구축한 순수 C# 유틸리티인 `AsyncTimeoutHelper`를 통해 스택 체인, 세트 카드 선택 등 유저 입력을 대기하는 모든 논리적 구간에 타임아웃(Timeout) 룰을 적용했습니다.
* 네트워크 지연이나 응답 없음 상황에서도 엔진이 데드락(Deadlock)에 빠지지 않으며, 제한 시간 초과 시 봇(Bot) AI 혹은 스킵(Skip) 로직으로 안전하게 폴백(Fallback)되어 게임 루프가 유지됩니다.

---

### 📂 코어 주요 구조 (Core Directory)
* `Scripts/Core/` : 카드의 논리적 뼈대(`Card.cs`)와 게임 글로벌 룰(`GameRules.cs`) 등 핵심 데이터 모델
* `Scripts/Systems/` : 게임 라이프사이클을 총괄하는 `BattleManager`와 전역 데이터를 관리하는 `GameDataManager`
* `Scripts/Interfaces/` : 의존성 역전 원칙(DIP)을 위한 명세서 (`IJsonLoader.cs` 등)
* `Scripts/Utils/` : 환경 독립적인 비동기 제어 및 헬퍼 클래스 (`AsyncTimeoutHelper.cs` 등)

개발 기록을 포함한 상세 정보는 이 [문서](https://github.com/tekyung/Ttakji_lab-mobile_development_dep/tree/gabriel/README.md)를 클릭해주세요.