# Store 리스팅 스크린샷 / Store listing screenshots

`--screens` dev 훅이 샘플 캡처를 시드해 주요 창을 렌더한 결과입니다. 각 PNG는 데스크톱 스토어 최소
요건인 **1366×768**(다크 캔버스 합성)이며, **한국어(`ko/`)와 영어(`en/`)** 두 세트를 제공합니다.

| 파일 | 내용 / Window |
|---|---|
| `01-inbox.png` | 인박스 / Inbox — 최근 캡처, 인라인 메모·태그, 프리셋 필터 |
| `02-compose.png` | 글쓰기 내보내기 / Compose & Export — 태그·프리셋·기간 필터 + Markdown 미리보기 |
| `03-settings.png` | 설정 / Settings — 프리셋·단축키·내보내기·자동 실행·**언어** |
| `04-quicknote.png` | 빠른 메모 / Quick Note — 캡처 직후 메모 카드 |

- 한국어 리스팅(ko-KR) → `ko/*.png`
- 영어 리스팅(en-US) → `en/*.png`

## 재생성 / Regenerate

```powershell
dotnet build ShotLog.sln
# 언어별로 culture를 적용해 렌더 (샘플 데이터도 해당 언어로 시드됨)
dotnet run --project src/ShotLog/ShotLog.csproj --no-build -- --screens "docs\store\screenshots\ko" ko
dotnet run --project src/ShotLog/ShotLog.csproj --no-build -- --screens "docs\store\screenshots\en" en
```

세 번째 인자(`ko`/`en`)를 생략하면 OS 언어를 따릅니다.

## 참고 / Notes

- Partner Center는 데스크톱 스크린샷 **1~10장**(1366×768 이상, PNG)을 언어별로 받습니다.
- 여기 이미지는 합성 샘플 데이터로 만든 **프로그램 렌더**입니다. 원하면 실데이터로 직접 캡처해 교체하면 더 좋습니다.
- 1:1 타일 / 16:9 히어로 등 프로모 아트는 `../assets/` 에 있습니다.
