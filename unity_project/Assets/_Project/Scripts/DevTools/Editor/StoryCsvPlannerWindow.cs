using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using LoveAlgo.Common;            // EventBus
using LoveAlgo.Events;            // PlayScriptCommand
using LoveAlgo.Story;             // ScriptParser, ScriptLine
using LoveAlgo.Story.StoryEngine; // ScriptValidator

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 기획자 전용 스토리 CSV 라이브 에디터(UI Toolkit EditorWindow, 첫 에디터 도구). 엔진 포맷 CSV를 편집하고
    /// — Validate(순수 <see cref="ScriptParser"/>+<see cref="ScriptValidator"/> 재사용) / Apply(PlayMode의
    /// NarrativeController에 <see cref="PlayScriptCommand"/> 발행 → 라이브 재생) / Save(디스크+리임포트) /
    /// Stop / Reload — 수정→적용 루프를 빠르게 돌린다.
    ///
    /// 설계: 런타임을 알지만 건드리지 않는 Editor 전용 어셈블리(ADR-007 그대로 EventBus로만 게임과 연결).
    /// 구 ScenarioEditor의 런타임 MonoBehaviour/Service Locator/IVT 모델은 답습하지 않음 — EditorWindow 단독.
    /// MVP: 텍스트 편집 + 검증 + 라이브 적용 + 저장. 표 편집/한글 변환/매핑은 후속(과설계 게이트).
    /// </summary>
    public class StoryCsvPlannerWindow : EditorWindow
    {
        const string PlayName = "story-live";
        const string StopName = "story-stop";

        TextAsset _source;
        ObjectField _sourceField;
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

            // 소스 피커: Resources/Story 등의 CSV TextAsset 선택 → .text 로드.
            _sourceField = new ObjectField("Source CSV") { objectType = typeof(TextAsset), allowSceneObjects = false };
            _sourceField.RegisterValueChangedCallback(e => LoadFrom(e.newValue as TextAsset));
            root.Add(_sourceField);

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

            // CSV 편집기(멀티라인).
            var editorLabel = new Label("CSV"); editorLabel.style.marginTop = 4;
            root.Add(editorLabel);
            _editor = new TextField { multiline = true };
            _editor.style.flexGrow = 1;
            _editor.style.minHeight = 200;
            root.Add(_editor);

            // 검증 패널.
            var reportLabel = new Label("Validation"); reportLabel.style.marginTop = 4;
            root.Add(reportLabel);
            _report = new ScrollView();
            _report.style.minHeight = 90; _report.style.maxHeight = 170;
            _report.style.borderTopWidth = 1; _report.style.borderBottomWidth = 1;
            _report.style.borderLeftWidth = 1; _report.style.borderRightWidth = 1;
            var border = new Color(0f, 0f, 0f, 0.3f);
            _report.style.borderTopColor = border; _report.style.borderBottomColor = border;
            _report.style.borderLeftColor = border; _report.style.borderRightColor = border;
            root.Add(_report);

            // 상태바.
            _status = new Label(); _status.style.marginTop = 4; _status.style.opacity = 0.85f;
            root.Add(_status);

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            RefreshModeUI();
        }

        void OnDestroy() => EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        void OnPlayModeChanged(PlayModeStateChange _) => RefreshModeUI();

        static Button MakeButton(string text, System.Action onClick) => new Button(onClick) { text = text };

        void LoadFrom(TextAsset asset)
        {
            _source = asset;
            _editor.value = asset != null ? asset.text : "";
            SetStatus(asset != null ? $"로드: {AssetDatabase.GetAssetPath(asset)}" : "소스 없음");
            _report.Clear();
        }

        void Reload()
        {
            if (_source == null) { SetStatus("리로드할 소스 없음"); return; }
            var path = AssetDatabase.GetAssetPath(_source);
            AssetDatabase.ImportAsset(path);
            var fresh = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            _source = fresh;
            _sourceField.SetValueWithoutNotify(fresh);
            _editor.value = fresh != null ? fresh.text : "";
            _report.Clear();
            SetStatus($"리로드: {path}");
        }

        void Validate()
        {
            bool prev = ScriptParser.Strict;
            ScriptParser.Strict = _strictToggle.value;
            List<ScriptLine> lines = ScriptParser.Parse(_editor.value ?? "");
            List<ScriptValidator.Violation> violations = ScriptValidator.Validate(lines);
            ScriptParser.Strict = prev; // 전역 토글 원복(러닝 게임 파싱에 영향 없도록).
            RenderReport(violations, lines.Count);
        }

        void Apply()
        {
            if (!EditorApplication.isPlaying)
            {
                SetStatus("Apply는 Play 모드 전용 — Game 씬을 Play하세요.");
                return;
            }
            var csv = _editor.value ?? "";
            if (string.IsNullOrWhiteSpace(csv)) { SetStatus("빈 CSV — Apply 생략"); return; }

            // 발행(동기 파싱) 동안만 Strict 토글 적용 후 원복.
            bool prev = ScriptParser.Strict;
            ScriptParser.Strict = _strictToggle.value;
            EventBus.Publish(new PlayScriptCommand(csv, PlayName));
            ScriptParser.Strict = prev;
            SetStatus("Apply → 러닝 게임에 재생 발행(story-live)");
        }

        void Stop()
        {
            if (!EditorApplication.isPlaying) { SetStatus("Stop은 Play 모드 전용"); return; }
            EventBus.Publish(new PlayScriptCommand("", StopName));
            SetStatus("Stop 발행");
        }

        void Save()
        {
            if (_source == null) { SetStatus("저장할 소스 없음 — Source CSV를 지정하세요"); return; }
            var path = AssetDatabase.GetAssetPath(_source);
            if (string.IsNullOrEmpty(path)) { SetStatus("에셋 경로 없음"); return; }
            File.WriteAllText(path, _editor.value ?? "", new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
            _source = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            _sourceField.SetValueWithoutNotify(_source);
            SetStatus($"저장: {path}");
        }

        void RefreshModeUI()
        {
            bool playing = EditorApplication.isPlaying;
            if (_applyBtn != null) _applyBtn.SetEnabled(playing);
            if (_stopBtn != null) _stopBtn.SetEnabled(playing);
            SetStatus(playing
                ? "Play 모드 — Apply 가능"
                : "Edit 모드 — 편집/검증/저장 (Apply는 Play에서)");
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
