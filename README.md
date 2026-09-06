# FarmFarm

도시 한가운데 농장을 운영하고, 드론으로 생산을 자동화하며 용병과 함께 좀비로부터 기지를 지키는 세로 화면 2D 픽셀 게임입니다.

개발자: KangJiwoong

[게임 플레이](https://woong020477.github.io/Project-FarmFarm/) · [위키](https://github.com/woong020477/Project-FarmFarm/wiki) · [GitHub Issues](https://github.com/woong020477/Project-FarmFarm/issues)

## 프로젝트 소개

플레이어가 농지에 물을 주면 드론이 씨앗을 심고, 성장한 작물을 수확해 창고로 운반합니다. 수송 헬기가 도착했을 때 작물을 판매하고, 획득한 골드로 농지와 드론을 확장하거나 용병을 고용합니다.

농장 운영과 기지 방호가 동시에 진행되며, 작물 제출 퀘스트와 기간 한정 이벤트가 생산·판매·전투에 변화를 줍니다.

```text
물주기 → 드론 심기·수확 → 창고 운반 → 헬기 도착 시 판매
                                       ↓
                         농지·드론 확장 / 용병 고용·기지 수리
```

## 기술 스택

| 구분 | 사용 기술 |
| --- | --- |
| 엔진 / 언어 | Unity 6000.3.21f1, C# |
| 그래픽 / 물리 | URP 2D, Tilemap, Sprite 애니메이션, Physics2D |
| 입력 / UI | Input System, uGUI, TextMesh Pro, 9-slice |
| 서버 연동 | PlayFab 인증·인벤토리·CloudScript, Google OpenID Connect |
| 웹 | Unity WebGL, HTML/CSS/JavaScript, GitHub Pages |
| 데이터 | ScriptableObject, JSON 입고 장부, PlayerPrefs |

## 구현 기능과 주요 스크립트

### 1. 셀 단위 농사와 작물 데이터

8×8 농지의 각 셀에 수분, 작물, 성장 상태를 관리합니다. 물이 있는 토양에만 심을 수 있고, 수분이 소진되면 성장이 멈췄다가 다시 물을 주면 이어집니다. 토양 색상은 수분 잔여량에 따라 3단계로 변경됩니다.

작물별 이미지·성장 시간·수확량·판매가는 ScriptableObject로 정의하고, 실제 심어진 작물의 상태는 별도로 관리합니다.

| 스크립트 | 담당 기능 |
| --- | --- |
| [FarmPlotController](game/Scripts/Farm/FarmPlotController.cs) | 농지 셀 등록, 물주기·심기·수확, 작물 선택/예약, 토양·성장 시각화 |
| [CropDefinition](game/Scripts/Crop/CropDefinition.cs) | 작물별 스프라이트, 성장 시간 범위, 수확량, 기본 가격 |
| [CropRuntimeState](game/Scripts/Crop/CropRuntimeState.cs) | 개별 작물의 성장 진행과 수확 가능 상태 |
| [CropCarrier](game/Scripts/Crop/CropCarrier.cs) | 운반 중인 작물·수량·수확 ID와 운반 스프라이트 |

### 2. 플레이어와 드론 자동화

플레이어는 상하좌우로 이동하며 물주기를 수행합니다. 드론은 대각선 비행으로 심기·수확·창고 반납을 반복합니다. 물주기, 드론 심기, 드론 수확은 각각 ON/OFF로 제어합니다.

| 스크립트 | 담당 기능 |
| --- | --- |
| [PlayerController](game/Scripts/Player/PlayerController.cs) | 이동 입력, 자동 물주기, 물리 회전 제한 |
| [DroneController](game/Scripts/Farm/DroneController.cs) | 작업 대상 탐색, 이동, 심기·수확, 운반 후 작업 복귀 |
| [IFarmAgent](game/Scripts/Farm/IFarmAgent.cs) | 플레이어·드론의 ID, 위치, 운반물 조회 및 위치 복원 공통 계약 |
| [FarmAgentOrigin](game/Scripts/Farm/FarmAgentOrigin.cs) | 애니메이션·운반물 크기에 흔들리지 않는 농사 행동 기준점 |
| [GameManager](game/Scripts/Core/GameManager.cs) | 공유 서비스 참조, 농지·드론 등록과 소유 농지 작업 대상 관리 |

### 3. 창고 입고와 상태 저장

창고 반납은 로컬에서 처리해 드론이 바로 다음 작업으로 복귀하도록 구성했습니다. 첫 입고 이후 10초 동안 쌓인 데이터를 묶어 서버에 전송합니다.

농장 상태는 기본 200초 간격으로 저장하고, 중요 행동에서는 저장 요청을 모아 처리합니다. 플레이어·드론 위치와 운반 상태도 저장 대상에 포함합니다.

| 스크립트 | 담당 기능 |
| --- | --- |
| [WarehouseController](game/Scripts/Farm/WarehouseController.cs) | 충돌 진입 시 입고, 로컬 장부·백업, 10초 배치 전송, 미전송 재고 관리 |
| [PlayFabInventoryService](game/Scripts/Server/Inventory/PlayFabInventoryService.cs) | 서버 재고 조회, 입고 요청, 골드·재고 변경 알림 |
| [FarmSaveService](game/Scripts/Server/Persistence/FarmSaveService.cs) | 정기·중요 시점 저장, 연속 저장 요청 통합, 상태 복원 |
| [FarmSaveModels](game/Scripts/Server/Persistence/FarmSaveModels.cs) | 농지·에이전트·운반물 저장 데이터 모델 |

### 4. 헬기 판매와 거래 처리

수송 헬기는 서버 시간 기준으로 5분 체류와 5분 부재를 반복합니다. 체류 중에만 판매 창을 이용할 수 있으며, 출발하면 거래 UI가 닫힙니다. 작물별 수량 입력, +1/+10/+100, 전체 선택과 판매 완료 후 10초 쿨다운을 제공합니다.

| 스크립트 | 담당 기능 |
| --- | --- |
| [Market](game/Scripts/Market/Market.cs) | 헬기 주기, 판매 가능 상태, 작물별 가격 조회 |
| [ServerClock](game/Scripts/Server/Time/ServerClock.cs) | 서버 시간 동기화와 동기화 사이의 시간 추정 |
| [PlayFabInventoryService.Sales](game/Scripts/Server/Inventory/PlayFabInventoryService.Sales.cs) | 판매 요청 ID·단계 저장, 진행률·골드 갱신, 미완료 거래 조회와 재개 |
| [MarketUI](game/Scripts/UI/Market/MarketUI.cs) / [MarketItem](game/Scripts/UI/Market/MarketItem.cs) | 작물별 가격·재고 목록과 거래 창 진입 |
| [TradeUI](game/Scripts/UI/Market/TradeUI.cs) | 선택 작물 표시, 판매 수량 입력, 소지금·쿨다운 표시 |

### 5. 성장·퀘스트·기간 이벤트

레벨과 경험치, 작물 제출 퀘스트, 최대 8개 농지 해금, 드론 구매, 용병 고용을 연결했습니다. 기간 이벤트의 배율은 작물 성장·판매 가격·이동 속도 등 공통 능력치 계산에 적용됩니다.

| 스크립트 | 담당 기능 |
| --- | --- |
| [Progression](game/Scripts/Progression/Progression.cs) | 퀘스트 제출, 농지 해금, 드론 구매, 용병 고용·수리 서버 요청 |
| [ProgressState](game/Scripts/Progression/ProgressState.cs) | 레벨·경험치·퀘스트·소유 상태 데이터 |
| [GameEvents](game/Scripts/Events/GameEvents.cs) | 서버 이벤트 조회, 활성 기간 관리, 능력치 배율 캐시 |
| [Stats](game/Scripts/Events/Stats.cs) / [StatModifier](game/Scripts/Events/StatModifier.cs) | 기본 수치와 이벤트 보정값 계산 |
| [GameplayUI](game/Scripts/UI/Progression/GameplayUI.cs) | 퀘스트·확장·막사·방호 화면과 상태 표시 |

### 6. 기지 방호와 용병 AI

기지 외곽 바리케이드는 하나의 공유 체력을 사용합니다. 좀비는 이동 가능한 경로를 따라 접근하고, 용병은 외곽을 순찰하다 적을 발견하면 빠르게 이동해 사격합니다. 바리케이드 수리, 피격 효과, 화면 밖 좀비 방향 안내도 구현했습니다.

| 스크립트 | 담당 기능 |
| --- | --- |
| [Defense](game/Scripts/Defense/Defense.cs) | 바리케이드 체력, 적·용병 관리, 전투와 방호 상태 |
| [DefenseMap](game/Scripts/Defense/DefenseMap.cs) | 장애물 셀, 이동 경로와 시야 판정 |
| [Zombie](game/Scripts/Defense/Zombie.cs) | 기지 접근, 경로 이동, 바리케이드 공격 |
| [Mercenary](game/Scripts/Defense/Mercenary.cs) | 목표 탐색, 순찰·추격 이동, 사격 판정 |
| [BarricadeImpact](game/Scripts/Defense/BarricadeImpact.cs) / [ZombieRadar](game/Scripts/Defense/ZombieRadar.cs) | 피격 연출과 적 위치 안내 |

### 7. 인증·UI·카메라·도시 연출

| 스크립트 | 담당 기능 |
| --- | --- |
| [PlayFabBootstrap](game/Scripts/Server/Auth/PlayFabBootstrap.cs) / [LoginUI](game/Scripts/UI/Login/LoginUI.cs) | Google 인증 연결, PlayFab 로그인, 최초 닉네임 설정과 자동 로그인 흐름 |
| [NicknameRules](game/Scripts/Server/Auth/NicknameRules.cs) | 닉네임 문자 종류·길이 검사 |
| [UIManager](game/Scripts/UI/UIManager.cs) / [PanelExit](game/Scripts/UI/Common/PanelExit.cs) | 공통 패널 열기·닫기와 패널 외부 클릭 처리 |
| [FarmUI](game/Scripts/UI/Farm/FarmUI.cs) / [FarmSelectionView](game/Scripts/UI/Farm/FarmSelectionView.cs) | 농지 선택, 잠금 표시, 작물 변경 화면 |
| [CameraController](game/Scripts/Camera/CameraController.cs) / [CameraManager](game/Scripts/Camera/CameraManager.cs) / [CameraHold](game/Scripts/Camera/CameraHold.cs) | 드래그 이동, 농지 포커스, 버튼을 누르는 동안 기지 전체 보기 |
| [TrafficSystem](game/Scripts/Exteriors/TrafficSystem.cs) / [TrafficSignal](game/Scripts/Exteriors/TrafficSignal.cs) | 도시 차량 이동과 신호 제어 |
| [BuildingFade](game/Scripts/Exteriors/BuildingFade.cs) / [ExteriorBridge](game/Scripts/Exteriors/ExteriorBridge.cs) | 건물 뒤 가림 완화와 고가 구조물 표현 |

### 8. 오디오와 설정

반복 BGM과 사격·수확·판매 효과음을 연결했습니다. BGM/SFX 볼륨은 각각 기본 50%이며, 설정을 닫을 때 로컬에 저장하고 다시 열면 슬라이더에 반영합니다.

| 스크립트 | 담당 기능 |
| --- | --- |
| [GameAudio](game/Scripts/Audio/GameAudio.cs) | 씬 간 BGM 유지, 효과음 소스 재사용, WebGL 첫 입력 후 오디오 시작 |
| [GameAudioFeedback](game/Scripts/Audio/GameAudioFeedback.cs) | 사격·수확·판매 완료 이벤트를 효과음으로 연결 |
| [AudioPreferences](game/Scripts/Audio/AudioPreferences.cs) | 볼륨 범위 보정과 PlayerPrefs 저장 |
| [SettingsUI](game/Scripts/UI/SettingsUI.cs) | 볼륨 슬라이더 동기화, 닫기 저장, 크레딧 패널 |

## 스크립트 구조

[전체 스크립트 보기](game/Scripts)

```text
game/Scripts/
├── Core/                 공유 참조와 농지·드론 등록
├── Farm/                 셀 단위 농사, 드론 작업, 창고
├── Crop/                 작물 정의, 성장 상태, 운반물
├── Player/               플레이어 이동·물주기, 프로필
├── Camera/               카메라 이동과 연출
├── Defense/              바리케이드, 경로, 좀비, 용병
├── Progression/          퀘스트·확장·성장 상태
├── Events/               기간 이벤트와 능력치 배율
├── Market/               시장 상태와 판매 결과 모델
├── UI/                   공통·농지·시장·로그인·설정 화면
├── Audio/                볼륨 저장, 재생, 게임 이벤트 연결
├── Server/
│   ├── Auth/             인증과 닉네임
│   ├── Inventory/        입고·판매·재고 조회
│   ├── Persistence/      농장 저장·복구와 동기화 모델
│   └── Time/             서버 시간 추정
└── Exteriors/            도시 교통과 시각 연출
```

게임 로직, 화면 표시, 서버 통신의 책임을 분리했습니다. 예를 들어 창고가 입고를 관리하고, 인벤토리 서비스가 서버와 통신하며, UI는 변경 알림을 받아 수량을 갱신합니다. 작물의 공유 설정과 개별 성장 상태도 분리하고, 플레이어·드론 저장에는 공통 인터페이스인 IFarmAgent를 사용합니다.

## 개발 이슈와 해결 과정

### 1. 대량 판매 시 재고만 차감되고 골드 지급이 지연되는 문제

- 증상: 적은 수량은 판매되지만 대량 판매에서는 재고가 먼저 줄고 골드 반영이 늦거나 중간에 실패했습니다.
- 원인: 기존 구조는 아이템 인스턴스를 모두 차감한 뒤 골드를 지급했습니다. 중간 API 실패가 발생하면 차감과 지급 사이에 불일치가 남을 수 있었고, 격리 테스트에서 해당 경로를 재현했습니다.
- 해결: 판매를 최대 4개 인스턴스 단위로 나누어 차감·정산하고, 요청 ID와 처리 단계를 서버 장부에 기록하도록 변경했습니다. 클라이언트는 단계별 처리 수량과 골드를 갱신하고 완료 후 재고를 다시 조회합니다.
- 관련 코드: [판매 진행·정산 응답 처리](game/Scripts/Server/Inventory/PlayFabInventoryService.Sales.cs)

### 2. 판매 응답이 끊겼을 때 완료 여부를 알 수 없는 문제

- 증상: 서버에서 처리가 진행됐어도 응답이 유실되면 클라이언트는 판매 실패와 완료를 구분하기 어려웠습니다.
- 해결: 진행 중인 요청 ID·수량·단계를 로컬에 보관하고, 재접속 시 getSaleState로 서버 상태를 조회해 이어가도록 구성했습니다. 통화 지급 결과 자체가 불확실한 경우에는 무조건 재지급하지 않고 확인 필요 상태를 유지합니다.
- 관련 코드: [미완료 거래 저장·조회·재개](game/Scripts/Server/Inventory/PlayFabInventoryService.Sales.cs)

### 3. 드론마다 입고 요청을 보내면서 운반이 지연되는 문제

- 증상: 드론이 작물을 반납할 때 서버 응답을 기다리고, 여러 드론이 각각 요청해 통신이 잦아졌습니다.
- 해결: 로컬 입고와 서버 전송을 분리했습니다. 장부 저장 후 운반물을 비우고 다음 작업으로 복귀시키며, 첫 입고부터 10초 동안 모은 데이터를 묶어 전송합니다. 표시 재고는 서버 확정 수량과 미전송 수량을 합산합니다.
- 복구 처리: 미전송 장부와 백업을 보관하고, 장부 저장에 실패하면 입고를 되돌려 운반물을 복원합니다.
- 관련 코드: [입고 장부와 배치 전송](game/Scripts/Farm/WarehouseController.cs)

### 4. 농사 행동 위치가 어긋나고 플레이어가 충돌 시 회전하는 문제

- 증상: 물주기·수확 위치가 캐릭터 중앙과 다르게 계산되고, 물체에 부딪히면 플레이어가 회전했습니다.
- 원인: 표시용 스프라이트 크기·애니메이션과 행동 좌표가 섞여 있었고, Rigidbody2D의 회전이 허용되어 있었습니다.
- 해결: 운반물 렌더러를 제외한 전용 행동 기준점을 사용하고, 애니메이션 캐릭터는 고정 Transform을 기준으로 삼았습니다. 플레이어 Rigidbody2D에는 FreezeRotation을 적용했습니다.
- 관련 코드: [FarmAgentOrigin](game/Scripts/Farm/FarmAgentOrigin.cs), [PlayerController](game/Scripts/Player/PlayerController.cs)

### 5. 8×8 농지의 선택 UI가 8×9로 늘어나는 문제

- 증상: 농지 선택 영역의 높이와 중심 좌표가 의도한 셀 범위에서 벗어났습니다.
- 원인: 농사 영역을 작물 표시용 타일맵의 범위에 의존해 계산하면서 시각 표현과 논리 영역이 결합되어 있었습니다.
- 해결: Soil 타일맵에서 실제 타일이 있는 셀만 농지로 등록하고, 등록된 셀 좌표로 경계를 캐싱했습니다. 선택 UI와 카메라 포커스는 이 농지 경계를 사용하도록 변경했습니다.
- 관련 코드: [농지 등록·월드 경계 계산](game/Scripts/Farm/FarmPlotController.cs), [선택 영역 표시](game/Scripts/UI/Farm/FarmSelectionView.cs)
