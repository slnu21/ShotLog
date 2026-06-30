# ShotLog — Microsoft Store(MSIX) 출시 체크리스트

이 문서는 ShotLog를 **MSIX 패키지**로 Microsoft Store에 올리기 위한 단계별 가이드입니다.
코드/패키징 준비물은 이미 리포지토리에 포함돼 있고(아래 "이미 완료" 참고), 나머지는 Partner Center 행정 + VS에서의 빌드/서명입니다.

> 근거: Store는 MSIX를 **무료로 재서명·자동업데이트·클린 설치** 처리하므로 유료 코드서명 인증서가 필요 없습니다.
> 풀트러스트 데스크톱 앱은 Medium IL로 일반 Win32 파일 접근이 되므로 `picturesLibrary`/`broadFileSystemAccess`는 **선언하지 않습니다**(후자는 오히려 심사 가중). 스크린샷을 다루므로 **개인정보처리방침 URL은 필수**입니다(Store 정책 10.5).

---

## 🔖 1.1.0.0 업데이트 (현재 준비 중)

1.0.0.0 최초 출시 이후의 **기능 업데이트**. 코드/매니페스트는 준비 완료, 남은 건 §4 빌드 → §7 재제출.

- **버전 1.1.0.0** — 세 곳 동기화 완료(`ShotLog.csproj`, `app.manifest`, `Package.appxmanifest`).
  revision(4번째)은 `0` 유지(§8 규칙).
- **이번 빌드의 새 기능**(Partner Center "이번 업데이트의 새로운 기능"에 사용):
  - Compose: 좌측 목록 메모 인라인 편집, 내보내기 이미지 너비 지정, 전체 선택/해제, 출력 폴더·너비 기억,
    HTML 내보내기 레이아웃 버그 수정.
  - Inbox: 카드 레이아웃 개편(큰 이미지·아이콘 액션·태그 칩 인라인 편집), 프리셋으로 이동(태그 교체 옵션·기억),
    화면(필터)에 보이는 항목 전체 선택/해제, 상단 툴바 슬림화.
  - Settings: 프리셋 색상 피커(팔레트 + Windows 색상 대화상자).
  - 전역: 다크 툴팁(가독성), 색상/변환기 추가.
- **스크린샷 갱신 완료**: 바뀐 UI로 `docs/store/screenshots/{ko,en}/*.png` 재생성(§0 `--screens` 훅).
- **의존성/라이선스 리스크 없음**: `docs/THIRD-PARTY-NOTICES.md` 참고(WebView2·.NET 모두 무료·상용 배포 허용,
  번들 폰트 없음 → 시스템 폰트 폴백).
- **자체서명 사이드로드 테스트는 이번 업데이트엔 선택**: 매니페스트 구조·capability 변화 없이 순수 관리코드(C#/XAML)만
  바뀌었고 1.0.0.0에서 전체 파이프라인+WACK PASS를 이미 검증했으므로, 곧장 §4의 Store 빌드(`.msixupload`)로 진행 가능.
  (제출 전 §6 WACK 1회만 권장.)

---

## ✅ 1.0.0.0 상태 (2026-06: 로컬에서 `.msixupload` 빌드 검증 완료)

VS Community **2026 (18.5.2)** 환경에서 Store 업로드 패키지까지 빌드·검증 완료. 남은 건 Partner Center 제출(§7)뿐.

- **Identity 채움(§2 완료)**: `Name=SlnU.ShotLog`, `Publisher=CN=1398342C-A2D7-4B4A-BFE2-34D8CCFD7FBA`, `PublisherDisplayName=SlnU`
- **솔루션 통합(§3 완료)**: `ShotLog.sln`에 wapproj + `Debug/Release|x64` 추가. `dotnet build ShotLog.sln`(Any CPU) 호환 유지(wapproj는 x64에서만 Build).
- **산출물**: `src/ShotLog.Package/AppPackages/ShotLog.Package_1.0.0.0_x64_bundle.msixupload` (self-contained, ~74MB)

### 빌드 전제조건 (VS 2026 기준)
- VS 컴포넌트 **`Microsoft.VisualStudio.ComponentGroup.MSIX.Packaging`** (DesktopBridge MSBuild 타겟). 미설치 시:
  `& "<VS>\..\Installer\setup.exe" modify --installPath "<VS>" --add Microsoft.VisualStudio.ComponentGroup.MSIX.Packaging --passive --norestart`
- Windows SDK **10.0.26100.0** (wapproj `TargetPlatformVersion`이 이 버전을 참조).

### 빌드를 위해 적용된 수정 (리포지토리에 반영됨)
1. `ShotLog.csproj` — `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>` 추가 (self-contained 복원; 누락 시 NETSDK1047).
2. `ShotLog.Package.wapproj` — `SelfContained`/`RuntimeIdentifier win-x64` 활성화, `TargetPlatformVersion`을 `10.0.26100.0`으로.
3. `Package.appxmanifest` — self-contained 페이로드가 `ShotLog\` 하위로 하베스팅되므로 Executable을 `ShotLog\ShotLog.exe`로 (Application + startupTask 둘 다).
4. `StoreAssetGenerator.cs` — 스케일 에셋 크기를 round-half-up으로 (정수나눗셈 잘림 → APPX1619). 수정 후 `--genassets`로 52종 재생성.

### 검증된 빌드 명령 (CLI)
```powershell
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
& $msbuild src\ShotLog.Package\ShotLog.Package.wapproj /restore `
  /p:Configuration=Release /p:Platform=x64 `
  /p:UapAppxPackageBuildMode=StoreUpload /p:AppxBundle=Always
```

---

## 0. 이미 완료된 준비물 (이 리포지토리)

| 항목 | 위치 |
|---|---|
| MSIX 인지 autostart(StartupTask, TaskId=`ShotLogStartup`) | `src/ShotLog/Infrastructure/AutoStartService.cs` |
| 패키지 매니페스트(runFullTrust, startupTask, 타일 BackgroundColor `#0D1117`) | `src/ShotLog.Package/Package.appxmanifest` |
| 패키징 프로젝트(.wapproj, x64/arm64) | `src/ShotLog.Package/ShotLog.Package.wapproj` |
| 스토어/타일 비주얼 에셋(52종) | `src/ShotLog.Package/Images/` |
| 버전 1.1.0.0 (revision=0) | `src/ShotLog/ShotLog.csproj`, `app.manifest`, `Package.appxmanifest` |
| 리스팅 카피/개인정보처리방침/스크린샷 | `docs/store/` |

에셋·스크린샷은 앱의 dev 훅으로 언제든 재생성:
```powershell
# 빌드
dotnet build ShotLog.sln
# 타일/스토어 로고 + 프로모 아트 (Images/ 와 docs/store/assets/ 로)
dotnet run --project src/ShotLog/ShotLog.csproj --no-build -- --genassets "src\ShotLog.Package\Images" "docs\store\assets"
# 리스팅 스크린샷 4종 (docs/store/screenshots/ 로)
dotnet run --project src/ShotLog/ShotLog.csproj --no-build -- --screens "docs\store\screenshots"
```

---

## 1. Partner Center 계정 & 이름 예약
1. https://partner.microsoft.com/dashboard 에서 개발자 계정 생성. 스크린샷을 다루는 앱이므로 **회사 계정 권장**(등록비/검증은 가입 시 현행 기준 확인).
2. **Apps and games → New product → MSIX/PWA app** 에서 이름 **"ShotLog"** 예약(사용 가능 시).
3. 예약 후 **Product → Product identity** 에서 다음 3개 값을 확인:
   - **Package/Identity/Name** (예: `12345Publisher.ShotLog`)
   - **Publisher** (예: `CN=ABCD1234-...`)
   - **Publisher display name**

## 2. 매니페스트 Identity 채우기
`src/ShotLog.Package/Package.appxmanifest` 의 placeholder를 1번에서 받은 값으로 교체:
```xml
<Identity Name="<Package/Identity/Name>" Publisher="<Publisher>" Version="1.0.0.0" />
...
<PublisherDisplayName><Publisher display name></PublisherDisplayName>
```
> 로컬 사이드로드 테스트만 할 때는 `Publisher`를 자체서명 인증서 주체(예 `CN=ShotLog-Test`)로 맞춰도 됩니다.

## 3. 솔루션에 패키징 프로젝트 추가 (Visual Studio 2022)
> `.wapproj`는 **MSBuild/VS 전용**입니다. VS 2022 설치 시 **"Windows 애플리케이션 패키징 프로젝트"/"MSIX Packaging Tools"** 구성요소가 필요합니다(없으면 Visual Studio Installer에서 추가).
1. `ShotLog.sln`을 VS로 열기 → 솔루션 우클릭 → **Add → Existing Project →** `src/ShotLog.Package/ShotLog.Package.wapproj`.
2. **Configuration Manager**에서 활성 플랫폼을 **x64**로(“Any CPU” 아님). `ShotLog`와 `ShotLog.Package` 모두 x64로 빌드되게 체크.
3. (권장) **self-contained .NET 8** — 사용자가 런타임 설치 없이 실행. `ShotLog.Package.wapproj` 의 주석 처리된 두 줄을 활성화:
   ```xml
   <SelfContained>true</SelfContained>
   <RuntimeIdentifier>win-x64</RuntimeIdentifier>
   ```
   (대안: framework-dependent — 패키지 작지만 .NET 8 Desktop Runtime 필요.)

## 4. MSIX 빌드
- VS: `ShotLog.Package` 우클릭 → **Publish → Create App Packages → Microsoft Store** 마법사(이름 예약과 연결) → `.msixupload` 생성.
- 또는 CLI(개발자 명령 프롬프트):
  ```powershell
  msbuild src\ShotLog.Package\ShotLog.Package.wapproj /restore `
    /p:Configuration=Release /p:Platform=x64 `
    /p:UapAppxPackageBuildMode=StoreUpload /p:AppxBundle=Always
  ```
  산출물: `src/ShotLog.Package/AppPackages/.../*.msixupload`(스토어 업로드용), `*.msix`(로컬 테스트용).

## 5. 로컬 사이드로드 테스트 (제출 전 권장)
```powershell
# 1) 자체서명 인증서 생성 — Subject는 매니페스트 Publisher와 동일해야 함
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=ShotLog-Test" `
  -KeyUsage DigitalSignature -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3","2.5.29.19={text}")
# 2) (VS Publish가 서명까지 해주면 생략) signtool로 .msix 서명
# 3) 인증서를 "신뢰할 수 있는 사람/루트"에 설치한 뒤 .msix 더블클릭 → 설치
```
설치 후 확인:
- 트레이 아이콘(흰 노트 카드)·창 제목표시줄/작업표시줄 아이콘 정상.
- **autostart**: 설정에서 "Windows 시작 시 자동 실행" 토글 → **작업 관리자 → 시작프로그램**에 "ShotLog" 표시, 로그인 시 시작. 끄면 비활성. (작업 관리자에서 사용자가 끄면 설정 체크박스가 비활성+안내 표시.)
- 단축키·캡처(즉시/영역/창)·Inbox/Compose/Markdown 내보내기 동작.
- 타일: 시작 메뉴에서 네이비 타일 위 흰 카드.

## 6. WACK (Windows App Certification Kit)
> WACK는 **관리자 권한** + **설치 가능(서명·신뢰)된 패키지**가 필요합니다(앱을 배포해 테스트하므로).
> Store 업로드 패키지는 미서명이라 그대로는 못 돌리므로, **테스트 번들**을 일회용 자체서명 인증서로 서명해 검사합니다.
> (배포용 `.msixupload`는 건드리지 않음.)

**간편 실행(권장)** — 위 전 과정을 자동화한 스크립트. **관리자 PowerShell**에서:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\run-wack.ps1
# 결과: build\wack\wack-<package>.xml  (브라우저로 열어 OVERALL_RESULT 확인)
```

**수동(참고)**:
```powershell
& "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe" reset
& "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe" test `
  -appxpackagepath "<...>\ShotLog_1.1.0.0_x64.msix" -reportoutputpath "wack.xml"
```
- 실제 결과(2026-06, 이 패키지): **OVERALL_RESULT = PASS** (`APP_TYPE=Centennial`).
  - 선택(OPTIONAL) 테스트 **"차단된 실행 파일"** 1건만 FAIL → self-contained .NET 런타임 DLL들이 `cmd`/`reg`/`CreateProcess`/`ShellExecute` 문자열을 내부에 포함하는 **알려진 오탐**. OPTIONAL이라 OVERALL/제출을 차단하지 않음.
  - `runFullTrust`는 제출 노트의 정당화 문구로 통과(§7).

## 7. Partner Center 제출
- **Packages**: `.msixupload` 업로드.
- **Store listing(언어별: 한국어/영어)**: `docs/store/listing.md` 의 카피 사용. 스크린샷 `docs/store/screenshots/*.png`(데스크톱 1366×768) 1~10장, 1:1 타일 `docs/store/assets/StoreTile-300x300.png`, 16:9 히어로 `Hero-1920x1080.png`.
- **Privacy policy URL**(필수): GitHub Pages로 호스팅됨 →
  **`https://slnu21.github.io/ShotLog/store/privacy-policy.html`** (소스: `docs/store/privacy-policy.md`, main:/docs).
  ⚠️ 제출 전 정책 본문의 `[연락처 이메일]`/`[contact email]`을 실제 값으로 채울 것.
- **Age ratings(IARC)**: 설문 작성(스크린샷 유틸리티 → 전연령 예상).
- **Properties**: 카테고리 *생산성*(또는 *유틸리티 및 도구*), 지원 연락처.
- **Notes for certification**: 아래 정당화 문구 붙여넣기.

### runFullTrust 정당화 (제출 노트에 붙여넣기)
```
ShotLog is a full-trust Win32 (.NET 8 WPF) desktop utility. The runFullTrust capability is
required to: (1) register system-wide capture hotkeys via RegisterHotKey, (2) capture the
screen / active window via GDI (CopyFromScreen, PrintWindow), and (3) provide a system-tray
presence (Shell_NotifyIcon) — none of which are available from an AppContainer. The app makes
no network calls; all screenshots and notes are stored locally on the user's device only.
```

## 8. 제출 후
- 인증 보통 수 영업일. 통과 시 게시.
- **버전 규칙**: MSIX 버전은 4부분 `Major.Minor.Build.Revision`. **Store 업로드 패키지는 4번째(revision)를
  반드시 `0`으로** 둬야 하며(Store가 예약), 업데이트는 `Major`/`Minor`/`Build` 중 하나를 올려 단조 증가시킨다.
  세 매니페스트(`ShotLog.csproj <Version>`, `app.manifest`, `Package.appxmanifest <Identity Version>`)를
  같은 값으로 맞춰 재빌드·재업로드.

---

## 참고: 패키지 안에서 달라지는 동작
- **데이터 저장 위치**: 패키지 앱의 `%APPDATA%\ShotLog`(설정·인덱스) 쓰기는 컨테이너로 리다이렉트될 수 있어, 기존 비패키지 사용자 데이터와 분리됩니다(첫 출시엔 무관).
- **autostart**: 비패키지(개발/포터블) 빌드는 HKCU Run, 패키지 빌드는 StartupTask로 자동 분기됩니다(`AutoStartService`).
- 캡처 PNG·내보내기는 풀트러스트로 Pictures/Documents/사용자 지정 폴더에 그대로 저장됩니다.
