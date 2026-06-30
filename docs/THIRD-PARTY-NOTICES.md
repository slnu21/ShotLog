# Third-Party Notices & License Review

ShotLog의 배포(특히 Microsoft Store 상용 배포) 관점에서 **유료화·로열티·재배포 제약 리스크가 있는지**
점검한 결과입니다. 결론: **리스크 없음** — 모든 구성요소가 무료이며 상용 배포가 허용됩니다.

| 구성요소 | 종류 | 라이선스 | 비용/로열티 | 재배포 |
|---|---|---|---|---|
| **Microsoft.Web.WebView2** (`1.0.3912.50`) | NuGet SDK + Evergreen 런타임 | Microsoft Software License Terms (WebView2 SDK) | **무료**, 로열티 없음 | 허용. Evergreen 런타임은 Microsoft가 배포·갱신, Windows 11 기본 탑재 |
| **.NET 8** (WPF·WinForms·BCL) | 런타임/프레임워크 | MIT | **무료**(상용 포함) | self-contained 번들 배포 허용(로열티 없음) |
| **Windows App SDK / MSIX 툴**(makeappx·signtool·WACK) | 빌드 전용 도구 | Windows SDK 라이선스 | 무료 | 재배포 대상 아님(빌드타임만) |
| **폰트**(Pretendard·D2Coding·Segoe UI·Malgun Gothic·Consolas) | FontFamily 폴백 이름만 참조 | — | — | **번들 안 함**. 미설치 시 Windows 기본 폰트(Segoe UI/Malgun Gothic)로 폴백 |
| **앱 아이콘**(`Assets/shotlog.ico`, 스토어 타일 52종) | 자체 제작 | 프로젝트 소유 | — | — |

## 세부 메모

- **WebView2** — NuGet 패키지는 단 하나의 외부 의존성. SDK 라이선스 약관상 응용프로그램에 **자유롭게 포함·재배포**
  할 수 있고(상용 포함), 별도 비용/로열티가 없다. ShotLog는 WebView2로 **로컬 HTML(렌더링 미리보기)만** 표시하며
  외부 네트워크 탐색을 하지 않으므로 런타임 텔레메트리 노출도 최소.
- **.NET 8** — MIT. self-contained(win-x64) 배포 시 런타임 DLL이 패키지에 포함되며 이는 Microsoft가 허용하는
  royalty-free 재배포다. (현 MSIX 빌드는 `SelfContained=true` → 사용자 런타임 설치 불필요.)
- **폰트 번들 없음** — 저장소·패키지에 `.ttf/.otf`가 없다(확인됨). 따라서 폰트 재배포 라이선스 의무가 발생하지 않는다.
  (참고로 Pretendard·D2Coding은 SIL OFL로, 만약 추후 번들하더라도 상용 무료다.)
- **네트워크/결제 없음** — 앱은 네트워크 호출이 없고(개인정보처리방침·`runFullTrust` 정당화 문구와 일치) 결제/구독
  SDK도 포함하지 않는다. 따라서 제3자 유료 컴포넌트로 인한 향후 과금 전환 리스크가 없다.

## 재점검 트리거

- 새 `PackageReference` 추가 시 이 표를 갱신.
- 폰트/아이콘 등 **에셋을 번들**하기로 하면 해당 라이선스 고지를 추가.
- WebView2를 **로컬 렌더링 외 용도**(외부 URL 탐색 등)로 확장하면 데이터 처리/개인정보처리방침 재검토.
