# Store 리스팅 스크린샷

`--screens` dev 훅이 샘플 캡처를 시드해 주요 창을 렌더한 결과입니다. 각 PNG는 데스크톱 스토어 최소 요건인 **1366×768**(다크 캔버스 합성)로 저장됩니다.

| 파일 | 내용 |
|---|---|
| `01-inbox.png` | 인박스 — 최근 캡처 목록, 인라인 메모/태그, 프리셋 필터 |
| `02-compose.png` | 글쓰기 내보내기 — 태그/프리셋/기간 필터 + Markdown 미리보기 |
| `03-settings.png` | 설정 — 프리셋·단축키·내보내기·자동 실행 |
| `04-quicknote.png` | 캡처 직후 빠른 메모 카드 |

## 재생성
```powershell
dotnet build ShotLog.sln
dotnet run --project src/ShotLog/ShotLog.csproj --no-build -- --screens "docs\store\screenshots"
```

## 참고
- Partner Center는 데스크톱 스크린샷 **1~10장**(1366×768 이상, PNG)을 받습니다.
- 여기 이미지는 합성 샘플 데이터로 만든 **프로그램 렌더**입니다. 원하면 실제 사용 화면을 직접 캡처해 교체하면 더 좋습니다(실데이터·실제 배경).
- 1:1 타일/16:9 히어로 등 프로모 아트는 `../assets/` 에 있습니다.
