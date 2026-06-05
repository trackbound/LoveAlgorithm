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
    /// 로직은 <see cref="StoryEditController"/>(빌드 런타임 패널과 공유)에 위임 — 이쪽은 에디트 모드 검증/저장 +
    /// Play 시 라이브 적용. Apply/Stop은 흐름 게이트(데이루프 전환 등)가 잠기면 거부된다(안전장치). 빌드용은 별도
    /// 런타임 패널(STORY_EDITOR_RUNTIME). 구 ScenarioEditor의 런타임 MonoBehaviour 모델은 답습하지 않음.
    /// </summary>
    public class StoryCsvPlannerWindow : EditorWindow
    {
        readonly StoryEditController _controller = new StoryEditController();

        DropdownField _storyDropdown;
        List<string> _stories = new();
        string _currentRel;
        TextField _editor;
        ScrollView _report;
        Label _status;
        Button _applyBtn;
        Button _stopBtn;
        Toggle _strictToggle;

        [MenuItem("Tools/Story/Story CSV Planner")]
        public static void Open()
        {
            var w = GetWindow<StoryCsvPlannerWindow>();
            w.titleContent = new GUIContent("Story CSV Planner");
            w.minSize = new Vector2(540, 440);
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = 6; root.style.paddingBottom = 6;
            root.style.paddingLeft = 6; root.style.paddingRight = 6;

            // 소스: StreamingAssets/Story 의 .csv 목록(스토리 CSV만).
            var srcRow = new VisualElement();
            srcRow.style.flexDirection = FlexDirection.Row;
            _storyDropdown = new DropdownField("Story CSV");
            _storyDropdown.style.flexGrow = 1;
            _storyDropdown.RegisterValueChangedCallback(_ => LoadSelected());
            srcRow.Add(_storyDropdown);
            srcRow.Add(MakeButton("Refresh", RefreshList));
            root.Add(srcRow);

            // 툴바.
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.marginTop = 4; bar.style.marginBottom = 4;
            bar.Add(MakeButton("Validate", Validate));
            _applyBtn = MakeButton("Apply ▶ (Play)", Apply); bar.Add(_applyBtn);
            _stopBtn = MakeButton("Stop ■", Stop); bar.Add(_stopBtn);
            bar.Add(MakeButton("Save", Save));
            bar.Add(MakeButton("Reload", Reload));
            _strictToggle = new Toggle("Strict");
            _strictToggle.style.marginLeft = 8;
            bar.Add(_strictToggle);
            root.Add(bar);

            var editorLabel = new Label("CSV"); editorLabel.style.marginTop = 4;
            root.Add(editorLabel);
            _editor = new TextField { multiline = true };
            _editor.style.flexGrow = 1;
            _editor.style.minHeight = 200;
            root.Add(_editor);

            var reportLabel = new Label("Validation"); reportLabel.style.marginTop = 4;
            root.Add(reportLabel);
            _report = new ScrollView();
            _report.style.minHeight = 90; _report.style.maxHeight = 170;
            var border = new Color(0f, 0f, 0f, 0.3f);
            _report.style.borderTopWidth = 1; _report.style.borderBottomWidth = 1;
            _report.style.borderLeftWidth = 1; _report.style.borderRightWidth = 1;
            _report.style.borderTopColor = border; _report.style.borderBottomColor = border;
            _report.style.borderLeftColor = border; _report.style.borderRightColor = border;
            root.Add(_report);

            _status = new Label(); _status.style.marginTop = 4; _status.style.opacity = 0.85f;
            root.Add(_status);

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            RefreshList();
            RefreshModeUI();
        }

        void OnDestroy() => EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        void OnPlayModeChanged(PlayModeStateChange _) => RefreshModeUI();

        static Button MakeButton(string text, System.Action onClick) => new Button(onClick) { text = text };

        void RefreshList()
        {
            _stories = _controller.ListStories();
            _storyDropdown.choices = new List<string>(_stories);
            _storyDropdown.SetValueWithoutNotify(""); // 선택 시 로드(빈 시작).
            SetStatus(_stories.Count > 0
                ? $"스토리 CSV {_stories.Count}개 (StreamingAssets/Story)"
                : "StreamingAssets/Story에 .csv 없음");
        }

        void LoadSelected()
        {
            int idx = _storyDropdown.index;
            if (idx < 0 || idx >= _stories.Count) return;
            _currentRel = _stories[idx];
            _editor.value = _controller.Load(_currentRel);
            _report.Clear();
            SetStatus($"로드: {_currentRel}");
        }

        void Reload()
        {
            if (string.IsNullOrEmpty(_currentRel)) { SetStatus("리로드할 소스 없음"); return; }
            _editor.value = _controller.Load(_currentRel);
            _report.Clear();
            SetStatus($"리로드: {_currentRel}");
        }

        void Validate()
        {
            var (violations, lineCount) = _controller.Validate(_editor.value ?? "", _strictToggle.value);
            RenderReport(violations, lineCount);
        }

        void Apply()
        {
            if (!EditorApplication.isPlaying) { SetStatus("Apply는 Play 모드 전용 — Game 씬을 Play하세요."); return; }
            if (!_controller.Apply(_editor.value ?? "", _strictToggle.value))
            {
                SetStatus(_controller.CanApply ? "빈 CSV — Apply 생략" : "전환 중(데이루프 등) — 잠시 후 Apply");
                return;
            }
            SetStatus("Apply → 러닝 게임에 재생(화면 정리 후, story-live)");
        }

        void Stop()
        {
            if (!EditorApplication.isPlaying) { SetStatus("Stop은 Play 모드 전용"); return; }
            SetStatus(_controller.Stop() ? "Stop 발행" : "전환 중 — 잠시 후");
        }

        void Save()
        {
            if (string.IsNullOrEmpty(_currentRel)) { SetStatus("저장할 소스 없음 — Story CSV를 선택하세요"); return; }
            if (_controller.Save(_currentRel, _editor.value ?? ""))
            {
                AssetDatabase.Refresh(); // Project 뷰 동기화(런타임 재생은 파일 I/O라 불필요하지만 무해).
                SetStatus($"저장: StreamingAssets/Story/{_currentRel}");
            }
            else SetStatus($"저장 실패: {_currentRel}");
        }

        void RefreshModeUI()
        {
            bool playing = EditorApplication.isPlaying;
            if (_applyBtn != null) _applyBtn.SetEnabled(playing);
            if (_stopBtn != null) _stopBtn.SetEnabled(playing);
            SetStatus(playing ? "Play 모드 — Apply 가능(전환 중엔 거부)" : "Edit 모드 — 편집/검증/저장 (Apply는 Play에서)");
        }

        void RenderReport(List<ScriptValidator.Violation> violations, int lineCount)
        {
            _report.Clear();
            if (violations == null || violations.Count == 0)
            {
                var ok = new Label($"OK — 위반 없음 ({lineCount} 라인)");
                ok.style.color = new Color(0.45f, 0.8f, 0.45f);
                _report.Add(ok);
                SetStatus($"검증 OK ({lineCount} 라인)");
                return;
            }
            int err = 0, warn = 0;
            foreach (var v in violations)
            {
                bool isErr = v.Severity == "Error";
                if (isErr) err++; else warn++;
                var l = new Label(v.ToString());
                l.style.color = isErr ? new Color(0.92f, 0.45f, 0.45f) : new Color(0.92f, 0.82f, 0.35f);
                l.style.whiteSpace = WhiteSpace.Normal;
                _report.Add(l);
            }
            SetStatus($"검증: Error {err} / Warning {warn}");
        }

        void SetStatus(string s) { if (_status != null) _status.text = s; }
    }
}
