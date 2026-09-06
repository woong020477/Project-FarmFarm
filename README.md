# FarmFarm

도시 한가운데 작은 농장을 운영하고, 드론과 용병으로 생산과 기지 방호를 자동화하는 세로 화면 2D 픽셀 게임입니다.

개발자: KangJiwoong

[게임 페이지](https://woong020477.github.io/Project-FarmFarm/) · [위키](https://github.com/woong020477/Project-FarmFarm/wiki) · [이슈 제보](https://github.com/woong020477/Project-FarmFarm/issues)

이 저장소는 WebGL 배포용입니다. 게임 링크와 웹 셸 준비가 실제 빌드 배포 완료를 의미하지는 않습니다. Unity 소스와 유료 에셋 원본은 이 배포 폴더에 포함하지 않습니다.

## 게임 소개

플레이어는 농지에 물을 주고, 드론은 젖은 농지에 씨앗을 심어 작물을 수확한 뒤 창고로 운반합니다. 수송 헬기가 머무는 동안 작물을 판매해 농지·드론·용병을 확장합니다. 기지 외곽에서는 좀비를 막고 바리케이드를 관리합니다.

## 구현 기능

| 영역 | 현재 구현 |
| --- | --- |
| 농사 | 농지별 작물 선택/예약, 물 유지 시간, 수분에 따른 성장 정지/재개, 성장 단계 스프라이트 |
| 자동화 | 플레이어 물주기 ON/OFF, 드론 심기·수확 개별 ON/OFF, 대각선 드론 이동, 창고 왕복 운반 |
| 확장 | 최대 8개 농지, 레벨 조건과 소유 상태에 따른 농지 잠금, 드론 생성 시 고유 ID |
| 창고/저장 | 로컬 입고 장부, 첫 입고 후 10초 묶음 전송, 정기 상태 저장과 중요 행동 저장, 위치·운반 상태 복원 |
| 시장 | 서버 시간 기준 헬기 체류 5분/부재 5분, 작물별 가격, 수량 선택·전체 판매, 10초 판매 쿨다운 |
| 거래 안정성 | 판매 요청 ID, 단계별 진행 상태, 불확실한 응답 재조회·복구, 완료 확인 후 재고/골드 갱신 |
| 성장/콘텐츠 | 레벨·경험치, 작물 제출 퀘스트, 보상과 확장, 기간 한정 능력치 이벤트 |
| 방호 | 공유 바리케이드 체력, 좀비 경로 이동, 용병 순찰/추격/사격, 수리, 피격 연출·위치 안내 |
| 도시 | 타일맵/스프라이트 기반 도시, 차량·신호등, 일부 구조물 가림/투명화, 애니메이션 |
| 로그인 | Google OAuth 연결, PlayFab 계정 조회, 최초 닉네임 설정, 자동 로그인 선택 |
| UI | 농지/거래/퀘스트/방호 패널, 외부 클릭 닫기, 카메라 드래그·기지 전체 보기 |
| 오디오 | 오리지널 반복 BGM, 사격·수확·판매 SFX, BGM/SFX 개별 슬라이더, 로컬 볼륨 저장, 크레딧 |

클라이언트 구현과 운영 서버에서의 검증은 구분합니다. 현재 농사 상태/입고 장부는 클라이언트 기반이므로 완전한 변조 방지 구조라고 주장하지 않습니다. 큰 규모의 동시 접속·모바일 실기기·네트워크 장애 시험은 별도 검증 대상입니다.

## 기술 구성

- Unity 6000.3.21f1 / C# / Universal Render Pipeline 2D
- Tilemap, Sprite 애니메이션, Physics2D, Input System
- uGUI / TextMesh Pro / 9-slice UI
- PlayFab 인증·인벤토리·CloudScript, 서버 시계 기준 거래 가능 시간
- HTML/CSS/JavaScript 웹 셸, GitHub Pages, manifest/홈 화면 추가 안내

## 소스 구조와 읽는 순서

Unity 프로젝트의 직접 작성한 코드만 기능별로 정리합니다. 외부 SDK와 에셋 코드는 리팩토링 대상에서 제외합니다.

```text
Assets/
  Scripts/
    Core/                 게임 내 공유 참조와 농지/드론 등록
    Farm/                 셀 단위 농사, 드론 작업, 창고
    Crop/                 작물 정의, 성장 상태, 운반물
    Player/               플레이어 이동/물주기, 프로필
    Camera/               카메라 이동과 연출
    Defense/              바리케이드, 경로, 좀비, 용병
    Progression/          퀘스트·확장·성장 상태
    Events/               기간 이벤트와 능력치 배율
    Market/               시장 상태와 판매 결과 모델
    UI/                   SettingsUI와 기능별 화면
    Audio/                볼륨 저장, 재생, 게임 이벤트 연결
    Server/
      Auth/               인증과 닉네임
      Inventory/          입고·판매·재고 조회
      Persistence/        농장 저장/복구, 동기화 모델
      Time/               서버 시간 추정
    Exteriors/            도시 교통과 시각 연출
  Data/Crops/             작물 ScriptableObject
  Server/CloudScript/     업로드할 서버 JavaScript
  Editor/                 씬 구성·검사·데이터 내보내기 도구
Tools/                    서버 모의 테스트, 오디오 생성, 코드 서식 도구
```

추천 읽기 순서:

1. `Core/GameManager.cs`: 장면의 공유 참조와 작업 대상 관리.
2. `Farm/FarmPlotController.cs`, `Farm/IFarmAgent.cs`, `Farm/DroneController.cs`: 셀 상태, 공통 작업 계약, 드론 작업 루프.
3. `Farm/WarehouseController.cs`, `Server/Inventory/PlayFabInventoryService.Sales.cs`: 입고 묶음과 판매 복구. 서버 호출 성공과 로컬 연출의 경계를 확인할 수 있습니다.
4. `Defense/Mercenary.cs`: 목표 탐색·순찰 경로·사격 판정의 분리.
5. `UI/SettingsUI.cs`, `Audio/GameAudioFeedback.cs`: UI/저장/재생 책임 분리와 이벤트 구독 해제.

리팩토링 시 Unity GUID, 직렬화 필드, 버튼에 연결된 공개 메서드, 기존 서버 함수 이름과 저장 키를 보존합니다. 익숙한 도메인 이름은 유지하고, 단순히 이름을 줄이기 위한 변경은 하지 않습니다.

## 오디오/설정

BGM과 SFX는 최초 50%입니다. 슬라이더를 움직이면 즉시 반영되고 닫기 버튼 또는 패널 바깥을 누르면 저장됩니다. 다시 열면 현재 볼륨과 슬라이더를 동기화합니다. 크레딧도 닫기/바깥 클릭을 지원합니다.

볼륨은 Unity `PlayerPrefs`의 `FarmFarm.Audio.v1.*` 키에 저장합니다. WebGL에서는 Unity의 브라우저 저장소를 사용하며 서버에 보내지 않습니다. 브라우저 데이터 삭제·시크릿 모드·기기 변경 시 유지가 보장되지 않습니다. 웹 오디오는 브라우저 정책상 첫 터치/키 입력 후 시작될 수 있습니다.

`FarmLoop`는 이번 프로젝트용 90 BPM, 약 42.67초 루프입니다. 사격/수확/동전 효과음도 수식으로 합성하며 기존 곡·녹음·샘플을 사용하지 않습니다. 생성 소스는 Unity 프로젝트의 `Tools/Audio/generate_audio.cjs`에 있습니다.

## 이슈 트래킹

아래는 문서 내 추적 ID입니다. GitHub 이슈가 자동 생성된 것은 아닙니다. [이슈 페이지](https://github.com/woong020477/Project-FarmFarm/issues)에 화면, 재현 단계, 기기/브라우저, 발생 시각을 적어 주세요. 인증 토큰이나 API 키는 첨부하지 마세요.

| ID | 상태 | 내용 / 확인 방법 |
| --- | --- | --- |
| FF-001 | 운영 검증 필요 | 대량 판매·타임아웃·재접속: 서버 요청 ID별 재고/골드 일치 확인 |
| FF-002 | 설계상 한계 | 클라이언트 농사/입고 데이터 변조 가능성: 운영 전 검증 범위 결정 |
| FF-003 | 배포 전 확인 | LoginUI의 LoginConfig 참조와 실제 Google OAuth origin, 계정 복귀 시험 |
| FF-004 | 웹 실기기 확인 | 첫 터치 오디오 재생, 무음/백그라운드 복귀, 브라우저 저장 유지 |
| FF-005 | 개선 진행 | 도시의 접합·높낮이·밀집도·충돌/가림 연출을 구역별 시각 검수 |
| FF-006 | 로컬 검사 통과 | 볼륨·닫기 저장·크레딧·SFX·루프 등 Unity Play Mode 34개 검사 통과. 웹 실기기 검증은 FF-004 |
| FF-007 | 로컬 회귀 통과 | 기존 스크립트 64개 GUID 유지, 농사/드론/용병/방호 Play Mode 통합 검사 통과 |

검증 로그는 Unity 프로젝트의 `Docs/AudioSettings/`, `Docs/Gameplay/`, `Docs/LoginSale/` 등에 분리 기록합니다. 컴파일 통과, 로컬 모의 테스트, Play Mode 동작, 실제 서비스/웹 빌드는 서로 다른 검증 단계입니다.

## WebGL 배포

이 폴더의 웹 셸은 `game/index.html`에서 Unity 빌드를 엽니다. 실제 빌드 파일은 `game/` 아래에 배치합니다. 빌드/게시 전에 Google OAuth 허용 origin, PlayFab 설정, 압축 형식과 GitHub Pages 응답을 확인해야 합니다. 클라이언트에 서버 비밀키를 포함하지 마세요.

## 크레딧

- 개발: KangJiwoong
- [LimeZu](https://limezu.itch.io): Modern Exteriors, Modern Farm, Modern User Interface
- [EmanuelleDev](https://emanuelledev.itch.io): Farm RPG Asset Pack
- [quiple / Lee Minseo](https://github.com/quiple/galmuri): 갈무리 폰트, SIL OFL 1.1
- 로그인 로고·배경·FF 아이콘: OpenAI 이미지 생성 도구 활용
- BGM/SFX: FarmFarm용 오리지널 합성 오디오

외부 에셋의 권리는 각 제작자에게 있습니다. 게임 배포 권한과 에셋 원본 재배포 권한은 다르므로 유료 에셋 원본을 공개 저장소에 올리지 않습니다. 크레딧과 라이선스 고지를 게임에 포함합니다.
