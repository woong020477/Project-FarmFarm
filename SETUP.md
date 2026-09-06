# FarmFarm GitHub Pages

이 폴더의 index.html은 게임을 여는 바깥 페이지입니다. Unity 실행 파일은 game/ 아래에 둡니다. 바깥 페이지에 덮어 빌드하지 마세요.

## 나중에 빌드할 위치

`/Users/kangjiwoong/Documents/GitHub/Project-FarmFarm/game`

완성 후 구조:

```text
Project-FarmFarm/
  index.html
  site.css
  site.js
  manifest.webmanifest
  sw.js
  icon.svg / icon-180.png / icon-192.png / icon-512.png
  logo.png
  .nojekyll
  game/
    index.html
    Build/
    StreamingAssets/ (생성되는 경우)
```

## Unity 빌드 시 확인

1. 시작 씬: Login_Scene. Farm_Scene도 포함합니다.
2. WebGL Template: FarmFarm.
3. Development Build: 끄기.
4. Publishing Settings에서 Compression Format: Gzip, Decompression Fallback: 켜기. GitHub Pages에서 사용자 지정 Content-Encoding 헤더 없이 압축 파일을 로드하기 위한 설정입니다.
5. 출력 폴더는 위의 game 경로입니다. 기존 개발 빌드를 그대로 복사하지 마세요. 개별 파일이 GitHub 일반 업로드의 100 MiB 제한을 넘는지도 확인하세요.
6. 이 단계에서는 빌드를 실행하지 않았습니다. game/index.html이 없으면 바깥 페이지에 안내와 다시 확인 버튼이 나오는 것이 정상입니다.

## GitHub Pages 설정

저장소 Settings → Pages에서 게시할 브랜치의 /(root)를 선택하거나, 기존 Pages 배포 워크플로를 사용합니다. 변경 파일과 나중에 생성할 game 폴더를 커밋·푸시해야 공개 페이지에 반영됩니다.

예상 주소: https://woong020477.github.io/Project-FarmFarm/

Google OAuth 승인된 JavaScript 원본은 https://woong020477.github.io 입니다. Project-FarmFarm 또는 game 경로를 원본에 붙이지 않습니다. OAuth 비밀키나 PlayFab Secret Key를 이 저장소에 넣지 마세요.

## 닉네임

Login_Scene에 닉네임 패널과 버튼이 연결되어 있습니다. Google 로그인 후 PlayFab Title DisplayName이 비어 있을 때만 표시됩니다. 한글 3~16자 또는 영문 4~20자를 허용하며 숫자·공백·특수문자·한영 혼용을 금지합니다. 한글 IME 조합을 방해하지 않도록 입력 중이 아닌 제출 시 검사합니다.

PlayFab 기본 UpdateUserTitleDisplayName으로 저장하므로 CloudScript 추가 업로드는 필요 없습니다. 서버 저장 성공 전에는 농장에 진입하지 않습니다. 응답이 끊긴 뒤 재시도하면 기존 서버 이름을 먼저 확인해 불필요한 재변경을 방지합니다. 기존 DisplayName은 덮어쓰지 않습니다.

중복을 막으려면 PlayFab 타이틀의 non-unique Title Display Names 허용 옵션을 꺼야 합니다. 서버가 NameNotAvailable을 반환하면 중복 안내가 표시됩니다. 해당 콘솔 설정은 이번 작업에서 변경하지 않았습니다.

한글/영문 상세 형식 제한은 게임 입력 검사입니다. PlayFab 자체의 길이/중복 정책과는 다르며, 변조 클라이언트의 API 직접 호출까지 막는 서버 규칙은 아닙니다.

## 모바일 / 홈 화면

화면 위 메뉴에서 저장소·위키·이슈와 홈 화면 추가를 사용할 수 있습니다. 지원되는 브라우저에서는 설치 요청을 띄웁니다. iPhone/iPad에서는 Safari 공유 → 홈 화면에 추가 안내가 나옵니다. OS 설치를 강제로 실행하지는 않습니다.

바깥 페이지와 게임 캔버스의 스크롤·오버스크롤을 차단하고 안전 영역과 화면 키보드에 따른 높이 변화를 반영합니다. 메뉴가 길어질 때는 메뉴 안에서만 스크롤됩니다. Unity 안의 ScrollView와 Google 로그인 입력은 차단하지 않습니다.

서비스 워커는 작은 웹 페이지 파일만 캐시합니다. Google/PlayFab 응답, Unity 빌드와 저장 데이터는 캐시하지 않습니다. 게임은 인터넷 연결이 필요합니다. 웹 UI 배포를 크게 바꾸면 sw.js의 CACHE 버전을 올리세요. 실행 중인 게임을 강제로 새로고침하지 않습니다.

실기기 Safari/Chrome에서 Google 로그인, 한글 키보드, 홈 화면 설치, 게임 내 드래그 동작은 새 빌드 배포 후 확인해야 합니다.

## 참고

- [PlayFab 표시 이름 API](https://learn.microsoft.com/en-us/rest/api/playfab/client/account-management/update-user-title-display-name?view=playfab-rest)
- [PlayFab 중복 이름 옵션 설명](https://learn.microsoft.com/en-us/rest/api/playfab/client/account-management/get-account-info?view=playfab-rest)
- [PWA 설치 안내](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/How_to/Trigger_install_prompt)
- [Unity 압축 및 배포](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-deploying.html)
 
