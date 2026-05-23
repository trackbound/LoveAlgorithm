using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.Story;
using LoveAlgo.Story.Data;
using LoveAlgo.Story.SaveSystem;
using LoveAlgo.Story.StoryEngine;

namespace LoveAlgo.DevTools.ScenarioEditor
{
    /// <summary>
    /// 빌드 내 시나리오 CSV 편집기 (IMGUI 버전 — MVP).
    /// F8로 열고/닫기. 열리면 게임 자동 일시정지, 닫을 때 현재 라인으로 재진입.
    ///
    /// 핵심 원칙:
    ///   - 시스템 필드(LineType/NextType/Char Action/Flow Subcommand)는 드롭다운만 — raw 입력 금지
    ///   - 카탈로그 항목(BG/CG/SD/BGM/Char/Emote)은 StoryMappings에서만 선택
    ///   - 저장 시 ScriptValidator로 Error 차단
    ///   - 저장 후 ScriptRunner.LoadScript + JumpWithStateSyncAsync(currentIndex)로 즉시 적용
    ///
    /// 자동 생성 (DebugPanel과 동일 패턴):
    ///   F8 핸들러는 DebugPanel에서 호출. 본 클래스는 항상 살아있고 isOpen 상태만 토글.
    /// </summary>
    public class ScenarioEditorIMGUI : MonoBehaviour
    {
        public static ScenarioEditorIMGUI Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (FindAnyObjectByType<ScenarioEditorIMGUI>() != null) return;
            var go = new GameObject("[ScenarioEditor]");
            Instance = go.AddComponent<ScenarioEditorIMGUI>();
            DontDestroyOnLoad(go);
        }

        // ── 편집 상태 ──
        bool isOpen;
        string scriptName;
        List<ScriptLine> workingLines;          // 편집 중인 사본
        List<ScriptLine> originalLines;         // 첫 로드 시점 (changelog diff용)
        int selectedIndex = -1;                 // 좌측 리스트 선택
        int currentRunningIndex = -1;           // 게임이 진행 중이던 라인

        // ── 스크롤·UI 상태 ──
        Vector2 listScroll;
        Vector2 widgetScroll;
        Vector2 validationScroll;
        List<ScriptValidator.Violation> violations = new();
        bool pendingScrollToSelected;           // 열 때 1회 — 진행 중 라인으로 자동 스크롤

        // ── 드롭다운 팝업 상태 (한 번에 하나만) ──
        string activeDropdownKey;
        Vector2 dropdownScroll;
        Rect dropdownRect;

        // ── 사용 가능한 스크립트 (StreamingAssets/Story 스캔) ──
        string[] availableScripts;

        // ── Undo/Redo ──
        const int UndoMax = 50;
        readonly Stack<UndoEntry> undoStack = new();
        readonly Stack<UndoEntry> redoStack = new();
        int _lastSnapshottedSelection = -1;
        string _selectionLineHashAtFocus;       // 선택 변경 시 그 라인 해시 — 변화 감지

        struct UndoEntry
        {
            public List<ScriptLine> Snapshot;
            public int SelectedIndex;
        }

        // ── 외부 변경 감지 ──
        DateTime _loadedAtMtime;
        bool _externalChangeDetected;

        // ── 저장 완료 표시 (5초간) ──
        DateTime _lastSavedAt;
        bool HasRecentSave => (DateTime.Now - _lastSavedAt).TotalSeconds < 5;

        // ══════════════════════════════════════════════
        //  진입·종료
        // ══════════════════════════════════════════════

        /// <summary>편집기 토글 (F8 핸들러에서 호출).</summary>
        public void Toggle()
        {
            if (isOpen) CloseWithoutSaving();
            else        OpenForCurrentScript();
        }

        float _nextMtimeCheck;

        void Update()
        {
            if (!isOpen) return;

            // Ctrl+Z / Ctrl+Y (or Ctrl+Shift+Z)
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
                bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                if (ctrl && kb.zKey.wasPressedThisFrame)
                {
                    if (shift) Redo(); else Undo();
                }
                else if (ctrl && kb.yKey.wasPressedThisFrame)
                {
                    Redo();
                }
            }

            // 외부 mtime 1초 간격 폴링
            if (Time.unscaledTime >= _nextMtimeCheck)
            {
                _nextMtimeCheck = Time.unscaledTime + 1.0f;
                CheckExternalChange();
            }
        }

        public void OpenForCurrentScript()
        {
            var runner = ScriptRunner.Instance;
            string name = runner?.CurrentScriptName;
            if (string.IsNullOrEmpty(name))
            {
                // 폴백 — Prologue
                name = "Prologue";
            }
            currentRunningIndex = runner?.CurrentIndex ?? -1;
            Open(name);
        }

        public void Open(string scriptNameToLoad)
        {
            scriptName = scriptNameToLoad;
            LoadScriptForEdit(scriptName).Forget();
            isOpen = true;

            // 게임 자동 일시정지 — 스크립트 실행 멈춤
            ScriptRunner.Instance?.Stop();
            Debug.Log($"[ScenarioEditor] OPEN '{scriptName}' (currentLine={currentRunningIndex})");
        }

        async UniTaskVoid LoadScriptForEdit(string name)
        {
            string csv = await StoryAssetLoader.LoadCsvAsync(name);
            if (string.IsNullOrEmpty(csv))
            {
                Debug.LogError($"[ScenarioEditor] 로드 실패: {name}");
                workingLines = new List<ScriptLine>();
                originalLines = new List<ScriptLine>();
                return;
            }
            workingLines = ScriptParser.Parse(csv);
            originalLines = DeepCopy(workingLines);
            selectedIndex = (currentRunningIndex >= 0 && currentRunningIndex < workingLines.Count)
                ? currentRunningIndex : (workingLines.Count > 0 ? 0 : -1);

            // 진행 중인 라인이 화면에 보이도록 자동 스크롤 (첫 그리기에서 적용)
            pendingScrollToSelected = selectedIndex >= 0;

            // 외부 변경 감지 베이스라인
            _loadedAtMtime = StoryAssetLoader.GetLastWriteTime(name);
            _externalChangeDetected = false;

            // Undo 초기화
            undoStack.Clear();
            redoStack.Clear();
            _lastSnapshottedSelection = -1;
            CaptureSelectionHash();

            RunValidation();
            ScanAvailableScripts();
        }

        void CloseWithoutSaving()
        {
            isOpen = false;
            activeDropdownKey = null;
            Debug.Log("[ScenarioEditor] CLOSE (변경사항 미저장) — 게임 재개");
            ResumeRunner();
        }

        /// <summary>편집기 열 때 Stop했던 ScriptRunner를 현재 라인부터 재개.</summary>
        void ResumeRunner()
        {
            var runner = ScriptRunner.Instance;
            if (runner == null) return;
            if (currentRunningIndex < 0) return;
            // 디스크 상태 그대로 — 미저장 변경은 적용 안 됨 (사용자가 [💾 저장] 했어야 반영됨)
            runner.JumpWithStateSyncAsync(currentRunningIndex).Forget();
        }

        async UniTaskVoid SaveAndApply(bool forceOverwriteExternal = false)
        {
            if (workingLines == null) return;

            // 1) 검증
            violations = ScriptValidator.Validate(workingLines);
            int errors = violations.Count(v => v.Severity == "Error");
            if (errors > 0)
            {
                Debug.LogError($"[ScenarioEditor] 저장 차단 — Error {errors}개 (먼저 수정 필요)");
                return;
            }

            // 2) 외부 변경 감지 (저장 직전 재확인)
            CheckExternalChange();
            if (_externalChangeDetected && !forceOverwriteExternal)
            {
                Debug.LogWarning($"[ScenarioEditor] 외부 변경 감지 — '강제 저장' 또는 '다시 로드' 선택 필요");
                return; // 사용자가 상단바 버튼으로 결정
            }

            // 3) 백업 (원본 저장 직전 스냅샷)
            BackupManager.Snapshot(scriptName);

            // 4) 직렬화 + 저장
            string csv = ScriptCsvSerializer.Serialize(workingLines);
            StoryAssetLoader.SaveCsv(scriptName, csv);

            // 5) Changelog 기록 (diff 계산은 originalLines 기준)
            ChangelogWriter.AppendDiff(scriptName, originalLines, workingLines);

            // 6) ScriptRunner 재로드 + 현재 라인으로 점프 (무대 합성).
            //    편집기 유지 중이라 IMGUI가 게임 화면을 덮음 → fade 보이지 않으므로 비활성
            int targetIndex = Mathf.Clamp(currentRunningIndex, 0, workingLines.Count - 1);
            var runner = ScriptRunner.Instance;
            if (runner != null && targetIndex >= 0)
            {
                runner.LoadScript(csv, scriptName);
                await runner.JumpWithStateSyncAsync(targetIndex, withFade: false);
            }

            // 7) 다음 세션 베이스라인 갱신 (Undo 스택은 그대로 — 저장 후 추가 undo 가능)
            originalLines = DeepCopy(workingLines);
            _loadedAtMtime = StoryAssetLoader.GetLastWriteTime(scriptName);
            _externalChangeDetected = false;
            _lastSavedAt = DateTime.Now;

            // 창 유지 — 사용자가 닫기 누를 때까지. Undo 스택도 유지.
            Debug.Log($"[ScenarioEditor] SAVED & APPLIED — {workingLines.Count} lines, resumed at #{targetIndex} (창 유지)");
        }

        /// <summary>
        /// 선택된 라인으로 ScriptRunner 점프 — 메모리 working 상태 그대로 사용 (디스크 안 씀).
        /// 미저장 변경 상태에서 미리보기 가능. 디스크는 원본 그대로.
        /// 점프 후 currentRunningIndex 갱신 → 좌측 리스트 ▶▶ 표시도 이동.
        /// </summary>
        async UniTaskVoid JumpToSelectedInMemory()
        {
            if (workingLines == null || selectedIndex < 0 || selectedIndex >= workingLines.Count) return;

            // 1. 편집기 먼저 닫기 — fade가 게임 화면에서 보이도록 (IMGUI 오버레이가 가리면 fade 안 보임)
            isOpen = false;
            activeDropdownKey = null;

            // 2. 현재 메모리 상태를 CSV 문자열로 직렬화
            string csv = ScriptCsvSerializer.Serialize(workingLines);
            var selectedLine = workingLines[selectedIndex];
            StageSyncLog.Info("EditorJump",
                $"target line=#{selectedIndex} lineId={selectedLine.LineID ?? "-"} type={selectedLine.Type} " +
                $"(memory state, lines={workingLines.Count}, csv {csv.Length} chars)");

            // 3. GameFlowJumper에 위임 — Phase/UI 스왑 + Tear-down + 페이드 포함
            //    어떤 페이즈(타이틀/락스크린/스토리 중) 어디서든 안전하게 점프
            var targetPhase = LoveAlgo.Core.GameFlowJumper.InferPhaseFromScript(scriptName);
            await LoveAlgo.Core.GameFlowJumper.JumpToMemoryAsync(csv, scriptName, selectedIndex, targetPhase);

            currentRunningIndex = selectedIndex;
        }

        /// <summary>외부 변경으로 인한 강제 reload — 현재 편집 내용 폐기.</summary>
        void ReloadFromDisk()
        {
            if (string.IsNullOrEmpty(scriptName)) return;
            Debug.LogWarning("[ScenarioEditor] 외부 변경 반영 — 현재 편집 폐기하고 디스크에서 재로드");
            LoadScriptForEdit(scriptName).Forget();
        }

        // ══════════════════════════════════════════════
        //  IMGUI 렌더
        // ══════════════════════════════════════════════

        void OnGUI()
        {
            if (!isOpen) return;

            // 풀스크린 불투명 배경 (어두운 차콜) — 반투명보다 가독성 좋음
            GUI.color = new Color(0.12f, 0.12f, 0.14f, 1.0f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float pad = 8f;
            float topBarH = 32f;
            float bottomBarH = 80f;
            float bodyY = topBarH + pad;
            float bodyH = Screen.height - topBarH - bottomBarH - pad * 2;

            DrawTopBar(new Rect(pad, pad, Screen.width - pad * 2, topBarH));

            // 좌측 리스트 확대 (40%/520 → 60%/900) + 우측 편집창 추가 축소
            float leftW = Mathf.Min(Screen.width * 0.60f, 900f);
            DrawLineList(new Rect(pad, bodyY, leftW, bodyH));
            DrawWidget(new Rect(pad + leftW + pad, bodyY, Screen.width - leftW - pad * 3, bodyH));

            DrawBottomBar(new Rect(pad, Screen.height - bottomBarH - pad, Screen.width - pad * 2, bottomBarH));

            // 활성 드롭다운은 마지막에 그려서 위에 표시
            DrawActiveDropdown();
        }

        // ── 상단 바 ──
        void DrawTopBar(Rect rect)
        {
            GUI.Box(rect, "");
            GUILayout.BeginArea(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8));
            GUILayout.BeginHorizontal();

            var title = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 };
            GUILayout.Label($"<b>📝 시나리오 편집기</b>  ·  파일: <b>{scriptName ?? "?"}</b>  ·  라인 {workingLines?.Count ?? 0}개", title, GUILayout.Width(500));

            // 다른 스크립트로 전환
            if (availableScripts != null && availableScripts.Length > 1)
            {
                if (GUILayout.Button("📂 다른 파일 열기 ▾", GUILayout.Width(140)))
                    ToggleDropdown("scriptSelect");
            }

            GUILayout.FlexibleSpace();

            // Undo/Redo 버튼
            GUI.enabled = undoStack.Count > 0;
            if (GUILayout.Button($"↶ Undo ({undoStack.Count})", GUILayout.Width(110))) Undo();
            GUI.enabled = redoStack.Count > 0;
            if (GUILayout.Button($"↷ Redo ({redoStack.Count})", GUILayout.Width(110))) Redo();
            GUI.enabled = true;

            int errors = violations.Count(v => v.Severity == "Error");
            int warnings = violations.Count(v => v.Severity == "Warning");
            var statStyle = new GUIStyle(GUI.skin.label) { richText = true };
            string statColor = errors > 0 ? "#ff7777" : warnings > 0 ? "#ffcc55" : "#88ff88";
            GUILayout.Label($"<color={statColor}>검증: E{errors} / W{warnings}</color>", statStyle, GUILayout.Width(110));

            // 저장 완료 표시 (5초)
            if (HasRecentSave)
                GUILayout.Label($"<color=#88ff88>✓ 저장됨 {_lastSavedAt:HH:mm:ss}</color>", statStyle, GUILayout.Width(140));

            // 외부 변경 경고
            if (_externalChangeDetected)
            {
                GUILayout.Label("<color=#ff8800><b>⚠ 외부 변경 감지!</b></color>", statStyle, GUILayout.Width(140));
                if (GUILayout.Button("📥 다시 로드", GUILayout.Width(100))) ReloadFromDisk();
                if (GUILayout.Button("⚠ 강제 저장", GUILayout.Width(100))) SaveAndApply(forceOverwriteExternal: true).Forget();
            }
            else
            {
                GUI.enabled = errors == 0;
                if (GUILayout.Button("💾 저장 & 적용", GUILayout.Width(120))) SaveAndApply().Forget();
                GUI.enabled = true;
            }
            if (GUILayout.Button("✕ 닫기", GUILayout.Width(80))) CloseWithoutSaving();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // 스크립트 선택 드롭다운 데이터 준비
            if (activeDropdownKey == "scriptSelect" && availableScripts != null)
            {
                dropdownRect = new Rect(rect.x + 510, rect.y + rect.height, 280, Mathf.Min(availableScripts.Length * 22 + 8, 220));
                BuildDropdownContent(availableScripts, i => { Open(availableScripts[i]); activeDropdownKey = null; });
            }
        }

        // ── 좌측: 라인 리스트 ──
        void DrawLineList(Rect rect)
        {
            GUI.Box(rect, "");
            GUILayout.BeginArea(new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8));

            var header = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 };
            GUILayout.Label("<b>라인 목록</b>  <color=#888>(▶ 현재 진행 위치)</color>", header);

            // 버튼 행
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ 추가", GUILayout.Width(70))) InsertLineAfterSelected();
            GUI.enabled = selectedIndex >= 0;
            if (GUILayout.Button("－ 삭제", GUILayout.Width(70))) DeleteSelected();
            if (GUILayout.Button("▲", GUILayout.Width(30))) MoveSelected(-1);
            if (GUILayout.Button("▼", GUILayout.Width(30))) MoveSelected(+1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            // 진행 중인 라인이 화면 중앙쯤 오도록 1회 자동 스크롤
            if (pendingScrollToSelected && selectedIndex >= 0 && Event.current.type == EventType.Layout)
            {
                const float rowHeight = 22f;                  // IMGUI 기본 버튼 1행 ≈ 22px
                float viewportH = rect.height - 80f;          // 헤더+버튼 행 차감 근사
                float targetY = selectedIndex * rowHeight - viewportH * 0.35f;
                listScroll.y = Mathf.Max(0f, targetY);
                pendingScrollToSelected = false;
            }

            listScroll = GUILayout.BeginScrollView(listScroll);

            if (workingLines != null)
            {
                for (int i = 0; i < workingLines.Count; i++)
                {
                    var line = workingLines[i];
                    bool isSelected = i == selectedIndex;
                    bool isCurrent  = i == currentRunningIndex;

                    // 행 버튼 스타일
                    var rowStyle = new GUIStyle(GUI.skin.button)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        richText = true,
                        fontSize = 11,
                    };
                    if (isCurrent)
                    {
                        // 현재 진행 라인 — 주황 굵게
                        rowStyle.fontStyle = FontStyle.Bold;
                        rowStyle.normal.textColor = new Color(1f, 0.7f, 0.3f);
                        rowStyle.hover.textColor  = new Color(1f, 0.85f, 0.5f);
                    }
                    else if (isSelected)
                    {
                        rowStyle.normal.textColor = Color.yellow;
                        rowStyle.hover.textColor = Color.yellow;
                    }

                    // 행 텍스트 (화살표를 버튼 본문에 inline — 별도 Label 안 만들어서 레이아웃 깨짐 방지)
                    string prefix = isCurrent ? "<color=#ffaa44>▶ </color>" : "<color=#00000000>▶ </color>"; // 투명 placeholder로 폭 통일
                    string label = prefix + FormatLineLabel(i, line);

                    if (GUILayout.Button(label, rowStyle))
                    {
                        if (selectedIndex != i)
                        {
                            selectedIndex = i;
                            CaptureSelectionHash();
                        }
                        activeDropdownKey = null;
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        string FormatLineLabel(int index, ScriptLine line)
        {
            string id = string.IsNullOrEmpty(line.LineID) ? "·" : line.LineID;
            string speakerOrPreview = line.Type == LineType.Text
                ? (string.IsNullOrEmpty(line.Speaker) ? "(나)" : line.Speaker)
                : "";
            string val = Trunc(line.Value ?? "", 52).Replace("\n", " ↵ ");
            string typeColor = TypeColor(line.Type);
            return $"<color=#888>{index,4}</color>  <color={typeColor}><b>{line.Type,-7}</b></color>  <color=#aaa>{id,-12}</color>  {speakerOrPreview}  <color=#ccc>{val}</color>";
        }

        static string TypeColor(LineType t)
        {
            switch (t)
            {
                case LineType.Text:    return "#ffffff";
                case LineType.Char:    return "#88ddff";
                case LineType.BG:      return "#aaffaa";
                case LineType.CG:
                case LineType.SD:      return "#ffaaff";
                case LineType.Overlay: return "#ffccaa";
                case LineType.Sound:   return "#ffff88";
                case LineType.FX:      return "#ff8888";
                case LineType.Flow:    return "#ffaa88";
                case LineType.Choice:
                case LineType.Option:  return "#ddaaff";
                case LineType.Place:   return "#aaffff";
                default:               return "#dddddd";
            }
        }

        // ── 우측: 위젯 ──
        void DrawWidget(Rect rect)
        {
            GUI.Box(rect, "");
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 8, rect.width - 16, rect.height - 16));

            if (selectedIndex < 0 || workingLines == null || selectedIndex >= workingLines.Count)
            {
                GUILayout.Label("(좌측에서 라인을 선택하세요)");
                GUILayout.EndArea();
                return;
            }

            var line = workingLines[selectedIndex];

            var h1 = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 };
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>라인 #{selectedIndex} 편집</b>", h1);
            GUILayout.FlexibleSpace();

            // 여기로 점프 — 메모리 상태 그대로 ScriptRunner에 주입 (디스크 안 씀)
            bool isCurrentRow = selectedIndex == currentRunningIndex;
            GUI.enabled = !isCurrentRow;
            if (GUILayout.Button(isCurrentRow ? "▶ 이미 여기 진행 중" : "▶ 여기로 점프 (미저장 변경 포함)", GUILayout.Width(260)))
                JumpToSelectedInMemory().Forget();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            widgetScroll = GUILayout.BeginScrollView(widgetScroll);

            // ── 공통: LineID, Type ──
            line.LineID = LabeledTextField("LineID", line.LineID ?? "", 200);

            // Type — 드롭다운 (변경 시 라인 reset 위험 → 경고 prompt 없이 그냥 변경 허용, 사용자 책임)
            line.Type = DrawEnumDropdown("Type", line.Type, $"type_{selectedIndex}", 200);

            GUILayout.Space(6);
            DrawSeparator();

            // ── 타입별 위젯 ──
            switch (line.Type)
            {
                case LineType.Text:    DrawTextWidget(line);    break;
                case LineType.Char:    DrawCharWidget(line);    break;
                case LineType.BG:      DrawBGWidget(line);      break;
                case LineType.Sound:   DrawSoundWidget(line);   break;
                case LineType.CG:
                {
                    List<ResourceCatalogSO.SpriteEntry> cgList = ResourceCatalogSO.Instance != null ? ResourceCatalogSO.Instance.CG : null;
                    DrawAssetWidget(line, "CG", cgList);
                    break;
                }
                case LineType.SD:
                {
                    List<ResourceCatalogSO.SpriteEntry> sdList = ResourceCatalogSO.Instance != null ? ResourceCatalogSO.Instance.SD : null;
                    DrawAssetWidget(line, "SD", sdList);
                    break;
                }
                case LineType.Overlay: DrawGenericValueWidget(line, "Overlay Value");           break;
                case LineType.FX:      DrawGenericValueWidget(line, "FX 명령 (예: CamShake:0.5)"); break;
                case LineType.Flow:    DrawFlowWidget(line);    break;
                case LineType.Choice:  DrawGenericValueWidget(line, "Choice 라벨");             break;
                case LineType.Option:  DrawGenericValueWidget(line, "Option:선택지문구:JumpTarget"); break;
                case LineType.Place:   DrawTextOnlyValueWidget(line, "장소 배너 텍스트");        break;
            }

            GUILayout.Space(8);
            DrawSeparator();

            // ── 공통: Next 선택 ──
            DrawNextSelector(line);

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // 위젯 편집 감지 → undo 스택에 push (한 selection 동안 첫 변경에만)
            DetectAndPushWidgetEdit();

            // 변경됐을 수 있으니 검증 갱신
            RunValidation();
        }

        // ── 하단: 검증 결과 ──
        void DrawBottomBar(Rect rect)
        {
            GUI.Box(rect, "");
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12));

            var h = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 };
            GUILayout.Label("<b>검증 결과</b>", h);

            validationScroll = GUILayout.BeginScrollView(validationScroll);
            var rowStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 11, wordWrap = true };
            if (violations.Count == 0)
            {
                GUILayout.Label("<color=#88ff88>문제 없음.</color>", rowStyle);
            }
            else
            {
                foreach (var v in violations.Take(20))
                {
                    string color = v.Severity == "Error" ? "#ff7777" : "#ffcc55";
                    GUILayout.Label($"<color={color}>[{v.Severity}]</color> L{v.LineNumber} ({v.Type} {v.LineID}): {v.Message}", rowStyle);
                }
                if (violations.Count > 20)
                    GUILayout.Label($"… 외 {violations.Count - 20}건", rowStyle);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ══════════════════════════════════════════════
        //  타입별 위젯
        // ══════════════════════════════════════════════

        void DrawTextWidget(ScriptLine line)
        {
            GUILayout.Label("<b>Speaker</b>", new GUIStyle(GUI.skin.label) { richText = true });

            var speakerOptions = BuildSpeakerOptions();
            int curIdx = Array.IndexOf(speakerOptions, line.Speaker ?? "");
            if (curIdx < 0) curIdx = 0;
            int newIdx = DrawDropdown("Speaker", curIdx, speakerOptions, $"speaker_{selectedIndex}", 240);
            if (newIdx >= 0 && newIdx < speakerOptions.Length)
            {
                string sel = speakerOptions[newIdx];
                line.Speaker = sel == "(나레이션)" ? "" : sel;
            }

            GUILayout.Space(6);
            GUILayout.Label("<b>대사</b>", new GUIStyle(GUI.skin.label) { richText = true });
            var newVal = GUILayout.TextArea(line.Value ?? "", GUILayout.MinHeight(80));
            line.Value = newVal;
        }

        void DrawCharWidget(ScriptLine line)
        {
            var parts = (line.Value ?? "").Split(':');
            string slot   = parts.Length > 0 ? parts[0] : "C";
            string action = parts.Length > 1 ? parts[1] : "Enter";
            string ch     = parts.Length > 2 ? parts[2] : "";
            string emote  = parts.Length > 3 ? parts[3] : "Default";

            // Slot
            GUILayout.Label("<b>슬롯</b>", new GUIStyle(GUI.skin.label) { richText = true });
            int slotIdx = SlotIndex(slot);
            int newSlotIdx = GUILayout.SelectionGrid(slotIdx, new[] { "L", "C", "R" }, 3, GUILayout.Width(180));
            string newSlot = new[] { "L", "C", "R" }[newSlotIdx];

            // Action
            GUILayout.Space(4);
            GUILayout.Label("<b>액션</b>", new GUIStyle(GUI.skin.label) { richText = true });
            string[] actions = { "Enter", "EnterUp", "Emote", "Exit", "ExitDown" };
            int actIdx = Array.IndexOf(actions, action);
            if (actIdx < 0) actIdx = 0;
            int newActIdx = GUILayout.SelectionGrid(actIdx, actions, 5, GUILayout.Width(360));
            string newAction = actions[newActIdx];

            string newCh = ch, newEmote = emote;

            if (newAction == "Enter" || newAction == "EnterUp" || newAction == "Emote")
            {
                if (newAction != "Emote")
                {
                    GUILayout.Space(4);
                    GUILayout.Label("<b>캐릭터</b>", new GUIStyle(GUI.skin.label) { richText = true });
                    var (chLabels, chVals) = GetCharacterOptions();
                    int chIdx = Array.IndexOf(chVals, ch);
                    if (chIdx < 0) chIdx = 0;
                    int newChIdx = DrawDropdown("Character", chIdx, chLabels, $"char_{selectedIndex}", 280);
                    if (newChIdx >= 0 && newChIdx < chVals.Length) newCh = chVals[newChIdx];
                }
                GUILayout.Space(4);
                GUILayout.Label("<b>표정</b>", new GUIStyle(GUI.skin.label) { richText = true });
                var (emLabels, emVals) = GetEmoteOptions();
                int emIdx = Array.IndexOf(emVals, emote);
                if (emIdx < 0) emIdx = Array.IndexOf(emVals, "Default");
                if (emIdx < 0) emIdx = 0;
                int newEmIdx = DrawDropdown("Emote", emIdx, emLabels, $"emote_{selectedIndex}", 280);
                if (newEmIdx >= 0 && newEmIdx < emVals.Length) newEmote = emVals[newEmIdx];
            }

            // Value 재합성
            string newValue;
            if (newAction == "Exit" || newAction == "ExitDown")
                newValue = $"{newSlot}:{newAction}";
            else if (newAction == "Emote")
                newValue = $"{newSlot}:{newAction}:{newEmote}";
            else
                newValue = $"{newSlot}:{newAction}:{newCh}:{newEmote}";

            line.Value = newValue;

            GUILayout.Space(6);
            GUILayout.Label($"<color=#888>Value: {newValue}</color>", new GUIStyle(GUI.skin.label) { richText = true });
        }

        void DrawBGWidget(ScriptLine line)
        {
            var parts = (line.Value ?? "").Split(':');
            string bgKey = parts.Length > 0 ? parts[0] : "";
            string transition = parts.Length > 1 ? parts[1] : "CrossFade";

            GUILayout.Label("<b>배경</b>", new GUIStyle(GUI.skin.label) { richText = true });
            var (labels, values) = GetSpriteOptions(ResourceCatalogSO.Instance?.BG);
            int bgIdx = Array.IndexOf(values, bgKey);
            int newIdx = DrawDropdown("BG", Mathf.Max(bgIdx, 0), labels, $"bg_{selectedIndex}", 320);
            string newBg = (newIdx >= 0 && newIdx < values.Length) ? values[newIdx] : bgKey;

            GUILayout.Space(4);
            GUILayout.Label("<b>전환</b>", new GUIStyle(GUI.skin.label) { richText = true });
            string[] trans = { "Cut", "Fade", "CrossFade" };
            int tIdx = Array.IndexOf(trans, transition);
            if (tIdx < 0) tIdx = 2;
            int newTIdx = GUILayout.SelectionGrid(tIdx, trans, 3, GUILayout.Width(270));

            line.Value = $"{newBg}:{trans[newTIdx]}";
            GUILayout.Space(4);
            GUILayout.Label($"<color=#888>Value: {line.Value}</color>", new GUIStyle(GUI.skin.label) { richText = true });
        }

        /// <summary>
        /// Catalog SpriteEntry 리스트 → (표시 라벨[], 저장 값[]) 페어.
        /// 표시: Aliases[0] 또는 Id. 저장도 동일 (Aliases[0] 있으면 한글 별칭 우선, 없으면 Id).
        /// Catalog 없으면 StoryMappings 한글 키 폴백.
        /// </summary>
        static (string[] labels, string[] values) GetSpriteOptions(List<ResourceCatalogSO.SpriteEntry> list)
        {
            if (list != null && list.Count > 0)
            {
                var labels = list.Select(e => e?.DisplayLabel ?? "(null)").ToArray();
                var values = list.Select(e => e?.DisplayLabel ?? "").ToArray(); // Aliases[0] 우선, 폴백 Id
                return (labels, values);
            }
            // 폴백: StoryMappings 한글 키
            return (new string[0], new string[0]);
        }

        static (string[] labels, string[] values) GetAudioOptions(List<ResourceCatalogSO.AudioEntry> list)
        {
            if (list != null && list.Count > 0)
            {
                var labels = list.Select(e => e?.DisplayLabel ?? "(null)").ToArray();
                var values = list.Select(e => e?.DisplayLabel ?? "").ToArray();
                return (labels, values);
            }
            return (new string[0], new string[0]);
        }

        static (string[] labels, string[] values) GetEmoteOptions()
        {
            var catalog = ResourceCatalogSO.Instance;
            if (catalog != null && catalog.Emotes != null && catalog.Emotes.Count > 0)
            {
                var labels = catalog.Emotes.Select(e => e?.DisplayLabel ?? "(null)").ToArray();
                var values = catalog.Emotes.Select(e => e?.DisplayLabel ?? "").ToArray();
                return (labels, values);
            }
            // 폴백: StoryMappings
            var keys = StoryMappings.Emote.Keys.ToArray();
            return (keys, keys);
        }

        static (string[] labels, string[] values) GetCharacterOptions()
        {
            var catalog = ResourceCatalogSO.Instance;
            if (catalog != null && catalog.Characters != null && catalog.Characters.Count > 0)
            {
                var labels = catalog.Characters.Select(c => $"{c.DisplayName} ({c.Id})").ToArray();
                // 저장값: Aliases[0] 우선 → DisplayName → Id 순
                var values = catalog.Characters.Select(c =>
                    (c.Aliases != null && c.Aliases.Length > 0 && !string.IsNullOrEmpty(c.Aliases[0]))
                        ? c.Aliases[0]
                        : (!string.IsNullOrEmpty(c.DisplayName) ? c.DisplayName : c.Id)).ToArray();
                return (labels, values);
            }
            // 폴백
            var lbls = StoryMappings.Characters.Select(c => $"{c.DisplayName} ({c.Id})").ToArray();
            var vals = StoryMappings.Characters.Select(c => c.Aliases?.FirstOrDefault() ?? c.Id).ToArray();
            return (lbls, vals);
        }

        void DrawSoundWidget(ScriptLine line)
        {
            var parts = (line.Value ?? "").Split(':');
            string channel = parts.Length > 0 ? parts[0] : "BGM";
            string name = parts.Length > 1 ? parts[1] : "";

            GUILayout.Label("<b>채널</b>", new GUIStyle(GUI.skin.label) { richText = true });
            string[] channels = { "BGM", "SFX", "Voice" };
            int chIdx = Array.IndexOf(channels, channel);
            if (chIdx < 0) chIdx = 0;
            int newChIdx = GUILayout.SelectionGrid(chIdx, channels, 3, GUILayout.Width(270));
            string newChannel = channels[newChIdx];

            GUILayout.Space(4);
            if (newChannel == "BGM")
            {
                GUILayout.Label("<b>BGM (Stop 또는 곡명)</b>", new GUIStyle(GUI.skin.label) { richText = true });
                var (bgmLabels, bgmVals) = GetAudioOptions(ResourceCatalogSO.Instance?.BGM);
                // "Stop" 옵션 prepend
                var labels = new[] { "Stop" }.Concat(bgmLabels).ToArray();
                var vals   = new[] { "Stop" }.Concat(bgmVals).ToArray();
                int nIdx = Array.IndexOf(vals, name);
                if (nIdx < 0) nIdx = 0;
                int newNIdx = DrawDropdown("BGM", nIdx, labels, $"bgm_{selectedIndex}", 280);
                if (newNIdx >= 0 && newNIdx < vals.Length) name = vals[newNIdx];
            }
            else if (newChannel == "SFX")
            {
                GUILayout.Label("<b>SFX (카탈로그 또는 직접 입력)</b>", new GUIStyle(GUI.skin.label) { richText = true });
                var (sfxLabels, sfxVals) = GetAudioOptions(ResourceCatalogSO.Instance?.SFX);
                if (sfxLabels.Length > 0)
                {
                    int nIdx = Array.IndexOf(sfxVals, name);
                    if (nIdx < 0) nIdx = 0;
                    int newNIdx = DrawDropdown("SFX", nIdx, sfxLabels, $"sfx_{selectedIndex}", 280);
                    if (newNIdx >= 0 && newNIdx < sfxVals.Length) name = sfxVals[newNIdx];
                }
                else
                {
                    name = LabeledTextField("SFX 이름", name, 280);
                    GUILayout.Label("<color=#ffaa88>※ SFX 카탈로그 비어있음 — 직접 입력</color>",
                        new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 });
                }
            }
            else
            {
                // Voice — 카탈로그 미정의
                name = LabeledTextField($"{newChannel} 이름", name, 280);
                GUILayout.Label("<color=#ffaa88>※ Voice 카탈로그 미정 — 정확한 파일명 입력 필요</color>",
                    new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10 });
            }

            line.Value = $"{newChannel}:{name}";
            GUILayout.Space(4);
            GUILayout.Label($"<color=#888>Value: {line.Value}</color>", new GUIStyle(GUI.skin.label) { richText = true });
        }

        void DrawAssetWidget(ScriptLine line, string label, List<ResourceCatalogSO.SpriteEntry> catalogList)
        {
            var parts = (line.Value ?? "").Split(':');
            string first = parts.Length > 0 ? parts[0] : "";

            // Show vs Exit/Close/Hide
            bool isHide = first.Equals("Exit", StringComparison.OrdinalIgnoreCase)
                       || first.Equals("Close", StringComparison.OrdinalIgnoreCase)
                       || first.Equals("Hide", StringComparison.OrdinalIgnoreCase);

            GUILayout.Label("<b>액션</b>", new GUIStyle(GUI.skin.label) { richText = true });
            string[] actions = { "Show", "Exit" };
            int actIdx = isHide ? 1 : 0;
            int newActIdx = GUILayout.SelectionGrid(actIdx, actions, 2, GUILayout.Width(180));

            if (newActIdx == 1)
            {
                line.Value = "Exit";
            }
            else
            {
                GUILayout.Space(4);
                GUILayout.Label($"<b>{label}</b>", new GUIStyle(GUI.skin.label) { richText = true });
                var (labels, values) = GetSpriteOptions(catalogList);
                int idx = Array.IndexOf(values, first);
                if (idx < 0) idx = 0;
                int newIdx = DrawDropdown(label, idx, labels, $"asset_{selectedIndex}", 320);
                string chosen = (newIdx >= 0 && newIdx < values.Length) ? values[newIdx] : first;
                line.Value = chosen;
            }
            GUILayout.Space(4);
            GUILayout.Label($"<color=#888>Value: {line.Value}</color>", new GUIStyle(GUI.skin.label) { richText = true });
        }

        void DrawFlowWidget(ScriptLine line)
        {
            var parts = (line.Value ?? "").Split(':');
            string sub = parts.Length > 0 ? parts[0] : "";

            string[] flowSubs = { "Mark", "Jump", "If", "End", "LoadingScene", "MiniGame", "Save", "Schedule", "Username", "Day", "Affinity", "LockScreen", "Message" };
            int sIdx = Array.IndexOf(flowSubs, sub);
            if (sIdx < 0) sIdx = 0;
            GUILayout.Label("<b>Flow 서브명령</b>", new GUIStyle(GUI.skin.label) { richText = true });
            int newSIdx = DrawDropdown("Flow", sIdx, flowSubs, $"flow_{selectedIndex}", 240);
            string newSub = flowSubs[newSIdx];

            // 나머지 부분 (서브명령 이후)
            string rest = parts.Length > 1 ? string.Join(":", parts.Skip(1)) : "";

            GUILayout.Space(4);
            GUILayout.Label("<b>인자</b>", new GUIStyle(GUI.skin.label) { richText = true });
            string newRest = GUILayout.TextField(rest, GUILayout.Width(380));

            line.Value = string.IsNullOrEmpty(newRest) ? newSub : $"{newSub}:{newRest}";
            GUILayout.Space(4);
            GUILayout.Label($"<color=#888>Value: {line.Value}</color>", new GUIStyle(GUI.skin.label) { richText = true });
        }

        void DrawGenericValueWidget(ScriptLine line, string label)
        {
            GUILayout.Label($"<b>{label}</b>", new GUIStyle(GUI.skin.label) { richText = true });
            line.Value = GUILayout.TextField(line.Value ?? "", GUILayout.Width(420));
        }

        void DrawTextOnlyValueWidget(ScriptLine line, string label)
        {
            GUILayout.Label($"<b>{label}</b>", new GUIStyle(GUI.skin.label) { richText = true });
            line.Value = GUILayout.TextArea(line.Value ?? "", GUILayout.MinHeight(60));
        }

        // ── Next 선택 ──
        void DrawNextSelector(ScriptLine line)
        {
            GUILayout.Label("<b>Next (라인 진행 방식)</b>", new GUIStyle(GUI.skin.label) { richText = true });
            string[] options = { "Immediate (>)", "Click", "Await", "Delay" };
            int idx = (int)line.NextType;
            int newIdx = GUILayout.SelectionGrid(idx, options, 4, GUILayout.Width(420));
            line.NextType = (NextType)newIdx;
            if (line.NextType == NextType.Delay)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Delay (초):", GUILayout.Width(80));
                string delayStr = GUILayout.TextField(line.DelaySeconds.ToString("0.##",
                    System.Globalization.CultureInfo.InvariantCulture), GUILayout.Width(80));
                if (float.TryParse(delayStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float d))
                    line.DelaySeconds = d;
                GUILayout.EndHorizontal();
            }
        }

        // ══════════════════════════════════════════════
        //  드롭다운 (커스텀)
        // ══════════════════════════════════════════════

        string[] _pendingDropdownOptions;
        Action<int> _pendingDropdownCallback;

        int DrawDropdown(string label, int currentIndex, string[] options, string key, float width)
        {
            string current = (options != null && currentIndex >= 0 && currentIndex < options.Length)
                ? options[currentIndex] : "(선택)";

            if (GUILayout.Button($"{Trunc(current, 36)} ▾", GUILayout.Width(width)))
            {
                ToggleDropdown(key);
                if (activeDropdownKey == key)
                {
                    // 클릭한 버튼 바로 아래에 팝업 위치 (GUILayoutUtility로 계산)
                    var r = GUILayoutUtility.GetLastRect();
                    dropdownRect = new Rect(r.x, r.y + r.height, Mathf.Max(width, 240),
                        Mathf.Min(options.Length * 22 + 8, 320));
                    BuildDropdownContent(options, idx => _pendingDropdownResult = (key, idx));
                }
            }

            // 이전 프레임에 선택된 결과 반환
            if (_pendingDropdownResult.HasValue && _pendingDropdownResult.Value.key == key)
            {
                int sel = _pendingDropdownResult.Value.index;
                _pendingDropdownResult = null;
                return sel;
            }
            return currentIndex;
        }

        (string key, int index)? _pendingDropdownResult;

        void ToggleDropdown(string key)
        {
            activeDropdownKey = activeDropdownKey == key ? null : key;
        }

        void BuildDropdownContent(string[] options, Action<int> onSelect)
        {
            _pendingDropdownOptions = options;
            _pendingDropdownCallback = onSelect;
        }

        void DrawActiveDropdown()
        {
            if (activeDropdownKey == null || _pendingDropdownOptions == null) return;
            // 배경 박스
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.98f);
            GUI.DrawTexture(dropdownRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Box(dropdownRect, "");

            GUILayout.BeginArea(new Rect(dropdownRect.x + 2, dropdownRect.y + 2, dropdownRect.width - 4, dropdownRect.height - 4));
            dropdownScroll = GUILayout.BeginScrollView(dropdownScroll);
            var btnStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontSize = 11 };
            for (int i = 0; i < _pendingDropdownOptions.Length; i++)
            {
                if (GUILayout.Button(_pendingDropdownOptions[i], btnStyle))
                {
                    _pendingDropdownCallback?.Invoke(i);
                    activeDropdownKey = null;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // Enum 드롭다운 헬퍼
        T DrawEnumDropdown<T>(string label, T value, string key, float width) where T : Enum
        {
            var names = Enum.GetNames(typeof(T));
            int curIdx = Array.IndexOf(names, value.ToString());
            if (curIdx < 0) curIdx = 0;
            int newIdx = DrawDropdown(label, curIdx, names, key, width);
            if (newIdx >= 0 && newIdx < names.Length)
                return (T)Enum.Parse(typeof(T), names[newIdx]);
            return value;
        }

        // ══════════════════════════════════════════════
        //  라인 조작
        // ══════════════════════════════════════════════

        void InsertLineAfterSelected()
        {
            if (workingLines == null) return;
            PushUndoSnapshot();
            int insertAt = (selectedIndex < 0) ? workingLines.Count : selectedIndex + 1;
            var newLine = new ScriptLine("", LineType.Text, "", "", NextType.Click, 0f, 0);
            workingLines.Insert(insertAt, newLine);
            selectedIndex = insertAt;
            CaptureSelectionHash();
        }

        void DeleteSelected()
        {
            if (workingLines == null || selectedIndex < 0 || selectedIndex >= workingLines.Count) return;
            PushUndoSnapshot();
            workingLines.RemoveAt(selectedIndex);
            if (selectedIndex >= workingLines.Count) selectedIndex = workingLines.Count - 1;
            CaptureSelectionHash();
        }

        void MoveSelected(int delta)
        {
            if (workingLines == null || selectedIndex < 0) return;
            int newIdx = selectedIndex + delta;
            if (newIdx < 0 || newIdx >= workingLines.Count) return;
            PushUndoSnapshot();
            var tmp = workingLines[selectedIndex];
            workingLines[selectedIndex] = workingLines[newIdx];
            workingLines[newIdx] = tmp;
            selectedIndex = newIdx;
            CaptureSelectionHash();
        }

        // ══════════════════════════════════════════════
        //  Undo / Redo
        // ══════════════════════════════════════════════

        void PushUndoSnapshot()
        {
            if (workingLines == null) return;
            undoStack.Push(new UndoEntry { Snapshot = DeepCopy(workingLines), SelectedIndex = selectedIndex });
            if (undoStack.Count > UndoMax)
            {
                // Stack은 bottom 제거 불가 — 한 번 List로 변환 후 재구성
                var arr = undoStack.ToArray();
                undoStack.Clear();
                for (int i = arr.Length - 2; i >= 0; i--) undoStack.Push(arr[i]); // 최신 UndoMax-1개 유지
            }
            redoStack.Clear();
        }

        void Undo()
        {
            if (undoStack.Count == 0) return;
            redoStack.Push(new UndoEntry { Snapshot = DeepCopy(workingLines), SelectedIndex = selectedIndex });
            var prev = undoStack.Pop();
            workingLines = prev.Snapshot;
            selectedIndex = Mathf.Clamp(prev.SelectedIndex, -1, (workingLines?.Count ?? 1) - 1);
            CaptureSelectionHash();
            RunValidation();
            Debug.Log($"[ScenarioEditor] Undo ({undoStack.Count} more)");
        }

        void Redo()
        {
            if (redoStack.Count == 0) return;
            undoStack.Push(new UndoEntry { Snapshot = DeepCopy(workingLines), SelectedIndex = selectedIndex });
            var next = redoStack.Pop();
            workingLines = next.Snapshot;
            selectedIndex = Mathf.Clamp(next.SelectedIndex, -1, (workingLines?.Count ?? 1) - 1);
            CaptureSelectionHash();
            RunValidation();
            Debug.Log($"[ScenarioEditor] Redo ({redoStack.Count} more)");
        }

        /// <summary>
        /// 위젯 편집(드롭다운·텍스트 변경)에 의한 라인 mutation을 감지해 한 번 push.
        /// 같은 라인 selection 동안 첫 변경에만 push (이후 변경은 합쳐서 한 undo로 묶음).
        /// </summary>
        void DetectAndPushWidgetEdit()
        {
            if (workingLines == null || selectedIndex < 0 || selectedIndex >= workingLines.Count) return;
            string currentHash = HashLine(workingLines[selectedIndex]);
            if (_selectionLineHashAtFocus != null
                && currentHash != _selectionLineHashAtFocus
                && _lastSnapshottedSelection != selectedIndex)
            {
                // 변경 감지 — focus 시점 상태가 이미 새 변경이 적용된 후이므로
                // push할 스냅샷은 "현재 상태에서 그 라인만 focus 시점 값으로 되돌린 버전"이 필요.
                // 간단화: 단순히 현재 상태를 push (변경 후) → undo 시 더 이전으로 못 감.
                // 더 정확: 별도 _preEditSnapshot 보관해서 그것 push. 아래 방식이 더 안전.
                if (_preEditSnapshot != null)
                {
                    undoStack.Push(new UndoEntry
                    {
                        Snapshot = _preEditSnapshot,
                        SelectedIndex = selectedIndex,
                    });
                    if (undoStack.Count > UndoMax)
                    {
                        var arr = undoStack.ToArray();
                        undoStack.Clear();
                        for (int i = arr.Length - 2; i >= 0; i--) undoStack.Push(arr[i]);
                    }
                    redoStack.Clear();
                    _lastSnapshottedSelection = selectedIndex;
                    _preEditSnapshot = null;
                }
            }
        }

        List<ScriptLine> _preEditSnapshot;

        void CaptureSelectionHash()
        {
            if (workingLines == null || selectedIndex < 0 || selectedIndex >= workingLines.Count)
            {
                _selectionLineHashAtFocus = null;
                _preEditSnapshot = null;
            }
            else
            {
                _selectionLineHashAtFocus = HashLine(workingLines[selectedIndex]);
                _preEditSnapshot = DeepCopy(workingLines);
                _lastSnapshottedSelection = -1; // 새 선택 — 변경 감지되면 한 번 push
            }
        }

        static string HashLine(ScriptLine l)
        {
            if (l == null) return "";
            return $"{l.LineID}|{l.Type}|{l.Speaker}|{l.Value}|{l.NextType}|{l.DelaySeconds:0.##}";
        }

        // ══════════════════════════════════════════════
        //  외부 mtime 감지
        // ══════════════════════════════════════════════

        void CheckExternalChange()
        {
            if (string.IsNullOrEmpty(scriptName)) return;
            var current = StoryAssetLoader.GetLastWriteTime(scriptName);
            // 1초 이상 차이날 때만 의미 있는 변경으로 간주 (저장 직후의 자기 자신 mtime 제외)
            if (current > _loadedAtMtime.AddSeconds(1))
                _externalChangeDetected = true;
        }

        // ══════════════════════════════════════════════
        //  헬퍼
        // ══════════════════════════════════════════════

        void RunValidation()
        {
            if (workingLines != null)
                violations = ScriptValidator.Validate(workingLines);
        }

        void ScanAvailableScripts()
        {
            try
            {
                if (!System.IO.Directory.Exists(StoryAssetLoader.StoryDir))
                {
                    availableScripts = Array.Empty<string>();
                    return;
                }
                availableScripts = System.IO.Directory.GetFiles(StoryAssetLoader.StoryDir, "*.csv")
                    .Select(p => System.IO.Path.GetFileNameWithoutExtension(p))
                    .OrderBy(n => n)
                    .ToArray();
            }
            catch { availableScripts = Array.Empty<string>(); }
        }

        static List<ScriptLine> DeepCopy(List<ScriptLine> src)
        {
            var copy = new List<ScriptLine>(src.Count);
            foreach (var l in src)
                copy.Add(new ScriptLine(l.LineID, l.Type, l.Speaker, l.Value, l.NextType, l.DelaySeconds, l.SourceLine));
            return copy;
        }

        static int SlotIndex(string slot)
        {
            switch (slot?.ToUpperInvariant())
            {
                case "L": case "LEFT": return 0;
                case "R": case "RIGHT": return 2;
                default: return 1;
            }
        }

        static string[] BuildSpeakerOptions()
        {
            var list = new List<string> { "(나레이션)" };
            // 한글 이름 우선 (작가 친화)
            foreach (var c in StoryMappings.Characters)
                list.Add(c.DisplayName);
            return list.ToArray();
        }

        static string LabeledTextField(string label, string value, float width)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(80));
            string r = GUILayout.TextField(value ?? "", GUILayout.Width(width));
            GUILayout.EndHorizontal();
            return r;
        }

        static void DrawSeparator()
        {
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
            GUILayout.Space(4);
        }

        static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }
}
