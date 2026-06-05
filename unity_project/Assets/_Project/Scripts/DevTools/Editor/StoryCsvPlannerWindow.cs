using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LoveAlgo.Story;             // StoryEditController
using LoveAlgo.Story.StoryEngine; // ScriptValidator.Violation

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 기획자 스토리 CSV 에디터(UI Toolkit EditorWindow). StreamingAssets/Story의 CSV를 편집·검증·라이브 적용·저장.
    /// 로직은 <see cref="StoryEditController"/>(빌드 런타임 패널과 공유)에 위임. UX: 미저장(dirty) 표시·전환 시
    /// 폐기 경고, 편집 중 자동 검증(디바운스), 위반 클릭→해당 줄 선택, Apply/Stop은 흐름 게이트로 차단.
    /// </summary>
    public class StoryCsvPlannerWindow : EditorWindow
    {
        const string BaseTitle = "Story CSV Planner";

        readonly StoryEditController _controller = new StoryEditController();

        DropdownField _storyDropdown;
        List<string> _stories = new();
        string _currentRel;
        TextField _editor;
        ScrollView _report;
        Label _status;
        Label _dirtyDot;
        Button _applyBtn;
        Button _stopBtn;
        Button _saveBtn;
        Toggle _strictToggle;
        IVisualElementScheduledItem _validateTimer;

        static readonly Color CGood = new(0.45f, 0.80f, 0.45f);
        static readonly Color CWarn = new(0.92f, 0.82f, 0.35f);
        static readonly Color CErr = new(0.92f, 0.45f, 0.45f);
        static readonly Color CMuted = new(0.7f, 0.7f, 0.7f);

        [MenuItem("Tools/Story/Story CSV Planner")]
        public static void Open()
        {
            var w = GetWindow<StoryCsvPlannerWindow>();
            w.titleContent = new GUIContent(BaseTitle);
            w.minSize = new Vector2(560, 460);
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = 8; root.style.paddingBottom = 8;
            root.style.paddingLeft = 8; root.style.paddingRight = 8;

            // ── 소스 행: 드롭다운 + dirty 점 + Refresh ──
            var srcRow = Row();
            _storyDropdown = new DropdownField("Story CSV");
            _storyDropdown.style.flexGrow = 1;
            _storyDropdown.RegisterValueChangedCallback(_ => OnPickStory());
            srcRow.Add(_storyDropdown);
            _dirtyDot = new Label("●") { tooltip = "미저장 변경" };
            _dirtyDot.style.color = CWarn; _dirtyDot.style.marginLeft = 4; _dirtyDot.style.marginRight = 4;
            _dirtyDot.style.unityTextAlign = TextAnchor.MiddleCenter;
            _dirtyDot.style.display = DisplayStyle.None;
            srcRow.Add(_dirtyDot);
            srcRow.Add(MakeButton("⟳", RefreshList, "목록 새로고침"));
            root.Add(srcRow);

            // ── 툴바 ──
            var bar = Row(); bar.style.marginTop = 6; bar.style.marginBottom = 6;
            bar.Add(MakeButton("Validate", Validate, "지금 검증"));
            _applyBtn = MakeButton("Apply ▶", Apply, "러닝 게임에 라이브 재생(Play 전용)"); bar.Add(_applyBtn);
            _stopBtn = MakeButton("Stop ■", Stop, "재생 중단(Play 전용)"); bar.Add(_stopBtn);
            _saveBtn = MakeButton("Save", Save, "StreamingAssets/Story에 저장"); bar.Add(_saveBtn);
            bar.Add(MakeButton("Reload", Reload, "디스크에서 다시 로드(편집 폐기)"));
            var spacer = new VisualElement(); spacer.style.flexGrow = 1; bar.Add(spacer);
            _strictToggle = new Toggle("Strict") { tooltip = "엄격 검증(경고도 에러로)" };
            bar.Add(_strictToggle);
            root.Add(bar);

            // ── 편집기 (긴 CSV=세로 / 긴 줄=가로 스크롤; ScrollView로 보장) ──
            root.Add(Header("CSV"));
            var editorScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            editorScroll.style.flexGrow = 1;
            editorScroll.style.minHeight = 220;
            Border(editorScroll);
            _editor = new TextField { multiline = true };
            _editor.style.whiteSpace = WhiteSpace.NoWrap;          // 줄바꿈 안 함 → 긴 줄은 가로 스크롤
            _editor.style.minWidth = Length.Percent(100);          // 짧은 내용도 폭 채움(길면 더 넓어짐)
            _editor.RegisterValueChangedCallback(OnEditorChanged); // 내용 크기로 늘어 → 바깥 ScrollView가 스크롤
            editorScroll.Add(_editor);
            root.Add(editorScroll);

            // ── 검증 패널 ──
            root.Add(Header("Validation"));
            _report = new ScrollView();
            _report.style.minHeight = 96; _report.style.maxHeight = 180;
            Border(_report);
            root.Add(_report);

            // ── 상태바 ──
            _status = new Label(); _status.style.marginTop = 6; _status.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_status);

            _validateTimer = root.schedule.Execute(Validate);
            _validateTimer.Pause();

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            RefreshList();
            RefreshModeUI();
        }

        void OnDestroy() => EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        void OnPlayModeChanged(PlayModeStateChange _) => RefreshModeUI();

        // ── UI 헬퍼 ──
        static VisualElement Row() { var v = new VisualElement(); v.style.flexDirection = FlexDirection.Row; return v; }
        static Label Header(string t)
        {
            var l = new Label(t);
            l.style.unityFontStyleAndWeight = FontStyle.Bold; l.style.marginTop = 6; l.style.marginBottom = 2;
            return l;
        }
        static Button MakeButton(string text, System.Action onClick, string tip = null)
        {
            var b = new Button(onClick) { text = text };
            if (tip != null) b.tooltip = tip;
            return b;
        }
        static void Border(VisualElement v)
        {
            var c = new Color(0f, 0f, 0f, 0.3f);
            v.style.borderTopWidth = 1; v.style.borderBottomWidth = 1; v.style.borderLeftWidth = 1; v.style.borderRightWidth = 1;
            v.style.borderTopColor = c; v.style.borderBottomColor = c; v.style.borderLeftColor = c; v.style.borderRightColor = c;
            v.style.paddingTop = 2; v.style.paddingBottom = 2; v.style.paddingLeft = 4; v.style.paddingRight = 4;
        }

        // ── 동작 ──
        void RefreshList()
        {
            _stories = _controller.ListStories();
            _storyDropdown.choices = new List<string>(_stories);
            _storyDropdown.SetValueWithoutNotify(_currentRel != null && _stories.Contains(_currentRel) ? _currentRel : "");
            SetStatus(_stories.Count > 0 ? $"스토리 CSV {_stories.Count}개 (StreamingAssets/Story)" : "StreamingAssets/Story에 .csv 없음", CMuted);
        }

        // 드롭다운 선택: 미저장이면 경고 후 폐기/취소.
        void OnPickStory()
        {
            int idx = _storyDropdown.index;
            if (idx < 0 || idx >= _stories.Count) return;
            string pick = _stories[idx];
            if (pick == _currentRel) return;
            if (!ConfirmDiscardIfDirty()) { _storyDropdown.SetValueWithoutNotify(_currentRel ?? ""); return; }
            LoadInto(pick);
        }

        void LoadInto(string rel)
        {
            _currentRel = rel;
            _editor.SetValueWithoutNotify(_controller.Load(rel));
            UpdateDirty();
            Validate();
            SetStatus($"로드: {rel}", CMuted);
        }

        void Reload()
        {
            if (string.IsNullOrEmpty(_currentRel)) { SetStatus("리로드할 소스 없음", CMuted); return; }
            if (!ConfirmDiscardIfDirty()) return;
            _editor.SetValueWithoutNotify(_controller.Load(_currentRel));
            UpdateDirty();
            Validate();
            SetStatus($"리로드: {_currentRel}", CMuted);
        }

        bool ConfirmDiscardIfDirty()
        {
            if (!_controller.IsDirty(_editor.value ?? "")) return true;
            return EditorUtility.DisplayDialog("미저장 변경",
                $"'{_currentRel}'에 저장하지 않은 변경이 있습니다. 폐기할까요?", "폐기", "취소");
        }

        void OnEditorChanged(ChangeEvent<string> _)
        {
            UpdateDirty();
            _validateTimer?.Pause();
            _validateTimer = rootVisualElement.schedule.Execute(Validate).StartingIn(350); // 편집 멈춤 후 자동 검증(디바운스)
        }

        void UpdateDirty()
        {
            bool dirty = _controller.IsDirty(_editor.value ?? "");
            if (_dirtyDot != null) _dirtyDot.style.display = dirty ? DisplayStyle.Flex : DisplayStyle.None;
            titleContent.text = dirty ? BaseTitle + " *" : BaseTitle;
        }

        void Validate()
        {
            var (violations, lineCount) = _controller.Validate(_editor.value ?? "", _strictToggle.value);
            RenderReport(violations, lineCount);
        }

        void Apply()
        {
            if (!EditorApplication.isPlaying) { SetStatus("Apply는 Play 모드 전용 — Game 씬을 Play하세요.", CWarn); return; }
            if (!_controller.Apply(_editor.value ?? "", _strictToggle.value))
            {
                SetStatus(_controller.CanApply ? "빈 CSV — Apply 생략" : "전환 중(데이루프 등) — 잠시 후 Apply", CWarn);
                return;
            }
            SetStatus("Apply ▶ 러닝 게임에 재생(화면 정리 후)", CGood);
        }

        void Stop()
        {
            if (!EditorApplication.isPlaying) { SetStatus("Stop은 Play 모드 전용", CWarn); return; }
            SetStatus(_controller.Stop() ? "Stop ■ 발행" : "전환 중 — 잠시 후", CMuted);
        }

        void Save()
        {
            if (string.IsNullOrEmpty(_currentRel)) { SetStatus("저장할 소스 없음 — Story CSV를 선택하세요", CWarn); return; }
            if (_controller.Save(_currentRel, _editor.value ?? ""))
            {
                AssetDatabase.Refresh();
                UpdateDirty();
                SetStatus($"저장됨: StreamingAssets/Story/{_currentRel}", CGood);
            }
            else SetStatus($"저장 실패: {_currentRel}", CErr);
        }

        void RefreshModeUI()
        {
            bool playing = EditorApplication.isPlaying;
            if (_applyBtn != null) _applyBtn.SetEnabled(playing);
            if (_stopBtn != null) _stopBtn.SetEnabled(playing);
            SetStatus(playing ? "Play 모드 — Apply 가능(전환 중엔 거부)" : "Edit 모드 — 편집/검증/저장 (Apply는 Play에서)", CMuted);
        }

        void RenderReport(List<ScriptValidator.Violation> violations, int lineCount)
        {
            _report.Clear();
            if (violations == null || violations.Count == 0)
            {
                var ok = new Label($"✔ OK — 위반 없음 ({lineCount} 라인)"); ok.style.color = CGood;
                _report.Add(ok);
                SetStatus($"검증 OK ({lineCount} 라인)", CGood);
                return;
            }
            int err = 0, warn = 0;
            foreach (var v in violations)
            {
                bool isErr = v.Severity == "Error";
                if (isErr) err++; else warn++;
                var line = v.LineNumber;
                var l = new Label((isErr ? "✖ " : "▲ ") + v.ToString());
                l.style.color = isErr ? CErr : CWarn;
                l.style.whiteSpace = WhiteSpace.Normal;
                l.tooltip = "클릭 → 해당 줄로 이동";
                l.RegisterCallback<ClickEvent>(_ => JumpToLine(line));
                l.RegisterCallback<MouseEnterEvent>(_ => l.style.unityFontStyleAndWeight = FontStyle.Bold);
                l.RegisterCallback<MouseLeaveEvent>(_ => l.style.unityFontStyleAndWeight = FontStyle.Normal);
                _report.Add(l);
            }
            SetStatus($"검증: Error {err} / Warning {warn} — 항목 클릭 시 해당 줄", err > 0 ? CErr : CWarn);
        }

        // 위반의 1-base 줄 번호로 편집기 커서를 옮기고 그 줄을 선택.
        void JumpToLine(int line1Based)
        {
            var text = _editor.value ?? "";
            int start = 0, ln = 1;
            while (start < text.Length && ln < line1Based) { if (text[start] == '\n') ln++; start++; }
            int end = text.IndexOf('\n', start);
            if (end < 0) end = text.Length;
            _editor.Focus();
            _editor.SelectRange(end, start); // (cursor, anchor) — 줄 전체 선택, 커서는 끝
        }

        void SetStatus(string s, Color c) { if (_status != null) { _status.text = s; _status.style.color = c; } }
    }
}
