using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;    // Keyboard
using UnityEngine.UIElements;
using LoveAlgo.Story;             // StoryEditController
using LoveAlgo.Story.StoryEngine; // ScriptValidator.Violation

namespace LoveAlgo.DevTools.Runtime
{
    /// <summary>
    /// 빌드 런타임 스토리 에디터 패널(작가용). 어셈블리 자체가 <c>STORY_EDITOR_RUNTIME</c> 디파인 제약이라,
    /// 그 디파인이 있는 작가 빌드/에디터에만 컴파일·존재하고 프로덕션엔 통째로 빠진다(#if 산재 불필요).
    /// <c>[RuntimeInitializeOnLoadMethod]</c>로 자가 스폰(씬 배선 0)하고 <b>F9</b>로 토글. StreamingAssets/Story
    /// CSV를 편집·검증·라이브 적용(EventBus)·저장 — 로직은 <see cref="StoryEditController"/>(에디터창과 공유)에 위임.
    /// Apply/Stop은 흐름 게이트(데이루프 전환 등)가 잠기면 거부된다(데드락 방지).
    /// </summary>
    public sealed class StoryEditorPanel : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var go = new GameObject("StoryEditorPanel");
            DontDestroyOnLoad(go);
            go.AddComponent<StoryEditorPanel>();
        }

        readonly StoryEditController _controller = new StoryEditController();
        VisualElement _panelRoot;
        DropdownField _storyDropdown;
        List<string> _stories = new();
        string _currentRel;
        TextField _editor;
        ScrollView _report;
        Label _status;
        Button _applyBtn, _stopBtn;
        Toggle _strictToggle;
        bool _visible;

        void Awake()
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            var theme = Resources.Load<ThemeStyleSheet>("unity-default-runtime-theme"); // 빌트인 런타임 테마(없으면 폴백 필요)
            if (theme != null) panelSettings.themeStyleSheet = theme;
            panelSettings.sortingOrder = 1000; // 게임 위 오버레이.

            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            BuildUI(doc.rootVisualElement);
            SetVisible(false);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame)
                SetVisible(!_visible);

            if (_visible)
            {
                bool can = _controller.CanApply; // 흐름 게이트(전환 중) 반영.
                if (_applyBtn != null) _applyBtn.SetEnabled(can);
                if (_stopBtn != null) _stopBtn.SetEnabled(can);
            }
        }

        void SetVisible(bool v)
        {
            _visible = v;
            if (_panelRoot != null) _panelRoot.style.display = v ? DisplayStyle.Flex : DisplayStyle.None;
            if (v) RefreshList();
        }

        void BuildUI(VisualElement root)
        {
            _panelRoot = new VisualElement();
            _panelRoot.style.position = Position.Absolute;
            _panelRoot.style.left = 8; _panelRoot.style.top = 8; _panelRoot.style.width = 560;
            _panelRoot.style.paddingTop = 6; _panelRoot.style.paddingBottom = 6;
            _panelRoot.style.paddingLeft = 6; _panelRoot.style.paddingRight = 6;
            _panelRoot.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.92f);
            root.Add(_panelRoot);

            var title = new Label("Story Editor (F9)");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            _panelRoot.Add(title);

            var srcRow = new VisualElement(); srcRow.style.flexDirection = FlexDirection.Row;
            _storyDropdown = new DropdownField("Story CSV"); _storyDropdown.style.flexGrow = 1;
            _storyDropdown.RegisterValueChangedCallback(_ => LoadSelected());
            srcRow.Add(_storyDropdown);
            srcRow.Add(Btn("Refresh", RefreshList));
            _panelRoot.Add(srcRow);

            var bar = new VisualElement(); bar.style.flexDirection = FlexDirection.Row; bar.style.marginTop = 4; bar.style.marginBottom = 4;
            bar.Add(Btn("Validate", Validate));
            _applyBtn = Btn("Apply", Apply); bar.Add(_applyBtn);
            _stopBtn = Btn("Stop", Stop); bar.Add(_stopBtn);
            bar.Add(Btn("Save", Save));
            _strictToggle = new Toggle("Strict"); _strictToggle.style.marginLeft = 8; bar.Add(_strictToggle);
            _panelRoot.Add(bar);

            _editor = new TextField { multiline = true };
            _editor.style.height = 240;
            _panelRoot.Add(_editor);

            _report = new ScrollView(); _report.style.maxHeight = 120;
            _panelRoot.Add(_report);

            _status = new Label(); _status.style.marginTop = 4;
            _panelRoot.Add(_status);
        }

        static Button Btn(string t, System.Action a) => new Button(a) { text = t };

        void RefreshList()
        {
            _stories = _controller.ListStories();
            _storyDropdown.choices = new List<string>(_stories);
            _storyDropdown.SetValueWithoutNotify("");
            SetStatus(_stories.Count > 0 ? $"{_stories.Count} CSV (StreamingAssets/Story)" : "StreamingAssets/Story에 .csv 없음");
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

        void Validate()
        {
            var (violations, lineCount) = _controller.Validate(_editor.value ?? "", _strictToggle.value);
            RenderReport(violations, lineCount);
        }

        void Apply()
        {
            if (!_controller.Apply(_editor.value ?? "", _strictToggle.value))
                SetStatus(_controller.CanApply ? "빈 CSV — Apply 생략" : "전환 중(데이루프 등) — 잠시 후");
            else
                SetStatus("Apply → 라이브 재생(화면 정리 후)");
        }

        void Stop() => SetStatus(_controller.Stop() ? "Stop" : "전환 중 — 잠시 후");

        void Save()
        {
            if (string.IsNullOrEmpty(_currentRel)) { SetStatus("CSV를 선택하세요"); return; }
            SetStatus(_controller.Save(_currentRel, _editor.value ?? "") ? $"저장: {_currentRel}" : "저장 실패");
        }

        void RenderReport(List<ScriptValidator.Violation> v, int lineCount)
        {
            _report.Clear();
            if (v == null || v.Count == 0)
            {
                var ok = new Label($"OK ({lineCount} 라인)");
                ok.style.color = new Color(0.45f, 0.8f, 0.45f);
                _report.Add(ok);
                SetStatus("검증 OK");
                return;
            }
            int err = 0, warn = 0;
            foreach (var x in v)
            {
                bool isErr = x.Severity == "Error";
                if (isErr) err++; else warn++;
                var l = new Label(x.ToString());
                l.style.color = isErr ? new Color(0.92f, 0.45f, 0.45f) : new Color(0.92f, 0.82f, 0.35f);
                l.style.whiteSpace = WhiteSpace.Normal;
                _report.Add(l);
            }
            SetStatus($"Error {err} / Warning {warn}");
        }

        void SetStatus(string s) { if (_status != null) _status.text = s; }
    }
}
