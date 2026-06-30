# -*- coding: utf-8 -*-
# Generates Strings.resx (neutral=English), Strings.ko.resx (Korean) and Strings.Designer.cs
# from one key table, so keys can never drift between the three. Run once, then delete.
import os, html

# (key, english/neutral, korean)
DATA = [
    # Common
    ("Common_Save", "Save", "저장"),
    ("Common_Close", "Close", "닫기"),
    ("Common_Delete", "Delete", "삭제"),
    ("Common_Browse", "Browse", "찾아보기"),
    ("Common_All", "All", "전체"),
    ("Common_Tags", "Tags", "태그"),
    ("Common_Preset", "Preset", "프리셋"),
    ("Common_DefaultPreset", "Default", "기본"),
    ("Common_OK", "OK", "확인"),
    ("Common_Cancel", "Cancel", "취소"),
    ("Common_Done", "Done", "완료"),
    ("Common_Copy", "Copy", "복사"),
    # Tray menu (WinForms)
    ("Tray_CaptureMonitor", "Capture Active Monitor", "활성 모니터 캡처"),
    ("Tray_CaptureNote", "Capture + Memo", "캡처 + 메모"),
    ("Tray_CaptureRegion", "Capture Region", "영역 선택 캡처"),
    ("Tray_CaptureWindow", "Capture Active Window", "활성 창 캡처"),
    ("Tray_CaptureClipboard", "Capture to Clipboard", "클립보드로 캡처"),
    ("Tray_Inbox", "Inbox", "인박스"),
    ("Tray_Compose", "Compose & Export", "글쓰기 내보내기"),
    ("Tray_Settings", "Settings", "설정"),
    ("Tray_Exit", "Exit", "종료"),
    # QuickNote
    ("QuickNote_Title", "ShotLog — Capture Note", "ShotLog — 캡처 메모"),
    ("QuickNote_Header", "Note on capture", "캡처에 메모"),
    ("QuickNote_SaveLocation", "Save location (preset)", "저장 위치 (프리셋)"),
    ("QuickNote_TagsWatermark", "Type a tag, then Enter", "태그 입력 후 Enter"),
    ("QuickNote_Memo", "Memo", "메모"),
    ("QuickNote_Hint", "Enter to save · Shift+Enter newline · Esc to discard", "Enter 저장 · Shift+Enter 줄바꿈 · Esc 버리기"),
    ("QuickNote_Discard", "Discard", "버리기"),
    ("QuickNote_SaveFailed", "Save failed: ", "저장 실패: "),
    ("QuickNote_Annotate", "✎ Edit", "✎ 편집"),
    # Region select
    ("Region_Title", "ShotLog — Select Region", "ShotLog — 영역 선택"),
    ("Region_Hint", "Drag to select a region · Esc to cancel", "드래그하여 영역 선택 · Esc 취소"),
    # Compose
    ("Compose_Title", "ShotLog — Compose & Export", "ShotLog — 글쓰기 내보내기"),
    ("Compose_GroupBy", "Group by", "묶음 기준"),
    ("Compose_Period", "Period", "기간"),
    ("Compose_TitleLabel", "Title", "제목"),
    ("Compose_OutputFolder", "Output folder", "출력 폴더"),
    ("Compose_Preview", "Preview (standard Markdown)", "미리보기 (표준 Markdown)"),
    ("Compose_IncludeFrontMatter", "Include YAML front matter", "YAML front matter 포함"),
    ("Compose_CreateAndOpen", "Create and open", "생성 후 열기"),
    ("Compose_Today", "Today", "오늘"),
    ("Compose_Last7", "Last 7 days", "최근 7일"),
    ("Compose_CountFormat", "Captures to include · chronological ({0}/{1})", "포함할 캡처 · 시간순 ({0}/{1})"),
    ("Compose_DefaultTitleFormat", "{0} Capture Log", "{0} 캡처 기록"),
    ("Compose_NoTags", "(no tags)", "(태그 없음)"),
    ("Compose_NoMemo", "(no memo)", "(메모 없음)"),
    ("Compose_SelectAtLeastOne", "Select at least one capture to include.", "포함할 캡처를 하나 이상 선택하세요."),
    ("Compose_ExportFailed", "Export failed: ", "내보내기 실패: "),
    ("Compose_GeneratedStatusFormat", "Generated: {0} · {1} image(s)", "생성됨: {0} · 이미지 {1}장"),
    ("Compose_RenderedPreview", "Rendered preview", "렌더링 미리보기"),
    ("Compose_WebViewMissing", "The WebView2 runtime is not installed, so the rendered preview is unavailable. The text preview on the left still works.", "WebView2 런타임이 설치되어 있지 않아 렌더링 미리보기를 표시할 수 없습니다. 왼쪽 텍스트 미리보기는 그대로 사용할 수 있습니다."),
    ("Compose_CopyMarkdown", "Copy Markdown", "MD 복사"),
    ("Compose_ExportHtml", "Export HTML", "HTML 내보내기"),
    ("Compose_CopiedStatus", "Copied Markdown to the clipboard.", "마크다운을 클립보드에 복사했습니다."),
    ("Compose_HtmlGeneratedFormat", "HTML generated: {0}", "HTML 생성됨: {0}"),
    ("Compose_ImageWidth", "Image width (px, 0 = original)", "이미지 너비 (px, 0=원본)"),
    ("Compose_SelectAll", "Select all", "전체 선택"),
    ("Compose_SelectNone", "Clear", "전체 해제"),
    # Inbox
    ("Inbox_Title", "ShotLog — Inbox", "ShotLog — 인박스"),
    ("Inbox_Refresh", "Refresh", "새로고침"),
    ("Inbox_ComposeButton", "✍ Compose & Export", "✍ 글쓰기 내보내기"),
    ("Inbox_NoImage", "No image", "이미지 없음"),
    ("Inbox_DeleteButton", "🗑 Delete", "🗑 삭제"),
    ("Inbox_Empty", "No captures yet. Try capturing with a hotkey.", "아직 캡처가 없습니다. 단축키로 캡처해 보세요."),
    ("Inbox_NoMatch", "No captures match the filter.", "조건에 맞는 캡처가 없습니다."),
    ("Inbox_DeleteConfirmFormat", "Delete this capture?\n{0}\n\nThe image file will be deleted too.", "이 캡처를 삭제할까요?\n{0}\n\n이미지 파일도 함께 삭제됩니다."),
    ("Inbox_DeleteTitle", "Delete capture", "캡처 삭제"),
    ("Inbox_Annotate", "✎ Annotate", "✎ 주석"),
    ("Inbox_CopyButton", "📋 Copy", "📋 복사"),
    ("Inbox_OpenButton", "📂 Folder", "📂 탐색기"),
    ("Inbox_DeleteSelected", "🗑 Delete selected", "🗑 선택 삭제"),
    ("Inbox_DeleteSelectedConfirmFormat", "Delete the {0} selected capture(s)?\n\nTheir image files will be deleted too.", "선택한 캡처 {0}개를 삭제할까요?\n\n이미지 파일도 함께 삭제됩니다."),
    ("Inbox_NoneSelected", "No captures are selected.", "선택된 캡처가 없습니다."),
    ("Inbox_MoveToPreset", "📁 Move to preset", "📁 프리셋으로 이동"),
    ("Inbox_MoveTitle", "Move to preset", "프리셋으로 이동"),
    ("Inbox_MovePrompt", "Choose the destination preset.", "이동할 대상 프리셋을 선택하세요."),
    ("Inbox_MoveReplaceTags", "Replace tags with the preset's default tags", "태그를 프리셋 기본 태그로 교체"),
    ("Inbox_MoveDoneFormat", "Moved {0} capture(s).", "캡처 {0}개를 이동했습니다."),
    ("Inbox_MoveNoPresets", "No presets are defined. Add one in Settings first.", "프리셋이 없습니다. 먼저 설정에서 추가하세요."),
    ("Inbox_TipCopy", "Copy image", "이미지 복사"),
    ("Inbox_TipAnnotate", "Annotate", "주석"),
    ("Inbox_TipFolder", "Show in Explorer", "탐색기에서 보기"),
    ("Inbox_TipDelete", "Delete", "삭제"),
    ("Inbox_EditTags", "Edit tags", "태그 편집"),
    ("Inbox_SelectVisible", "Select all", "전체 선택"),
    ("Inbox_SelectVisibleTip", "Selects only the captures currently shown by the filter", "현재 필터로 보이는 캡처만 전체 선택"),
    ("Inbox_SelectNone", "Clear", "전체 해제"),
    ("Inbox_TagsWatermark", "tag1, tag2 …", "태그1, 태그2 …"),
    # Settings
    ("Settings_Title", "ShotLog — Settings", "ShotLog — 설정"),
    ("Settings_PresetsSection", "Presets (save locations)", "프리셋 (저장 위치)"),
    ("Settings_Name", "Name", "이름"),
    ("Settings_Folder", "Folder", "폴더"),
    ("Settings_DefaultTags", "Default tags (comma)", "기본 태그 (쉼표)"),
    ("Settings_Color", "Color", "색상"),
    ("Settings_AddPreset", "+ Add preset", "+ 프리셋 추가"),
    ("Settings_HotkeysSection", "Hotkeys", "단축키"),
    ("Settings_HkInstant", "Instant capture → active preset", "즉시 캡처 → 활성 프리셋"),
    ("Settings_HkNote", "Capture + memo", "캡처 + 메모"),
    ("Settings_HkRegion", "Region capture", "영역 선택 캡처"),
    ("Settings_HkWindow", "Active window capture", "활성 창 캡처"),
    ("Settings_HkInbox", "Open Inbox", "인박스 열기"),
    ("Settings_HkClipboard", "Capture to clipboard", "클립보드로 캡처"),
    ("Settings_HotkeyHint", "Format: Ctrl+Alt+S, Ctrl+Shift+1 …  (applies on save)", "형식: Ctrl+Alt+S, Ctrl+Shift+1 …  (변경은 저장 시 적용)"),
    ("Settings_ExportSection", "Export", "내보내기"),
    ("Settings_DefaultOutputFolder", "Default output folder", "기본 출력 폴더"),
    ("Settings_Sidecar", "Write a same-named .md sidecar next to the PNG (memo visible in Explorer)", "PNG 옆에 같은 이름 .md 사이드카 생성 (탐색기에서도 메모 확인)"),
    ("Settings_Notify", "Show a tray notification after instant capture", "즉시 캡처 후 트레이 알림 표시"),
    ("Settings_GeneralSection", "General", "일반"),
    ("Settings_AutoStart", "Start with Windows", "Windows 시작 시 자동 실행"),
    ("Settings_Language", "Language", "언어"),
    ("Settings_LangSystem", "System (auto)", "시스템 자동"),
    ("Settings_HkShortInstant", "Instant capture", "즉시 캡처"),
    ("Settings_HkShortNote", "Capture+memo", "캡처+메모"),
    ("Settings_HkShortRegion", "Region", "영역 선택"),
    ("Settings_HkShortWindow", "Active window", "활성 창"),
    ("Settings_HkShortInbox", "Inbox", "인박스"),
    ("Settings_HkShortClipboard", "Clipboard", "클립보드"),
    ("Settings_InvalidHotkeyFormat", "Invalid hotkey: {0}\nExample: Ctrl+Alt+S", "올바르지 않은 단축키: {0}\n예: Ctrl+Alt+S"),
    ("Settings_AutoStartLockedTip", "Enable ShotLog in Task Manager > Startup apps.", "작업 관리자 > 시작프로그램에서 ShotLog를 켜야 합니다."),
    # Annotation editor
    ("Annot_Title", "ShotLog — Annotate", "ShotLog — 주석"),
    ("Annot_Select", "Move / select", "이동 / 선택"),
    ("Annot_Pen", "Pen", "펜"),
    ("Annot_Highlighter", "Highlighter", "형광펜"),
    ("Annot_Arrow", "Arrow", "화살표"),
    ("Annot_Rect", "Rectangle", "사각형"),
    ("Annot_Text", "Text box", "텍스트 상자"),
    ("Annot_Undo", "Undo", "실행취소"),
    ("Annot_Clear", "Clear all", "모두 지우기"),
    ("Annot_Color", "Color", "색상"),
    ("Annot_Width", "Thickness", "굵기"),
    ("Annot_TextWatermark", "Type text…", "텍스트 입력…"),
    # Dialogs
    ("Dialog_Title", "ShotLog", "ShotLog"),
    ("Picker_Custom", "Custom…", "사용자 지정…"),
    # Export / Sidecar / Notify
    ("Export_Untitled", "Untitled", "제목 없음"),
    ("Sidecar_Time", "Time", "시각"),
    ("Notify_SavedFormat", "{0} · {1} saved (add note {2})", "{0} · {1} 저장됨 (메모 추가 {2})"),
    ("Notify_Clipboard", "Captured to the clipboard.", "클립보드에 캡처했습니다."),
]

RESX_HEADER = '''<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
'''

def write_resx(path, idx):
    parts = [RESX_HEADER]
    for key, *vals in DATA:
        val = html.escape(vals[idx], quote=False)  # escapes & < >
        parts.append('  <data name="%s" xml:space="preserve">\n    <value>%s</value>\n  </data>\n' % (key, val))
    parts.append('</root>\n')
    with open(path, "w", encoding="utf-8") as f:
        f.write("".join(parts))

def write_designer(path):
    lines = []
    lines.append("// <auto-generated>")
    lines.append("//   Generated by _gen_strings.py from the single key table. Do not edit by hand;")
    lines.append("//   regenerate to keep Strings.resx / Strings.ko.resx / this file in sync.")
    lines.append("// </auto-generated>")
    lines.append("#nullable enable")
    lines.append("using System.Globalization;")
    lines.append("using System.Resources;")
    lines.append("")
    lines.append("namespace ShotLog.Resources;")
    lines.append("")
    lines.append("/// <summary>Typed accessor over the embedded Strings resources (neutral = English, ko satellite).</summary>")
    lines.append("public static class Strings")
    lines.append("{")
    lines.append('    private static readonly ResourceManager _rm =')
    lines.append('        new ResourceManager("ShotLog.Resources.Strings", typeof(Strings).Assembly);')
    lines.append("")
    lines.append("    /// <summary>Overrides the lookup culture. Null falls back to CurrentUICulture.</summary>")
    lines.append("    public static CultureInfo? Culture { get; set; }")
    lines.append("")
    lines.append("    private static string G(string key) => _rm.GetString(key, Culture) ?? key;")
    lines.append("")
    for key, *_ in DATA:
        lines.append('    public static string %s => G("%s");' % (key, key))
    lines.append("}")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

base = os.path.join("src", "ShotLog", "Resources")
os.makedirs(base, exist_ok=True)
write_resx(os.path.join(base, "Strings.resx"), 0)      # English / neutral
write_resx(os.path.join(base, "Strings.ko.resx"), 1)   # Korean
write_designer(os.path.join(base, "Strings.Designer.cs"))
print("generated %d keys -> %s" % (len(DATA), base))
# sanity: keys unique
ks = [k for k, *_ in DATA]
assert len(ks) == len(set(ks)), "DUPLICATE KEY!"
print("unique keys OK")
