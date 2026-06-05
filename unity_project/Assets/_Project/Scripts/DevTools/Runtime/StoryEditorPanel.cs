using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;    // Keyboard
using UnityEngine.UIElements;
using LoveAlgo.Story;             // StoryEditController
using LoveAlgo.Story.StoryEngine; // ScriptValidator.Violation

namespace LoveAlgo.DevTools.Runtime
{
    /// <summary>
    /// 빌드 런타임 스토리 에디터 패널(작가용). 어셈블리 자체가 <c>STORY_EDITOR_RUNTIME</c> 디파인 제약이라 그
    /// 디파인이 있는 작가 빌드/에디터에만 컴파일·존재하고 프로덕션엔 통째로 빠진다. <c>[RuntimeInitializeOnLoadMethod]</c>로
    /// 자가 스폰(씬 배선 0), <b>F9</b>로 토글. 게임 창 안의 "이동/리사이즈/닫기 되는 창"처럼 동작한다(런타임 UI Toolkit은
    /// 별도 OS 창 불가). StreamingAssets/Story CSV를 편집·검증·라이브 적용(EventBus)·저장 — 로직은
    /// <see cref="StoryEditController"/>(에디터창과 공유)에 위임. UX: 미저장 표시·전환 시 인라인 폐기확인·자동 검증
    /// (디바운스)·위반 클릭→해당 줄. Apply/Stop은 흐름 게이트(데이루프 전환 등)가 잠기면 거부.
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

        static readonly Color CGood = new(0.45f, 0.80f, 0.45f);
        static readonly Color CWarn = new(0.92f, 0.82f, 0.35f);
        static readonly Color CErr = new(0.92f, 0.45f, 0.45f);
        static readonly Color CMuted = new(0.75f, 0.75f, 0.78f);

        readonly StoryEditController _controller = new StoryEditController();
        VisualElement _panelRoot;
        DropdownField _storyDropdown;
        List<string> _stories = new();
        string _currentRel;
        TextField _editor;
        ScrollView _report;
        Label _status;
        Label _dirtyDot;
        Button _applyBtn, _stopBtn;
        Toggle _strictToggle;
        VisualElement _confirmRow;
        Action _pendingDiscard;
        IVisualElementScheduledItem _validateTimer;
        bool _visible;

        // 창 위치/크기(드래그/리사이즈).
        float _px = 12, _py = 12, _pw = 620, _ph = 480;

        void Awake()
        {
            // 테마가 박힌 PanelSettings 에셋 우선 로드(렌더 필수). 없으면 프로그래매틱 폴백.
            var panelSettings = Resources.Load<PanelSettings>("StoryEditorPanelSettings");
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                var theme = Resources.Load<ThemeStyleSheet>("unity-default-runtime-theme");
                if (theme != null) panelSettings.themeStyleSheet = theme;
                panelSettings.sortingOrder = 1000;
            }

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
                bool can = _controller.CanApply;
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

        // ── UI ──
        void BuildUI(VisualElement root)
        {
            _panelRoot = new VisualElement();
            _panelRoot.style.position = Position.Absolute;
            _panelRoot.style.left = _px; _panelRoot.style.top = _py;
            _panelRoot.style.width = _pw; _panelRoot.style.height = _ph;
            _panelRoot.style.backgroundColor = new Color(0.10f, 0.10f, 0.13f, 0.94f);
            Border(_panelRoot, new Color(0f, 0f, 0f, 0.5f));
            root.Add(_panelRoot);

            // 타이틀바(드래그 이동) + 닫기.
            var titleBar = Row();
            titleBar.style.backgroundColor = new Color(0.18f, 0.18f, 0.22f, 1f);
            titleBar.style.paddingTop = 3; titleBar.style.paddingBottom = 3; titleBar.style.paddingLeft = 6; titleBar.style.paddingRight = 4;
            var grip = new Label("Story Editor   —   드래그로 이동 · F9 토글");
            grip.style.unityFontStyleAndWeight = FontStyle.Bold; grip.style.flexGrow = 1; grip.style.color = CMuted;
            titleBar.Add(grip);
            _dirtyDot = new Label("●") { tooltip = "미저장 변경" };
            _dirtyDot.style.color = CWarn; _dirtyDot.style.marginRight = 6; _dirtyDot.style.display = DisplayStyle.None;
            titleBar.Add(_dirtyDot);
            var closeBtn = Btn("X", () => SetVisible(false)); closeBtn.tooltip = "닫기(F9로 다시)";
            titleBar.Add(closeBtn);
            _panelRoot.Add(titleBar);
            MakeDraggable(grip, d => { _px += d.x; _py += d.y; _panelRoot.style.left = _px; _panelRoot.style.top = _py; });

            var body = new VisualElement();
            body.style.flexGrow = 1; body.style.paddingTop = 6; body.style.paddingBottom = 6; body.style.paddingLeft = 6; body.style.paddingRight = 6;
            _panelRoot.Add(body);

            // 소스 행.
            var srcRow = Row();
            _storyDropdown = new DropdownField("Story CSV"); _storyDropdown.style.flexGrow = 1;
            _storyDropdown.RegisterValueChangedCallback(_ => OnPickStory());
            srcRow.Add(_storyDropdown);
            srcRow.Add(Btn("Refresh", RefreshList));
            body.Add(srcRow);

            // 툴바.
            var bar = Row(); bar.style.marginTop = 4; bar.style.marginBottom = 4;
            bar.Add(Btn("Validate", Validate));
            _applyBtn = Btn("Apply", Apply); _applyBtn.tooltip = "편집본을 러닝 게임에 즉시 재생"; bar.Add(_applyBtn);
            _stopBtn = Btn("Stop", Stop); _stopBtn.tooltip = "재생 중단 + 화면 정리"; bar.Add(_stopBtn);
            bar.Add(Btn("Save", Save));
            bar.Add(Btn("Reload", Reload));
            var sp = new VisualElement(); sp.style.flexGrow = 1; bar.Add(sp);
            _strictToggle = new Toggle("Strict"); bar.Add(_strictToggle);
            body.Add(bar);

            // 인라인 폐기 확인(미저장 전환 시).
            _confirmRow = Row();
            _confirmRow.style.display = DisplayStyle.None;
            _confirmRow.style.backgroundColor = new Color(0.3f, 0.25f, 0.1f, 1f);
            _confirmRow.style.paddingTop = 3; _confirmRow.style.paddingBottom = 3; _confirmRow.style.paddingLeft = 6; _confirmRow.style.paddingRight = 6;
            var cl = new Label("미저장 변경이 있습니다."); cl.style.flexGrow = 1; cl.style.color = CWarn;
            _confirmRow.Add(cl);
            _confirmRow.Add(Btn("폐기하고 진행", () => { var a = _pendingDiscard; HideConfirm(); a?.Invoke(); }));
            _confirmRow.Add(Btn("취소", () => { HideConfirm(); _storyDropdown.SetValueWithoutNotify(_currentRel ?? ""); }));
            body.Add(_confirmRow);

            // 편집기(양방향 스크롤).
            var editorScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            editorScroll.style.flexGrow = 1; editorScroll.style.minHeight = 120;
            Border(editorScroll, new Color(0f, 0f, 0f, 0.3f));
            _editor = new TextField { multiline = true };
            _editor.style.whiteSpace = WhiteSpace.NoWrap; _editor.style.minWidth = Length.Percent(100);
            _editor.RegisterValueChangedCallback(OnEditorChanged);
            editorScroll.Add(_editor);
            body.Add(editorScroll);

            // 검증.
            _report = new ScrollView(); _report.style.minHeight = 60; _report.style.maxHeight = 140;
            Border(_report, new Color(0f, 0f, 0f, 0.3f));
            body.Add(_report);

            _status = new Label(); _status.style.marginTop = 4; _status.style.whiteSpace = WhiteSpace.Normal;
            body.Add(_status);

            // 리사이즈 핸들(우하단, 글리프 대신 색 박스 — 폰트 무관 렌더).
            var resize = new VisualElement();
            resize.style.position = Position.Absolute; resize.style.right = 0; resize.style.bottom = 0;
            resize.style.width = 16; resize.style.height = 16;
            resize.style.backgroundColor = new Color(0.5f, 0.5f, 0.55f, 0.6f);
            resize.tooltip = "드래그로 크기 조절";
            _panelRoot.Add(resize);
            MakeDraggable(resize, d =>
            {
                _pw = Mathf.Max(380, _pw + d.x); _ph = Mathf.Max(260, _ph + d.y);
                _panelRoot.style.width = _pw; _panelRoot.style.height = _ph;
            });

            _validateTimer = _panelRoot.schedule.Execute(Validate);
            _validateTimer.Pause();
        }

        static VisualElement Row() { var v = new VisualElement(); v.style.flexDirection = FlexDirection.Row; return v; }
        static Button Btn(string t, Action a) => new Button(a) { text = t };
        static void Border(VisualElement v, Color c)
        {
            v.style.borderTopWidth = 1; v.style.borderBottomWidth = 1; v.style.borderLeftWidth = 1; v.style.borderRightWidth = 1;
            v.style.borderTopColor = c; v.style.borderBottomColor = c; v.style.borderLeftColor = c; v.style.borderRightColor = c;
        }

        // 핸들 드래그 → 증분 델타 콜백.
        static void MakeDraggable(VisualElement handle, Action<Vector2> onDelta)
        {
            bool active = false; Vector2 last = default;
            handle.RegisterCallback<PointerDownEvent>(e =>
            {
                active = true; last = new Vector2(e.position.x, e.position.y);
                handle.CapturePointer(e.pointerId); e.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!active) return;
                var p = new Vector2(e.position.x, e.position.y);
                onDelta(p - last); last = p;
            });
            handle.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!active) return;
                active = false; handle.ReleasePointer(e.pointerId);
            });
        }

        // ── 동작 ──
        void RefreshList()
        {
            _stories = _controller.ListStories();
            _storyDropdown.choices = new List<string>(_stories);
            _storyDropdown.SetValueWithoutNotify(_currentRel != null && _stories.Contains(_currentRel) ? _currentRel : "");
            SetStatus(_stories.Count > 0 ? $"{_stories.Count} CSV (StreamingAssets/Story)" : "StreamingAssets/Story에 .csv 없음", CMuted);
        }

        void OnPickStory()
        {
            int idx = _storyDropdown.index;
            if (idx < 0 || idx >= _stories.Count) return;
            string pick = _stories[idx];
            if (pick == _currentRel) return;
            if (_controller.IsDirty(_editor.value ?? "")) { ShowConfirm(() => LoadInto(pick)); return; }
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
            if (_controller.IsDirty(_editor.value ?? "")) { ShowConfirm(() => LoadInto(_currentRel)); return; }
            LoadInto(_currentRel);
        }

        void ShowConfirm(Action onDiscard)
        {
            _pendingDiscard = onDiscard;
            if (_confirmRow != null) _confirmRow.style.display = DisplayStyle.Flex;
        }
        void HideConfirm()
        {
            _pendingDiscard = null;
            if (_confirmRow != null) _confirmRow.style.display = DisplayStyle.None;
        }

        void OnEditorChanged(ChangeEvent<string> _)
        {
            UpdateDirty();
            _validateTimer?.Pause();
            _validateTimer = _panelRoot.schedule.Execute(Validate).StartingIn(350);
        }

        void UpdateDirty()
        {
            if (_dirtyDot != null)
                _dirtyDot.style.display = _controller.IsDirty(_editor.value ?? "") ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Validate()
        {
            var (violations, lineCount) = _controller.Validate(_editor.value ?? "", _strictToggle.value);
            RenderReport(violations, lineCount);
        }

        void Apply()
        {
            if (!_controller.Apply(_editor.value ?? "", _strictToggle.value))
                SetStatus(_controller.CanApply ? "빈 CSV — Apply 생략" : "전환 중(데이루프 등) — 잠시 후", CWarn);
            else
                SetStatus("Apply ▶ 라이브 재생(화면 정리 후)", CGood);
        }

        void Stop() => SetStatus(_controller.Stop() ? "Stop ■ 발행" : "전환 중 — 잠시 후", CMuted);

        void Save()
        {
            if (string.IsNullOrEmpty(_currentRel)) { SetStatus("CSV를 선택하세요", CWarn); return; }
            if (_controller.Save(_currentRel, _editor.value ?? ""))
            {
                UpdateDirty();
                SetStatus($"저장됨: {_currentRel}", CGood);
            }
            else SetStatus($"저장 실패: {_currentRel}", CErr);
        }

        void RenderReport(List<ScriptValidator.Violation> v, int lineCount)
        {
            _report.Clear();
            if (v == null || v.Count == 0)
            {
                var ok = new Label($"OK ({lineCount} 라인)"); ok.style.color = CGood;
                _report.Add(ok);
                SetStatus($"검증 OK ({lineCount} 라인)", CGood);
                return;
            }
            int err = 0, warn = 0;
            foreach (var x in v)
            {
                bool isErr = x.Severity == "Error";
                if (isErr) err++; else warn++;
                int line = x.LineNumber;
                var l = new Label(x.ToString());
                l.style.color = isErr ? CErr : CWarn; l.style.whiteSpace = WhiteSpace.Normal;
                l.tooltip = "클릭 → 해당 줄";
                l.RegisterCallback<ClickEvent>(_ => JumpToLine(line));
                _report.Add(l);
            }
            SetStatus($"Error {err} / Warning {warn} — 항목 클릭 시 해당 줄", err > 0 ? CErr : CWarn);
        }

        void JumpToLine(int line1Based)
        {
            var text = _editor.value ?? "";
            int start = 0, ln = 1;
            while (start < text.Length && ln < line1Based) { if (text[start] == '\n') ln++; start++; }
            int end = text.IndexOf('\n', start);
            if (end < 0) end = text.Length;
            _editor.Focus();
            _editor.SelectRange(end, start);
        }

        void SetStatus(string s, Color c) { if (_status != null) { _status.text = s; _status.style.color = c; } }
    }
}
